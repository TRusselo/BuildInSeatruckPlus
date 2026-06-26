using UnityEngine;

namespace BuildInSeatruckPlus
{
    internal class HotkeyController : MonoBehaviour
    {
        private float _downTime = -1f;
        private bool _longFired;

        private void Update()
        {
            var cfg = Plugin.Config;
            if (cfg == null || Player.main == null) return;
            if (!PlayerInInterior()) { _downTime = -1f; _longFired = false; return; }

            if (Input.GetKeyDown(cfg.Hotkey))
            {
                _downTime = Time.unscaledTime;
                _longFired = false;
            }

            if (_downTime >= 0f && !_longFired && Input.GetKey(cfg.Hotkey)
                && Time.unscaledTime - _downTime >= cfg.LongPressSeconds)
            {
                _longFired = true;
                TogglePlayStop();
            }

            if (Input.GetKeyUp(cfg.Hotkey))
            {
                if (_downTime >= 0f && !_longFired)
                    NextTrack();
                _downTime = -1f;
                _longFired = false;
            }
        }

        private static bool PlayerInInterior()
        {
            return Player.main.currentInterior is SeaTruckSegment
                   || Player.main.GetCurrentSub() != null;
        }

        private static JukeboxInstance ResolveInstance()
        {
            var playerHost = Speaker.GetHost(Player.main.currentInterior as MonoBehaviour);
            if (playerHost == null && Player.main.GetCurrentSub() != null)
                playerHost = Speaker.GetHost(Player.main.GetCurrentSub());

            // If a juke is already playing audibly on our (possibly dock-bridged) host, keep
            // controlling it so hold=stop / tap=next act on the running playback.
            var current = Jukebox.instance;
            if (current != null && current.isActiveAndEnabled && Jukebox.isStartingOrPlaying
                && Speaker.IsSameHost(Speaker.GetHost(current), playerHost)
                && current.GetSoundPosition(out _, out _, out _))
                return current;

            // Otherwise pick a live juke we can actually hear: enabled, on our host, whose host
            // has speakers, nearest to the player. This skips the "dead" parked cab juke (no
            // working speakers while docked) that otherwise swallowed the hotkey into silence.
            Vector3 playerPos = Player.main.transform.position;
            JukeboxInstance best = null;
            float bestDistSqr = float.MaxValue;
            bool bestHasSpeakers = false;

            foreach (var ji in JukeboxInstance.all)
            {
                if (ji == null || !ji.isActiveAndEnabled) continue;
                if (!Speaker.IsSameHost(Speaker.GetHost(ji), playerHost)) continue;

                bool hasSpeakers = ji.GetSoundPosition(out _, out _, out _);
                float distSqr = (ji.transform.position - playerPos).sqrMagnitude;

                bool better = best == null
                    || (hasSpeakers && !bestHasSpeakers)
                    || (hasSpeakers == bestHasSpeakers && distSqr < bestDistSqr);
                if (better)
                {
                    best = ji;
                    bestDistSqr = distSqr;
                    bestHasSpeakers = hasSpeakers;
                }
            }

            return best != null ? best : current;
        }

        private void TogglePlayStop()
        {
            var ji = ResolveInstance();
            if (ji == null) return;

            if (Jukebox.isStartingOrPlaying && Jukebox.instance == ji)
            {
                Jukebox.Stop();
            }
            else
            {
                ji.shuffle = Plugin.Config.DefaultShuffle;
                Jukebox.Play(ji);
            }
        }

        private void NextTrack()
        {
            var ji = ResolveInstance();
            if (ji == null) return;

            string next = Jukebox.GetNext(ji, true);
            if (string.IsNullOrEmpty(next)) return;
            ji.file = next;
            Jukebox.Play(ji);

            if (Plugin.Config.ShowTrackName)
            {
                var label = Jukebox.GetInfo(next).label;
                if (!string.IsNullOrEmpty(label))
                    ErrorMessage.AddMessage($"Now playing: {label}");
            }
        }
    }
}
