# Examples

Games and demos (SDL2 words, see `ports/posix/SDL2.md`): `sdl2_demo.fs`,
`sdl2_breakout.fs`, `sdl2_drums.fs`, `sdl2_pianoroll.fs`, `sdl2_roguelike.fs`, `sdl2_3d.fs` (turning wireframe cube, pyramid and octahedron),
`sdl2_pinball.fs`.
Run one with `lugforth examples/sdl2_breakout.fs`, or `include` it from the prompt.

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

## sdl2_pinball.fs

Night Raid, a pinball in the style of Night Mission Pinball (subLOGIC, 1982):
lines on a black screen, the score on a panel, missions from cadet to ace,
gravity, bounce and number of balls to choose, and a demo where the machine
plays until Enter. Keys: z/m (or the shifts, or the arrows) for the flippers,
space or down held then let go for the plunger, n to nudge (TILT if too
much). The physics is in fixed point (4096 units to a pixel, 6 steps a
frame): circle against segments, turning flippers that give their speed to
the ball, bumpers and slingshots that kick it. It also runs in the browser,
`ports/web/sdl2_pinball.html`, with touch buttons on phones. Its arithmetic stays
within 32-bit cells, which the web build has.

## varvara/

A Uxn/Varvara virtual machine in Forth: `lugforth examples/varvara/uxncli.fs
program.rom` runs a ROM in the terminal. See `varvara/README.md`.

## txt2tags.fs

A converter from basic txt2tags syntax to HTML, written in Forth:

```
lugforth examples/txt2tags.fs examples/txt2tags_demo.t2t > demo.html
```

or `include examples/txt2tags.fs` then `s" doc.t2t" t2t-file`. The header
comment lists the supported syntax.

## Ports from eForth Windows

[eForth Windows](https://github.com/MPETREMANN11/eForth-Windows) by Marc Petremann is a
ueForth for Windows, so its code loads in lugforth almost as is (`value`/`to`, `{ }` locals,
`vocabulary`, `:noname`, floating point). Three of its projects are here; they are GPL
(derived from his code), unlike the rest of the repository. Each file says what is his and what is new.

* `2048.fs`: the game in a terminal (`include examples/2048.fs`, then `2048-play`; arrows or wasd). `sdl2_2048.fs` is the same game in an SDL2 window (`lugforth examples/sdl2_2048.fs`; arrows, wasd or a mouse drag, r restarts; the tiles slide and pop, `--no-anim` after the file name turns that off), digits drawn from a bitmap so no font is needed.
  The original only has the grid and its display; moves, merging, score, keys and end are new.
* `3d.fs` + `sdl2_3d.fs`: 3D wireframes (`lugforth examples/sdl2_3d.fs`). `3d.fs` has the points, edges,
  sine table, rotation and perspective (no display); the original draws with the Windows GDI, here SDL2.
* `inference/` (`inference.fs`, `weather.fs`, `lamp_diagnosis.fs`) (explanations in `inference/README.md`, in French in `inference/README.fr.md`): facts (`FACT:`), rules (`:CONDITION`, `:ACTION`, `RULE:`) and the
  engine loop `infer` (new): `include examples/inference/inference.fs`, `include examples/inference/weather.fs`,
  `also facts  -1 to rain  -1 to cold  infer  .facts`.

`make test-ports` runs `2048_tests.fs`, `3d_tests.fs` and `inference/inference_tests.fs` and `inference/lamp_diagnosis_tests.fs`.
