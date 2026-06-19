# Changelog

All notable changes to this project are documented here.
This project adheres to [Semantic Versioning](https://semver.org/).

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
