using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace BuildInSeatruckPlus.Patches
{
    // Compatibility shim for the third-party AutosortLockers "Autosort Vehicle
    // Unloader". Its AccumulateUnloadTargets() loops every SeaTruckSegment and
    // GetComponentsInChildren<StorageContainer>() to find containers to empty into
    // base storage when the Seatruck is docked. That broad sweep is intentional —
    // it's how it unloads player-built lockers inside the truck — but a vanilla
    // Planter is *also* a StorageContainer, so the unloader rips seeds, growing
    // plants and grown plants out of planters (via AddItem, which bypasses the
    // planter's isAllowedToRemove guard). That's what makes plants "vanish" while
    // docked, leaving empty planters.
    //
    // We don't want to stop locker unloading; we only want planters left alone.
    // This postfix runs after the unloader builds its target list and removes any
    // planter-owned container from it, so lockers still unload and planters don't.
    // Containers are matched by reference against the live Planters in the scene,
    // so we don't depend on AutosortLockers' internals beyond the one list field.
    //
    // Fully optional and guarded: if AutosortLockers isn't installed (or the method
    // is renamed in a future build) we simply never patch, and nothing else in our
    // mod is affected.
    internal static class AutosortCompat
    {
        private const string UnloaderTypeName = "AutosortLockers.AutosortUnloader";
        private const string MethodName = "AccumulateUnloadTargets";
        private static bool done;

        // Plugin load order isn't guaranteed, so if AutosortLockers' assembly isn't
        // loaded yet we retry for a short while before giving up.
        internal static IEnumerator TryPatchWhenReady(Harmony harmony)
        {
            for (int attempt = 0; attempt < 10 && !done; attempt++)
            {
                if (TryPatch(harmony))
                    yield break;
                yield return new WaitForSeconds(1f);
            }
        }

        private static bool TryPatch(Harmony harmony)
        {
            if (done)
                return true;
            try
            {
                var type = AccessTools.TypeByName(UnloaderTypeName);
                if (type == null)
                    return false; // not loaded yet — let the coroutine retry

                var method = AccessTools.Method(type, MethodName);
                if (method == null)
                {
                    Plugin.Log.LogInfo(
                        $"AutosortLockers present but {MethodName} not found; planter-protection patch skipped.");
                    done = true;
                    return true;
                }

                var postfix = new HarmonyMethod(typeof(AutosortCompat), nameof(AccumulateUnloadTargets_Postfix));
                harmony.Patch(method, postfix: postfix);
                done = true;
                Plugin.Log.LogInfo("AutosortLockers detected — unloader patched to leave Seatruck planters alone.");
                return true;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"AutosortLockers planter-protection patch failed (harmless): {e.Message}");
                done = true; // don't spin on a broken attempt
                return true;
            }
        }

        // Drop protected containers from the unloader's target list. Planters are
        // always protected (their contents are plants, not loose items). Storage
        // containers whose editable label color matches the user's configured
        // "skip" color are also protected, so players can mark lockers/modules to
        // keep. Everything else still unloads normally.
        private static void AccumulateUnloadTargets_Postfix(object __instance)
        {
            try
            {
                var list = Traverse.Create(__instance).Field("containerTargets")
                    .GetValue<List<ItemsContainer>>();
                if (list == null || list.Count == 0)
                    return;

                // 1) Always leave planters alone.
                var planters = Object.FindObjectsOfType<Planter>();
                if (planters.Length > 0)
                {
                    list.RemoveAll(ct => ct != null && planters.Any(p =>
                        p != null && p.storageContainer != null && p.storageContainer.container == ct));
                }

                // 2) Leave color-marked storage containers alone (opt-in via config).
                RemoveColorMarkedContainers(list);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"AutosortLockers protection postfix error (harmless): {e.Message}");
            }
        }

        // Removes from the unload list any StorageContainer whose editable label
        // color matches the color the player chose in settings. Matching is done on
        // the container's *actual displayed* color (looked up via the live
        // ColoredLabel/sign), so it doesn't depend on the game's preset ordering.
        private static void RemoveColorMarkedContainers(List<ItemsContainer> list)
        {
            if (list == null || list.Count == 0)
                return;

            var skip = Plugin.Config != null ? Plugin.Config.UnloaderSkipColorChoice : UnloaderSkipColor.Off;
            if (skip == UnloaderSkipColor.Off)
                return;

            var labels = Object.FindObjectsOfType<ColoredLabel>();
            if (labels.Length == 0)
                return;

            var protectedContainers = new HashSet<ItemsContainer>();
            foreach (var cl in labels)
            {
                if (cl == null || cl.signInput == null)
                    continue;

                var colors = cl.signInput.colors;
                int idx = cl.signInput.colorIndex;
                if (colors == null || idx < 0 || idx >= colors.Length)
                    continue;

                if (Classify(colors[idx]) != skip)
                    continue;

                var sc = ResolveContainer(cl);
                if (sc != null && sc.container != null)
                    protectedContainers.Add(sc.container);
            }

            if (protectedContainers.Count > 0)
                list.RemoveAll(ct => ct != null && protectedContainers.Contains(ct));
        }

        // Finds the StorageContainer a ColoredLabel belongs to. Two prefab layouts
        // exist: (1) wall lockers, where the StorageContainer is an ancestor of the
        // label; and (2) flat layouts (the SeaTruck storage module's drawers, the
        // SmallStorage lid) where the containers and their labels are siblings with no
        // script link between them.
        //
        // For (1) the direct ancestor lookup is exact. For (2) nothing in the game
        // references ColoredLabel, so we pair the label with the physically nearest
        // StorageContainer in the closest ancestor subtree that actually contains one.
        // Each drawer/lid label is mounted on its own container, so nearest wins, and
        // stopping at the first non-empty ancestor keeps us from reaching up into the
        // whole Seatruck and grabbing an unrelated container.
        private static StorageContainer ResolveContainer(ColoredLabel cl)
        {
            var direct = cl.GetComponentInParent<StorageContainer>(true);
            if (direct != null)
                return direct;

            Vector3 lp = cl.transform.position;
            var scope = cl.transform.parent;
            for (int depth = 0; depth < 4 && scope != null; depth++, scope = scope.parent)
            {
                var best = NearestContainer(scope, lp);
                if (best != null)
                    return best;
            }
            return null;
        }

        // Closest StorageContainer (with a live ItemsContainer) within a subtree.
        private static StorageContainer NearestContainer(Transform scope, Vector3 lp)
        {
            StorageContainer best = null;
            float bestSqr = float.MaxValue;
            foreach (var c in scope.GetComponentsInChildren<StorageContainer>(true))
            {
                if (c == null || c.container == null)
                    continue;
                float d = (c.transform.position - lp).sqrMagnitude;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = c;
                }
            }

            return best;
        }

        // Maps an actual label RGB color to one of the configurable marker colors by
        // hue (and saturation/value for the achromatic cases). Returns Off for
        // colors that aren't one of our named markers (e.g. near-black), which never
        // matches a chosen skip color.
        private static UnloaderSkipColor Classify(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (v < 0.15f)
                return UnloaderSkipColor.Off;        // effectively black — not a marker
            if (s < 0.25f)
                return UnloaderSkipColor.White;      // white / grey
            float hue = h * 360f;
            if (hue < 20f || hue >= 330f)
                return UnloaderSkipColor.Red;
            if (hue < 70f)
                return UnloaderSkipColor.Yellow;
            if (hue < 170f)
                return UnloaderSkipColor.Green;
            if (hue < 260f)
                return UnloaderSkipColor.Blue;
            return UnloaderSkipColor.Magenta;        // 260–330
        }
    }
}
