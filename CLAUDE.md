# Lugforth: notes for Claude

Lugforth is a fork of ueForth (Brad Nelson), 64-bit, ignores case. Read `README.md` and, before touching code, the
notes in `docs/en/dev/` (index: `docs/en/dev/README.md`):

| file | when to read it |
|---|---|
| `docs/en/dev/PITFALLS.md` | before writing Forth, a loader, web code or a new platform: traps learned the hard way |
| `docs/en/dev/ARCHITECTURE.md` | build system, tests, C kernel, boot chain, platform tree |
| `docs/en/dev/FEATURES.md` | SDL2 words, editors `ed` / `writhdeck`, games, `graphic`, `play`: what exists, what was tested |
| `docs/en/dev/PORTS.md` | Android, Raspberry Pi, UEFI PC, terminals, the web build and console, GBA, the bare-metal disk |
| `docs/en/dev/IDEAS.md` | open questions (control structures outside a definition), ideas left |

This file keeps the rules of work only. Facts about a feature or a port go in `docs/en/dev/`, not here.

## Start from the current main

Several sessions work on this repository at once, and `main` moves while a session runs
(a cloud session clones it once, at its start). Before the first commit of a change, and
again before opening a pull request: `git fetch origin main`, then start from (or merge in)
`origin/main`, and say so if `main` has moved meanwhile. Look for a file (`CLAUDE.md`
included) in `origin/main` too before saying it does not exist.

The global rules of the maintainer apply: no commit, push or amend without their agreement (they read a `git diff` first);
leave changes in the working tree, and do not remind them that they can read the `git diff` (they know). A commit asked for once does not cover the next change.

## Build and test

    python3 configure.py && ninja posix     # out/posix/lugforth (and lugforth-mini); also runs all .fs tests
    python3 tools/editors_pty_test.py out/posix/lugforth   # pty test of ed, writhdeck (save, browser, idle CPU)
    make -C examples smoke                  # headless load of the SDL2 examples (SDL_VIDEODRIVER=dummy)
    make -C examples test-ports             # logic tests of the games, 3d, inference, raycaster, turtle
    ninja posix-static-sdl                  # out/posix/lugforth-static-sdl: static musl binary with SDL2 + X11 inside (builds them first, see docs/en/dev/PORTS.md)
    ninja windows-mingw                     # out/windows-mingw/lugforth.exe with MinGW, tested under wine (see docs/en/dev/PORTS.md)
    ninja web                               # out/web: pages and lugforth.js; serve from there (python3 -m http.server)
    ninja forth2012                         # the Forth 2012 test suite (Hayes, Gerry Jackson), cloned into out/forth2012-suite or $F2012_DIR; all word sets must pass
    make -C ports/tic80 test                # out/tic80/lugforth-tic80.wasm, the module for TIC-80, tested under node

Xvfb and `xdotool` are installed: run `Xvfb :99 &`, then the SDL examples with `DISPLAY=:99` and screenshot with `import -window root`
(hold a key with `xdotool keydown`, sleep 0.25, `keyup`: a quick `xdotool key` is missed by the game loop). Headless alone: dummy driver,
`SDL_RenderReadPixels`. The web pages can be tested with headless Chromium over CDP (see `docs/en/dev/PITFALLS.md`).
The Android toolchain is NOT on this PC: anything in `ports/android` is written blind and compiled later on another machine.
Write the code carefully and say so in the commit. The web build is tested here (ninja web, Chromium), the efi and rpi tests
need packages that may be missing (see `docs/en/dev/PORTS.md`).

Do not `pkill -f` a pattern that appears in your own command line (it kills your shell): use `ps` and kill by PID.

## Version

At each modification of the code, tools or ports, raise the version, even by a fraction (a change that only adds to the docs does not need it): in the root `BUILD`,
`SetVersions(version='YYYY.MM.DD.N', date='YYYY-MM-DD')` (the date of the change, N = 1, 2... for the changes of that day; `info` shows it
under the revision), and the banner of `docs/en/MANUAL.md` (`Lugforth vYYYY.MM.DD.N`). Now 2026.10.30.8 (2026-10-30). The release tag is
`v` + this version (`v2026.10.23.1`): the release workflow refuses a tag that differs from `BUILD`. Dotted digits on purpose (the release
workflow, the packages and the Android `versionCode` read it). Do it in the same change set. (Before 2026.10.23.1 it was 7.0.11.N, and the
tags v0.5.x.)

## Layout

- `examples/` is split in `games/` (with `pinball/`, the tile sheet, their tests), `graphics/`, `music/` (with `fm/`), `utils/`
  (`inference/`, `infix/`, `gemini/`, txt2tags...), `system/` (network, assemblers, `varvara/`), `bench/`, `web/`. Tests stay
  beside the code they load (`include` is relative to the including file). Write paths as `examples/<folder>/name.fs`.
- `editors/`: `editor.fs` (`ed`), `writhdeck.fs` and their tests, shared by the POSIX boot, Windows, the EFI and Raspberry Pi disks (`lib/`), the release and gforth (`ports/gforth`). After a change run `python3 tools/editors_pty_test.py` on `out/posix/lugforth` and on `ports/gforth/writhdeck-gforth.fs`.
- `examples/games/zmachine.fs` also runs under gforth: after a change run `gforth examples/games/zmachine_tests.fs` as well as the Lugforth one.
  Compiling the Inform 6 stories (`cave.inf`, `cave_fr.inf`: compilers and libraries are not installed or not in the repo) and their traps: `docs/en/dev/FEATURES.md`, section "Inform 6 stories".
- `docs/en/`: `MANUAL.md` (tutorial and reference), `WORDS.md` (generated by `tools/words_reference.py` from the built interpreters: run it again after adding words), `dev/` (the notes above),
  `docs/pages/` (our website, Markdown, built by `tools/make_pages.py`, published by `.github/workflows/pages.yml`). The HTML pages
  inherited from uEforth and the tools that read them are archived in `attic/oldsite/` (see its README; `tools/words_reference.py` still reads it for the descriptions).
- `docs/fr/` holds the French twins of `docs/en/`, named `*_fr.md` (`MANUAL_fr.md`: the stick keeps them in one flat folder); a few notes
  (`dev/STATUS_fr.md`, `dev/inspirations/`) exist in French only. `docs/images/` and `docs/pages/` belong to no language. Wrap the paragraphs
  of every `docs/**/*.md` at 72 columns (the EFI console has 80, `writhdeck` wraps at 72): tables and code blocks stay as they are.
- The website copies its demo files by name from `out/web` (`ports/web/BUILD`): adding a web page means a line in `ports/web/BUILD`,
  in the demo list of `tools/make_pages.py` and in the `ninja` targets of `.github/workflows/pages.yml`.
- `python3 tools/make_pdf.py` builds `docs/lugforth_manual.pdf` (pandoc + xelatex) from every `.md` of `docs/en/` (`--fr`: `docs/fr/`); run it again after editing the docs.
- The bare-metal disk and the USB stick keep the folders of `examples/` and carry `MANUAL.md` and `WORDS.md`.

## Forth pitfalls hit again and again (more in `docs/en/dev/PITFALLS.md`)

`b@` in the editor takes a *position in the text*, not an address; `{ locals }` inside a conditional or loop body break (declare
them at the top of the word); a `|` inside an `r|` block ends it; `[char]` only inside definitions; `?do` with start > limit runs
away; `if`, `do`, `begin` outside a definition work (common/interpret_control.fs, a temporary word compiled in a scratch buffer) except on the esp32, pico, pico-ice and gba boots. On the web build the cells are 32 bits
(`*/` for 16.16 products). Silent "Error including: file" = a throw somewhere: bisect with a scratch file using `.` prints.

## Other projects to credit

Check a project online before naming it in the README ("Inspiration"), NOTICE or a list of ideas. Code borrowed under a license
goes in NOTICE with its text.

## Web page: JS inside ports/web/*.fs

The JavaScript of `ports/web/platform.fs` and `sdl2.fs` sits in `r~ ... ~` blocks, themselves inside a JS template string of the built
`lugforth.js`: a `~` or a backtick in it breaks every page (use `String.fromCharCode(126, 96)`). On a phone (not the Android app, `; wv)` in the
user agent) platform.fs draws a built-in keyboard (az-/qw- button, Tab on the letters page; arrows and Esc on the numbers page, sticky Ctrl, paste, two symbol pages, "native"
to get the phone's own keyboard back, choices in localStorage `lugkb-*`). Tested in headless Chrome with a mobile user agent driven over
the DevTools protocol (mouse events on the buttons), not on a real phone.
