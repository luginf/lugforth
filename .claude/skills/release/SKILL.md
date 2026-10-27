---
name: release
description: Prepare a Lugforth release tag; use when the user asks to tag or publish a release
---

The workflow `.github/workflows/release.yml` runs on a pushed tag `v*` and REFUSES a tag that differs from `BUILD`.
1. `git fetch origin main`; the tree must be at `origin/main` or ahead, clean, tests green (`ninja posix`).
2. Tag = `v` + version of `BUILD` (e.g. `v2026.10.27.4`): `grep -o "version='[^']*'" BUILD`.
3. Do NOT tag or push without the user's explicit agreement (a tag push publishes binaries on GitHub).
4. Release notes: summarize `git log <previous tag>..HEAD`.
