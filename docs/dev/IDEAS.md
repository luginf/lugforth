# Ideas, open questions and other Forths

Decisions pending and things not done. Part of [README.md](../../README.md). The to-do list of the editors is in [FEATURES.md](FEATURES.md).

## Open question: control structures outside a definition

Decision pending (the maintainer is thinking about it), nothing is implemented yet.

Today `do`, `?do`, `if`, `begin`... typed outside a definition (at the prompt,
at the top level of a file, in an `evaluate`d string) give no error and a wrong
result: `3 0 do 42 . loop` prints 42 once and leaves `3 0` on the stack,
`1 if 7 . then` runs its body whatever the flag and leaves the `1`. Wrap such
code in a definition (`: t 3 0 do ... loop ; t`), tests included.

The standard leaves this case undefined. What other Forths do:

1. An error, the word being compile-only (gforth, pForth): `-14 throw`.
2. Interpreted loops in brackets that re-read the source, like the standard
   `[IF] [ELSE] [THEN]`: gforth has `[DO] [?DO] [LOOP] [+LOOP] [BEGIN] [UNTIL]
   [AGAIN] [WHILE] [REPEAT] [FOR] [NEXT]`.
3. A temporary anonymous definition: met in interpret state, `do`/`if`/`begin`
   open a `:noname`, compiled up to the end of the structure, then run and
   forgotten (Win32Forth, for example).
4. Quotations, always compiled (RetroForth, Konilo, Factor): `#10 [ ... ] times`.

Lugforth has only the conditionals in brackets (`common/conditionals.fs`:
`[IF] [ELSE] [THEN] [DEFINED] [UNDEFINED] DEFINED?`); none of the bracket loops
of point 2 exist.

Options considered:

- Point 1 only: the control words throw -14 in interpret state, a clear
  message instead of a silent wrong result. Smallest change.
- Point 3: what is typed at the prompt works as in a definition (and runs
  compiled). Nesting has to be handled, and the boot, ESP32 and Pico checked.
- Point 2: `[DO] [?DO] [LOOP] [+LOOP] [I] [BEGIN] [UNTIL] [AGAIN] [WHILE]
  [REPEAT]` beside `[IF]`, with tests in `common/conditionals_tests.fs`. Easy
  within one line (reset `>in`); across lines it needs `save-input` /
  `restore-input` (not checked whether lugforth has them), and lines typed at
  the prompt are gone. Mostly useful to generate code or data while a file loads.

## Ideas left, after DurexForth (to decide later)

DurexForth (https://github.com/jkotlinski/durexforth, C64; manual https://jkotlinski.github.io/durexforth/, read 2026-10-20, license not checked
before borrowing code: write our own) suggests, for `graphic` and `play`:
- drawing: `paint ( x y -- )` flood fill, `plot`, `peek`, `pen` in inverse (XOR) mode, `line` from the last point;
- turtle (examples/graphics/sdl2_turtle.fs): `back`, `turtle@` / `turtle!` (save and restore the state, for recursive fractals), `moveto ( x y deg -- )`;
- sound: an ADSR envelope on `beep` (notes are flat and click at both ends), SID-like `voice! note! cutoff! filter!`;
- `play` with several voices (DurexForth's `play-mml` takes one string per SID voice; ours has one voice);
- the other settings (`zoom`, `width`, `height`, `key-hold`) are still values with `to`, unlike `color`, `wave`, `beep-volume`.

## Other projects to credit or borrow from

Check a project online before naming it in the README ("Inspiration"), NOTICE or a list of ideas. Bifurcan, once
suggested as a Forth for Uxn, is a Tal program (a clock), not a Forth.

Other r3forth candidates: bdash (Boulder Dash, own sprites needed), sokoban (levels of unclear license), term demos (fire, matrix, torus), util/penner.r3 easing, lib/sdl2poly.r3 polygon fill. From eForth Windows: more inference examples (`INFERENCE/docs/*.odt`), `3Dmodelisation` / `dumpTool` extras, other upstream projects (lotto, 6809, flog, BMP, HTTP).

## Other Forths, for comparison (not part of this repo)

- gforth 0.7.9 bundles no SDL. Bindings exist: `JeremiahCheatham/Gforth-SDL2-Bindings` (52 files, SDL2 + image + mixer + ttf, raw API, event enums suffixed `_ENUM`), `foggynight/gforth-sdl2` (SWIG generated, copy in `garvalf/forth-is-fun` `gforth/lib` with only SDL, events, render, scancode, timer, video, plus a small `sdl_init.fth` helper and the `forthtoise.fth` turtle), and the `magnoquill-gforth.fth` cart.
- They use `c-library` (C compiled at load time, needs gcc + libsdl2-dev; floats and structs possible) and are 1:1 with the C API; gforth needs case-sensitive wordlists (`table >order`) to avoid the same `SDL_Quit`/`SDL_QUIT` clash. This branch uses `dlopen` (only the `.so` is needed), has a high level vocabulary, and is the only one that also runs in a browser.


