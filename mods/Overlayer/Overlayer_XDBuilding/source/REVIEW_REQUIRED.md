# Review Required Before Public Release

## Code editor source origin

`Overlayer/CodeEditor/` is inherited from the supplied Overlayer-Lagacy source.

The supplied upstream README says UnityCodeEditor is unlicensed and uses MIT-licensed code. The visible UnityCodeEditor repository does not provide enough information to establish the exact origin and license of every file in this folder.

Before describing the complete source package as fully license-cleared:

1. identify the upstream source and revision for each copied code editor file;
2. preserve all applicable copyright and license notices;
3. remove or replace any file whose redistribution permission cannot be confirmed.

## Reproducible clean-machine build

The supplied 3.49.5 main assembly and replacement Bootstrapper both show Release PDB paths and version 3.49.5.

A fully reproducible clean Windows build is still not established because `LibreHardwareMonitorLib.dll` is resolved from a user-specific NuGet cache path while the package is absent from `PackageReference`.

Before the next source-level release:

1. declare and restore `LibreHardwareMonitorLib` 0.9.6 explicitly;
2. build on a clean Windows machine;
3. confirm both projects use Release configuration;
4. run the game regression checks again.

## Binary dependency notices

The player package contains third-party runtime DLL files.

The adjacent public source package preserves the known project notices and verified RapidGUI and Jint license texts. Package-specific redistribution requirements for every runtime DLL should still be reviewed and preserved where required.

## Final 3.49.5 player package check

The final package generated during this audit passed these static checks:

- `info.json` reports version 3.49.5.
- `Overlayer.dll` contains a Release PDB path and no matching Debug PDB path.
- `Overlayer.Bootstrapper.dll` contains a Release PDB path and no matching Debug PDB path.
- No PDB, EXE, `.vs`, `bin`, `obj`, or `packages` entries are present in the archive.
- The package keeps the existing Overlayer player layout at the ZIP root.
