using HarmonyLib;

namespace BuildInSeatruckPlus.Patches
{
    // The collider fix applied when a buildable is placed (or a plant rendered) is
    // runtime-only: on save/load the game respawns everything from prefabs on their
    // solid default layers, and our placement/SetupRenderers hooks don't re-run for
    // the restored objects, so docking gets blocked again. Re-apply the fix at the
    // two maneuvers that precede a collision. Both re-run every session, so the fix
    // no longer depends on having been placed this session.

    // Driving in to dock: StartPiloting is the entry point for piloting (and re-fires
    // on load via SeaTruckMotor.Start when you saved while piloting). Re-layer the
    // piloted segment and its attached modules' buildables before the player can
    // drive into the dock.
    [HarmonyPatch(typeof(SeaTruckMotor), nameof(SeaTruckMotor.StartPiloting))]
    internal static class SeaTruckMotor_StartPiloting_Patch
    {
        static void Postfix(SeaTruckMotor __instance)
        {
            if (__instance != null && __instance.truckSegment != null)
                SeaTruckSegmentHelper.NeutralizeBuiltColliders(__instance.truckSegment.gameObject);
        }
    }

    // Undocking out: the cab is force-ejected from the dock, which StartPiloting does
    // not precede (undocking hands control back via SetPiloting). Cover the case of a
    // game loaded while docked. The tail is detached and tracked separately at this
    // point, so re-layer both the docked head and the docked tail chains.
    [HarmonyPatch(typeof(MoonpoolExpansionManager), nameof(MoonpoolExpansionManager.StartUndocking))]
    internal static class MoonpoolExpansionManager_StartUndocking_Patch
    {
        static void Postfix(MoonpoolExpansionManager __instance)
        {
            var head = __instance.GetDockedHead();
            if (head != null)
                SeaTruckSegmentHelper.NeutralizeBuiltColliders(head.gameObject);

            var tail = __instance.GetDockedTail();
            if (tail != null)
                SeaTruckSegmentHelper.NeutralizeBuiltColliders(tail.gameObject);
        }
    }
}
