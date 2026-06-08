# Overlayer XDBuilding

Unofficial temporary compatibility patch for Overlayer-Lagacy.

This source package is unified under the following public metadata:

- Name: Overlayer XDBuilding
- Base version: Overlayer-Lagacy 3.49.0
- Patch version: 3.49.3
- Target ADOFAI version: 3.1.1
- Patch maintainer: Building114
- License: GPL-3.0

This is not an official release from modlist.org, c3nb, or the original Overlayer maintainers.

Current V5 upstream note: `modlist-org/Overlayer` is the V5 mainline. This 3.x patch points to the legacy repository `modlist-org/Overlayer-Lagacy`.

## Source

Original project: modlist-org/Overlayer-Lagacy  
Base version: Overlayer-Lagacy 3.49.0  
Patch version: Overlayer XDBuilding 3.49.3  
License: GPL-3.0

## Patch notes

- Keeps the original Overlayer structure, license, and attribution.
- Applies XDBuilding compatibility changes for ADOFAI 3.1.1.
- Adds additional practical fields.
- Improves parsing logic.
- Disables AutoUpdater so the official updater does not overwrite this unofficial build.
- Keeps the legacy Overlayer repository link separate from the current V5 mainline.

## Build notes

See [`BUILDING.md`](BUILDING.md) for step-by-step commands.

Requires the .NET Framework 4.8.1 targeting pack and a C# compiler capable of the configured language version.

Set the ADOFAI game path before building, using either:

- `ADOFAI_GAME_DIR`
- `GameDir`
- `Overlayer/Local.Build.props`, copied from `Overlayer/Local.Build.props.example` if present

Deployment to the local game Mods folder is opt-in. Build normally to compile only. To deploy after build, pass:

```text
/p:DeployToGame=true
```

---

<img src = "ov3.png" width="25%" height="25%">

# 🖥️ Overlayer
> **Display everything as you wish.**

Overlayer is a mod that displays texts in ADOFAI in-game.   

Originally made by [c3nb](https://github.com/c3nb).

# ⚖️ Licenses
This project uses third-party libraries as follows:

- **Included in source code:**
  - [RapidGUI](https://github.com/fuqunaga/RapidGUI) — MIT License
  - [UnityCodeEditor](https://github.com/joshcamas/UnityCodeEditor/) — Unlicensed (uses MIT-licensed code)

- **NuGet/runtime dependencies:**
  - [Jint](https://github.com/sebastienros/jint) 4.7.1 — BSD-2-Clause License
    - This source package references Jint through NuGet.
    - It does not claim to bundle a modified `modlist-org/jint` DLL.

# 🌐 Translations are welcome!

You can freely adapt the meaning to fit the style and culture of each language.

* 📝 Literal translation is not required. Additional explanations are allowed.
* 📂 Translation files are located in `/Overlayer/MiscFiles/lang/`.

If you are adding a new language instead of updating an existing one, you can refer to the official English or Korean translations.  
➡️ Please use a pull request!


## Third-party review note

The RapidGUI MIT license text is preserved in
`THIRD_PARTY_LICENSES/RapidGUI-LICENSE.md`.

Jint is restored through NuGet. Its BSD-2-Clause license text is included for
reference in `THIRD_PARTY_LICENSES/Jint-LICENSE.txt`.

Some code-editor-related source files still need a source-origin and license
review before this source package is treated as fully cleared for public
redistribution. See `REVIEW_REQUIRED.md`.
## Known Issue

A public report says Overlayer v3.49 may stop the ADOFAI level editor from saving levels.
Treat this package as Testing until the editor-save problem is reproduced and fixed.
