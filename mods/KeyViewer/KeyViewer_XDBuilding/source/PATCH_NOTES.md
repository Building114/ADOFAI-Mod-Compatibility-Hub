# KeyViewer XDBuilding Patch Notes

## Summary

This is an unofficial temporary compatibility patch based on KeyViewer `4.13.1`.

The current source metadata reports version `4.14.0-preview.5`.

## Main Fixes

### Profile import and restart behavior

- JSON profile import saves the active-profile settings without immediately rewriting the imported profile file.
- Active-profile rows are de-duplicated and stale rows are cleaned up.
- Missing managers are repaired more carefully for editing and exporting.
- New and migrated profiles are saved at clearer points.

### Rapid show and hide behavior

- Key-per-second worker threads are stopped and reused more carefully.
- Key and manager cleanup is performed when objects are disabled or destroyed.
- Asset initialization waits for the required state.
- The settings screen keeps a stable loading branch during Unity layout and repaint passes.

### Configuration preservation

- False boolean values are written explicitly instead of being silently omitted.
- Blank dummy-key names remain distinguishable from real keys.
- `Code=None` can display as blank.
- Rotation and offset values are saved when either the pressed or released value differs from the default.
- The released easing condition is corrected.

### NCalc dependency handling

- The build embeds `NCalc.dll` as a fallback resource after package restore.
- Runtime loading also checks the mod `lib` folder and the mod root.
- Editing fails cleanly when NCalc still cannot be loaded.

## Recovery Note

A profile that was already saved after an offset or rotation field was omitted cannot always reconstruct its old value automatically.

Re-import the original profile JSON after installing a fixed build, or restore the missing vector values manually.
