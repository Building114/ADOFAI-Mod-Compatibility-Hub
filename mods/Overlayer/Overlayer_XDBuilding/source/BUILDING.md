# Building Overlayer XDBuilding 3.49.7

> 翻译部分由 AI 提供，可能不准确。 / Translated portions were provided by AI and may be inaccurate.

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

and edit the local path. The example enables automatic deployment after a successful build. `Local.Build.props` is machine-specific and is intentionally excluded from the public source package.

## Build

Use the root solution:

```bat
msbuild Overlayer.sln /restore /p:Configuration=Release
```

You can pass the game directory explicitly:

```bat
msbuild Overlayer.sln /restore /p:Configuration=Release /p:GameDir="C:\Path\To\A Dance of Fire and Ice"
```

Automatic deployment (also enabled by the copied `Local.Build.props.example`):

```bat
msbuild Overlayer.sln /restore /p:Configuration=Release /p:DeployToGame=true
```

Build without deployment by passing `/p:DeployToGame=false`. Deploy copies the two DLLs, dependency DLLs, languages and metadata to `Mods\Overlayer`. It does not delete existing files from `lib`. Close the game first and back up the existing Mod folder before the first deploy. Build artifacts remain in `bin` and `obj` (ignored by Git).

## Known restore limitation

`Overlayer.csproj` references `LibreHardwareMonitorLib.dll` from the user's global NuGet cache at version 0.9.6, but the package is not declared as a `PackageReference`.

Therefore a completely clean machine may still need `LibreHardwareMonitorLib` 0.9.6 present in the expected NuGet cache path before the project builds.

