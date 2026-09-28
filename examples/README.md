# Benchmarks: ueforth vs plain C vs uxn

Two small, self-contained benchmarks compare this fork's interactive Forth
(`out/posix/ueforth`, a native-threaded interpreter with direct FFI into
shared libraries such as SDL2) against a compiled C reference and against
[uxn](https://100r.co/site/uxn.html), the small stack-based virtual machine
whose portability (it also runs on GBA, NDS, and other tiny targets) prompted
the comparison. Each benchmark exists in three files doing the exact same
work — same algorithm, same constants, same data — so the numbers below are
directly comparable:

| benchmark | ueforth | C | uxntal |
|---|---|---|---|
| pure computation | `bench_sieve.fs` | `bench_sieve.c` | `bench_sieve.tal` |
| drawing throughput | `bench_lines.fs` | `bench_lines.c` | `bench_lines.tal` |

Run `make bench` to build and run all six, or see [Makefile](Makefile) for
individual targets (`make help` lists them). Numbers below were measured on
this machine and will vary on others, but the *ratios* are the interesting
part.

## bench_sieve: pure computation, no display

The sieve of Eratosthenes up to 32768 (kept below 65536 so it fits, together
with the code, in uxn's single 64K memory bank), run 200 times back to back.
No I/O in the timed part — this isolates raw interpreter/VM dispatch
overhead from everything else. All three agree on the answer: 3512 primes.

| | time per run | vs C |
|---|---:|---:|
| C (`cc -O2`) | 0.078 ms | 1.0× |
| uxn (`uxncli`) | 7.1 ms | ~91× |
| ueforth | 7.8 ms | ~100× |

ueforth and uxn land in the same ballpark — both are simple, non-JIT
interpreters — and both are roughly two orders of magnitude slower than
compiled C for tight, branch-heavy integer loops. Nothing surprising here:
this is close to interpreter-dispatch overhead in its purest form.

## bench_lines: drawing throughput, real display path

100 lines, endpoints picked by a 16-bit LCG (so the three versions draw
*exactly* the same pixels), rasterized with a hand-written Bresenham line
algorithm (no built-in "line" primitive is used, to keep the comparison at
the pixel level) and redrawn 50 times through the real display path of each
platform: `SDL_RenderDrawPoint`/`SDL_RenderPresent` for C and ueforth,
uxn's Screen device (`.Screen/pixel` DEO) for uxntal. A checksum of every
plotted pixel, reset each frame, confirms the geometry is identical
everywhere: `6942` (hex).

| | time per frame | vs C |
|---|---:|---:|
| C+SDL2 (`SDL_VIDEODRIVER=dummy`) | 0.43 ms | 1.0× |
| ueforth+SDL2 (`SDL_VIDEODRIVER=dummy`) | 3.0 ms | ~7.0× |
| uxn (`uxncli`, headless) | 3.7 ms | ~8.7× |

The gap to C narrows sharply compared to bench_sieve (~100×  down to
~7-9×): once real work happens on the other side of a device/library call —
the SDL2 renderer, or uxn's Screen device — the cost of *that* call
dominates, and the overhead of the interpreter or VM that issued it matters
much less. This is the case that actually matters for "ueforth as an
interactive SDL2 frontend, uxn-style": drawing lots of small things every
frame is not where an interpreted language pays the most.

`make view-lines` (ueforth), `make view-lines-c` (C) and `make view-lines-uxn`
(uxn, via `uxnemu`) each open a real window with the 100 lines drawn, left on
screen after the timed part, to check the rendering by eye rather than just
trust the checksum.

## Notes

* Numbers vary run to run (background load, thermal throttling, `dummy` vs a
  real video driver); what stays stable across runs on this machine is the
  order of magnitude of each ratio.
* `bench_lines.fs`'s window normally stays open on the last frame (press esc
  or close it to quit); set `BENCH_HEADLESS=1` to skip straight to exit,
  which is what the Makefile's timing targets do.
* uxntal has no signed comparison instructions, so Bresenham's error term
  (which goes negative) is tested by hand: subtract, then check the top bit
  of the 16-bit two's complement result. `ADD2`/`SUB2` don't care about
  signedness, only the branches do.
* uxn's default palette is all black; `bench_lines.tal` sets one explicitly
  (`System/r`, `/g`, `/b`) or the lines would be correct but invisible.
