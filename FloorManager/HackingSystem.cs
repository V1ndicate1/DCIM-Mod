#if !STRIP_HACKING
using System;
using System.Collections.Generic;
using System.Text;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using MelonLoader.Preferences;
using UnityEngine;

namespace FloorManager
{
    // ── Data classes ─────────────────────────────────────────────────

    public class FirewallEntry
    {
        public string SwitchId;
        public int MaxHP;
        public int CurrentHP;
        public NetworkSwitch Switch; // transient — resolved on load
    }

    public enum WaveType { Probe, Intrusion, Ransomware, DataExfiltration }

    public class ActiveWaveInfo
    {
        public WaveType Type;
        public float TimeRemaining;
        public string HackerProfileName;
    }

    public class HackerProfile
    {
        public string Name;
        public string Specialty;
        public float DpsMultiplier;
        public float FrequencyMultiplier;
        public int Persistence;
        public float DisengageThreshold; // attractiveness below this → disengage
    }

    // ── HackingSystem ─────────────────────────────────────────────────

    public static class HackingSystem
    {
        // ── In-game clock ─────────────────────────────────────────────
        // 1 real minute of play time = 1 in-game day
        // 30 in-game days = 1 month, 12 months = 1 year
        public const float SecondsPerDay    = 300f;
        public const int   DaysPerMonth     = 30;
        public const int   MonthsPerYear    = 12;
        public const int   FirewallBaseHP   = 100;
        public const int   FirewallMaxHP    = 200; // cap — 2× base, encourages chaining multiple switches

        private static float _elapsedSeconds = 0f;
        private static int   _lastTotalDays  = 0;

        private static readonly HackerProfile[] Profiles = new HackerProfile[4]
        {
            new HackerProfile { Name = "Script Kiddie",  Specialty = "Opportunistic",              DpsMultiplier = 0.8f, FrequencyMultiplier = 1.4f, Persistence = 1, DisengageThreshold = 600f  },
            new HackerProfile { Name = "Hacktivist",     Specialty = "Disruption",                 DpsMultiplier = 1f,   FrequencyMultiplier = 1f,   Persistence = 2, DisengageThreshold = 900f  },
            new HackerProfile { Name = "Cybercriminal",  Specialty = "Ransomware",                 DpsMultiplier = 1.2f, FrequencyMultiplier = 0.9f, Persistence = 3, DisengageThreshold = 1200f },
            new HackerProfile { Name = "APT-9",          Specialty = "Advanced Persistent Threat", DpsMultiplier = 1.6f, FrequencyMultiplier = 0.7f, Persistence = 5, DisengageThreshold = 2000f },
        };

        private static readonly List<FirewallEntry>            _chain            = new List<FirewallEntry>();
        private static readonly Dictionary<string, float>      _eolAccumulators  = new Dictionary<string, float>();
        private static readonly List<NetworkSwitch>            _idsSwitches      = new List<NetworkSwitch>();
        private static readonly HashSet<string>                _idsSwitchIds     = new HashSet<string>();
        private static readonly HashSet<string>                _honeypotServerIds = new HashSet<string>();
        private static readonly List<Server>                   _honeypotServers  = new List<Server>();

        private static bool                  _waveActive            = false;
        private static readonly List<ActiveWaveInfo> _activeWaves   = new List<ActiveWaveInfo>();
        private static bool                  _chainBreachedThisWave = false;
        private static float                 _serverEolAccum        = 0f;
        private static readonly Dictionary<string, float> _waveHpAccumulators = new Dictionary<string, float>();

        private static float  _waveTriggerAt          = 0f;
        private static bool   _hackingEnabled         = false;
        private static int    _consecutiveWaves       = 0;
        private static bool   _dormantActive          = false;
        private static float  _dormantUntil           = 0f;

        private static bool   _lockdownActive         = false;
        private static float  _lockdownSecondsRemaining = 0f;
        private static float  _lockdownCooldownRemaining = 0f;
        private static float  _lockdownCostAccum      = 0f;
        private const  float  LockdownDuration        = 120f;
        private const  float  LockdownCooldown        = 300f;
        private const  float  LockdownCostPerSec      = 15f;

        private static int    _successiveDefenses     = 0;
        private static bool   _counterTraceAvailable  = false;

        private static int    _offsiteTier            = 0;
        private static float  _offsiteHP              = 0f;
        private static float  _offsiteMaxHP           = 0f;
        private static bool   _offsiteOverloaded      = false;

        private static bool          _hackerIdentified = false;
        private static HackerProfile _currentProfile   = null;

        private static float  _rep                    = 80f;
        private static float  _repRecoveryAccum       = 0f;
        private static float  _revPenaltyAccum        = 0f;

        private static bool   _ransomDemandActive     = false;
        private static float  _ransomTimer            = 0f;
        private static int    _ransomCustomerID       = -1;
        private static readonly HashSet<int> _ransomed = new HashSet<int>();

        private static readonly List<string> _attackLog = new List<string>();

        // ── Prefs ─────────────────────────────────────────────────────
        private static MelonPreferences_Category         _cat;
        private static MelonPreferences_Entry<string>   _prefFirewalls;
        private static MelonPreferences_Entry<string>   _prefIDS;
        private static MelonPreferences_Entry<string>   _prefHoneypots;
        private static MelonPreferences_Entry<int>      _prefOffsiteTier;
        private static MelonPreferences_Entry<float>    _prefOffsiteHP;
        private static MelonPreferences_Entry<float>    _prefRep;
        private static MelonPreferences_Entry<float>    _prefElapsedSeconds;
        private static MelonPreferences_Entry<float>    _prefWaveTriggerAt;
        private static MelonPreferences_Entry<float>    _prefDormantUntil;
        private static MelonPreferences_Entry<bool>     _prefHackingEnabled;
        private static MelonPreferences_Entry<int>      _prefConsecutiveWaves;
        private static float _prefSaveAccum = 0f;

        // ── Public properties ─────────────────────────────────────────
        public static float  ElapsedSeconds      => _elapsedSeconds;
        public static int    TotalDaysElapsed     => (int)(_elapsedSeconds / SecondsPerDay);
        public static int    CurrentDay          => TotalDaysElapsed % DaysPerMonth + 1;
        public static int    CurrentMonth        => TotalDaysElapsed / DaysPerMonth % MonthsPerYear + 1;
        public static int    CurrentYear         => TotalDaysElapsed / (DaysPerMonth * MonthsPerYear) + 1;
        public static string DateString          => $"Day {CurrentDay} / Month {CurrentMonth} / Year {CurrentYear}";

        public static bool   WaveActive          => _waveActive;
        public static string ThreatLevel         => GetThreatLevel();

        public static float  NextWaveSeconds
        {
            get
            {
                if (!(_waveTriggerAt > 0f) || _waveActive || _dormantActive) return 0f;
                return Math.Max(0f, _waveTriggerAt - _elapsedSeconds);
            }
        }

        public static float DormantSecondsRemaining
        {
            get
            {
                if (!_dormantActive) return 0f;
                return Math.Max(0f, _dormantUntil - _elapsedSeconds);
            }
        }

        public static List<ActiveWaveInfo> ActiveWaves      => _activeWaves;
        public static bool   LockdownActive                  => _lockdownActive;
        public static float  LockdownSecondsRemaining        => _lockdownSecondsRemaining;
        public static float  LockdownCooldownRemaining       => _lockdownCooldownRemaining;
        public static int    SuccessiveDefenses              => _successiveDefenses;
        public static bool   CounterTraceAvailable           => _counterTraceAvailable;
        public static int    OffsiteTier                     => _offsiteTier;
        public static float  OffsiteHP                       => _offsiteHP;
        public static float  OffsiteMaxHP                    => _offsiteMaxHP;
        public static bool   OffsiteOverloaded               => _offsiteOverloaded;
        public static bool   HackerIdentified                => _hackerIdentified;

        public static string HackerName
        {
            get
            {
                if (!_hackerIdentified || _currentProfile == null) return "Unknown Threat";
                return _currentProfile.Name;
            }
        }

        public static string HackerSpecialty
        {
            get
            {
                if (!_hackerIdentified || _currentProfile == null) return "Unknown";
                return _currentProfile.Specialty;
            }
        }

        public static int HackerPersistence
        {
            get
            {
                if (_currentProfile == null) return 0;
                return _currentProfile.Persistence;
            }
        }

        public static float  Rep                 => _rep;
        public static bool   RansomDemandActive  => _ransomDemandActive;
        public static float  RansomTimer         => _ransomTimer;
        public static int    RansomCustomerID    => _ransomCustomerID;
        public static List<string> AttackLog     => _attackLog;

        // ── Public API ────────────────────────────────────────────────

        public static void DesignateFirewall(NetworkSwitch sw)
        {
            string id = sw.GetSwitchId();
            RemoveIDS(sw);
            for (int i = 0; i < _chain.Count; i++)
                if (_chain[i].SwitchId == id) return;
            _chain.Add(new FirewallEntry { SwitchId = id, MaxHP = 100, CurrentHP = 100, Switch = sw });
            _eolAccumulators[id] = 0f;
            SaveFirewalls();
            MelonLogger.Msg("[DCIM Security] " + id + " designated as Firewall (HP 100)");
        }

        public static void RemoveFirewall(NetworkSwitch sw)
        {
            string id = sw.GetSwitchId();
            for (int i = _chain.Count - 1; i >= 0; i--)
            {
                if (_chain[i].SwitchId == id)
                {
                    _chain.RemoveAt(i);
                    _eolAccumulators.Remove(id);
                    break;
                }
            }
            SaveFirewalls();
        }

        public static void DesignateIDS(NetworkSwitch sw)
        {
            string id = sw.GetSwitchId();
            RemoveFirewall(sw);
            if (!_idsSwitchIds.Contains(id))
            {
                _idsSwitchIds.Add(id);
                _idsSwitches.Add(sw);
                SaveIDS();
                MelonLogger.Msg("[DCIM Security] " + id + " designated as IDS");
            }
        }

        public static void RemoveIDS(NetworkSwitch sw)
        {
            string id = sw.GetSwitchId();
            if (!_idsSwitchIds.Contains(id)) return;
            _idsSwitchIds.Remove(id);
            for (int i = _idsSwitches.Count - 1; i >= 0; i--)
            {
                if ((UnityEngine.Object)(object)_idsSwitches[i] != (UnityEngine.Object)null && _idsSwitches[i].GetSwitchId() == id)
                {
                    _idsSwitches.RemoveAt(i);
                    break;
                }
            }
            SaveIDS();
        }

        public static void DesignateHoneypot(Server server)
        {
            string id = server.ServerID;
            if (!_honeypotServerIds.Contains(id))
            {
                _honeypotServerIds.Add(id);
                _honeypotServers.Add(server);
                if (server.isOn) server.PowerButton(false);
                SaveHoneypots();
                MelonLogger.Msg("[DCIM Security] Server " + id + " designated as Honeypot");
            }
        }

        public static void RemoveHoneypot(Server server)
        {
            string id = server.ServerID;
            if (!_honeypotServerIds.Contains(id)) return;
            _honeypotServerIds.Remove(id);
            for (int i = _honeypotServers.Count - 1; i >= 0; i--)
            {
                if ((UnityEngine.Object)(object)_honeypotServers[i] != (UnityEngine.Object)null && _honeypotServers[i].ServerID == id)
                {
                    _honeypotServers.RemoveAt(i);
                    break;
                }
            }
            if (!server.isOn) server.PowerButton(false);
            SaveHoneypots();
        }

        public static bool IsFirewall(NetworkSwitch sw)
        {
            string id = sw.GetSwitchId();
            for (int i = 0; i < _chain.Count; i++)
                if (_chain[i].SwitchId == id) return true;
            return false;
        }

        public static bool IsIDS(NetworkSwitch sw)
            => _idsSwitchIds.Contains(sw.GetSwitchId());

        public static bool IsHoneypot(Server server)
            => _honeypotServerIds.Contains(server.ServerID);

        public static int GetFirewallHP(NetworkSwitch sw)
        {
            string id = sw.GetSwitchId();
            for (int i = 0; i < _chain.Count; i++)
                if (_chain[i].SwitchId == id) return _chain[i].CurrentHP;
            return -1;
        }

        public static int GetFirewallMaxHP(NetworkSwitch sw)
        {
            string id = sw.GetSwitchId();
            for (int i = 0; i < _chain.Count; i++)
                if (_chain[i].SwitchId == id) return _chain[i].MaxHP;
            return -1;
        }

        public static void ApplySecurityPatch(NetworkSwitch sw)
        {
            string id = sw.GetSwitchId();
            for (int i = 0; i < _chain.Count; i++)
            {
                if (_chain[i].SwitchId != id) continue;
                if (_chain[i].MaxHP >= FirewallMaxHP)
                {
                    try { StaticUIElements.instance.AddMeesageInField("Firewall already at max HP - add another switch to the chain for more protection"); } catch { }
                    break;
                }
                int newMax = Math.Min(FirewallMaxHP, (int)(_chain[i].MaxHP * 1.2f));
                _chain[i].MaxHP = newMax;
                _chain[i].CurrentHP = newMax;
                int cost = 500 + _offsiteTier * 200;
                PlayerManager.instance.playerClass.UpdateCoin(-cost, false);
                StaticUIElements.instance.AddMeesageInField($"Security patch applied - firewall HP boosted to {newMax}/{FirewallMaxHP}");
                SaveFirewalls();
                LogAttack($"Security patch applied to firewall {id} - HP {newMax}/{FirewallMaxHP}");
                break;
            }
        }

        public static void OnFirewallRepaired(NetworkSwitch sw)
        {
            string id = sw.GetSwitchId();
            for (int i = 0; i < _chain.Count; i++)
            {
                if (_chain[i].SwitchId == id)
                {
                    _chain[i].CurrentHP = _chain[i].MaxHP;
                    MelonLogger.Msg("[DCIM Security] Firewall " + id + " repaired - HP restored");
                    break;
                }
            }
        }

        public static void ActivateLockdown()
        {
            if (!_lockdownActive && !(_lockdownCooldownRemaining > 0f))
            {
                _lockdownActive = true;
                _lockdownSecondsRemaining = LockdownDuration;
                _lockdownCostAccum = 0f;
                StaticUIElements.instance.AddMeesageInField("? LOCKDOWN ACTIVATED - attack paused");
                LogAttack("Lockdown activated");
            }
        }

        public static void UseCounterTrace()
        {
            if (_counterTraceAvailable)
            {
                _counterTraceAvailable = false;
                _hackerIdentified = true;
                if (_currentProfile == null)
                    _currentProfile = Profiles[UnityEngine.Random.Range(0, Profiles.Length)];
                StaticUIElements.instance.AddMeesageInField("Counter-trace complete - " + _currentProfile.Name + " identified");
                LogAttack($"Counter-trace: {_currentProfile.Name} ({_currentProfile.Specialty}) identified");
            }
        }

        public static void SubscribeOffsite(int tier)
        {
            _offsiteTier = tier;
            _offsiteMaxHP = tier * 50f;
            _offsiteHP = _offsiteMaxHP;
            _offsiteOverloaded = false;
            SaveOffsitePrefs();
            StaticUIElements.instance.AddMeesageInField($"Offsite Firewall Tier {tier} subscribed");
        }

        public static void CancelOffsite()
        {
            _offsiteTier = 0;
            _offsiteHP = 0f;
            _offsiteMaxHP = 0f;
            _offsiteOverloaded = false;
            SaveOffsitePrefs();
        }

        public static void PayRansom()
        {
            if (_ransomDemandActive)
            {
                int cost = CalculateRansomCost();
                PlayerManager.instance.playerClass.UpdateCoin(-cost, false);
                _ransomDemandActive = false;
                _ransomTimer = 0f;
                _ransomCustomerID = -1;
                _ransomed.Clear();
                StaticUIElements.instance.AddMeesageInField($"Ransom paid - ${cost}");
                LogAttack($"Ransom paid: ${cost}");
            }
        }

        public static bool IsRansomed(Server server)
            => _ransomed.Contains(((UnityEngine.Object)server).GetInstanceID());

        public static void OnServerRepaired(Server server)
        {
            int id = ((UnityEngine.Object)server).GetInstanceID();
            if (_ransomed.Contains(id)) _ransomed.Remove(id);
        }

        public static void OnRepLoss(float amount)
        {
            _rep = Math.Max(0f, _rep - amount);
            MelonLogger.Msg($"[DCIM Security] Rep -{amount:F0}  {_rep:F0}");
            SaveRep();
        }

        public static void OnSaveLoaded()
        {
            MelonLogger.Msg("[DCIM HackingSystem] Save loaded - initializing.");
            InitPrefs();
            LoadFromPrefs();
            _lastTotalDays         = (int)(_elapsedSeconds / SecondsPerDay);
            _waveActive            = false;
            _activeWaves.Clear();
            _ransomed.Clear();
            _ransomDemandActive    = false;
            _ransomTimer           = 0f;
            _ransomCustomerID      = -1;
            _attackLog.Clear();
            _successiveDefenses    = 0;
            _counterTraceAvailable = false;
            _lockdownActive        = false;
            _lockdownSecondsRemaining  = 0f;
            _lockdownCooldownRemaining = 0f;
            _lockdownCostAccum     = 0f;
            _repRecoveryAccum      = 0f;
            _revPenaltyAccum       = 0f;
            _hackerIdentified      = false;
            _currentProfile        = null;
            _chainBreachedThisWave = false;
            _serverEolAccum        = 0f;
            _waveHpAccumulators.Clear();
            if (_dormantActive && _elapsedSeconds >= _dormantUntil)
            {
                _dormantActive = false;
                _dormantUntil  = 0f;
                _waveTriggerAt = 0f;
                SaveTimerPrefs();
            }
            if (_hackingEnabled && !_dormantActive)
            {
                SelectNewHackerProfile(GetCustomerCount());
                if (_waveTriggerAt <= _elapsedSeconds)
                {
                    ScheduleNextWave();
                    MelonLogger.Msg("[DCIM HackingSystem] Wave rescheduled (was in the past on load).");
                }
            }
            MelonLogger.Msg($"[DCIM HackingSystem] Initialized - {DateString} | Elapsed: {_elapsedSeconds:F0}s | Chain: {_chain.Count} | IDS: {_idsSwitches.Count} | Rep: {_rep:F0} | HackingEnabled: {_hackingEnabled} | WaveTriggerAt: {_waveTriggerAt:F0}s | DormantUntil: {_dormantUntil:F0}s");
        }

        public static void OnLateUpdateTick(float deltaTime)
        {
            _elapsedSeconds += deltaTime;
            int totalDays = (int)(_elapsedSeconds / SecondsPerDay);
            if (totalDays != _lastTotalDays)
            {
                int daysAdvanced = totalDays - _lastTotalDays;
                _lastTotalDays = totalDays;
                OnDayChanged(daysAdvanced);
            }
            if (!_hackingEnabled)
            {
                int customerCount = GetCustomerCount();
                bool timeThreshold = _elapsedSeconds >= 1200f;
                if (customerCount >= 4 || timeThreshold)
                {
                    _hackingEnabled = true;
                    _prefHackingEnabled.Value = true;
                    if (_currentProfile == null) SelectNewHackerProfile(customerCount);
                    ScheduleNextWave();
                    MelonLogger.Msg("[DCIM HackingSystem] Hacking threat system ACTIVATED.");
                    try { StaticUIElements.instance.AddMeesageInField("? Security threat detected - monitor DCIM Security tab"); } catch { }
                }
            }
            if (_hackingEnabled)
            {
                if (_dormantActive)
                {
                    if (_elapsedSeconds >= _dormantUntil)
                    {
                        _dormantActive = false;
                        _dormantUntil  = 0f;
                        if (CalculateAttractiveness() > 1200f)
                        {
                            SelectNewHackerProfile(GetCustomerCount());
                            ScheduleNextWave();
                            MelonLogger.Msg("[DCIM HackingSystem] Threat re-engaged after dormancy.");
                        }
                        SaveTimerPrefs();
                    }
                }
                else
                {
                    if (!_waveActive && _waveTriggerAt > 0f && _elapsedSeconds >= _waveTriggerAt)
                        TriggerWave();
                    if (_waveActive)
                        TickActiveWaves(deltaTime);
                }
            }
            // EOL drain on firewall switches
            float drainRate = _waveActive ? 3f : 1f;
            for (int i = 0; i < _chain.Count; i++)
            {
                FirewallEntry fw = _chain[i];
                if ((UnityEngine.Object)(object)fw.Switch == (UnityEngine.Object)null || fw.Switch.isBroken || fw.Switch.eolTime <= 0)
                    continue;
                if (!_eolAccumulators.ContainsKey(fw.SwitchId))
                    _eolAccumulators[fw.SwitchId] = 0f;
                _eolAccumulators[fw.SwitchId] += drainRate * deltaTime;
                while (_eolAccumulators[fw.SwitchId] >= 1f)
                {
                    _eolAccumulators[fw.SwitchId] -= 1f;
                    if (fw.Switch.eolTime > 0) fw.Switch.eolTime--;
                    if (fw.Switch.eolTime <= 0) { fw.Switch.isBroken = true; fw.CurrentHP = 0; break; }
                }
            }
            // Server EOL drain when chain breached
            if (_chainBreachedThisWave && _waveActive)
            {
                _serverEolAccum += 2f * deltaTime;
                while (_serverEolAccum >= 1f)
                {
                    _serverEolAccum -= 1f;
                    Il2CppArrayBase<Server> servers = UnityEngine.Object.FindObjectsOfType<Server>();
                    for (int j = 0; j < servers.Length; j++)
                    {
                        if (!((UnityEngine.Object)(object)servers[j] == (UnityEngine.Object)null) && !servers[j].isBroken && servers[j].eolTime > 0)
                            servers[j].eolTime = Math.Max(0, servers[j].eolTime - 1);
                    }
                }
            }
            // Lockdown tick
            if (_lockdownActive)
            {
                _lockdownSecondsRemaining -= deltaTime;
                _lockdownCostAccum += deltaTime;
                while (_lockdownCostAccum >= 1f)
                {
                    _lockdownCostAccum -= 1f;
                    try { PlayerManager.instance.playerClass.UpdateCoin(-LockdownCostPerSec, false); } catch { }
                }
                if (_lockdownSecondsRemaining <= 0f)
                {
                    _lockdownActive = false;
                    _lockdownSecondsRemaining = 0f;
                    _lockdownCooldownRemaining = LockdownCooldown;
                    try { StaticUIElements.instance.AddMeesageInField("Lockdown ended"); } catch { }
                    LogAttack("Lockdown ended");
                }
            }
            if (_lockdownCooldownRemaining > 0f)
                _lockdownCooldownRemaining = Math.Max(0f, _lockdownCooldownRemaining - deltaTime);
            // Ransom tick
            if (_ransomDemandActive)
            {
                _ransomTimer -= deltaTime;
                if (_ransomTimer <= 0f) OnRansomExpired();
            }
            // Auto-save prefs
            _prefSaveAccum += deltaTime;
            if (_prefSaveAccum >= 30f)
            {
                _prefSaveAccum = 0f;
                SaveElapsedSeconds();
            }
        }

        // ── Private helpers ───────────────────────────────────────────

        private static void OnDayChanged(int daysAdvanced)
        {
            MelonLogger.Msg($"[DCIM HackingSystem] {DateString} | Elapsed: {_elapsedSeconds:F0}s");
            _rep = Math.Min(100f, _rep + 2f * daysAdvanced);
            SaveRep();
            if (_offsiteTier > 0 && !_offsiteOverloaded)
            {
                _offsiteHP = Math.Min(_offsiteMaxHP, _offsiteHP + _offsiteTier * 5f * daysAdvanced);
                SaveOffsitePrefs();
            }
            ApplyRevenuePenalty();
            bool waveScheduled = _waveTriggerAt > _elapsedSeconds;
            if (_hackingEnabled && !_waveActive && !waveScheduled && !_dormantActive && _currentProfile != null && _rep > 10f && CalculateAttractiveness() < _currentProfile.DisengageThreshold)
                EnterDormant();
        }

        private static void SelectNewHackerProfile(int customerCount)
        {
            int maxTier = 0;
            if (customerCount >= 8  || _consecutiveWaves >= 3)  maxTier = 1;
            if (customerCount >= 12 || _consecutiveWaves >= 6)  maxTier = 2;
            if (customerCount >= 16 || _consecutiveWaves >= 10) maxTier = 3;
            _currentProfile = Profiles[UnityEngine.Random.Range(0, maxTier + 1)];
            MelonLogger.Msg("[DCIM HackingSystem] Profile: " + _currentProfile.Name);
        }

        private static void ScheduleNextWave()
        {
            if (_currentProfile == null) return;
            float days = UnityEngine.Random.Range(3f, 7f);
            if (_honeypotServerIds.Count > 0) days *= 1.4f;
            float secs = days / _currentProfile.FrequencyMultiplier * SecondsPerDay;
            _waveTriggerAt = _elapsedSeconds + secs;
            SaveTimerPrefs();
            MelonLogger.Msg($"[DCIM HackingSystem] Wave scheduled in {secs:F0}s ({secs / SecondsPerDay:F1} days) - triggers at {_waveTriggerAt:F0}s");
        }

        private static void TriggerWave()
        {
            if (_currentProfile == null) return;
            _chainBreachedThisWave = false;
            _waveHpAccumulators.Clear();
            float r = UnityEngine.Random.value;
            WaveType type;
            if (_currentProfile.Specialty == "Ransomware")               type = WaveType.Ransomware;
            else if (_currentProfile.Specialty == "Advanced Persistent Threat") type = r < 0.4f ? WaveType.DataExfiltration : WaveType.Intrusion;
            else                                                           type = r < 0.3f ? WaveType.Probe : WaveType.Intrusion;
            _activeWaves.Add(new ActiveWaveInfo { Type = type, TimeRemaining = 45f, HackerProfileName = _currentProfile.Name });
            _waveActive = true;
            _waveTriggerAt = 0f;
            _consecutiveWaves++;
            _prefConsecutiveWaves.Value = _consecutiveWaves;
            SaveTimerPrefs();
            LogAttack("Wave: " + type + " by " + _currentProfile.Name);
            try { StaticUIElements.instance.AddMeesageInField("? Attack wave incoming - " + type + "!"); } catch { }
            MelonLogger.Msg("[DCIM HackingSystem] Wave triggered: " + type);
        }

        private static void EnterDormant()
        {
            float days = UnityEngine.Random.Range(3f, 7f);
            _dormantActive      = true;
            _dormantUntil       = _elapsedSeconds + days * SecondsPerDay;
            _waveTriggerAt      = 0f;
            _currentProfile     = null;
            _hackerIdentified   = false;
            _consecutiveWaves   = 0;
            _prefConsecutiveWaves.Value = 0;
            SaveTimerPrefs();
            MelonLogger.Msg($"[DCIM HackingSystem] Attacker disengaged - dormant for {days:F1} in-game days");
            LogAttack($"Attacker disengaged - dormant {days:F1} days");
        }

        private static void TickActiveWaves(float deltaTime)
        {
            for (int i = _activeWaves.Count - 1; i >= 0; i--)
            {
                ActiveWaveInfo wave = _activeWaves[i];
                if (!_lockdownActive) wave.TimeRemaining -= deltaTime;
                if (!_lockdownActive && !_chainBreachedThisWave) DealWaveDamage(wave, deltaTime);
                if (wave.TimeRemaining <= 0f)
                {
                    if (!_chainBreachedThisWave) OnWaveDefeated();
                    _activeWaves.RemoveAt(i);
                }
            }
            if (_activeWaves.Count == 0)
            {
                _waveActive            = false;
                _chainBreachedThisWave = false;
                _serverEolAccum        = 0f;
                _waveHpAccumulators.Clear();
                ScheduleNextWave();
            }
        }

        private static void DealWaveDamage(ActiveWaveInfo wave, float deltaTime)
        {
            float dps      = 10f * (_currentProfile?.DpsMultiplier ?? 1f);
            float idsBlock = Math.Min(0.6f, _idsSwitches.Count * 0.3f);
            float dmg      = dps * (1f - idsBlock);

            if (_offsiteTier > 0 && !_offsiteOverloaded && _offsiteHP > 0f)
            {
                _offsiteHP -= dmg * deltaTime;
                if (_offsiteHP <= 0f)
                {
                    _offsiteHP = 0f;
                    _offsiteOverloaded = true;
                    SaveOffsitePrefs();
                    LogAttack("Offsite firewall overloaded - physical chain exposed");
                    try { StaticUIElements.instance.AddMeesageInField("? Offsite firewall overloaded!"); } catch { }
                }
                return;
            }
            for (int i = 0; i < _chain.Count; i++)
            {
                FirewallEntry fw = _chain[i];
                if (fw.CurrentHP <= 0) continue;
                if ((UnityEngine.Object)(object)fw.Switch != (UnityEngine.Object)null && fw.Switch.isBroken)
                {
                    fw.CurrentHP = 0;
                    continue;
                }
                if (!_waveHpAccumulators.ContainsKey(fw.SwitchId))
                    _waveHpAccumulators[fw.SwitchId] = 0f;
                _waveHpAccumulators[fw.SwitchId] += dmg * deltaTime;
                int dmgInt = (int)_waveHpAccumulators[fw.SwitchId];
                if (dmgInt < 1) return;
                _waveHpAccumulators[fw.SwitchId] -= dmgInt;
                fw.CurrentHP = Math.Max(0, fw.CurrentHP - dmgInt);
                if (fw.CurrentHP <= 0)
                {
                    LogAttack("Firewall layer " + fw.SwitchId + " BREACHED");
                    try { StaticUIElements.instance.AddMeesageInField("? Firewall layer breached!"); } catch { }
                    if (IsChainFullyBreached()) OnChainBreached(wave);
                }
                return;
            }
            if (!_chainBreachedThisWave) OnChainBreached(wave);
        }

        private static bool IsChainFullyBreached()
        {
            if (_offsiteTier > 0 && !_offsiteOverloaded && _offsiteHP > 0f) return false;
            for (int i = 0; i < _chain.Count; i++)
                if (_chain[i].CurrentHP > 0) return false;
            return true;
        }

        private static void OnChainBreached(ActiveWaveInfo wave)
        {
            if (_chainBreachedThisWave) return;
            _chainBreachedThisWave = true;
            _successiveDefenses = 0;
            float repLoss = wave.Type == WaveType.Probe ? 5f : wave.Type == WaveType.DataExfiltration ? 15f : 10f;
            OnRepLoss(repLoss);
            LogAttack($"Network BREACHED by {wave.Type} - rep -{repLoss}");
            try { StaticUIElements.instance.AddMeesageInField("? BREACH! Attackers are in the network!"); } catch { }
            if (wave.Type == WaveType.Ransomware) ApplyRansomware();
        }

        private static void OnWaveDefeated()
        {
            _successiveDefenses++;
            if (_successiveDefenses >= 3) _counterTraceAvailable = true;
            for (int i = 0; i < _chain.Count; i++)
            {
                FirewallEntry fw = _chain[i];
                fw.CurrentHP = Math.Min(fw.MaxHP, fw.CurrentHP + Math.Max(1, fw.MaxHP / 10));
            }
            LogAttack($"Wave repelled! ({_successiveDefenses} successive defense{(_successiveDefenses > 1 ? "s" : "")})");
            try { StaticUIElements.instance.AddMeesageInField($"Attack repelled! ({_successiveDefenses} in a row)"); } catch { }
            if (_counterTraceAvailable)
                try { StaticUIElements.instance.AddMeesageInField("Counter-trace available - check Security tab"); } catch { }
        }

        private static void ApplyRansomware()
        {
            if (_ransomDemandActive) return;
            Il2CppArrayBase<Server> servers = UnityEngine.Object.FindObjectsOfType<Server>();
            if (servers.Length == 0) return;
            int count = UnityEngine.Random.Range(1, Math.Min(4, servers.Length + 1));
            int added = 0;
            for (int i = 0; i < servers.Length && added < count; i++)
            {
                if (!((UnityEngine.Object)(object)servers[i] == (UnityEngine.Object)null))
                {
                    _ransomed.Add(((UnityEngine.Object)servers[i]).GetInstanceID());
                    added++;
                }
            }
            _ransomDemandActive = true;
            _ransomTimer        = 120f;
            _ransomCustomerID   = -1;
            int demand = CalculateRansomCost();
            LogAttack($"Ransomware: {added} server(s) encrypted - ${demand} demand");
            try { StaticUIElements.instance.AddMeesageInField($"? RANSOMWARE! {added} server(s) encrypted - pay ${demand} ransom!"); } catch { }
        }

        private static void OnRansomExpired()
        {
            _ransomDemandActive = false;
            _ransomTimer = 0f;
            int persistence = _currentProfile?.Persistence ?? 2;
            if (UnityEngine.Random.value < persistence * 0.1f)
            {
                OnRepLoss(15f);
                LogAttack("Ransom expired - data destroyed, rep -15");
                try { StaticUIElements.instance.AddMeesageInField("? Ransom expired - data destroyed!"); } catch { }
            }
            else
            {
                OnRepLoss(5f);
                LogAttack("Ransom expired - partial recovery, rep -5");
                try { StaticUIElements.instance.AddMeesageInField("Ransom expired - partial data recovery"); } catch { }
            }
            _ransomed.Clear();
        }

        private static void ApplyRevenuePenalty()
        {
            if (_rep >= 100f) return;
            try
            {
                float loss = CalculateTotalDailyRevenue() * (1f - _rep / 100f);
                if (loss > 1f)
                {
                    PlayerManager.instance.playerClass.UpdateCoin(-(int)loss, false);
                    MelonLogger.Msg($"[DCIM HackingSystem] Rep penalty: -${loss:F0}");
                }
            }
            catch (Exception ex) { MelonLogger.Warning("[DCIM HackingSystem] Revenue penalty error: " + ex.Message); }
        }

        private static string GetThreatLevel()
        {
            if (!_hackingEnabled) return "None";
            if (_dormantActive)   return "Dormant";
            if (_waveActive)      return _rep <= 20f ? "Critical" : "Active";
            float next = NextWaveSeconds;
            if (next > 0f && next < SecondsPerDay) return "Elevated";
            return "Low";
        }

        private static float CalculateAttractiveness()
            => CalculateTotalDailyRevenue() * 0.5f + _rep * 10f;

        private static float CalculateTotalDailyRevenue()
        {
            float total = 0f;
            try
            {
                List<SearchEngine.CustomerInfo> list = SearchEngine.GetCustomerList();
                for (int i = 0; i < list.Count; i++)
                    total += Math.Max(0f, list[i].NetRevenue);
            }
            catch { }
            return total;
        }

        private static int GetCustomerCount()
        {
            try { return MainGameManager.instance.existingCustomerIDs.Count; }
            catch { return 0; }
        }

        private static int CalculateRansomCost()
            => 1000 + _ransomed.Count * 500;

        private static void LogAttack(string msg)
        {
            string entry = "[" + DateString + "] " + msg;
            _attackLog.Add(entry);
            if (_attackLog.Count > 50) _attackLog.RemoveAt(0);
            MelonLogger.Msg("[DCIM Security] " + entry);
        }

        // ── Prefs ─────────────────────────────────────────────────────

        private static void InitPrefs()
        {
            if (_cat != null) return;
            _cat = MelonPreferences.CreateCategory("DCIM", "DCIM Mod Settings");
            _prefFirewalls        = GetOrCreate("Firewalls",        "",    "Firewall Chain");
            _prefIDS              = GetOrCreate("IDS",              "",    "IDS Switches");
            _prefHoneypots        = GetOrCreate("Honeypots",        "",    "Honeypot Servers");
            _prefOffsiteTier      = GetOrCreate("OffsiteTier",       0,    "Offsite FW Tier");
            _prefOffsiteHP        = GetOrCreate("OffsiteHP",         0f,   "Offsite FW HP");
            _prefRep              = GetOrCreate("Rep",               80f,  "Security Rep");
            _prefElapsedSeconds   = GetOrCreate("ElapsedSeconds",    0f,   "Play Time (s)");
            _prefWaveTriggerAt    = GetOrCreate("WaveTriggerAt",     0f,   "Wave Trigger Time");
            _prefDormantUntil     = GetOrCreate("DormantUntil",      0f,   "Dormant Until Time");
            _prefHackingEnabled   = GetOrCreate("HackingEnabled",    false, "Hacking Enabled");
            _prefConsecutiveWaves = GetOrCreate("ConsecutiveWaves",  0,    "Consecutive Waves");
        }

        private static MelonPreferences_Entry<T> GetOrCreate<T>(string key, T def, string label)
            => _cat.GetEntry<T>(key) ?? _cat.CreateEntry<T>(key, def, label, null, false, false, null, null);

        private static void LoadFromPrefs()
        {
            _chain.Clear(); _eolAccumulators.Clear();
            _idsSwitches.Clear(); _idsSwitchIds.Clear();
            _honeypotServerIds.Clear(); _honeypotServers.Clear();

            string fwStr = _prefFirewalls.Value ?? "";
            if (!string.IsNullOrEmpty(fwStr))
            {
                string[] parts = fwStr.Split(';');
                for (int i = 0; i < parts.Length; i++)
                {
                    string p = parts[i].Trim();
                    if (string.IsNullOrEmpty(p)) continue;
                    string[] kv = p.Split(':');
                    if (kv.Length < 2) continue;
                    string swId = kv[0];
                    if (!int.TryParse(kv[1], out int hp)) hp = 100;
                    NetworkSwitch sw = FindSwitchById(swId);
                    _chain.Add(new FirewallEntry { SwitchId = swId, MaxHP = hp, CurrentHP = hp, Switch = sw });
                    _eolAccumulators[swId] = 0f;
                }
            }

            string idsStr = _prefIDS.Value ?? "";
            if (!string.IsNullOrEmpty(idsStr))
            {
                string[] parts = idsStr.Split(';');
                for (int i = 0; i < parts.Length; i++)
                {
                    string p = parts[i].Trim();
                    if (string.IsNullOrEmpty(p)) continue;
                    _idsSwitchIds.Add(p);
                    NetworkSwitch sw = FindSwitchById(p);
                    if ((UnityEngine.Object)(object)sw != (UnityEngine.Object)null) _idsSwitches.Add(sw);
                }
            }

            string hpStr = _prefHoneypots.Value ?? "";
            if (!string.IsNullOrEmpty(hpStr))
            {
                string[] parts = hpStr.Split(';');
                for (int i = 0; i < parts.Length; i++)
                {
                    string p = parts[i].Trim();
                    if (!string.IsNullOrEmpty(p)) _honeypotServerIds.Add(p);
                }
                Il2CppArrayBase<Server> allServers = UnityEngine.Object.FindObjectsOfType<Server>();
                for (int i = 0; i < allServers.Length; i++)
                    if (_honeypotServerIds.Contains(allServers[i].ServerID)) _honeypotServers.Add(allServers[i]);
            }

            _offsiteTier   = _prefOffsiteTier.Value;
            _offsiteHP     = _prefOffsiteHP.Value;
            _offsiteMaxHP  = _offsiteTier > 0 ? _offsiteTier * 50f : 0f;
            float rep      = _prefRep.Value;
            _rep           = rep > 0f ? rep : 80f;
            _elapsedSeconds   = _prefElapsedSeconds.Value;
            _waveTriggerAt    = _prefWaveTriggerAt.Value;
            _dormantUntil     = _prefDormantUntil.Value;
            _hackingEnabled   = _prefHackingEnabled.Value;
            _consecutiveWaves = _prefConsecutiveWaves.Value;
            _dormantActive    = _dormantUntil > _elapsedSeconds;
        }

        private static NetworkSwitch FindSwitchById(string switchId)
        {
            try
            {
                Il2CppArrayBase<NetworkSwitch> all = UnityEngine.Object.FindObjectsOfType<NetworkSwitch>();
                for (int i = 0; i < all.Length; i++)
                    if (all[i].GetSwitchId() == switchId) return all[i];
            }
            catch { }
            return null;
        }

        private static void SaveFirewalls()
        {
            if (_prefFirewalls == null) return;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < _chain.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(_chain[i].SwitchId).Append(':').Append(_chain[i].MaxHP);
            }
            _prefFirewalls.Value = sb.ToString();
            MelonPreferences.Save();
        }

        private static void SaveIDS()
        {
            if (_prefIDS == null) return;
            StringBuilder sb = new StringBuilder();
            bool first = true;
            foreach (string id in _idsSwitchIds)
            {
                if (!first) sb.Append(';');
                sb.Append(id);
                first = false;
            }
            _prefIDS.Value = sb.ToString();
            MelonPreferences.Save();
        }

        private static void SaveHoneypots()
        {
            if (_prefHoneypots == null) return;
            _prefHoneypots.Value = string.Join(";", _honeypotServerIds);
            MelonPreferences.Save();
        }

        private static void SaveOffsitePrefs()
        {
            if (_prefOffsiteTier == null) return;
            _prefOffsiteTier.Value = _offsiteTier;
            _prefOffsiteHP.Value   = _offsiteHP;
            MelonPreferences.Save();
        }

        private static void SaveRep()
        {
            if (_prefRep == null) return;
            _prefRep.Value = _rep;
            MelonPreferences.Save();
        }

        private static void SaveElapsedSeconds()
        {
            if (_prefElapsedSeconds == null) return;
            _prefElapsedSeconds.Value = _elapsedSeconds;
            MelonPreferences.Save();
        }

        private static void SaveTimerPrefs()
        {
            if (_prefWaveTriggerAt == null) return;
            _prefElapsedSeconds.Value  = _elapsedSeconds;
            _prefWaveTriggerAt.Value   = _waveTriggerAt;
            _prefDormantUntil.Value    = _dormantUntil;
            _prefHackingEnabled.Value  = _hackingEnabled;
            MelonPreferences.Save();
        }
    }
}
#endif
