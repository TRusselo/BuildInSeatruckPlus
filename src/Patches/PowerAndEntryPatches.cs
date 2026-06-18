using HarmonyLib;
using UnityEngine;

namespace BuildInSeatruckPlus.Patches
{
    [HarmonyPatch(typeof(PowerConsumer), nameof(PowerConsumer.IsPowered))]
    internal static class PowerConsumer_IsPowered_Patch
    {
        static bool Prefix(PowerConsumer __instance, ref bool __result)
        {
            var seg = SeaTruckSegmentHelper.getParentSeaTruckSegment(__instance.gameObject);
            if (seg == null)
                return true;
            __result = ((IInteriorSpace)seg).CanBreathe();
            return false;
        }
    }

    [HarmonyPatch(typeof(SeaTruckSegment), nameof(SeaTruckSegment.CanEnter))]
    internal static class SeaTruckSegment_CanEnter_Patch
    {
        static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }
}
