# TimingShow XDBuilding r143 Patch

## Basic Information

| Item | Value |
|---|---|
| Original project | TimingShow by SleepingCui |
| Original source base | `1.3.0` |
| Patch version | `1.3.2` |
| Target game build | ADOFAI r143 |
| Status | Source-level temporary compatibility patch |
| Included here | Source only |

## Notice

This is an unofficial temporary compatibility patch. It is not an official release from the original TimingShow maintainer.

## Main fix

ADOFAI r141+ moved some gameplay state away from older direct fields. This patch follows the same basic idea shown by newer mods: for example, JipperResourcePack uses `controller.playerOne.planetarySystem.speed` for newer versions. TimingShow now reads important fields through `Compat.cs` so old and new layouts both have a chance to work.

## Known limitation

No compiled DLL is included. Build locally with ADOFAI r143 `Assembly-CSharp.dll`, Unity assemblies, UnityModManager, and Harmony.


## 1.3.2 update

- Added local-multiplayer timing buckets so each player keeps their own latest offset, judgement color, and result-page average.
- Judgement-color following no longer depends on replacing the judgement popup; it updates whenever `scrHitTextMesh.Show` exposes a color.
- Judgement popup replacement now resolves the player from `scrPlanet`, `scrHitTextMesh`, or r141+ `scrMarginTracker` fallback.
- Result-page append now tries old `txtResults`, several newer likely text fields, and active result text objects as fallback.
- Timing calculation now prefers the triggering planet/player `planetarySystem` before falling back to `controller.playerOne.planetarySystem`.
