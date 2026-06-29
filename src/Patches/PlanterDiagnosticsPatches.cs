using System.Text;
using HarmonyLib;
using UnityEngine;
using UWE;

namespace BuildInSeatruckPlus.Patches
{
    // DIAGNOSTIC ONLY - no gameplay change.
    //
    // We are tracking down why grown plants vanish from planters built inside a
    // Seatruck (the planter shell survives but comes back empty). A plant's
    // survival depends on the *item* in the planter's StorageContainer surviving
    // a save/load: that item carries the slot id and growth age, and the visible
    // plant is rebuilt from it. Fruit-bearing plants additionally swap their
    // original seed item for a different "plant" item at 100% growth via
    // Planter.ReplaceItem, so the stored item is not always the seed you planted.
    //
    // These patches log, for Seatruck planters only, exactly what is in the
    // planter's storage at save time, at load time, and a few seconds after load
    // (once the game's async restore has run). Comparing those three across a
    // session where a plant is lost tells us whether the item was dropped from
    // the save stream, or culled during the planter's async restore.
    //
    // Everything is wrapped so a logging error can never affect the game.
    internal static class PlanterDiagnostics
    {
        private const string Tag = "[PlantDiag]";

        internal static bool IsSeatruckPlanter(Planter p)
        {
            return p != null && p.GetComponentInParent<SeaTruckSegment>() != null;
        }

        internal static string PlanterId(Planter p)
        {
            var uid = p.GetComponent<UniqueIdentifier>() ?? p.GetComponentInParent<UniqueIdentifier>();
            return uid != null ? uid.Id : "<no-uid>";
        }

        // Dumps the raw storageRoot children (the source of truth for what is
        // serialized) and the grownPlantsRoot child count. Reading storageRoot
        // transform children works even before the StorageContainer has rebuilt
        // its in-memory ItemsContainer on load.
        internal static void Dump(Planter p, string phase)
        {
            try
            {
                if (p == null)
                    return;

                var sb = new StringBuilder();
                sb.Append(Tag).Append(' ').Append(phase)
                  .Append(" planter=").Append(PlanterId(p));

                var seg = p.GetComponentInParent<SeaTruckSegment>();
                sb.Append(" seg=").Append(seg != null ? seg.name : "<none>");

                int grown = p.grownPlantsRoot != null ? p.grownPlantsRoot.childCount : -1;
                sb.Append(" grownModels=").Append(grown);

                Transform storageRoot = (p.storageContainer != null && p.storageContainer.storageRoot != null)
                    ? p.storageContainer.storageRoot.transform
                    : null;
                int stored = storageRoot != null ? storageRoot.childCount : -1;
                sb.Append(" storedItems=").Append(stored);

                Plugin.Log.LogInfo(sb.ToString());

                if (storageRoot != null)
                {
                    for (int i = 0; i < storageRoot.childCount; i++)
                    {
                        var child = storageRoot.GetChild(i);
                        var line = new StringBuilder();
                        line.Append(Tag).Append("   item[").Append(i).Append("] ").Append(child.name);

                        var pick = child.GetComponent<Pickupable>();
                        line.Append(" tech=").Append(pick != null ? pick.GetTechType().ToString() : "<no-pickupable>");

                        var plantable = child.GetComponent<Plantable>();
                        if (plantable != null)
                            line.Append(" slot=").Append(plantable.planterSlotId)
                                .Append(" age=").Append(plantable.plantAge.ToString("0.000"));

                        var cuid = child.GetComponent<UniqueIdentifier>();
                        line.Append(" uid=").Append(cuid != null ? cuid.Id : "<none>")
                            .Append(" active=").Append(child.gameObject.activeSelf);

                        Plugin.Log.LogInfo(line.ToString());
                    }
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"{Tag} dump failed: {e.Message}");
            }
        }

        internal static System.Collections.IEnumerator DumpDelayed(Planter p, string phase, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            Dump(p, phase);
        }
    }

    [HarmonyPatch(typeof(Planter), nameof(Planter.OnProtoSerializeObjectTree))]
    internal static class Planter_OnProtoSerializeObjectTree_Diag
    {
        static void Postfix(Planter __instance)
        {
            if (PlanterDiagnostics.IsSeatruckPlanter(__instance))
                PlanterDiagnostics.Dump(__instance, "SAVE");
        }
    }

    [HarmonyPatch(typeof(Planter), nameof(Planter.OnProtoDeserializeObjectTree))]
    internal static class Planter_OnProtoDeserializeObjectTree_Diag
    {
        static void Postfix(Planter __instance)
        {
            if (!PlanterDiagnostics.IsSeatruckPlanter(__instance))
                return;
            PlanterDiagnostics.Dump(__instance, "LOAD-start");
            CoroutineHost.StartCoroutine(
                PlanterDiagnostics.DumpDelayed(__instance, "LOAD+5s", 5f));
        }
    }

    // Fires when a fruit-bearing plant reaches 100% growth and swaps its seed
    // item for a plant item in the planter's storage. If plant loss correlates
    // with this event, the swapped-in item is the suspect.
    [HarmonyPatch(typeof(Planter), nameof(Planter.ReplaceItem))]
    internal static class Planter_ReplaceItem_Diag
    {
        static void Postfix(Planter __instance, Plantable seed, Plantable plant, bool __result)
        {
            if (!PlanterDiagnostics.IsSeatruckPlanter(__instance))
                return;
            try
            {
                string seedTech = seed != null && seed.pickupable != null
                    ? seed.pickupable.GetTechType().ToString() : "<null>";
                string plantTech = plant != null && plant.pickupable != null
                    ? plant.pickupable.GetTechType().ToString() : "<null>";
                Plugin.Log.LogInfo(
                    $"[PlantDiag] REPLACE (seed->plant) planter={PlanterDiagnostics.PlanterId(__instance)} " +
                    $"seed={seedTech} plant={plantTech} result={__result}");
            }
            catch { }
        }
    }
}
