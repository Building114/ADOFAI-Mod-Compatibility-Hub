# Overlayer XDBuilding 3.49.6 Patch Notes

## Scope

This is an incremental maintenance release from XDBuilding 3.49.5 to 3.49.6.

Source comparison result:

- Added source files: 0
- Removed source files: 0
- Changed source files: 22

No source files were added or removed between the supplied 3.49.5 and the final 3.49.6 source trees.

## Confirmed 3.49.6 changes

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

## Exact changed files

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
