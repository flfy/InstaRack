# Changelog

All notable changes to this project will be documented in this file.

The format is based on Keep a Changelog, and this project follows Semantic Versioning.

## [1.0.4] - 2026-04-15

### Fixed
- Reapply and persist rack colors across late object initialization so purchased rack boxes and reloaded racks keep their selected color after full game restarts.

## [1.0.3] - 2026-04-14

### Fixed
- Preserve rack custom colors during instant placement, third time *was* the charm.

## [1.0.2] - 2026-04-14

### Fixed
- Preserve rack custom colors during instant placement by carrying the spawned rack uid and applying the tracked shop color/material to the installed rack.

## [1.0.1] - 2026-04-14

### Fixed
- Preserve the selected rack color when placing racks instantly.

## [1.0.0] - 2026-04-14

### Added
- Initial `InstaRack` release for Data Center.
