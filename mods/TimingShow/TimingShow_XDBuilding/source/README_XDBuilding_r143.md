# TimingShow XDBuilding r143 compatibility source package

TimingShow XDBuilding is an unofficial temporary compatibility patch for TimingShow.

- Original mod: TimingShow
- Original author: SleepingCui
- Original repository: https://github.com/SleepingCui/TimingShow
- Patch maintainer: Building114
- Base version: 1.3.0
- Patch version: 1.3.2

This is not an official TimingShow release. Original TimingShow files remain under their original author notice and license.

## What changed

- Kept the original UnityModManager `Id` as `TimingShow` so old settings are not lost.
- Kept the original author field as `SleepingCui`.
- Added `Compat.cs`, a runtime compatibility layer for newer ADOFAI builds.
- Replaced direct reads such as `scrController.speed` and `scrController.isCW` with safer runtime lookups.
- Added newer player speed lookup through `controller.playerOne.planetarySystem.speed`.
- Reworked judgement text, death text, result text, and song title text writes to use safer text-field lookup.
- Added per-local-player timing state for local multiplayer.
- Made HUD hiding more reliable when leaving gameplay.

## Build requirements

This source package needs local ADOFAI and UnityModManager assemblies to build.

Pass paths through MSBuild properties or environment variables instead of editing private paths into the project file.

Example:

```bat
msbuild TimingShow.sln /p:Configuration=Release /p:GameDir="C:\Path\To\A Dance of Fire and Ice" /p:UnityModManagerInstallerDir="C:\Path\To\UnityModManagerInstaller" /p:UnityModManagerRuntimeDir="C:\Path\To\UnityModManagerRuntime"
```

You can also set these environment variables before building:

```bat
set ADOFAI_GAME_DIR=C:\Path\To\A Dance of Fire and Ice
set UMM_INSTALLER_DIR=C:\Path\To\UnityModManagerInstaller
set UMM_RUNTIME_DIR=C:\Path\To\UnityModManagerRuntime
```

After building, use the generated `TimingShow.dll` together with `Info.json` as a normal UnityModManager mod.

## Important notice

This patch is meant as a temporary compatibility fix. If the original author publishes an official compatible TimingShow version, users should prefer the official version.
