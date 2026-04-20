using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace AutoDispatcher
{
    public static class DispatchController
    {
        private static readonly HashSet<string> _assignedDevices = new HashSet<string>();

        private static Server[]        _serverCache = System.Array.Empty<Server>();
        private static NetworkSwitch[] _switchCache = System.Array.Empty<NetworkSwitch>();

        private static float _lastWatchdogRealtime = 0f;
        private const float WATCHDOG_INTERVAL = 30f;

        // ── Stuck tech detection ─────────────────────────────────────────────
        private static readonly Dictionary<int, string>  _techTargetDevice = new Dictionary<int, string>();
        private static readonly Dictionary<int, Vector3> _lastPosition     = new Dictionary<int, Vector3>();
        private static readonly Dictionary<int, float>   _frozenSince      = new Dictionary<int, float>();
        private const float STUCK_TIMEOUT   = 120f; // seconds frozen in place before considered stuck
        private const float MOVE_THRESHOLD  = 0.1f; // minimum movement (units) to count as progress

        // ── Stuck device tracking — retry once on first stuck, cooldown on repeat ─
        private static readonly Dictionary<string, float> _stuckCooldowns = new Dictionary<string, float>();
        private static readonly Dictionary<string, int>   _stuckCount     = new Dictionary<string, int>();
        private const float STUCK_COOLDOWN = 300f; // 5 minutes before retrying after 2+ stucks

        // ── Load cooldown — suppresses polling after a save load ────────────
        private static float _loadCooldown = 0f;
        private const float LOAD_COOLDOWN_SECONDS = 10f;
        private static bool _postLoadDiagPending = false;

        // ── Entry points from Harmony patches ────────────────────────────────

        public static void OnDeviceBroken(Server server, NetworkSwitch sw)
        {
            if (!AutoDispatcherMod.IsEnabled) return;
            if (_loadCooldown > 0f || _postLoadDiagPending) return;
            string id = DeviceId(server, sw);
            if (_assignedDevices.Contains(id)) return;
            TryDispatch(server, sw, id);
        }

        public static void OnDeviceRepaired(Server server, NetworkSwitch sw)
        {
            string id = DeviceId(server, sw);
            _assignedDevices.Remove(id);
            _stuckCount.Remove(id);
            _stuckCooldowns.Remove(id);
        }

        // ── Main poll — called every 2 seconds ───────────────────────────────

        public static void Poll()
        {
            if (!AutoDispatcherMod.IsEnabled) return;

            // Load cooldown — wait for game to fully restore its state
            if (_loadCooldown > 0f)
            {
                _loadCooldown -= 2f; // poll runs every 2s
                if (_loadCooldown <= 0f)
                {
                    _loadCooldown = 0f;
                    _postLoadDiagPending = true;
                }
                return;
            }

            // Post-load — skip one dispatch cycle to let game state settle
            if (_postLoadDiagPending)
            {
                _postLoadDiagPending = false;
                return;
            }

            _serverCache = Object.FindObjectsOfType<Server>();
            _switchCache = Object.FindObjectsOfType<NetworkSwitch>();

            // Broken pass — highest priority, uses all free techs
            bool anyBrokenPending = false;
            foreach (var s in _serverCache)
            {
                if (s == null || !s.isBroken) continue;
                string id = "server_" + s.ServerID;
                if (_assignedDevices.Contains(id)) continue;
                anyBrokenPending = true;
                TryDispatch(s, null, id);
            }
            foreach (var sw in _switchCache)
            {
                if (sw == null || !sw.isBroken) continue;
                string id = "switch_" + sw.GetSwitchId();
                if (_assignedDevices.Contains(id)) continue;
                anyBrokenPending = true;
                TryDispatch(null, sw, id);
            }

            // EOL pass — orange devices (eolTime <= 0), only if no broken pending
            if (!anyBrokenPending && AutoDispatcherMod.IsEolEnabled)
            {
                foreach (var s in _serverCache)
                {
                    if (s == null || s.isBroken || s.eolTime > 0) continue;
                    string id = "server_" + s.ServerID;
                    if (_assignedDevices.Contains(id)) continue;
                    TryDispatch(s, null, id);
                }
                foreach (var sw in _switchCache)
                {
                    if (sw == null || sw.isBroken || sw.eolTime > 0) continue;
                    string id = "switch_" + sw.GetSwitchId();
                    if (_assignedDevices.Contains(id)) continue;
                    TryDispatch(null, sw, id);
                }
            }

            // Warning suppression — clear signs every poll tick
            if (AutoDispatcherMod.IsWarnEnabled)
                SuppressDeviceWarnings();

            // Watchdog — every 30s, clear orphaned device IDs
            float now = Time.realtimeSinceStartup;
            if (now - _lastWatchdogRealtime >= WATCHDOG_INTERVAL)
            {
                _lastWatchdogRealtime = now;
                RunWatchdog();
                CheckStuckTechs();
            }
        }

        // ── Dispatch ─────────────────────────────────────────────────────────

        private static bool TryDispatch(Server server, NetworkSwitch sw, string id)
        {
            var tm = TechnicianManager.instance;
            if (tm == null) return false;

            // Skip devices that recently caused repeated stuck techs
            if (_stuckCooldowns.TryGetValue(id, out float cooldownEnd))
            {
                if (Time.realtimeSinceStartup < cooldownEnd)
                    return false;
                // Cooldown expired — give the device a fresh start
                _stuckCooldowns.Remove(id);
                _stuckCount.Remove(id);
            }

            // Count real techs (list can contain stale objects after reload)
            int realTechCount = 0;
            int freeTechCount = 0;
            for (int i = 0; i < tm.technicians.Count; i++)
            {
                var t = tm.technicians[i];
                if (t == null) continue;
                realTechCount++;
                if (!t.isBusy) freeTechCount++;
            }

            // Capacity check — don't dispatch if no free techs
            if (freeTechCount <= 0)
                return false;

            // Check active jobs for this device (by ID)
            var activeJobs = tm.GetActiveJobs();
            for (int i = 0; i < activeJobs.Count; i++)
            {
                var j = activeJobs[i];
                if (server != null && j.server != null && j.server.ServerID == server.ServerID)
                {
                    _assignedDevices.Add(id);
                    return false;
                }
                if (sw != null && j.networkSwitch != null && j.networkSwitch.GetSwitchId() == sw.GetSwitchId())
                {
                    _assignedDevices.Add(id);
                    return false;
                }
            }

            // Check queued jobs for this device (by ID)
            var queuedJobs = tm.GetQueuedJobs();
            for (int i = 0; i < queuedJobs.Count; i++)
            {
                var j = queuedJobs[i];
                if (server != null && j.server != null && j.server.ServerID == server.ServerID)
                {
                    _assignedDevices.Add(id);
                    return false;
                }
                if (sw != null && j.networkSwitch != null && j.networkSwitch.GetSwitchId() == sw.GetSwitchId())
                {
                    _assignedDevices.Add(id);
                    return false;
                }
            }

            // ── Cost deduction ───────────────────────────────────────────────
            int cost = 0;
            if (server != null && server.item != null)
                cost = server.item.price;
            else if (sw != null && sw.item != null)
                cost = sw.item.price;
            else
                cost = GetFallbackPrice(server, sw);

            if (cost > 0)
            {
                var pm = PlayerManager.instance;
                if (pm == null || pm.playerClass == null)
                {
                    MelonLogger.Warning($"[AD] Cannot deduct cost for {id}: PlayerManager not available");
                    return false;
                }
                if (pm.playerClass.money < cost)
                {
                    MelonLogger.Msg($"[AD] Cannot afford {id}: need ${cost}, have ${pm.playerClass.money:F0}");
                    return false;
                }
                pm.playerClass.UpdateCoin(-cost, true);
            }

            // Use the game's proper dispatch API — handles queueing correctly
            tm.SendTechnician(sw, server);
            _assignedDevices.Add(id);
            return true;
        }

        // ── Free retry after first stuck — no cost deduction ─────────────────

        private static void RetryDispatch(Server server, NetworkSwitch sw, string id)
        {
            var tm = TechnicianManager.instance;
            if (tm == null) return;

            bool hasFree = false;
            for (int i = 0; i < tm.technicians.Count; i++)
            {
                var t = tm.technicians[i];
                if (t != null && !t.isBusy) { hasFree = true; break; }
            }

            if (!hasFree)
            {
                MelonLogger.Msg($"[AD]   Retry skipped — no free techs, will dispatch next poll");
                return;
            }

            tm.SendTechnician(sw, server);
            _assignedDevices.Add(id);
            MelonLogger.Msg($"[AD]   Retry dispatched {id} (no charge)");
        }

        // ── Watchdog — clears orphans so they re-enter the pool ──────────────

        private static void RunWatchdog()
        {
            var tm = TechnicianManager.instance;
            if (tm == null) return;

            var gameIds = new HashSet<string>();
            int nullRefJobs = 0;

            var activeJobs = tm.GetActiveJobs();
            for (int i = 0; i < activeJobs.Count; i++)
            {
                var j = activeJobs[i];
                if (j.server != null) gameIds.Add("server_" + j.server.ServerID);
                else if (j.networkSwitch != null) gameIds.Add("switch_" + j.networkSwitch.GetSwitchId());
                else nullRefJobs++;
            }

            var queuedJobs = tm.GetQueuedJobs();
            for (int i = 0; i < queuedJobs.Count; i++)
            {
                var j = queuedJobs[i];
                if (j.server != null) gameIds.Add("server_" + j.server.ServerID);
                else if (j.networkSwitch != null) gameIds.Add("switch_" + j.networkSwitch.GetSwitchId());
                else nullRefJobs++;
            }

            // If there are null-ref jobs, we're in a post-load state with stale data
            // Don't clear orphans — we can't reliably tell what's actually orphaned
            if (nullRefJobs > 0)
            {
                MelonLogger.Msg($"[AD] Watchdog: {nullRefJobs} null-ref jobs in queue — skipping cleanup");
                return;
            }

            var orphans = new List<string>();
            foreach (var id in _assignedDevices)
                if (!gameIds.Contains(id)) orphans.Add(id);

            if (orphans.Count > 0)
                MelonLogger.Msg($"[AD] Watchdog: clearing {orphans.Count} orphaned device(s)");

            foreach (var id in orphans)
                _assignedDevices.Remove(id);
        }

        // ── Stuck tech detection — runs every watchdog tick ────────────────────

        private static void CheckStuckTechs()
        {
            var tm = TechnicianManager.instance;
            if (tm == null) return;

            float now = Time.realtimeSinceStartup;

            for (int i = 0; i < tm.technicians.Count; i++)
            {
                var tech = tm.technicians[i];
                if (tech == null) continue;

                if (tech.isBusy)
                {
                    // Track position-based progress — only reset a tech that is frozen in place,
                    // not one that is actively walking to or from a device
                    Vector3 pos = Vector3.zero;
                    try { pos = tech.transform.position; } catch { }

                    if (_lastPosition.TryGetValue(tech.technicianID, out Vector3 prevPos))
                    {
                        if (Vector3.Distance(pos, prevPos) > MOVE_THRESHOLD)
                        {
                            // Tech moved — reset frozen timer, they're making progress
                            _frozenSince[tech.technicianID] = now;
                        }
                        else if (!_frozenSince.ContainsKey(tech.technicianID))
                        {
                            // First tick with no movement — start frozen timer
                            _frozenSince[tech.technicianID] = now;
                        }
                    }
                    else
                    {
                        // First time we see this tech busy — record position, start frozen timer
                        _frozenSince[tech.technicianID] = now;
                    }
                    _lastPosition[tech.technicianID] = pos;

                    float frozenElapsed = _frozenSince.TryGetValue(tech.technicianID, out float fs) ? now - fs : 0f;
                    if (frozenElapsed > STUCK_TIMEOUT)
                    {
                        // Capture device refs BEFORE UnstickTech clears tech.server/tech.networkSwitch
                        string stuckDeviceId = null;
                        Server stuckServer = null;
                        NetworkSwitch stuckSwitch = null;
                        try
                        {
                            if (tech.server != null)
                            {
                                stuckServer    = tech.server;
                                stuckDeviceId  = "server_" + tech.server.ServerID;
                            }
                            else if (tech.networkSwitch != null)
                            {
                                stuckSwitch   = tech.networkSwitch;
                                stuckDeviceId = "switch_" + tech.networkSwitch.GetSwitchId();
                            }
                        }
                        catch { }

                        MelonLogger.Warning($"[AD] Tech {tech.technicianName} stuck busy for {frozenElapsed:F0}s — forcing reset");
                        UnstickTech(tech);

                        _lastPosition.Remove(tech.technicianID);
                        _frozenSince.Remove(tech.technicianID);

                        if (stuckDeviceId != null)
                        {
                            _stuckCount.TryGetValue(stuckDeviceId, out int prevCount);
                            int newCount = prevCount + 1;
                            _stuckCount[stuckDeviceId] = newCount;

                            if (newCount == 1)
                            {
                                // First stuck — could be a transient pathfinding glitch, retry for free
                                MelonLogger.Msg($"[AD]   {stuckDeviceId} first stuck — retrying (no charge)");
                                RetryDispatch(stuckServer, stuckSwitch, stuckDeviceId);
                            }
                            else
                            {
                                // Repeated stuck — repair coroutine is broken, apply cooldown
                                _stuckCooldowns[stuckDeviceId] = now + STUCK_COOLDOWN;
                                MelonLogger.Msg($"[AD]   {stuckDeviceId} stuck {newCount} times — 5-min cooldown applied");
                            }
                        }
                    }
                }
                else
                {
                    // Tech is idle — clear all tracking state
                    _lastPosition.Remove(tech.technicianID);
                    _frozenSince.Remove(tech.technicianID);

                    // Check for visual artifact: idle but still holding a device
                    try
                    {
                        var deviceObj = tech.deviceInHand;
                        if (deviceObj != null)
                        {
                            MelonLogger.Warning($"[AD] Tech {tech.technicianName} idle but holding device — cleaning up");
                            UnstickTech(tech);
                        }
                    }
                    catch { /* IL2CPP null — field not accessible, skip */ }
                }
            }
        }

        private static void UnstickTech(Technician tech)
        {
            try
            {
                // 1. Stop all running coroutines (movement, carry, IK transitions)
                tech.StopAllCoroutines();

                // 2. Destroy the held device model (the visual server/switch stuck on them)
                try
                {
                    var device = tech.deviceInHand;
                    if (device != null)
                    {
                        Object.Destroy(device);
                        MelonLogger.Msg($"[AD]   Destroyed deviceInHand on {tech.technicianName}");
                    }
                    tech.deviceInHand = null; // clear ref so we don't re-detect as stuck
                }
                catch { /* IL2CPP null */ }

                // 3. Reset tech state to idle
                tech.isBusy = false;
                try { tech.currentState = 0; } // TechnicianState.Idle = 0
                catch { /* enum access failed */ }

                // 4. Clear stale job target references
                try { tech.server = null; } catch { }
                try { tech.networkSwitch = null; } catch { }

                // 5. Reset hand IK weights so arms go back to normal
                try
                {
                    var leftIK = tech.leftHandIK;
                    if (leftIK != null) leftIK.weight = 0f;
                }
                catch { }
                try
                {
                    var rightIK = tech.rightHandIK;
                    if (rightIK != null) rightIK.weight = 0f;
                }
                catch { }

                // 6. Send tech back to their idle position
                try
                {
                    var charCtrl = tech.characterControl;
                    var idlePos = tech.transformIdle;
                    if (charCtrl != null && idlePos != null)
                        charCtrl.SetTarget(idlePos.position);
                }
                catch { }

                MelonLogger.Msg($"[AD] Tech {tech.technicianName} unstuck and reset to idle");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[AD] Failed to unstick {tech.technicianName}: {ex.Message}");
            }
        }

        // ── Rebuild from game state after save load ───────────────────────────

        public static void RebuildFromGameState()
        {
            // Ignore repeat calls while cooldown is already active
            if (_loadCooldown > 0f) return;

            MelonLogger.Msg("[AD] Load detected — clearing state, starting 10s cooldown");
            _assignedDevices.Clear();
            _lastPosition.Clear();
            _frozenSince.Clear();
            _stuckCooldowns.Clear();
            _stuckCount.Clear();
            _loadCooldown = LOAD_COOLDOWN_SECONDS;
            _postLoadDiagPending = false;
        }

        // ── Warning suppression — called every poll tick ─────────────────────

        public static void SuppressDeviceWarnings()
        {
            foreach (var s in _serverCache)
            {
                if (s == null) continue;
                if (!s.isWarningCleared) s.ClearWarningSign(true);
                if (s.isBroken) s.ClearErrorSign();
            }
            foreach (var sw in _switchCache)
            {
                if (sw == null) continue;
                if (!sw.isWarningCleared) sw.ClearWarningSign(true);
                if (sw.isBroken) sw.ClearErrorSign();
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string DeviceId(Server server, NetworkSwitch sw)
            => server != null ? "server_" + server.ServerID : "switch_" + (sw?.GetSwitchId() ?? "null");

        // ── Fallback prices (from shop) when item reference is null ─────────
        private static readonly Dictionary<string, int> _fallbackServerPrices = new Dictionary<string, int>
        {
            { "System X 3U 5000 IOPS", 400 },
            { "System X 7U 12000 IOPS", 1600 },
            { "RISC 3U 5000 IOPS", 450 },
            { "RISC 7U 12000 IOPS", 1750 },
            { "Mainframe 3U 5000 IOPS", 850 },
            { "Mainframe 7U 12000 IOPS", 2000 },
            { "GPU 3U 5000 IOPS", 550 },
            { "GPU 7U 12000 IOPS", 2200 },
        };

        private static readonly Dictionary<string, int> _fallbackSwitchPrices = new Dictionary<string, int>
        {
            { "16 x 10Gbps RJ45", 250 },
            { "4 x SFP+/SFP28", 400 },
            { "32 x QSFP+", 3800 },
            { "4 x QSFP+ 16 x SFP+/SFP28", 3500 },
        };

        private static int GetFallbackPrice(Server server, NetworkSwitch sw)
        {
            try
            {
                var mgm = MainGameManager.instance;
                if (mgm == null) return 0;

                if (server != null)
                {
                    string name = mgm.ReturnServerNameFromType(server.serverType);
                    if (name != null && _fallbackServerPrices.TryGetValue(name, out int p))
                        return p;
                }
                else if (sw != null)
                {
                    string name = mgm.ReturnSwitchNameFromType(sw.switchType);
                    if (name != null && _fallbackSwitchPrices.TryGetValue(name, out int p))
                        return p;
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[AD] Fallback price lookup failed: {ex.Message}");
            }
            return 0;
        }
    }
}
