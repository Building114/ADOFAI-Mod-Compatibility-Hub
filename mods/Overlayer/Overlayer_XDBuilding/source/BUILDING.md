# Building Overlayer XDBuilding

This is an unofficial temporary compatibility patch source package.

## Requirements

- Windows
- A Dance of Fire and Ice installed locally
- Visual Studio or Visual Studio Build Tools with MSBuild
- .NET Framework 4.8.1 targeting pack
- NuGet restore support
- A C# compiler that supports the configured language version

The project references DLL files from the local game installation. Those game
files are not redistributed in this source package.

## Set the game folder

Use one of these methods.

### Method A: temporary command prompt setting

```bat
set "ADOFAI_GAME_DIR=C:\Path\To\A Dance of Fire and Ice"
```

### Method B: local props file

Copy:

```text
Overlayer\Local.Build.props.example
```

to:

```text
Overlayer\Local.Build.props
```

Then replace the example path with your own game folder.

`Local.Build.props` is ignored by Git and should not be committed.

## Build without copying files into the game

Run this from a Visual Studio Developer Command Prompt:

```bat
msbuild Overlayer.sln /restore /p:Configuration=Release
```

The main outputs are normally written under:

```text
Overlayer\bin\Release\
Overlayer.Bootstrapper\bin\Release\
```

## Build and copy the result into the local game Mods folder

```bat
msbuild Overlayer.sln /restore /p:Configuration=Release /p:DeployToGame=true
```

You can also pass the game folder directly:

```bat
msbuild Overlayer.sln /restore /p:Configuration=Release /p:GameDir="C:\Path\To\A Dance of Fire and Ice"
```

## Important

A successful source build is not automatically a ready-to-publish player
package. Before publishing a binary zip, check which third-party DLL files are
included and preserve the required license notices.
