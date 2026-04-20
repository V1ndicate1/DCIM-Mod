using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace AutoDispatcher.Patches
{
    [HarmonyPatch(typeof(Server), "ItIsBroken")]
    public class ServerBreakPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Server __instance)
        {
            if (__instance == null) { MelonLogger.Warning("[AD] Server.ItIsBroken fired with null instance"); return; }
            DispatchController.OnDeviceBroken(__instance, null);
        }
    }

    [HarmonyPatch(typeof(NetworkSwitch), "ItIsBroken")]
    public class SwitchBreakPatch
    {
        [HarmonyPostfix]
        public static void Postfix(NetworkSwitch __instance)
        {
            if (__instance == null) { MelonLogger.Warning("[AD] NetworkSwitch.ItIsBroken fired with null instance"); return; }
            DispatchController.OnDeviceBroken(null, __instance);
        }
    }

    [HarmonyPatch(typeof(Server), "RepairDevice")]
    public class ServerRepairPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Server __instance)
        {
            if (__instance == null) { MelonLogger.Warning("[AD] Server.RepairDevice fired with null instance"); return; }
            DispatchController.OnDeviceRepaired(__instance, null);
        }
    }

    [HarmonyPatch(typeof(NetworkSwitch), "RepairDevice")]
    public class SwitchRepairPatch
    {
        [HarmonyPostfix]
        public static void Postfix(NetworkSwitch __instance)
        {
            if (__instance == null) { MelonLogger.Warning("[AD] NetworkSwitch.RepairDevice fired with null instance"); return; }
            DispatchController.OnDeviceRepaired(null, __instance);
        }
    }
}
