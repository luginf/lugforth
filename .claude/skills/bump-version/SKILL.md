---
name: bump-version
description: Raise the Lugforth version after any modification (BUILD, docs/MANUAL.md banner, CLAUDE.md); use at the end of every change set
---

1. Read the current version in `BUILD`: `SetVersions(version='YYYY.MM.DD.N', date='YYYY-MM-DD')`.
2. Today's date (`date +%F`). Same day: N + 1. New day: `YYYY.MM.DD.1`.
3. Change it in the same change set, in three places: `BUILD` (version and date), the banner of
   `docs/MANUAL.md` (`Lugforth vYYYY.MM.DD.N`), the "Now ..." sentence of the Version section of `CLAUDE.md`.
   If `docs/fr/MANUAL.md` exists, its banner too.
4. Check: `grep -rn "<new version>" BUILD CLAUDE.md docs/MANUAL.md docs/fr/MANUAL.md`.

Dotted digits on purpose (release workflow, packages and the Android `versionCode` read it). Never commit without
the user's agreement.
