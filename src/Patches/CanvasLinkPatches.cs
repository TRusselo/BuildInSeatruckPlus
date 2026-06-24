using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BuildInSeatruckPlus.Patches
{
    // CanvasLink is a visibility-culling helper on the jukebox/speaker UI: it just
    // toggles its serialized canvases/rectMasks on and off. On our cloned mini
    // buildables those serialized arrays (or individual elements) can still be null
    // during a load — a race that makes it intermittent. The unguarded vanilla
    // methods then throw an NRE, and because JukeboxInstance.OnEnable calls into this
    // path BEFORE it registers the instance in JukeboxInstance.all, a throw here
    // leaves the mini-jukebox unregistered: ResolveInstance can't find it, so pressing
    // the hotkey plays nothing and the juke doesn't animate until a luckier load.
    //
    // Replace both toggle methods with null-safe versions (identical behaviour minus
    // the crash). Canvas and RectMask2D both derive from Behaviour, so we read the
    // arrays via reflection and toggle .enabled uniformly — no UI assembly reference
    // needed. Harmless for every other CanvasLink in the game.
    [HarmonyPatch(typeof(CanvasLink), "SetCanvasesEnabled")]
    internal static class CanvasLink_SetCanvasesEnabled_Patch
    {
        static readonly FieldInfo Field = AccessTools.Field(typeof(CanvasLink), "canvases");
        static bool Prefix(CanvasLink __instance, bool enabled) => CanvasLinkToggle.Run(Field, __instance, enabled);
    }

    [HarmonyPatch(typeof(CanvasLink), "SetRectMasksEnabled")]
    internal static class CanvasLink_SetRectMasksEnabled_Patch
    {
        static readonly FieldInfo Field = AccessTools.Field(typeof(CanvasLink), "rectMasks");
        static bool Prefix(CanvasLink __instance, bool enabled) => CanvasLinkToggle.Run(Field, __instance, enabled);
    }

    internal static class CanvasLinkToggle
    {
        public static bool Run(FieldInfo field, CanvasLink instance, bool enabled)
        {
            // Array covariance: the runtime Canvas[]/RectMask2D[] casts to Behaviour[].
            var arr = field.GetValue(instance) as Behaviour[];
            if (arr != null)
                foreach (var b in arr)
                    if (b != null)
                        b.enabled = enabled;
            return false; // skip the original (we did its work, null-safe)
        }
    }
}
