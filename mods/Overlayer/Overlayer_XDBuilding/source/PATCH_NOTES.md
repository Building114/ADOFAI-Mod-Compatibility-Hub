# Overlayer XDBuilding Patch Notes

> 翻译部分由 AI 提供，可能不准确。 / Translated portions were provided by AI and may be inaccurate.

## 3.49.7

Continued maintenance based on XDBuilding 3.49.6, targeting ADOFAI 3.4.0.

### Main changes

- Accuracy, XAccuracy, XPerfect, XScore and newer judgement fields now prefer official game data.
- Fix some legacy Tags returning incorrect values, `NaN`, or becoming unavailable after game field changes.
- Keep legacy Tag names, while fields without an official equivalent still return `NaN` or `false`.
- Fix `StartProgress` sometimes reading the previous run's ending progress, and keep start-tile and progress initialization consistent.
- Fix `PlayerTotalHits` counting special records such as Midspin, Auto and Multipress as ordinary player hits.
- Make `MarginCombo:Perfect` use the game's official Perfect classification.
- Fix `SpecialPlayMark` reporting PP or FC before a valid hit has occurred.
- Fix checkpoint lists sometimes retaining state after editor play or level changes.

### Tags and editor

- Add `Wave`, `PingPong` and `ColorPulse` as time-based animation Tags.
- Let `ColorRange` accept direct numbers and nested numeric Tags, including `Wave` and `Calc`.
- Support nested braces in the code editor's Tag scan so highlighting and click detection match runtime parsing more closely.
- Make the `ColorRange`, `MovingMan` and `EasedValue` editors more robust.
- Fix several `Rainbow` edge cases.
- Fix `ExprText` potentially throwing on invalid numeric formats.
- Fix integer and floating-point parameter parsing for invalid input and very long decimal values.
- Fix `MaxTileBpm` scene-null handling and BPM edge cases.
- Fix interrupted `EasedValue` animations jumping on progress rewind; cache repeated per-frame tag evaluation and color parsing to reduce heavy progress-bar stutter.
- Add `PlayerPlanetColor(player, planet)` (1-based, RGBA hex), usable as a nested color argument in `ColorRange` and `ColorPulse` with `Wave`.

### Text, scripting and resources

- Adjust OverlayerText font and material binding to reduce state conflicts.
- Keep global ChangeFont from overwriting font settings on Overlayer's own text objects.
- Fix selected script import, reload, dynamic Tag refresh and lifecycle issues.
- Fix selected Profile import/export, settings backup and resource cleanup issues.
- Keep `update.txt` until the player explicitly chooses to delete it after reading. Remember the last viewed log content in `Settings.json`, so keeping the log does not cause repeated automatic popups. Kept logs can be reopened from the menu.
- Improve the in-game guide hierarchy: numbered bold lesson headings, separate paragraphs, distinct example blocks and TMP category titles. Set the standalone TMP preview default font size to 100, remove the bottom-of-page TMP tip, and shorten the collapse label to 收起.
- Expand **Rich Text & Tags** into a 37-tag reference and 15 Overlayer rules. Each lesson has a 200-character temporary experiment with a separate TextMeshPro render-target preview. Only one experiment runs at a time. Scripts and non-allowlisted Tags are not evaluated, and the input is never saved. Nested example code is kept on one line. The guide closes and clears its transient draft when the mod menu is hidden.

## 3.49.6

### Scope

This is an incremental maintenance release from XDBuilding 3.49.5 to 3.49.6.

Source comparison result:

- Added source files: 0
- Removed source files: 0
- Changed source files: 22

No source files were added or removed between the supplied 3.49.5 and the final 3.49.6 source trees.

### Confirmed 3.49.6 changes

### Font refresh

`FontPatch` now also refreshes fonts after `scrHitTextMesh.Show`, while keeping the existing periodic scan as a fallback. This addresses judgement text that appears too briefly or too late for the old scan path.

### Windows file picker

`StandaloneFileBrowserShim` now prefers the native Windows common dialog through `comdlg32.dll`. The PowerShell/Windows Forms implementation remains as a fallback when the native call reports an error.

### Dynamic script tags

Dynamic tag registration/removal/release now notifies existing consumers. Script-tag tooltips are cleaned when tags are released, and the tag picker handles disappearing tags safely.

### Multi-argument tags and Coalesce

Colon-separated arguments are split for tags that actually take multiple arguments, without splitting single-argument expression tags internally.

`Coalesce` no longer eagerly resolves every candidate. It stops at the first valid value as intended.

### Script startup

Automatic script loading is delayed by two Unity frames so UnityModManager has a chance to finish loading mods ordered after Overlayer.

Scripts can optionally include:

```js
// @order 100
```

Lower values run first. Scripts without the directive use order `0`.

### Build target and warning cleanup

The main Overlayer project explicitly targets x64 to match `LibreHardwareMonitorLib`'s AMD64 runtime assembly.

A small set of confirmed unused variables and private fields was removed.

### Exact changed files

- `Overlayer.Bootstrapper/Properties/AssemblyInfo.cs`
- `Overlayer/CodeEditor/CodeEditor.cs`
- `Overlayer/CodeEditor/ColorRangeEditor.cs`
- `Overlayer/CodeEditor/EasedValueEditor.cs`
- `Overlayer/CodeEditor/MovingManEditor.cs`
- `Overlayer/Core/Drawer.cs`
- `Overlayer/Core/Scripting/Impl.cs`
- `Overlayer/Core/Scripting/Scripting.cs`
- `Overlayer/Core/TagManager.cs`
- `Overlayer/Core/TextReplacing/Parsing/ParsedTag.cs`
- `Overlayer/Core/TextReplacing/Parsing/Parser.cs`
- `Overlayer/Main.cs`
- `Overlayer/MiscFiles/info.json`
- `Overlayer/MiscFiles/update.txt`
- `Overlayer/Overlayer.csproj`
- `Overlayer/Patches/FontPatch.cs`
- `Overlayer/Properties/AssemblyInfo.cs`
- `Overlayer/Unity/OverlayerImage.cs`
- `Overlayer/Unity/OverlayerText.cs`
- `Overlayer/Unity/StandaloneFileBrowserShim.cs`
- `Overlayer/Utils/DeletePopup.cs`
- `Overlayer/Views/SettingsDrawer.cs`
