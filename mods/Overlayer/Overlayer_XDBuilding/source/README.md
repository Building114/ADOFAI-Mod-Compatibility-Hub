# Overlayer XDBuilding

> 翻译部分由 AI 提供，可能不准确。 / Translated portions were provided by AI and may be inaccurate.

Overlayer XDBuilding is a community-maintained compatibility patch for the Overlayer 3.x source line.

- Original mod: Overlayer
- Original repository: [https://github.com/modlist-org/Overlayer-Legacy](https://github.com/modlist-org/Overlayer-Legacy)
- Base version: 3.49.0
- Patch version: 3.49.7
- Previous patch version: 3.49.6
- Target ADOFAI version: 3.4.0
- Patch maintainer: Building114
- Overlayer-derived source license: GNU GPL v3.0

Original author credits, notices, and license terms remain in effect.

## 3.49.7

- Target ADOFAI 3.4.0 and prefer official game data for Accuracy, XAccuracy, XPerfect, XScore and newer judgement fields.
- Fix legacy Tag compatibility, start-progress state, player-hit totals, Perfect grouping and checkpoint state.
- Add `Wave`, `PingPong` and `ColorPulse`, improve `ColorRange`, and support nested-brace Tag scanning in the editor.
- Add `PlayerPlanetColor(player, planet)` for live player-planet colors and fix interrupted `EasedValue` animation while reducing repeat parsing for complex progress bars.
- Make the `ColorRange`, `MovingMan` and `EasedValue` editors more robust.
- Improve OverlayerText font/material binding and keep global ChangeFont from overwriting per-text font settings.
- Fix selected script lifecycle, Profile/resource, numeric parsing and BPM edge cases.
- Keep legacy Tag names where practical. Fields without an official equivalent still return `NaN` or `false`.
- Ask whether to delete `update.txt` only after the player finishes reading; kept logs remain available through an **Update Log** menu button.
- Expand the bottom-menu **Rich Text & Tags** guide with a 37-tag TextMeshPro reference, 15 Overlayer rules, and one inline experiment for every lesson. The experiment uses a separate offscreen TMP renderer (up to 200 input characters, no disk persistence), keeps nested examples on one line, and limits preview Tag evaluation to a small built-in subset. Closing the mod menu also closes the guide and clears its draft.

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
