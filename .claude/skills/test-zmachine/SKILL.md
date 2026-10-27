---
name: test-zmachine
description: Run both Z-machine test suites after editing examples/games/zmachine.fs or a story
---

    out/posix/lugforth examples/games/zmachine_tests.fs
    gforth examples/games/zmachine_tests.fs -e bye

Both must PASS (49 checks at last count). `zmachine.fs` runs under both: avoid words only one has. Stories and
their traps: skill `inform6-story`. Bump the version afterwards (skill `bump-version`).
