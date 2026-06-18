using HarmonyLib;

namespace SeatruckJukebox.Patches
{
    [HarmonyPatch(typeof(Constructable), nameof(Constructable.CheckFlags))]
    internal static class Constructable_CheckFlags_Patch
    {
        static bool Prefix(bool allowedInBase, bool allowedInSub, bool allowedOutside,
                           bool allowedUnderwater, UnityEngine.Vector3 hitPoint, ref bool __result)
        {
            var seg = SeaTruckSegmentHelper.getCurrentSeaTruckSegment();
            if (seg == null || seg.isFrontConnected || seg.isRearConnected)
                return true; // not in a standalone seatruck segment: run vanilla

            var sub = SeaTruckSegmentHelper.getCurrentSeaTruckSegmentSubRoot();
            if (sub == null)
                sub = seg.gameObject.AddComponent<SubRoot>();
            if (sub == null)
                return true;

            sub.isCyclops = true;
            sub.modulesRoot = seg.transform;

            __result = allowedInSub;
            return false;
        }
    }
}
