# Examples

Games and demos (SDL2 words, see `ports/posix/SDL2.md`): `sdl2_demo.fs`,
`sdl2_breakout.fs`, `sdl2_drums.fs`, `sdl2_pianoroll.fs`, `sdl2_roguelike.fs`.
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
