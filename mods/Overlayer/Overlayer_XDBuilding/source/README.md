# Overlayer XDBuilding

Overlayer XDBuilding is an unofficial temporary compatibility patch for the legacy Overlayer 3.x source line.

- Original mod: Overlayer
- Original repository: https://github.com/modlist-org/Overlayer-Lagacy
- Base version: 3.49.0
- Patch version: 3.49.5
- Target compatibility recorded by this patch: ADOFAI through 3.3.0
- Patch maintainer: Building114
- Overlayer-derived source license: GNU GPL v3.0

This is not an official release from modlist.org, c3nb, or the original Overlayer maintainers. Original authorship, notices, and license terms remain in effect.

## Main changes

- Compatibility accessors for changed ADOFAI, Unity, player, planet, floor, judgement, and text fields.
- Expression, multiplayer judgement, player hit statistic, and XPerfect integration tags.
- Reworked tag lexing and parsing with escaped braces, nested tags, nested parameters, and expression arguments.
- Jint 4.7.1 interop and scripting lifecycle updates.
- Stronger profile import, migration, backup, deletion, and object lifecycle handling.
- Dependency loading, image conversion, file picker, and main-thread compatibility helpers.
- The upstream automatic updater is disabled for this unofficial build.

See `PATCH_NOTES.md` for the consolidated change summary and `BUILDING.md` for the actual build path.

## Package notes

The source archive excludes local paths, editor state, build output, compiled binaries, PDB files, restored packages, nested release archives, and the local deployment settings file.

The exact origin and redistribution terms of every file under `Overlayer/CodeEditor/` still need verification. The LibreHardwareMonitor reference also needs a reproducible NuGet restore declaration. See `REVIEW_REQUIRED.md`.

The original upstream README is preserved as `UPSTREAM_README.md`.
