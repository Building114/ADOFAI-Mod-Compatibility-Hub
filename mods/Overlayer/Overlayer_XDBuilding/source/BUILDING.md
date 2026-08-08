# Building Overlayer XDBuilding 3.49.6

## Requirements

- Windows
- A Dance of Fire and Ice installed locally
- Visual Studio or Visual Studio Build Tools with MSBuild
- .NET Framework 4.8.1 targeting pack
- NuGet restore support
- A compiler supporting the configured C# 13 language version

The main Overlayer project targets x64.

## Configure the game folder

Use an environment variable:

```bat
set "ADOFAI_GAME_DIR=C:\Path\To\A Dance of Fire and Ice"
```

Or copy:

```text
Overlayer\Local.Build.props.example
```

to:

```text
Overlayer\Local.Build.props
```

and edit the local path. `Local.Build.props` is machine-specific and is intentionally excluded from the public source package.

## Build

Use the root solution:

```bat
msbuild Overlayer.sln /restore /p:Configuration=Release
```

You can pass the game directory explicitly:

```bat
msbuild Overlayer.sln /restore /p:Configuration=Release /p:GameDir="C:\Path\To\A Dance of Fire and Ice"
```

Optional local deployment:

```bat
msbuild Overlayer.sln /restore /p:Configuration=Release /p:DeployToGame=true
```

## Known restore limitation

`Overlayer.csproj` references `LibreHardwareMonitorLib.dll` from the user's global NuGet cache at version 0.9.6, but the package is not declared as a `PackageReference`.

Therefore a completely clean machine may still need `LibreHardwareMonitorLib` 0.9.6 present in the expected NuGet cache path before the project builds.

## Release check

The player archive used for this 3.49.6 release reports version `3.49.6` in `info.json` and is the same tested archive supplied to this packaging step.
