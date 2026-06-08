# Source Package Note

This archive contains the cleaned source for **Overlayer XDBuilding 3.49.3**.

Compared with the previous 3.49.3 source package, this revision also:

- replaces the local example game path with a placeholder;
- replaces temporary maintainer labels with `Building114`;
- avoids metadata that could look like an official modlist.org release;
- draws the update popup only once per `OnGUI()` pass;
- calculates popup size from `OnGUI()` before reading GUI styles;
- adds build instructions and third-party license files;
- records the remaining code-editor source-origin review in `REVIEW_REQUIRED.md`.

This is a source archive, not a player installation package.
