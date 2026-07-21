# Overlayer XDBuilding 3.49.5 Patch Notes

## Compatibility

- Adds guarded field, property, and method access across changed game versions.
- Updates player, planet, floor, judgement, checkpoint, combo, fail, progress, status, BPM, and tile access paths.
- Adds Unity image conversion, dependency loading, file picker, and main-thread compatibility helpers.
- Adds fallback behavior when reflected members are unavailable.

## Tags and parsing

- Adds a lexer and token model for tag parsing.
- Supports escaped braces, nested tags, nested parameters, recursive references, and expression format arguments.
- Adds expression tags including `Expr`, `Calc`, `ExprText`, `IfExpr`, `IfText`, and `Coalesce`.
- Adds multiplayer judgement and accuracy tags with optional player numbers.
- Adds player hit statistics and XPerfect integration tags.

## JavaScript scripting

- Updates Jint 4.7.1 function creation and .NET interop handling.
- Reworks script start, stop, reload, registration, callbacks, import, export, and error handling.
- Adds main-thread dispatch for Unity-facing calls.
- Adds judgement text, icon, audio, input, Discord, and global variable interfaces.

## Profiles and objects

- Strengthens profile loading, saving, migration, backup, rename, and deletion.
- Accepts complete profile JSON, object arrays, and single-object JSON.
- Improves cleanup of inactive, missing, or damaged objects and profiles.
- Separates mutable expression values during configuration copies.

## Bootstrapper and maintenance

- Adjusts dependency load order and shared System assembly reuse.
- Loads the main Overlayer assembly from bytes to reduce file locking and load-context conflicts.
- Stops initialization when required dependencies are missing.
- Disables the upstream automatic updater for this unofficial build.
