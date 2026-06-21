# Changelog

All notable changes to this project are documented here.
This project adheres to [Semantic Versioning](https://semver.org/).

## [1.0.4] - 2026-06-21

### Fixed
- **Docking collision fix now survives save/load.** The collider re-layering ran
  only at placement (buildables) and render setup (plants), both runtime-only. On
  load the game respawns buildables and plants from their prefabs on their solid
  default layers, so docking got blocked again after reloading. The fix is now
  re-applied at the two maneuvers that precede a collision — `SeaTruckMotor.StartPiloting`
  (driving in to dock; also re-fires on load when saved while piloting) and
  `MoonpoolExpansionManager.StartUndocking` (the undock eject, for a game loaded while
  docked). Both sweep buildable `Constructable` subtrees, so plants and fruit ride
  along and the Seatruck hull is left alone.

## [1.0.3] - 2026-06-20

### Fixed
- **Plants in Seatruck planters no longer block docking/undocking.** Plant models
  are spawned into a planter after it's placed (seedlings via `Planter.AddItem`,
  grown models via `GrowingPlant`'s async spawn), so the placement-time collider
  fix never touched them — they kept solid `Default`-layer colliders that snagged
  on the dock. A `Planter.SetupRenderers` postfix now moves plant colliders to the
  `Useable` layer, but only for planters inside a Seatruck. Covers seedlings,
  grown plants, harvestable fruit/seeds (incl. ones currently picked), and plants
  restored on save load.

## [1.0.2] - 2026-06-20

### Fixed
- **Items built in rear modules now stay with their module when docking.** Placed
  items were parented to the interior's head segment (the cab) because
  `Player.currentInterior` always resolves to the head, regardless of which module
  you're standing in. On docking, the game disconnects the tail and rotates the cab
  90°, which dragged module-built items away with the cab instead of leaving them in
  their module. Items are now parented to the segment that owns the surface they're
  built on, so they ride and detach with the correct module.
- **Resolved collision when undocking.** A downstream effect of the same
  mis-parenting: items displaced by the docking rotation clipped during the cab's
  undock eject. Correct parenting keeps them in place. *(Items built before this
  update keep their old parent — deconstruct and rebuild them to apply the fix.)*

## [1.0.1] - 2026-06-18

### Fixed
- **Docking no longer blocked by items built in the Seatruck.** Items placed in the
  truck kept solid colliders that snagged on the moonpool dock's geometry while
  driving in, stopping the hull before the auto-dock sequence could take over.
  Placed items are now moved to the `Useable` layer (interaction/raycast only, still
  usable and deconstructable) so they no longer obstruct docking. Affects all built
  items, not just the mini buildables.
- **Restored building inside the Seatruck dock room.** Its surfaces carry the
  `DenyBuilding` tag; a scoped `Builder.CheckTag` patch now allows placement there
  (only when aiming at moonpool-expansion geometry — every other no-build surface
  stays protected).

### Docs
- Removed an inaccurate "used with permission" claim from the Nexus description and
  added a do-not-upload guard: Nexus requires the original author's explicit
  permission to host ported code, independent of the MIT license.
- Updated the README media gallery with real in-game screenshots.

## [1.0.0] - 2026-06-17

Initial release.

### Added
- **Build In Seatruck** — place buildables inside Seatruck segments with the
  Habitat Builder; they ride with the vehicle and survive detach/reattach. A
  BepInEx port of BluesKutya's mod, updated for the current game's
  `Builder.CheckAsSubModule` behaviour, and scoped so normal/base building is
  untouched.
- **Mini Jukebox** buildable (25% scale) — plays unlocked jukebox tracks through
  the game's jukebox engine, with the native in-world control UI.
- **Mini Speaker** buildable (50% scale) for clear audio inside the Seatruck.
- **Playback hotkey** (default `R`): hold = play/stop, tap = next track, gated to
  Seatruck/base interiors, with an optional "Now playing" notification.
- **Mod options** (Nautilus): rebindable hotkey, long-press duration, cheap
  recipe toggle, unlock-at-start toggle, default shuffle, notification toggle.

### Notes
- Liberal placement collision on the mini items so they fit the tight cab.
- Mini items default to unlocking with the vanilla Jukebox; the recipe matches
  the vanilla Jukebox unless the cheap-recipe option is enabled.
