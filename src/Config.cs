using Nautilus.Json;
using Nautilus.Options.Attributes;
using UnityEngine;

namespace BuildInSeatruckPlus
{
    [Menu("Build In Seatruck Plus")]
    public class Config : ConfigFile
    {
        [Keybind("Playback hotkey (in Seatruck / base)")]
        public KeyCode Hotkey = KeyCode.R;

        [Slider("Long-press seconds", 0.1f, 2f, DefaultValue = 0.5f, Step = 0.05f, Format = "{0:F2}")]
        public float LongPressSeconds = 0.5f;

        [Toggle("Cheap recipe (1 Titanium) - restart required")]
        public bool CheapRecipe = false;

        [Toggle("Unlock buildables at start - restart required")]
        public bool UnlockBuildables = false;

        [Toggle("New jukeboxes start in shuffle mode")]
        public bool DefaultShuffle = true;

        [Toggle("Show 'Now playing' notifications")]
        public bool ShowTrackName = true;

        [Choice("Don't unload containers labeled this color (needs AutosortLockers)")]
        public UnloaderSkipColor UnloaderSkipColorChoice = UnloaderSkipColor.Red;
    }

    // Which storage-label color marks a container as "leave alone" for the
    // AutosortLockers unloader. Matched against the container's actual displayed
    // label color at runtime (by hue, not by preset index), so it's robust to
    // prefab color ordering. Off disables the feature; no effect without
    // AutosortLockers installed.
    public enum UnloaderSkipColor
    {
        Off,
        Red,
        Yellow,
        Green,
        Blue,
        Magenta,
        White
    }
}
