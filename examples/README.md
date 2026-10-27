# Examples

The examples are in folders by kind (SDL2 words, see `docs/SDL2.md`):

| folder | what |
|---|---|
| `games/` | `sdl2_breakout.fs`, `sdl2_roguelike.fs` (with its tile sheet), `sdl2_2048.fs` (and the terminal `2048.fs`), three games after r3forth (below: `sdl2_tetris.fs`, `sdl2_snake.fs`, `sdl2_minesweeper.fs`), `sdl2_mastermind.fs`, `sdl2_raycaster.fs` (a Doom-like maze by raycasting, steered with the mouse), `heart_game.fs`, `pinball/pinball.fs`; `games_tests.fs`, `raycaster_tests.fs` and `2048_tests.fs` are their tests |
| `graphics/` | `sdl2_demo.fs`, `sdl2_3d.fs` (turning wireframe cube, pyramid and octahedron; `3d.fs` is the math), `sdl2_turtle.fs` (Logo turtle graphics, words of forthtoise from [Forth is Fun](https://gitlab.com/garvalf/forth-is-fun), English and French), `sdl2_magnoquill.fs` (Magnoquill Driftflare Quiverbloom: a cloud of 4000 points drawn from a shader's formulas, rainbow colors and additive blending, ported from the gforth + SDL2 version; keys in the header), `boxes.fs`, `heart_graph.fs`, `min_grf.fs`, `bezier_test.fs` |
| `music/` | `sdl2_drums.fs`, `sdl2_pianoroll.fs`, `fm/sdl2_fm.fs` (FM synthesizer with MIDI input, also in the browser; see `music/fm/README.md`) |
| `utils/` | `txt2tags.fs`, `logs.fs`, `into.fs`, `complex.fs`, `infix/`, `inference/` (rules engine), `gemini/` (arrays, JSON) |
| `system/` | networking (`curl.fs`, `udp.fs`, `httpd_sample.fs`, `espnow.fs`), `cpu_load.fs` (on the bare-metal PC: the CPU load of a wait, of a key wait and of a loop that never waits), assemblers (`asm_xtensa.fs`, `code_x64.fs`, `code_xtensa.fs`), `varvara/` (Uxn virtual machine) |
| `bench/` | the benchmarks below, `speed.fs` |
| `web/` | HTML pages that load the web build |

The Turtle: the window shows the drawing, you type lines in the terminal (`100 forward 90 right`, loops allowed), then `go` draws at once, `speed 100` and `play` draw stroke by stroke, `list`, `undo`, `new`, `demo`, `help`.
Run one with `lugforth examples/games/sdl2_breakout.fs`, or `include games/sdl2_breakout.fs` from the examples folder.
On the bare-metal boards the folders are kept on the disk or the stick (`include games/sdl2_snake.fs`, `include system/varvara/varvara_rpi.fs`),
and the website (`ports/web/BUILD`) copies the files that run in the browser by name.

## In the browser

Several examples also run in the browser, from the [website](https://luginf.github.io/lugforth/), with the very same files:

* [Terminal](https://luginf.github.io/lugforth/demo/terminal.html): the Forth prompt, also on a phone. Type `sdl2`, then
  `10 10 150 140 line`, `red color 200 20 100 60 box`, `s" t200 o4 c d e f g" play`.
  `terminal.html?run=sdl2_roguelike.fs` runs a game file of the demo before the prompt.
* Games: [Breakout](https://luginf.github.io/lugforth/demo/sdl2_breakout.html),
  [roguelike](https://luginf.github.io/lugforth/demo/sdl2_roguelike.html),
  [raycaster](https://luginf.github.io/lugforth/demo/sdl2_raycaster.html) (the 3D maze, steered with the mouse),
  [pinball](https://luginf.github.io/lugforth/demo/sdl2_pinball.html).
* Music: [drums](https://luginf.github.io/lugforth/demo/sdl2_drums.html),
  [FM synthesizer](https://luginf.github.io/lugforth/demo/sdl2_fm.html) (with MIDI input).
* Drawing from HTML (`web/`): [a button calling a Forth word](https://luginf.github.io/lugforth/demo/button.html),
  [string art](https://luginf.github.io/lugforth/demo/string_art.html),
  [vector lettering](https://luginf.github.io/lugforth/demo/vector_logo.html) and
  [its outline](https://luginf.github.io/lugforth/demo/vector_logo_outline.html).

## Benchmarks: lugforth vs C vs uxn

Two benchmarks, each in three files doing the same work (same algorithm, constants
and data): `bench_sieve` and `bench_lines`, as `.fs`, `.c` and `.tal`
([uxn](https://100r.co/site/uxn.html) assembly). `make bench` builds and runs all
six; `make help` lists the targets. The ratios matter, the times depend on the
machine.

| benchmark | what it measures | C | uxn | lugforth |
|---|---|---:|---:|---:|
| `bench_sieve` | sieve up to 32768, 200 runs, no I/O | 0.078 ms | 7.1 ms | 7.8 ms |
| `bench_lines` | 100 Bresenham lines per frame through the real display path | 0.43 ms | 3.7 ms | 3.0 ms |

Pure interpretation is about 100 times slower than C for tight integer loops, for
lugforth and uxn alike. With real work behind a library or device call (the SDL2
renderer, uxn's Screen) the gap shrinks to 7-9 times, which is the case that
matters for drawing games.

`bench_lines.fs` stays open on the last frame (Esc to quit); `BENCH_HEADLESS=1`
exits at once. `make view-lines`, `view-lines-c` and `view-lines-uxn` open a window
to check the drawing by eye.

## pinball/

`lugforth examples/games/pinball/pinball.fs`: a menu (up/down, Enter, or 1 and 2; Esc in a game
comes back to it) with two tables. `common.fs` holds what they share (fixed point helpers,
3x5 font, seven segment digits).
- `raid.fs`, Night Raid: lines on a black screen, the score on a panel, missions from cadet to
  ace, gravity, bounce and number of balls to choose, and a demo where the machine plays until
  Enter. Keys: z/m (or the shifts, or the arrows) for the flippers, down held then let
  go for the plunger, space or n to nudge (TILT if too much).
- `night.fs`, Night Strike: a table laid out after an early 80s coin-op pinball whose screen
  captures and fix mode (38 game variables) were studied: NIGHT lanes, DROP targets, A B C D,
  F L Y, a captive hole, a dive bomb chute that can hold balls (multi-ball), two spinners,
  bonus and multipliers, 1 to 4 players, coins (q) and start (s or Enter), free games, tilt
  (a and l nudge). The rules are in the header of the file.
The physics is in fixed point (4096 units to a pixel, 6 steps a frame): circle against segments,
turning flippers that give their speed to the ball, bumpers and slingshots that kick it. It also
runs in the browser, `ports/web/sdl2_pinball.html` (written blind, not run here), with touch
buttons for the Raid keys. Its arithmetic stays within 32-bit cells, which the web build has.
`play_raid.fs` and `play_night.fs` start one table without the menu (used by `make smoke`).

## varvara/

A Uxn/Varvara virtual machine in Forth: `lugforth examples/system/varvara/uxncli.fs
program.rom` runs a ROM in the terminal. See `varvara/README.md`.

## txt2tags.fs

A converter from basic txt2tags syntax to HTML, written in Forth:

```
lugforth examples/utils/txt2tags.fs examples/utils/txt2tags_demo.t2t > demo.html
```

or `include examples/utils/txt2tags.fs` then `s" doc.t2t" t2t-file`. The header
comment lists the supported syntax.

## Games after r3forth

[r3forth](https://github.com/phreda4/r3) by Pablo Reda (MIT license) is a Forth of the ColorForth family
with its own syntax (`:word`, `#var`, conditionals followed by `( )` blocks, fixed point numbers), so its
games are not loaded as they are but written again in lugforth, keeping their ideas. Only libSDL2 is
needed: no font or image files, the digits come from a 3x5 bitmap. They also run on the bare-metal
boards (`ports/rpi`, `ports/efi`: `include sdl2_tetris.fs` from the card or the stick).

* `sdl2_tetris.fs`: the seven pieces, wall kicks, a ghost piece, the next piece, levels; r3's score table
  (40, 100, 300, 1200 times the level). Left/right, up or x turns, down drops faster, space drops at once,
  p pauses, r after the end.
* `sdl2_snake.fs`: through the edges as in r3, one more cell for each fruit and a little faster, and the
  end when it bites itself (r3's has none). Arrows or wasd, p, r after the end.
* `sdl2_minesweeper.fs`: r3's three levels (1, 2, 3: 9x9 and 10 mines, 16x16 and 40, 30x16 and 99) and its
  list rather than recursion to open the empty cells; the first cell is never a mine, a click on a number
  opens around it when its flags are all there. Mouse (left opens, right flags) or keyboard (arrows,
  space opens, f flags), so it plays on the PC under UEFI where the mouse may be missing.

Beside them, written for lugforth:

* `sdl2_raycaster.fs`: a walk in a maze drawn by raycasting (one ray per screen column, 16.16 fixed point,
  no floats), after the raycasting demo of JS-Forth (Chris Curl, MIT, in NOTICE). The mouse turns (the further
  from the middle of the window, the faster) and looks up or down; w a s d (or z q s d on an AZERTY keyboard,
  by key place) and the arrows walk and turn; m switches the mouse off. A map of the maze, with you in red, is
  at the top left. Checked by screenshots under Xvfb with xdotool (keys and mouse), and in headless Chromium for the web
  page (`ports/web/sdl2_raycaster.html`: it draws, a key moves the view; mouse turning not confirmed there). The math is
  32-bit safe (products through `*/`) because the browser has 32-bit cells. The boards have not run it (on them `scan?`
  is 0, so w/s/a/d by letter only, and the mouse is not tried).
* `sdl2_mastermind.fs`: find the four hidden colors (out of six, repeats allowed) in ten tries; after each
  row, a black peg per color at its place, a white one per right color elsewhere. Keys 1 to 6 put a color
  in the frame, left/right move it, up/down change its color, backspace takes it away, space or enter
  checks the row; or the mouse (a color below, a hole, the green box). r a new game.

`games_tests.fs` tests the logic of the four without opening a window (`make test-ports`).

## Ports from eForth Windows

[eForth Windows](https://github.com/MPETREMANN11/eForth-Windows) by Marc Petremann is a
ueForth for Windows, so its code loads in lugforth almost as is (`value`/`to`, `{ }` locals,
`vocabulary`, `:noname`, floating point). Three of its projects are here; they are GPL
(derived from his code), unlike the rest of the repository. Each file says what is his and what is new.

* `2048.fs`: the game in a terminal (`include examples/games/2048.fs`, then `2048-play`; arrows or wasd). `sdl2_2048.fs` is the same game in an SDL2 window (`lugforth examples/games/sdl2_2048.fs`; arrows, wasd or a mouse drag, r restarts; the tiles slide and pop, `--no-anim` after the file name turns that off), digits drawn from a bitmap so no font is needed.
  The original only has the grid and its display; moves, merging, score, keys and end are new.
* `3d.fs` + `sdl2_3d.fs`: 3D wireframes (`lugforth examples/graphics/sdl2_3d.fs`). `3d.fs` has the points, edges,
  sine table, rotation and perspective (no display); the original draws with the Windows GDI, here SDL2.
* `inference/` (`inference.fs`, `weather.fs`, `lamp_diagnosis.fs`) (explanations in `inference/README.md`, in French in `inference/README.fr.md`): facts (`FACT:`), rules (`:CONDITION`, `:ACTION`, `RULE:`) and the
  engine loop `infer` (new): `include examples/utils/inference/inference.fs`, `include examples/utils/inference/weather.fs`,
  `also facts  -1 to rain  -1 to cold  infer  .facts`.

`make test-ports` runs `2048_tests.fs`, `3d_tests.fs`, `inference/inference_tests.fs`, `inference/lamp_diagnosis_tests.fs` and `games_tests.fs`.
