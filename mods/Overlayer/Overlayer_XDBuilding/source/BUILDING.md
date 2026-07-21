# Building Overlayer XDBuilding

## Requirements

- Windows
- A Dance of Fire and Ice installed locally
- Visual Studio or Visual Studio Build Tools with MSBuild
- .NET Framework 4.8.1 targeting pack
- NuGet restore support
- A compiler supporting the configured C# 13 language version

The project references ADOFAI, Unity, Harmony, and UnityModManager assemblies from the local game installation.

## Configure the game folder

Set an environment variable:

```bat
set "ADOFAI_GAME_DIR=C:\Path\To\A Dance of Fire and Ice"
```

Or copy `Overlayer\Local.Build.props.example` to `Overlayer\Local.Build.props` and edit the local path. The real local props file must not be committed.

## Build both projects

Use the root solution:

```bat
msbuild Overlayer.sln /restore /p:Configuration=Release
```

You can pass the game directory directly:

```bat
msbuild Overlayer.sln /restore /p:Configuration=Release /p:GameDir="C:\Path\To\A Dance of Fire and Ice"
```

Expected project outputs:

```text
Overlayer\bin\Release\
Overlayer.Bootstrapper\bin\Release\
```

Do not build only `Overlayer\Overlayer.sln`; that duplicate solution contains the main project only and will leave an older Bootstrapper in the game folder.

## Optional local deployment

Deployment is disabled by default.

```bat
msbuild Overlayer.sln /restore /p:Configuration=Release /p:DeployToGame=true
```

The post-build deployment deletes existing DLL files under `Mods\Overlayer\lib` before copying dependencies. Use it only against a backed-up local test installation.

## Known restore issue

`Overlayer.csproj` references `LibreHardwareMonitorLib.dll` from the user's global NuGet cache, but version 3.49.5 does not declare `LibreHardwareMonitorLib` as a `PackageReference`.

A clean machine can therefore fail even after `/restore`. Add and verify a reproducible restore declaration before claiming fully reproducible builds.

## Final package verification

For the 3.49.5 package, verify both assemblies before archiving:

```bat
findstr /m /c:"obj\Release\Overlayer.pdb" Overlayer.dll
findstr /m /c:"obj\Release\Overlayer.Bootstrapper.pdb" Overlayer.Bootstrapper.dll
```

Also confirm that the ZIP contains no PDB, `.vs`, `bin`, `obj`, or local build settings files.
