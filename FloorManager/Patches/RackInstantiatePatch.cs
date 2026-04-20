using Il2Cpp;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace FloorManager.Patches
{
    [HarmonyPatch(typeof(RackMount), "InstantiateRack")]
    public class RackInstantiatePatch
    {
        [HarmonyPostfix]
        public static void Postfix(RackMount __instance, GameObject __result)
        {
            if (__result == null) return;

            // Re-scan all racks and refresh labels for consistent numbering
            RackLabelManager.RefreshAllLabels();
        }
    }
}
