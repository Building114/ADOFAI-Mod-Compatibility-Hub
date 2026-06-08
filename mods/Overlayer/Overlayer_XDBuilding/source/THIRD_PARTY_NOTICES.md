# Third Party Notices

This project is based on Overlayer and keeps the upstream third-party notices.

## RapidGUI

RapidGUI source files are included under `Overlayer/RapidGUI/`.

License:
- MIT License

Preserved license text:
- `THIRD_PARTY_LICENSES/RapidGUI-LICENSE.md`

## Code editor related source

Code-editor-related source files are included under `Overlayer/CodeEditor/`.

The current package does not contain enough evidence to prove the exact source
origin and redistribution terms for every one of these files.

Before treating this source archive as fully cleared for public redistribution:

1. identify the upstream source or commit for each copied file;
2. preserve the matching license text and attribution;
3. remove or replace files that cannot be redistributed.

See `REVIEW_REQUIRED.md`.

## Jint

Jint is referenced through NuGet as `Jint` 4.7.1.

License:
- BSD-2-Clause License

Preserved license text:
- `THIRD_PARTY_LICENSES/Jint-LICENSE.txt`

Notice:
- This repository does not claim ownership of Jint.
- This source package does not claim to bundle a modified Jint DLL.
- If a future binary release includes Jint DLL files, preserve the required
  binary redistribution notice.

## Other NuGet dependencies

This project uses NuGet `PackageReference` entries listed in
`Overlayer/Overlayer.csproj`.

These packages are not committed into this source archive. Restore them through
NuGet when building.

Before publishing a binary release, inspect every included dependency DLL and
preserve the license notices required by the corresponding package.
