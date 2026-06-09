# Building TimingShow XDBuilding

This file describes the actual build path used by the cleaned source package.

## Required Local Files

You need a local ADOFAI install and UnityModManager files. The project reads them through MSBuild properties or environment variables.

| Needed by project | Environment variable | MSBuild property |
|---|---|---|
| ADOFAI game folder | `ADOFAI_GAME_DIR` | `GameDir` |
| UnityModManager installer folder | `UMM_INSTALLER_DIR` | `UnityModManagerInstallerDir` |
| UnityModManager runtime folder | `UMM_RUNTIME_DIR` | `UnityModManagerRuntimeDir` |

The project derives the Unity managed assembly folder from:

```text
<GameDir>\A Dance of Fire and Ice_Data\Managed
```

## Build with environment variables

```bat
set ADOFAI_GAME_DIR=C:\Path\To\A Dance of Fire and Ice
set UMM_INSTALLER_DIR=C:\Path\To\UnityModManagerInstaller
set UMM_RUNTIME_DIR=C:\Path\To\UnityModManagerRuntime
build.bat
```

## Build with direct MSBuild properties

```bat
msbuild TimingShow.sln /p:Configuration=Release /p:Platform="Any CPU" /p:GameDir="C:\Path\To\A Dance of Fire and Ice" /p:UnityModManagerInstallerDir="C:\Path\To\UnityModManagerInstaller" /p:UnityModManagerRuntimeDir="C:\Path\To\UnityModManagerRuntime"
```

## Output

A successful build produces:

```text
TimingShow\bin\Release\TimingShow.dll
TimingShow\bin\Release\Info.json
```

`build.bat` then creates the player package at:

```text
..\release\TimingShow.zip
```

That zip should contain only:

```text
TimingShow.dll
Info.json
```

## Files outside this source package

The following files are local build artifacts or local dependencies and are not part of this source package:

- `bin/`
- `obj/`
- copied ADOFAI assemblies
- copied Unity assemblies
- copied UnityModManager or Harmony binaries
- private local path files
- editor cache files
