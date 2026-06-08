# Review Required Before Public Redistribution

This cleaned 3.49.3 source archive fixes the obvious local-path, maintainer-label,
metadata, popup-layout, and notice problems found during static review.

One item still needs a human check before treating the archive as fully cleared
for public redistribution.

## Code editor related source files

The package contains:

```text
Overlayer/CodeEditor/
```

The existing README described these files as related to UnityCodeEditor, but the
archive itself did not include enough source-origin records or license text to
verify the redistribution terms for every copied file.

Before public release:

1. compare the files against the actual upstream source or commit;
2. identify the license that applies to each copied file;
3. add the matching copyright and license text;
4. remove or replace any file whose redistribution terms cannot be confirmed.

## Player package reminder

This zip is a source package. It is not a player installation package.

A player-facing release zip must be built separately, tested separately, and
checked for third-party DLL notices separately.
