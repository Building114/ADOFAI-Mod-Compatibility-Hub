# Overlayer XDBuilding

This is an unofficial temporary compatibility patch for Overlayer.

## Basic Information

| Item | Value |
|---|---|
| Original mod | Overlayer |
| Original repository | `https://github.com/modlist-org/Overlayer-Lagacy` |
| Original status | Archived / read-only since 2026-06-06 |
| Patch name | Overlayer XDBuilding |
| Base version | `3.49.0` |
| Patch version | `3.49.5` |
| Target ADOFAI version | `3.3.0` |
| Status | Available |
| Included here | Yes |

## Files

| Type | Path |
|---|---|
| Source | `source/` |
| Player release package | `release/Overlayer-XDBuilding-v3.49.5.zip` |

## Notice

This is not an official release from modlist.org, c3nb, or the original Overlayer maintainers.

Original author credits, upstream repository information, and license notices are preserved where applicable.

## Purpose

This patch makes Overlayer compatible with ADOFAI 3.3.0.

Main changes:

- Adds practical fields.
- Improves parsing logic.
- Keeps this build separate from the current Overlayer mainline.
- Provides a temporary compatibility build for users who need this version.

## License Notes

The original project is licensed under GPL-3.0.

This repository includes the patched source for this entry so users can inspect the corresponding source for the included release package.

## 3.49.4 fix note

This release fixes the confirmed editor-save failure caused by loading shared System.* dependencies from Overlayer/lib before ADOFAI's own managed assemblies.

The bootstrapper now prefers already-loaded assemblies and ADOFAI's own `A Dance of Fire and Ice_Data/Managed` copies for:

- `System.Buffers`
- `System.Memory`
- `System.Runtime.CompilerServices.Unsafe`
- `System.Threading.Tasks.Extensions`
