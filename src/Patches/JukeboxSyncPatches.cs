using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace BuildInSeatruckPlus.Patches
{
    // Make every jukebox that shares the playing juke's speaker host behave as one
    // synchronized terminal: same track name, EQ animation, position, play/pause state and
    // flashing lights, with its transport buttons driving the single shared playback instead
    // of hijacking it. Covers every host the game recognizes -- several jukes in one Seatruck,
    // several jukes in a base, and a base + a docked Seatruck (Speaker.IsSameHost already
    // bridges a BaseRoot to its moonpool's docked tail).
    //
    // A JukeboxInstance drives ALL of that off one private flag, isControlling
    // (== Jukebox.instance). Forcing isControlling to read true for a "mirror" (a non-active
    // juke on the same host while something is playing) makes vanilla's own UpdateUI /
    // UpdateEffects / button handlers display the live state and act on the global Jukebox for
    // free. The only two places a forced-true isControlling would WRONGLY stop the music are
    // the per-frame power gate (UpdatePower) and OnDisable -- both are guarded below -- and we
    // copy the active juke's track/repeat/shuffle onto each mirror so labels and icons match.
    internal static class JukeboxSync
    {
        // True while a JukeboxInstance.OnDisable runs, so isControlling reports its REAL value
        // there: a mirror being disabled must not believe it is controlling and stop playback.
        internal static bool InDisable;

        private static int _frame = -1;
        private static readonly Dictionary<JukeboxInstance, bool> _cache =
            new Dictionary<JukeboxInstance, bool>();

        // A juke is a mirror when it isn't the active instance, something is playing, and it
        // shares the active juke's speaker host. Memoized per frame -- isControlling is hot.
        internal static bool IsMirror(JukeboxInstance ji)
        {
            if (Time.frameCount != _frame)
            {
                _frame = Time.frameCount;
                _cache.Clear();
            }
            if (_cache.TryGetValue(ji, out bool cached))
                return cached;

            bool result = Compute(ji);
            _cache[ji] = result;
            return result;
        }

        private static bool Compute(JukeboxInstance ji)
        {
            var active = Jukebox.instance;
            if (active == null || active == ji)
                return false; // nothing playing, or this IS the source
            if (!Jukebox.isStartingOrPlaying)
                return false; // stopped -> jukes behave independently
            return Speaker.IsSameHost(Speaker.GetHost(ji), Speaker.GetHost(active));
        }
    }

    // Force isControlling true for mirrors so vanilla draws and controls them as the active juke.
    [HarmonyPatch(typeof(JukeboxInstance), "isControlling", MethodType.Getter)]
    internal static class JukeboxInstance_isControlling_Patch
    {
        static void Postfix(JukeboxInstance __instance, ref bool __result)
        {
            if (__result || JukeboxSync.InDisable)
                return;
            if (JukeboxSync.IsMirror(__instance))
                __result = true;
        }
    }

    // A mirror must never run the power gate: it would call Jukebox.Stop() off its own
    // (possibly unpowered) relay and kill the shared playback. The real active juke still
    // gates playback on its own power exactly as vanilla does.
    [HarmonyPatch(typeof(JukeboxInstance), "UpdatePower")]
    internal static class JukeboxInstance_UpdatePower_Patch
    {
        static bool Prefix(JukeboxInstance __instance) => !JukeboxSync.IsMirror(__instance);
    }

    // Neutralize the forced isControlling for the duration of OnDisable, so a mirror being
    // disabled (e.g. a far base juke unloading while a Seatruck juke plays) doesn't stop the
    // music. The real active juke still stops on disable, as vanilla intends.
    [HarmonyPatch(typeof(JukeboxInstance), "OnDisable")]
    internal static class JukeboxInstance_OnDisable_Patch
    {
        static void Prefix() => JukeboxSync.InDisable = true;
        static void Finalizer() => JukeboxSync.InDisable = false;
    }

    // Copy the active juke's track + repeat/shuffle onto each mirror before its UI redraws so
    // labels and mode icons match. Position, EQ and the play/pause sprite already follow from
    // the forced isControlling reading the global Jukebox state.
    [HarmonyPatch(typeof(JukeboxInstance), "UpdateUI")]
    internal static class JukeboxInstance_UpdateUI_Patch
    {
        static void Prefix(JukeboxInstance __instance)
        {
            if (!JukeboxSync.IsMirror(__instance))
                return;
            var active = Jukebox.instance;
            if (active == null)
                return;

            if (__instance.file != active.file)
                __instance.file = active.file; // setter refreshes the track label + length

            if (__instance.repeat != active.repeat)
            {
                __instance.repeat = active.repeat;
                __instance.imageRepeat.sprite = __instance.spritesRepeat[(int)active.repeat];
            }
            if (__instance.shuffle != active.shuffle)
            {
                __instance.shuffle = active.shuffle;
                __instance.imageShuffle.sprite =
                    active.shuffle ? __instance.spriteShuffleOn : __instance.spriteShuffleOff;
            }
        }
    }
}
