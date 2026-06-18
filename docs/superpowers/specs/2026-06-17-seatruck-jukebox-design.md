# Build In Seatruck Plus — Design Spec

**Date:** 2026-06-17
**Game:** Subnautica: Below Zero (current Steam build)
**Framework:** BepInEx + Nautilus + HarmonyX, targeting `net472`

## Summary

A single BepInEx/Nautilus plugin that lets the player enjoy the game's
jukebox music inside the Seatruck. It bundles three features:

1. **Build In Seatruck (ported)** — a faithful BepInEx port of BluesKutya's
   QModManager mod (NexusMods 287), which enables placing buildables inside a
   Seatruck segment.
2. **Mini buildables** — a 25%-scale **Mini Jukebox** and 50%-scale **Mini
   Speaker**, cloned from the vanilla prefabs, with very liberal placement
   collision so they fit the cramped Seatruck interior.
3. **Gated hotkeys** — configurable R long-press = play/stop, short-press =
   next track, active only while the player is inside a Seatruck or base.

The original idea of a synthetic hotkey-only music player (no physical
jukebox) was dropped in favor of placing a real `JukeboxInstance`, which
brings the game's native playback engine, UI, unlocked-track playlist, FMOD
streaming, and game-music ducking for free.

## Background / research findings

Confirmed by decompiling the current `Assembly-CSharp.dll`:

- **`Jukebox`** is a global singleton playback engine (`Jukebox.main`). Only
  one track plays game-wide at a time. Key API:
  - `static void Play(JukeboxInstance instance)`, `static void Stop()`
  - `static string GetNext(JukeboxInstance jukebox, bool forward)`
  - `static bool isStartingOrPlaying`, `static bool shuffle`, `static Repeat repeat`,
    `static float volume`, `static JukeboxInstance instance`
  - `static TrackInfo GetInfo(string id)` → `TrackInfo.label`
  - The playlist `_playlist` is auto-built from `Player.main.unlockedTracks`
    (the songs unlocked by picking up Jukebox Disks), so **unlocked-song
    handling is automatic** — we never enumerate it ourselves.
- **`JukeboxInstance`** is the buildable controller. It is heavily UI-wired
  (`canvas`, TextMeshPro labels, Images, sprites; dereferenced in
  `Start`/`OnEnable`/`UpdateUI`). It cannot be `AddComponent`'d from scratch —
  it must come from the cloned prefab. It pulls power from a parent
  `PowerRelay` and registers itself in a static `all` list.
- **Audibility** is 3D and gated by the `Speaker` host system:
  `SetParameters` mutes/muffles unless the jukebox's speaker host
  (`GetComponentInParent<ISpeakerHost>()`) matches the player's
  `currentInterior` host, within `maxDistance` (20 m). `SeaTruckSegment`
  implements `ISpeakerHost`, so a jukebox parented into the Seatruck segment
  shares the player's host → audible. A placed **Speaker** strengthens this
  (`GetSoundPosition` uses nearby `Speaker` components).
- **`TechType.Jukebox = 1564`** and **`TechType.Speaker = 1566`** both exist
  as buildables → both can be cloned with Nautilus `CloneTemplate`.

### Build-In-Seatruck patch targets (current signatures)

| Patch target | Status |
|---|---|
| `Builder.CheckTag(Collider c)` (private static bool) | ports verbatim |
| `Builder.TryPlace()` (public static bool) | ports verbatim |
| `Builder.ValidateOutdoor(GameObject)` (public static bool) | ports verbatim |
| `PowerConsumer.IsPowered()` (public bool) | ports verbatim |
| `SeaTruckSegment.CanEnter()` (public bool) | ports verbatim |
| `Constructable.CheckFlags(...)` | **reworked** — now `static CheckFlags(bool allowedInBase, bool allowedInSub, bool allowedOutside, bool allowedUnderwater, Vector3 hitPoint)` |

`SeaTruckSegment` implements `IInteriorSpace, ISpeakerHost`. The
`SeaTruckSegmentHelper` utility (player-in-segment, current segment, parent
segment, segment SubRoot) ports unchanged.

## Architecture

One plugin assembly, `BuildInSeatruckPlus.dll`, with these units:

### 1. `Plugin` (entry point)
- `BaseUnityPlugin` with `[BepInPlugin(GUID, NAME, VERSION)]` and
  `[BepInDependency("com.snmodding.nautilus")]`.
- `Awake()`: load config, run `new Harmony(GUID).PatchAll()`, register the
  mini buildables, register the Nautilus options menu, and add the hotkey
  controller component.
- Replaces the old `[QModCore]`/`[QModPatch]` entry point.

### 2. `Patches/` — Build-In-Seatruck (Harmony)
- One file per patch class, ported from the original. `Constructable.CheckFlags`
  prefix: when `SeaTruckSegmentHelper.getCurrentSeaTruckSegment()` is non-null
  and not docked/connected, set `__result = allowedInSub` and skip the
  original; otherwise run the original.
- `SeaTruckSegmentHelper` utility class ported as-is.

### 3. `Buildables/` — mini items (Nautilus)
- `MiniJukebox`: `CustomPrefab` from `CloneTemplate(TechType.Jukebox)`.
  - `OnPrefabBuilt`: set `transform.localScale *= 0.25f`; shrink the
    `ConstructableBounds` (and/or clear extra `OrientedBounds`) for liberal
    collision; ensure `Constructable.allowedInSub = true`.
  - Recipe: clone of the vanilla Jukebox recipe; if `CheapRecipe` config is on,
    use `1 × Titanium` instead.
  - PDA group/category: same as vanilla Jukebox (Habitat Builder).
- `MiniSpeaker`: `CustomPrefab` from `CloneTemplate(TechType.Speaker)`,
  scaled to 0.5, same liberal collision and recipe handling.
- **Unlock:** both **locked at game start**. Unlock logic:
  - If `UnlockBuildables` config checkbox is **on** → `KnownTech.Add` at start
    (available immediately).
  - If **off** → remain locked; they unlock alongside the vanilla Jukebox
    (tied to the same unlock as `TechType.Jukebox`).

### 4. `HotkeyController` (MonoBehaviour)
- Polls in `Update()`. No-op unless `Player.main` exists **and**
  `Player.main.currentInterior is SeaTruckSegment` **or** the player is in a
  base/sub (`Player.main.GetCurrentSub() != null`).
- Tracks press duration of the configured key to distinguish short vs long
  press (threshold from config).
- Resolves the target `JukeboxInstance`: the one whose host matches the
  player's current interior (search `JukeboxInstance` instances; fall back to
  `Jukebox.instance` if already controlling).
- **Long-press:** if currently playing that instance → `Jukebox.Stop()`,
  else `Jukebox.Play(instance)`.
- **Short-press:** `instance.file = Jukebox.GetNext(instance, true)`; if
  playing, re-`Play`; show "Now playing: {label}" via `ErrorMessage`/popup
  when `ShowTrackName` config is on.
- Graceful no-op (optional subtle message) when no jukebox is present.

### 5. `Config` (Nautilus options menu)
- `Nautilus.Options`-backed config bound to a BepInEx `ConfigFile`, shown in
  the in-game Mod Options menu:
  - **Hotkey** (rebindable `KeyCode`, default `R`)
  - **Long-press seconds** (slider/float, default `0.5`)
  - **Cheap recipe** (toggle, default off — applies on game restart, since
    recipes register at load)
  - **Unlock buildables** (toggle, default off)
  - **Default shuffle** (toggle, default on — applied to a mini jukebox when placed)
  - **Show track-name notifications** (toggle, default on)

## Data flow

1. Plugin loads → patches applied, mini prefabs + config registered.
2. Player builds a Mini Jukebox (+ optional Mini Speaker) inside the Seatruck
   via the now-permitted Habitat Builder.
3. The `JukeboxInstance` parents into the Seatruck segment (its modules root),
   draws power from the Seatruck `PowerRelay`, and shares the player's speaker
   host → audible.
4. Player clicks the jukebox UI **or** uses the R hotkey to play/stop/skip the
   global `Jukebox` engine, which streams unlocked tracks via FMOD.

## Error handling
- All patch prefixes null-guard game objects (mirroring the original mod).
- Hotkey controller guards every dereference (`Player.main`, interior,
  resolved instance) and degrades to no-op.
- Prefab registration wrapped so a clone failure logs and disables only that
  buildable, not the whole plugin.
- All notable steps logged via the BepInEx `ManualLogSource`.

## Testing / verification
- Build succeeds on Linux (`dotnet build`, `net472`).
- Plugin loads without errors (check `BepInEx/LogOutput.log`).
- In-game: enter Seatruck → Habitat Builder shows Mini Jukebox/Speaker only
  when unlocked per config → place them (liberal collision lets them fit) →
  they power on → music is audible → native UI and R hotkey both control
  play/stop/next → track-name notification appears.
- Confirm building other items inside the Seatruck still works (full port).

## Build & deployment
- Source project in `/home/user/BuildInSeatruckPlusMod/` (location irrelevant to
  the user). Targets `net472` via `Microsoft.NETFramework.ReferenceAssemblies`.
  References: game `Assembly-CSharp.dll` + Unity modules, `Nautilus.dll`,
  BepInEx core, `0Harmony.dll`, `FMODUnity.dll`.
- **Final artifact ships to `BepInEx/plugins/BuildInSeatruckPlus/BuildInSeatruckPlus.dll`**
  (same layout as the other installed mods).
- **Housekeeping:** disable/remove the old QMod **`BuildInSeatruck/`** (game-root
  folder) and the two `Build In Seatruck-*.zip` files in `BepInEx/plugins/` —
  they cannot load without QModManager and would conflict with the port.

## Open items defaulted (flag in review if wrong)
- "Locked, unless the Unlock checkbox is on" also unlocks the minis together
  with the vanilla Jukebox unlock when the checkbox is off. (Alternative:
  stay permanently locked unless the checkbox is on.)
- Mini Jukebox recipe mirrors the vanilla Jukebox; cheap mode = 1 Titanium.
- Hotkey "in a base/sub" includes any `SubRoot`, not only the player's own base.
