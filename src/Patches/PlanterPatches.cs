using HarmonyLib;
using UnityEngine;

namespace BuildInSeatruckPlus.Patches
{
    // A planter built in the Seatruck is fixed up at placement, but plants are
    // spawned into it later (seedlings via Planter.AddItem, grown models via
    // GrowingPlant's async spawn). Those plant models keep solid colliders on the
    // Default layer (Planter.SetupRenderers puts them there), so a growing plant
    // re-introduces exactly the docking/undocking snag we cleared for buildables.
    //
    // SetupRenderers is the one chokepoint both spawn paths run through *after* the
    // plant is parented under the planter, with the planter as __instance, so a
    // postfix here catches seedlings, instantly-grown plants, async grown models,
    // and plants restored on save load. Only act when the planter is inside a
    // Seatruck; every other planter in the game is untouched.
    [HarmonyPatch(typeof(Planter), nameof(Planter.SetupRenderers))]
    internal static class Planter_SetupRenderers_Patch
    {
        static void Postfix(Planter __instance, GameObject gameObject)
        {
            if (__instance == null || gameObject == null)
                return;
            if (__instance.GetComponentInParent<SeaTruckSegment>() == null)
                return;
            SeaTruckSegmentHelper.NeutralizeCollidersForDocking(gameObject);
        }
    }
}
