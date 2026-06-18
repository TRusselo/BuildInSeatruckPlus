# Changelog

All notable changes to this project are documented here.
This project adheres to [Semantic Versioning](https://semver.org/).

## [1.0.0] - 2026-06-17

Initial release.

### Added
- **Mini Jukebox** buildable (25% scale) — plays unlocked jukebox tracks through
  the game's jukebox engine, with the native in-world control UI.
- **Mini Speaker** buildable (50% scale) for clear audio inside the Seatruck.
- **Build In Seatruck** — place buildables inside Seatruck segments; they ride
  with the vehicle (BepInEx port of BluesKutya's mod, updated for the current
  game's `Builder.CheckAsSubModule` behaviour).
- **Playback hotkey** (default `R`): hold = play/stop, tap = next track, gated to
  Seatruck/base interiors, with an optional "Now playing" notification.
- **Mod options** (Nautilus): rebindable hotkey, long-press duration, cheap
  recipe toggle, unlock-at-start toggle, default shuffle, notification toggle.

### Notes
- Liberal placement collision on the mini items so they fit the tight cab.
- Mini items default to unlocking with the vanilla Jukebox; the recipe matches
  the vanilla Jukebox unless the cheap-recipe option is enabled.
