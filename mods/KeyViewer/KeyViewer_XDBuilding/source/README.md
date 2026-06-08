# KeyViewer XDBuilding

This is an unofficial temporary compatibility patch source package for KeyViewer.

## Important Notice

- This package is not an official KeyViewer release.
- Building114 maintains this temporary compatibility patch, but is not the original author of KeyViewer.
- The original project belongs to its original authors and contributors.
- The original KeyViewer project should be preferred if an official update becomes available and works for your use case.
- The KeyViewer name is used only to identify the project that this patch is related to.
- This archive contains source code. It is not a player installation package.

Original project: https://github.com/modlist-org/KeyViewer  
Patch hub: https://github.com/Building114/ADOFAI-Mod-Compatibility-Hub

## Version Information

| Item | Value |
|---|---|
| Original source base | `4.13.1` |
| Patch runtime metadata | `4.14.0-preview.5` |
| Patch name | `KeyViewer XDBuilding` |
| Status | Temporary compatibility patch |

## Building

1. Install or locate the game.
2. Set the `ADOFAI_GAME_DIR` environment variable to the game folder, or pass `GameDir` when building.
3. Restore the NuGet package listed in `packages.config`.
4. Build `KeyViewer.sln`.
5. Use a Release build for a player-facing package.

Example:

```bat
nuget restore KeyViewer.sln
msbuild KeyViewer.sln /p:Configuration=Release /p:GameDir="D:\Games\A Dance of Fire and Ice"
```

The project copies the built mod files into the game's `Mods/KeyViewer` folder after a successful build.

## Included and Excluded Content

This cleaned source archive includes source code, resources, project files, the original GPL-3.0 license text, patch notes, and notices.

It intentionally does not include local editor data, build output, debug files, restored package folders, or a copied `NCalc.dll` binary. Restore dependencies before building.

See `PATCH_NOTES.md`, `NOTICE.md`, and `THIRD_PARTY_NOTICES.md`.
