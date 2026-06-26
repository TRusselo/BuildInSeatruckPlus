# Releases

A condensed history of every release. For full detail see [CHANGELOG.md](CHANGELOG.md).

## v1.0.7 — Synchronized jukeboxes + continuous audio across docking
- All jukeboxes on a shared host mirror the active one (track / EQ / position / play-pause / lights), and their buttons drive the one shared playback
- Docked Seatruck + base share speaker pools → music audible throughout the whole docked structure; docking no longer interrupts playback
- Smart hotkey targets a live, audible jukebox (not the silent parked cab juke)
- Cab jukebox now works while docked (base + cab + modules treated as one host group)

## v1.0.6 — Mini Jukebox silent-after-load fix
- `JukeboxInstance.ConsumePower` prefix re-resolves a null-cached `PowerRelay` (a load-order race that stopped playback one frame after the hotkey)
- Also shipped the v1.0.5 `CanvasLink` fix (see below)

## v1.0.5 — *(folded into v1.0.6, never tagged on its own)*
- Mini Jukebox sometimes silent after a load — `CanvasLink` null-canvas guard so the instance always registers

## v1.0.4 — Collision fix survives save/load
- Re-applies the collider fix at `StartPiloting` (drive-in) and `StartUndocking` (eject), since the game respawns buildables from prefabs on load

## v1.0.3 — Planter plants stop blocking docking
- `Planter.SetupRenderers` postfix relayers plant/fruit colliders to the `Useable` layer (only for Seatruck planters)

## v1.0.2 — Module-built items stay put when docking
- Items built in rear modules now parent to their own segment (not the cab), so they ride and detach with the correct module; fixes undock collision

## v1.0.1 — Docking unblocked + dock-room building
- Built items moved to the `Useable` layer so they stop snagging the moonpool dock
- Restored building inside the Seatruck dock room (scoped `CheckTag` patch)
- Docs: removed an inaccurate Nexus permission claim; added real in-game screenshots

## v1.0.0 — Initial release
- Build In Seatruck (BepInEx port of BluesKutya's mod) — place buildables inside Seatruck segments
- Mini Jukebox (25% scale) + Mini Speaker (50% scale) buildables
- Playback hotkey `R` (hold = play/stop, tap = next), gated to interiors
- Nautilus mod options (rebindable hotkey, long-press duration, cheap recipe, unlock toggle, shuffle, notifications)
