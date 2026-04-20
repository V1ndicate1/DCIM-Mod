using Il2Cpp;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;

[assembly: MelonInfo(typeof(FloorManager.FloorManagerMod), "DCIM", "1.0.3", "V1ndicate1")]
[assembly: MelonGame("Waseku", "Data Center")]

namespace FloorManager
{
    public class FloorManagerMod : MelonMod
    {
        public static FloorManagerMod Instance { get; private set; }

        // EOL warning thresholds (seconds). Match these to your game's Command Center auto-repair settings.
        // 2 hours = 7200, 4 hours = 14400
        public const int EOL_WARN_SECONDS     = 7200;   // within this → yellow warning
        public const int EOL_APPROACH_SECONDS = 14400;  // within this → amber approaching

        // Static refs for cross-file access
        public static GameObject DCIMScreen;
        public static ComputerShop ComputerShopRef;
        public static GameObject MainScreenRef;

        public override void OnInitializeMelon()
        {
            Instance = this;
            SaveSystem.onLoadingDataLater += (System.Action)OnSaveLoaded;
            // Initialize prefs at game start so saved values load from disk immediately,
            // not lazily on first laptop open.
            RackDiagramPanel.InitPrefs();
            FloorMapApp.InitPrefs();
            MelonLogger.Msg("[DCIM] v1.0.3 loaded.");
        }

        public override void OnLateUpdate()
        {
            // Kill Unity's selection highlight every frame — prevents the blue box artifact
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
                EventSystem.current.SetSelectedGameObject(null);

            // If DCIM screen is active but the laptop canvas was closed (ESC), hide it
            if (DCIMScreen != null && DCIMScreen.activeSelf && ComputerShopRef != null)
            {
                var laptopCanvas = ComputerShopRef.canvasComputerShop;
                if (laptopCanvas != null && !laptopCanvas.activeSelf)
                {
                    RackDiagramPanel.HideMiniShop();
                    DCIMScreen.SetActive(false);
                }
            }

#if !STRIP_HACKING
            // HackingSystem per-frame tick (EOL drain, lockdown timer, ransom timer)
            HackingSystem.OnLateUpdateTick(Time.deltaTime);
#endif
        }

        private static void OnSaveLoaded()
        {
            MelonLogger.Msg("[DCIM] Save loaded — initializing 3D rack labels.");
            RackLabelManager.RefreshAllLabels();
#if !STRIP_HACKING
            HackingSystem.OnSaveLoaded();
#endif
            FloorMapApp.RestoreRackColors();
        }
    }

#if !STRIP_HACKING
    [HarmonyPatch(typeof(NetworkSwitch), nameof(NetworkSwitch.RepairDevice))]
    class NetworkSwitchRepairPatch
    {
        static void Postfix(NetworkSwitch __instance)
            => HackingSystem.OnFirewallRepaired(__instance);
    }

    [HarmonyPatch(typeof(Server), nameof(Server.RepairDevice))]
    class ServerRepairPatch
    {
        static void Postfix(Server __instance)
            => HackingSystem.OnServerRepaired(__instance);
    }
#endif
}
