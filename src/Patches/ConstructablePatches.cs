using HarmonyLib;

namespace SeatruckJukebox.Patches
{
    // When the player is standing inside a Seatruck segment, allow construction
    // flags to pass. Vanilla CheckFlags falls through to its "outdoors" branch for
    // the Seatruck (which is not a SubRoot), rejecting interior modules. Scoped to
    // the Seatruck only; everywhere else vanilla runs unchanged.
    [HarmonyPatch(typeof(Constructable), nameof(Constructable.CheckFlags))]
    internal static class Constructable_CheckFlags_Patch
    {
        static bool Prefix(ref bool __result)
        {
            if (SeaTruckSegmentHelper.getCurrentSeaTruckSegment() == null)
                return true; // not in a Seatruck: run vanilla

            __result = true;
            return false;
        }
    }
}
