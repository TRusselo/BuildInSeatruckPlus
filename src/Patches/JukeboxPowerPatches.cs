using HarmonyLib;
using UnityEngine;

namespace BuildInSeatruckPlus.Patches
{
    // JukeboxInstance caches its PowerRelay exactly once, in Start(), via
    // GetComponentInParent<PowerRelay>(). On a save load the mini-juke's Start() can run
    // BEFORE it's re-parented under the SeaTruck that owns the relay, so _powerRelay caches
    // as null for the whole session. ConsumePower() then always returns false, and the
    // game's UpdatePower() calls Jukebox.Stop() one frame after our hotkey starts playback
    // (label flips to "JukeboxNoPower") -- a silent juke with no animation. It's intermittent
    // because it depends purely on load ordering.
    //
    // Re-resolve the relay lazily right before the game consumes power, but only when the
    // cache is null. When the juke is sitting in a powered SeaTruck (always true when the
    // player is inside pressing R) GetComponentInParent finds the truck's relay, so playback
    // survives. A healthy vanilla base jukebox already has a non-null relay, so this is a
    // no-op for everything else.
    [HarmonyPatch(typeof(JukeboxInstance), "ConsumePower")]
    internal static class JukeboxInstance_ConsumePower_Patch
    {
        static void Prefix(JukeboxInstance __instance)
        {
            if (__instance._powerRelay != null) return;

            var relay = __instance.GetComponentInParent<PowerRelay>();
            if (relay != null)
            {
                __instance._powerRelay = relay;
                Plugin.Log.LogInfo($"Re-resolved missing PowerRelay for {__instance.name} -> {relay.name} (load-timing fix).");
            }
        }
    }
}
