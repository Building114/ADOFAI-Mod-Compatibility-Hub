# Overlayer XDBuilding

Overlayer XDBuilding is an unofficial temporary compatibility patch for the legacy Overlayer 3.x source line.

- Original mod: Overlayer
- Original repository: https://github.com/modlist-org/Overlayer-Lagacy
- Base version: 3.49.0
- Patch version: 3.49.6
- Previous patch version: 3.49.5
- Target compatibility recorded by this patch: ADOFAI through 3.3.0
- Patch maintainer: Building114
- Overlayer-derived source license: GNU GPL v3.0

This is not an official release from modlist.org, c3nb, or the original Overlayer maintainers. Original authorship, notices, and license terms remain in effect.

## 3.49.6

3.49.6 is a small maintenance update on top of XDBuilding 3.49.5.

Main fixes:

- Refresh the selected global font when judgement text is shown, so short-lived judgement text is less likely to keep the old font.
- Use the native Windows common file dialog first, with the previous PowerShell file picker retained only as a fallback.
- Refresh script-created tag consumers when dynamic tags are registered, removed, or released.
- Fix the tag picker when a previously selected dynamic tag disappears.
- Fix colon-separated multi-argument tag parsing and make `Coalesce` stop at the first valid value.
- Delay automatic script startup slightly so later UnityModManager mods can finish loading first.
- Allow deterministic script order with a `// @order N` header.
- Target x64 explicitly to match the AMD64 LibreHardwareMonitor dependency.
- Clean a few confirmed unused variables and fields.

The large compatibility work remains the 3.49.5 baseline. See `PATCH_NOTES.md` for the exact 3.49.5 -> 3.49.6 file list.

## Building

See `BUILDING.md`.

## Package notes

The public source archive excludes the machine-specific `Overlayer/Local.Build.props`. Copy `Overlayer/Local.Build.props.example` locally if a fixed game path is preferred.

The upstream-facing README content from the supplied source is preserved in `UPSTREAM_README.md`.
