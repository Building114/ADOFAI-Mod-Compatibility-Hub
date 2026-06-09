# TimingShow XDBuilding

This is an unofficial temporary compatibility patch source package for TimingShow.

## Important Notice

- This package is not an official TimingShow release.
- Building114 maintains this temporary compatibility patch, but is not the original author of TimingShow.
- The original project belongs to SleepingCui and any other original contributors.
- The original TimingShow project should be preferred if an official compatible update becomes available.
- The TimingShow name is used only to identify the project that this patch is related to.
- This archive contains source code. The player installation package is stored separately in `../release/TimingShow.zip`.

Original project: https://github.com/SleepingCui/TimingShow
Patch hub: https://github.com/Building114/ADOFAI-Mod-Compatibility-Hub

## Version Information

| Item | Value |
|---|---|
| Original source base | `1.3.0` |
| Patch runtime metadata | `1.3.2` |
| Patch name | `TimingShow XDBuilding` |
| Target game build noted during patching | `ADOFAI r143` |
| Status | Temporary compatibility patch |

## Main Changes

- Keeps the original UnityModManager `Id` as `TimingShow`, so old settings can still be found.
- Keeps the original author field as `SleepingCui` in `Info.json`.
- Adds `Compat.cs`, a runtime compatibility layer for newer ADOFAI builds.
- Replaces older direct reads such as `scrController.speed` and `scrController.isCW` with safer runtime lookups.
- Reads newer player speed data through `controller.playerOne.planetarySystem.speed` when available.
- Reworks judgement text, death text, result text, and song-title writes through safer text-field lookup.
- Adds per-local-player timing state for local multiplayer.
- Adds a small debug panel and controlled hit-text rewrite settings for troubleshooting.
- Adds delayed hit-text reading through `HitTextReadDelayFrames` for builds where `mainText` is populated after `scrHitTextMesh.Show`.

See `PATCH_NOTES.md` for the consolidated patch history.

## Building

This project needs local ADOFAI and UnityModManager assemblies to build. Those files are not included in this repository.

You may build by setting environment variables:

```bat
set ADOFAI_GAME_DIR=C:\Path\To\A Dance of Fire and Ice
set UMM_INSTALLER_DIR=C:\Path\To\UnityModManagerInstaller
set UMM_RUNTIME_DIR=C:\Path\To\UnityModManagerRuntime
build.bat
```

You may also pass MSBuild properties directly:

```bat
msbuild TimingShow.sln /p:Configuration=Release /p:Platform="Any CPU" /p:GameDir="C:\Path\To\A Dance of Fire and Ice" /p:UnityModManagerInstallerDir="C:\Path\To\UnityModManagerInstaller" /p:UnityModManagerRuntimeDir="C:\Path\To\UnityModManagerRuntime"
```

`build.bat` builds `TimingShow.sln` and packages the release as:

```text
../release/TimingShow.zip
```

The release package contains:

```text
TimingShow.dll
Info.json
```

## Included and Excluded Content

This cleaned source archive includes source code, project files, the original license file as supplied in this package, patch notes, and notices.

It intentionally does not include local editor data, build output, debug files, restored package folders, copied game assemblies, Unity assemblies, UnityModManager binaries, Harmony binaries, or private local paths.

See `NOTICE.md`, `PATCH_NOTES.md`, and `THIRD_PARTY_NOTICES.md` for attribution and redistribution notes.
