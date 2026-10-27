# Ideas, open questions and other Forths

Decisions pending and things not done. Part of
[README.md](../../README.md). The to-do list of the editors is in
[FEATURES.md](FEATURES.md).

## Control structures outside a definition - done 2026-10-23

`common/interpret_control.fs` (tests in `interpret_control_tests.fs`)
implements option 3 below: a recognizer (`rec-ctl`, first in `RECSTACK`)
catches `if begin do ?do for case aft ahead` in interpret state, opens a
`:noname`, compiles every word up to the matching
`then until again repeat loop +loop next endcase` (depth counted from a
table of deltas), runs it, then reclaims it (kept if the structure
allotted data). Loaded by the posix, web, efi, rpi and windows boots;
NOT by esp32, pico, pico-ice, gba (not built here). Known limit: a
compile error inside the structure leaves the interpreter in compile
state until `[`, as for any definition; the next opener restarts
cleanly.

The standard leaves interpretation semantics of these words undefined.
gforth makes them compile-only (error) and offers
`[DO] [?DO] [LOOP] [BEGIN] [UNTIL]...` brackets instead (point 2). Other
options considered:

1. Error only (`-14 throw`). 2. Bracket loops that re-read the
   source. 3. Temporary anonymous definition (chosen).
4. Quotations (RetroForth, Factor).

## Ideas left, after DurexForth (to decide later)

DurexForth (https://github.com/jkotlinski/durexforth, C64; manual
https://jkotlinski.github.io/durexforth/, read 2026-10-20, license not
checked before borrowing code: write our own) suggests, for `graphic`
and `play`:
- drawing: `paint ( x y -- )` flood fill, `plot`, `peek`, `pen` in
  inverse (XOR) mode, `line` from the last point;
- turtle (examples/graphics/sdl2_turtle.fs): `back`, `turtle@` /
  `turtle!` (save and restore the state, for recursive fractals),
  `moveto ( x y deg -- )`;
- sound: SID-like `voice! note! cutoff! filter!` (the ADSR envelope,
  `adsr`, and the voices of `play`, separated by commas, are done since
  7.0.11.11);
- the other settings (`zoom`, `width`, `height`, `key-hold`) are still
  values with `to`, unlike `color`, `wave`, `beep-volume`.

## Other projects to credit or borrow from

Check a project online before naming it in the README ("Inspiration"),
NOTICE or a list of ideas. Bifurcan, once suggested as a Forth for Uxn,
is a Tal program (a clock), not a Forth.

Other r3forth candidates: bdash (Boulder Dash, own sprites needed),
sokoban (levels of unclear license), term demos (fire, matrix, torus),
util/penner.r3 easing, lib/sdl2poly.r3 polygon fill. From eForth
Windows: more inference examples (`INFERENCE/docs/*.odt`),
`3Dmodelisation` / `dumpTool` extras, other upstream projects (lotto,
6809, flog, BMP, HTTP).

## Other Forths, for comparison (not part of this repo)

- gforth 0.7.9 bundles no SDL. Bindings exist:
  `JeremiahCheatham/Gforth-SDL2-Bindings` (52 files, SDL2 + image +
  mixer + ttf, raw API, event enums suffixed `_ENUM`),
  `foggynight/gforth-sdl2` (SWIG generated, copy in
  `garvalf/forth-is-fun` `gforth/lib` with only SDL, events, render,
  scancode, timer, video, plus a small `sdl_init.fth` helper and the
  `forthtoise.fth` turtle), and the `magnoquill-gforth.fth` cart.
- They use `c-library` (C compiled at load time, needs gcc +
  libsdl2-dev; floats and structs possible) and are 1:1 with the C API;
  gforth needs case-sensitive wordlists (`table >order`) to avoid the
  same `SDL_Quit`/`SDL_QUIT` clash. This branch uses `dlopen` (only the
  `.so` is needed), has a high level vocabulary, and is the only one
  that also runs in a browser.


