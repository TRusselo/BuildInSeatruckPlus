using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace BuildInSeatruckPlus.Patches
{
    internal static class DockedAudio
    {
        // Live moonpools, so a docked Seatruck can find the base it is docked to. (A base
        // already lists its moonpools, but a Seatruck has no direct back-reference.)
        internal static readonly List<MoonpoolExpansionManager> Moonpools =
            new List<MoonpoolExpansionManager>();
    }

    [HarmonyPatch(typeof(MoonpoolExpansionManager), "Start")]
    internal static class MoonpoolExpansionManager_Start_Patch
    {
        static void Postfix(MoonpoolExpansionManager __instance)
        {
            if (!DockedAudio.Moonpools.Contains(__instance))
                DockedAudio.Moonpools.Add(__instance);
        }
    }

    // The jukebox is a single positional source whose position/volume blend ALL of the
    // controlling juke's host speakers within range (Jukebox.SetParameters ->
    // JukeboxInstance.GetSoundPosition -> Speaker.GetSpeakers). That blend is what makes a
    // base play through every speaker in every room. But the speaker pool is per-host and
    // does NOT span a dock -- a base and the Seatruck docked to it are different hosts -- so
    // the music is audible on only one side at a time.
    //
    // Bridge the pool: while a Seatruck is docked to a base, each side also contributes the
    // other side's speakers, so the single source blends speakers across the whole docked
    // structure (multi-speaker, distance-attenuated, just like in-base multi-room). Only
    // fires while actually docked; vanilla everywhere else.
    [HarmonyPatch(typeof(Speaker), nameof(Speaker.GetSpeakers))]
    internal static class Speaker_GetSpeakers_Patch
    {
        // Guards the recursive GetSpeakers calls we make to gather the other side.
        private static bool _bridging;
        private static readonly List<Speaker> _other = new List<Speaker>();

        static void Postfix(ISpeakerHost host, Vector3 position, float radius, List<Speaker> results)
        {
            if (_bridging || host == null)
                return;

            _bridging = true;
            try
            {
                for (int i = DockedAudio.Moonpools.Count - 1; i >= 0; i--)
                {
                    var mp = DockedAudio.Moonpools[i];
                    if (mp == null) { DockedAudio.Moonpools.RemoveAt(i); continue; }

                    var head = mp.GetDockedHead();
                    var tail = mp.GetDockedTail();
                    if (head == null && tail == null)
                        continue; // nothing docked at this moonpool

                    var baseRoot = mp.baseRoot;
                    bool hostIsBase = host is BaseRoot && ReferenceEquals(host, baseRoot);
                    bool hostIsTruck = !hostIsBase && host is SeaTruckSegment
                        && ((tail != null && Speaker.IsSameHost(host, tail))
                            || (head != null && Speaker.IsSameHost(host, head)));

                    if (hostIsBase)
                    {
                        // base juke is the source -> add the docked Seatruck's speakers
                        Append(tail, position, radius, results);
                        Append(head, position, radius, results);
                    }
                    else if (hostIsTruck)
                    {
                        // a juke in the docked Seatruck is the source -> add the base's speakers
                        Append(baseRoot, position, radius, results);
                    }
                }
            }
            finally { _bridging = false; }
        }

        private static void Append(ISpeakerHost other, Vector3 position, float radius, List<Speaker> results)
        {
            if (other == null) return;
            _other.Clear();
            Speaker.GetSpeakers(other, position, radius, _other); // _bridging makes this vanilla-only
            for (int i = 0; i < _other.Count; i++)
            {
                var s = _other[i];
                if (s != null && !results.Contains(s))
                    results.Add(s);
            }
        }
    }

    // Vanilla Speaker.IsSameHost bridges a base only to its docked TAIL (the modules), never
    // the docked HEAD (the cab). So while docked, a juke in the cab is treated as a different
    // host from the base and the modules -- which leaves the cab juke out of the sync group
    // (no screen/light mirroring) and, because Jukebox.SetParameters gates its mute on
    // IsSameHost, mutes the cab juke entirely (it plays, but silent). Treat the whole docked
    // structure -- base + cab + modules -- as one host group.
    [HarmonyPatch(typeof(Speaker), nameof(Speaker.IsSameHost))]
    internal static class Speaker_IsSameHost_Patch
    {
        private static bool _reentry; // guards the IsSameHost calls we make below

        static void Postfix(ISpeakerHost a, ISpeakerHost b, ref bool __result)
        {
            if (__result || _reentry || a == null || b == null)
                return;

            _reentry = true;
            try
            {
                for (int i = DockedAudio.Moonpools.Count - 1; i >= 0; i--)
                {
                    var mp = DockedAudio.Moonpools[i];
                    if (mp == null) { DockedAudio.Moonpools.RemoveAt(i); continue; }

                    var head = mp.GetDockedHead();
                    var tail = mp.GetDockedTail();
                    if (head == null && tail == null)
                        continue; // nothing docked here

                    if (InGroup(a, mp, head, tail) && InGroup(b, mp, head, tail))
                    {
                        __result = true;
                        return;
                    }
                }
            }
            finally { _reentry = false; }
        }

        private static bool InGroup(ISpeakerHost h, MoonpoolExpansionManager mp,
                                    SeaTruckSegment head, SeaTruckSegment tail)
        {
            if (h is BaseRoot && ReferenceEquals(h, mp.baseRoot))
                return true;
            if (head != null && Speaker.IsSameHost(h, head)) // vanilla (re-entry guarded)
                return true;
            if (tail != null && Speaker.IsSameHost(h, tail))
                return true;
            return false;
        }
    }
}
