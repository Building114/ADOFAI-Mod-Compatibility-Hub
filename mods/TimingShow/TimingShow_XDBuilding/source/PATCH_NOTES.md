# TimingShow XDBuilding Patch Notes

## Summary

This is an unofficial temporary compatibility patch based on TimingShow `1.3.0`.

The current source metadata reports version `1.3.2`.

The patch was prepared for newer ADOFAI builds where several older gameplay fields and hit-text paths are no longer safe to read directly.

## Main Compatibility Work

- Adds `Compat.cs` as a runtime compatibility layer.
- Replaces direct reads such as `scrController.speed` and `scrController.isCW` with guarded runtime lookup.
- Reads speed from newer player data such as `controller.playerOne.planetarySystem.speed` when available.
- Reworks judgement text, death text, result text, and song-title writes through safer text-field lookup.
- Keeps the UnityModManager `Id` as `TimingShow` to avoid losing existing user settings.

## Local Multiplayer

- Adds per-local-player timing buckets.
- Each player can keep their own latest offset, judgement color, and result-page average.
- Timing calculation prefers the triggering planet or player planetary system before falling back to player one.

## Hit Text Handling

The later fixes narrowed the hit-text path instead of guessing many unrelated classes.

Current behavior:

- Hooks `scrHitTextMesh.Show`.
- Reads the judgement name primarily from the `Show` arguments.
- Reads the text object primarily from `scrHitTextMesh.mainText`.
- Reads color primarily from `mainText.color`.
- Writes once after `Show`, with optional controlled extra rewrites.
- Uses `scrHitTextMesh.LateUpdate` as the preferred follow-up point, falling back to `Update` only when needed.
- Caches the hit-text object instead of repeatedly scanning child objects.

Small fallback paths remain for older or slightly different layouts:

- If `mainText` is not found, try `text` and `textMesh`.
- If `mainText.color` is not found, try `hitText.color`.
- If the judgement name is not found in method arguments, try `hitMargin` and `margin`.
- If no color can be read, fall back to the default color for the judgement name.

## Debug and Tuning Settings

- Adds a UnityModManager settings debug panel. It is off by default.
- Adds `HitTextExtraRewriteCount`, range `0..2`, default `0`.
- Adds `HitTextReadDelayFrames`, range `0..3`, default `0`.

`HitTextReadDelayFrames` exists for builds where `mainText` or its color becomes valid a few frames after `scrHitTextMesh.Show`.

When the delay is greater than zero, the timing value is still frozen at `Show` time. The later read only delays text/color lookup; it should not let the next key press overwrite the previous timing value.

## Recovery Note

This patch intentionally avoids broad automatic compatibility hacks such as hiding specific player text or skipping specific judgement titles without confirmed fields. If a newer game build changes the real hit-text fields again, use the debug panel to confirm the new field path first, then narrow the compatibility layer instead of adding wide repeated scans.
