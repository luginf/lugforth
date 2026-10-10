# lugforth for the Game Boy Advance (prototype)

Modeled on `ports/pico-ice/`: the same kernel (`common/*.h`), the same "platform
opcode list" pattern for the small amount of C glue a platform needs
(`builtins.h`), the same Forth-level boot chain shape (`platform.fs`,
`autoboot.fs`). Why GBA specifically: `ports/pico-ice/` already builds with
`arm-none-eabi-gcc`, the same toolchain family GBA homebrew uses, and the
GBA's 288K RAM (256K EWRAM + 32K IWRAM) is the same size class as the 150K
heap already proven working on `ports/pico-ice/`'s RP2040.

## What works, what doesn't

**The simulator (`LUGFORTH_SIM`, an ordinary POSIX program) is fully working
and was tested extensively**: boots, reaches the normal `Lugforth` banner and
` ok` prompt, and the demo `autoboot.fs` gradient runs to completion without
error. Build and run it:

```sh
python3 ../../tools/importation.py -i gba_boot.fs -o /tmp/gba/gen/gba_boot.h \
  -I ../.. -I /tmp/gba --name boot --header cpp
g++ -DLUGFORTH_SIM=1 -O2 -I ../.. -I /tmp/gba -std=c++11 main.c -o /tmp/gba/sim
/tmp/gba/sim
```

(This is the same two-step "flatten the boot text, then compile main.c
against it" pipeline `ports/pico-ice/BUILD` drives through the repo's own build
system; wiring this platform into that system the same way is the obvious
next step, not done in this first pass.)

**The bare-metal ROM boots correctly, confirmed both under `mgba` and by the
user on their own machine** (tiles drawn on screen, matching the simulator —
see "Fixed, and confirmed booting" below for the bug that had to be found
first). `gba.gba` in this directory is that confirmed-working build,
kept out of git (see `.gitignore`) but here to run directly, no rebuild
needed:

```sh
mgba gba.gba   # or uxnemu-style: whatever GBA emulator, or real hardware
```

Rebuild it from source with a hand-written `crt0.s` + `gba.ld` (no libgba/
devkitARM):

```sh
arm-none-eabi-as -mcpu=arm7tdmi crt0.s -o /tmp/gba/crt0.o
arm-none-eabi-g++ -c -mcpu=arm7tdmi -marm -O2 -fno-exceptions -std=c++11 \
  -I ../.. -I /tmp/gba main.c -o /tmp/gba/main.o
arm-none-eabi-g++ -mcpu=arm7tdmi -marm -nostartfiles --specs=nosys.specs \
  -T gba.ld /tmp/gba/crt0.o /tmp/gba/main.o -o /tmp/gba/gba.elf
arm-none-eabi-objcopy -O binary /tmp/gba/gba.elf gba.gba
python3 fix_header.py gba.gba
```

**Fixed, and confirmed booting under `mgba` (0.10.2) and on the user's own
machine**: the tile sheet draws correctly on screen, matching the
simulator. The bug (a uniform white screen, first seen by the user too, so
not a sandbox artifact) was a missing `.data` copy in `crt0.s`. Bisection
trail, in case a similar bug recurs on another platform:

* `crt0.s` + `gba.ld` + real hardware register/VRAM access were fine from
  the start: a *minimal* ROM (no Forth kernel at all) filling VRAM red
  showed solid red correctly, including with the full kernel compiled in as
  long as `forth_init`/`forth_run` were never actually called.
* The moment `forth_init` ran — even alone, before any Forth text is ever
  interpreted — the screen turned white. That pointed at `forth_init`'s own
  setup code, not at `platform.fs`/`autoboot.fs`.
* Ruled out: ARM-vs-Thumb codegen (`-mthumb` main.c against the ARM-mode
  `crt0.s`: still white) and `h!`'s two-byte-write access width for I/O
  registers (a standalone byte-pair write to `REG_DISPCNT`, no Forth,
  stayed red).
* The actual cause: `crt0.s` cleared `.bss` but never copied `.data` from
  its ROM image to its EWRAM runtime address. `.data` here holds newlib's
  own internal state (`_impure_ptr`, `_impure_data`, malloc's bookkeeping —
  see `nm` output filtered to that address range), needed by essentially
  any newlib call. With it uninitialized, `_impure_ptr` was garbage, and
  the first thing dereferencing it wrote through that garbage pointer —
  landing, apparently, in VRAM. Confirmed by isolating `forth_init()` alone
  in a standalone test file: white before adding the `.data` copy loop to
  `crt0.s`, red (i.e. correct, unchanged) after.
* One more wrinkle, not a bug: under this specific sandboxed setup (Xvfb,
  no GPU, shared machine), the emulated CPU ran far slower than real GBA
  speed — the demo took on the order of 20-30 seconds of wall-clock time to
  reach the point of drawing the tiles, not the sub-second one would expect
  from a ~17MHz ARM7TDMI. A screenshot taken too early still reads as a
  blank/white screen and can look like the same bug; give it time (or watch
  in a real desktop mgba, which should not have this problem) before
  concluding otherwise.

`crt0.s`'s zeroed Nintendo logo bytes and lack of a real BIOS file remain
untested but likely fine (mgba's defaults are documented not to require
either); worth knowing about if a *stricter* emulator or real hardware
still doesn't boot this.

## Tiles (`examples/sdl2_tiles.png` -> `tiles_data.h`)

```sh
python3 ports/gba/convert_tiles.py                      # examples/sdl2_tiles.png -> ports/gba/tiles_data.h
```

Reads whatever is at `examples/sdl2_tiles.png` as-is (it does not
regenerate it — that sheet is now hand-edited, not built by
`make_sdl2_tiles.fs`/`.py`) and writes plain RGB15 pixels, no palette: mode
3 (see `gba-mode3`) is a direct 16-bit bitmap, so each of the 8 tiles
becomes 64 `unsigned short`s, one tile after another, row-major within a
tile. A pixel whose source alpha is below 128 becomes the sentinel `$ffff`
("transparent, skip it" — `rgb15`'s own encoding never sets that bit, its
max value is `$7fff`). `platform.fs`'s `draw-tile ( tile x y -- )` blits one
8x8 tile at a screen position, pixel by pixel through `gba-dot`, skipping
transparent ones; `tile-pixel ( tile px py -- color )` reads one pixel
without drawing it. Both directions (an opaque pixel landing correctly, a
transparent one leaving what was already there alone) were checked in the
simulator by reading VRAM back after drawing, not just by eye.

This is a software blit (mode 3, like the rest of this first pass), not the
GBA's tile/sprite hardware (mode 0 BG tiles + OAM) — correct and simple,
not what real GBA homebrew would do for a tile-heavy game; see "What's left
for `sdl2_roguelike.fs`" below.

## `examples/sdl2_roguelike.fs`, ported: `roguelike.fs`

`ports/gba/roguelike.fs` is a full port (minus music), built from
`ports/gba/gba_roguelike_boot.fs` exactly like the tile demo, just needing
`roguelike.fs` instead of `autoboot.fs`:

```sh
python3 ../../tools/importation.py -i gba_roguelike_boot.fs -o /tmp/gba/gen/gba_boot.h \
  -I ../.. -I /tmp/gba --name boot --header cpp
g++ -DLUGFORTH_SIM=1 -O2 -I ../.. -I /tmp/gba -std=c++11 main.c -o /tmp/gba/sim_rogue   # simulator
# or, for a real ROM: same crt0.s/gba.ld/objcopy/fix_header.py steps as above,
# writing to roguelike.gba instead of gba.gba
```

Game logic (room/corridor generation, turn-based combat, monster AI, line of
sight, potions/stairs) is copied unchanged from the SDL2 version. Everything
that touched SDL2 was rewritten: a scrolling 30x18-tile viewport (the map is
still 40x24, too big for the 240x160 screen) that follows the player instead
of showing the whole map; a real per-channel `tint` (in `platform.fs`) for
remembered-but-not-visible tiles, replacing `image-tint`; d-pad + A/B/START
instead of keyboard/mouse (no click-to-walk, no Esc, a cartridge has
nowhere to quit to); the translucent "hurt" screen flash dropped (no alpha
blending set up yet), the player tile's own red flash tint kept as the
primary feedback. No music: the ABC/soundfont player is SDL2/host-audio
specific and stays out of scope, same as this file's original note, a
from-scratch GBA sound-hardware backend would be its own separate piece of
work. At some point, real mode 0 tile/sprite hardware instead of today's
mode-3 software blits would be the next real improvement (faster, smoother
scrolling, actual DMA-driven blits), not needed for this to be playable
(see "Performance" below) but the right long-term direction for a
tile-heavy game.

### Performance: a native blit, not a per-pixel Forth loop

The first version drew every pixel through plain Forth (`gba-dot`, `tint`,
`tile-pixel`, one interpreted word dispatch per step), same as the tile
demo. That is fine for the demo's 8 static tiles, but the roguelike redraws
up to ~500 tiles a turn (the whole 30x18 viewport), 30000+ pixels, each one
several interpreted calls deep — the user confirmed this on real `mgba`:
about **20 seconds per redraw, unplayable**, and it redrew on every single
game-loop iteration, not just on an actual move.

Fix: `gba_blit_tile`/`gba_fill_rect` in `main.c`, two new opcodes
(`GBA-BLIT-TILE`, `GBA-FILL-RECT`, `GBA_FAST_BLIT_SUPPORT` in
`builtins.h`) that do the exact same transparency/tint/bounds math as
before, just as compiled C instead of interpreted Forth — one call per
tile or rectangle instead of 64 (or 38400, for a full-screen clear)
individual word dispatches. `platform.fs`'s `draw-tile-tinted` and
`gba-box` keep their original Forth-level names and stack effects, just
call the new opcode internally, so `roguelike.fs` itself needed no changes
at all. The old per-pixel Forth path (`draw-tile-pixel-tinted`, the
`tint`-in-a-loop version of `gba-box`) is gone from `platform.fs`; `tint`
itself is kept, unused, as the reference for the C version's channel math
(same `$1f and`/`5 rshift`/`10 rshift` layout, see the comment on `tint`).
This is the general lesson, not just a GBA-specific one: on hardware this
modest, a hot per-pixel loop belongs in C, and everything above it
(picking *what* to draw, camera, game state) can stay in Forth without
that cost — GBA/NDS homebrew reaches for the tile/sprite hardware and DMA
for the same reason; this is a smaller step in that same direction, not a
substitute for it.

**Second round, after the user tried the native-blit build: "un peu mieux,
mais ça clignote tout le temps... même immobile"** (better, but flickers
constantly, even standing still). Cause: `run`'s loop called `draw-all`,
a full clear-then-redraw of the whole viewport, on *every* game-loop
iteration, unconditionally, whether or not a turn had actually happened.
Mode 3 is a single framebuffer (no page flip set up here), so the CPU
writes directly over what is currently on screen; redrawing continuously,
forever, even completely idle, is visible as constant flicker (a black
clear, then the redraw catching up, over and over). Fix: a `dirty` flag
(`roguelike.fs`), set only when something actually changed (`act` after a
real turn, `new-game` on restart, or the hurt-flash still counting down)
and cleared right after the one `draw-all` it triggers; `step` only
redraws when `dirty` is set. Idle frames now do nothing but poll input and
`pause`.

**Boot itself was also reported slow** ("le boot est assez lent"). Likely
cause, not yet independently measured: there is no saved/precompiled
image (`setup-saving-base` in `platform.fs` is still a no-op stub, "no
save-image support yet"), so every power-on interprets the *entire* boot
chain from source text, phase1/phase2/tasks/streams/platform/roguelike/
fini, several hundred word definitions, same as the tile demo, just with
far more source now that a full game is in it. `ports/pico-ice/` does the exact
same from-source boot, but its RP2040 runs at 125-133MHz; the GBA's
ARM7TDMI runs at 16.78MHz, roughly 8-10x slower clock, no cache. A real
fix (embedding a precompiled dictionary/heap snapshot in the ROM instead
of recompiling from source every time) is a substantially bigger piece of
work than anything above, touches `setup-saving-base`/`fini.fs`'s
`transfer` machinery, not just `roguelike.fs`/`platform.fs`, and hasn't
been started.

Neither of these two was re-verified visually in this sandbox: the same
mgba screenshot pipeline that failed for the white-screen investigation's
follow-up (see above) was tried again here and is still not producing a
readable screen capture this session, for this ROM or for mgba in
general, so this really does look like a session-local sandbox issue
rather than anything ROM-specific. Verified instead the way the tile
converter was originally verified: reading the simulator's VRAM back
directly (pixel counts, camera-relative coordinates) rather than by eye,
plus the same 2000-move soak test, both still clean after these two
fixes.

**Testing note: no real key input exists in the `LUGFORTH_SIM` build**
(`gba_reg_keyinput` just points at a static variable that always reads
"nothing pressed", see `main.c`), so this was never played by hand in the
sandbox. It was instead soak-tested with a throwaway boot file that called
the game's own words directly: `act` with 2000 pseudo-random directions,
then forcing `dead`, `new-game` (restart), and `next-level` several times in
a row, checking each step still rendered (`draw-all`) without crashing.
This caught two real bugs, both worth remembering (also in `SKILLS.md`):

* **A boolean OR/AND chain of more than two terms needs one combinator per
  pair, not one per operand.** `a b c or d e f or or` combines four flags
  two at a time (`a b or`, `d e or`, then the outer `or` of those); writing
  it as `a b or c d e or` (one combinator short) leaves a stray flag on the
  stack forever after. Found this twice, independently, in `draw-monster`'s
  and `draw-cell`'s viewport bounds checks (`x0<=x<x1 and y0<=y<y1`, each
  itself an `and` of two `or`s, four terms, needs three combinators total).
  `draw-cell`'s copy ran on every visible tile, every frame (up to ~500
  times), so the data stack overflowed within a few dozen frames, a bus
  error, not a clean message, by the time it actually broke.
* **A `{ local }` declared *inside* a `do ... loop` body is unsafe.**
  `common/locals.fs` implements locals with `>r` on the return stack
  (`local@`/`local!` read/write via `rp@`), unwound only once, at the
  enclosing word's own `;` (`scope-clear`), not once per loop iteration.
  Declaring one directly in a loop body pushes a fresh value every time the
  loop comes back around without ever popping the old one, silently
  corrupting the do-loop's own index/limit that live on the same stack.
  `examples/sdl2_roguelike.fs`'s original `draw-bar` avoids this by leaning
  on stateful `shade` instead of a local inside its hearts loop, worth
  restating explicitly, since it is easy to miss why that pattern was used
  there. Fixed here by either factoring the loop body into its own word
  (`draw-monster`, called once per iteration, its own scope, cleaned up
  correctly), or just not naming the value (`draw-bar`'s hearts loop).

## Design notes

* **Far less C glue than pico-ice.** GBA video modes, VRAM, palette, input
  are all fixed memory-mapped addresses, which the kernel can already reach
  with plain `@ ! c@ c!`; `builtins.h` only needs opcodes for the handful of
  things that genuinely require a C call (an mGBA debug-console print,
  MS-TICKS, a bump allocator for `allocate`/`free`/`resize`). Everything
  else (`REG_DISPCNT`, `VRAM`, `gba-dot`, `rgb15`, ...) is plain Forth in
  `platform.fs`.
* **A 16-bit register, a not-16-bit cell.** The kernel's cell size follows
  the host (8 bytes under the POSIX simulator, 4 on real ARM), never 16
  bits, so GBA registers and VRAM words are written as two explicit bytes
  (`h!`/`h@` in `platform.fs`), not through `!`/`@`.
* **A bump allocator, not malloc**, for `allocate`/`free`/`resize` on real
  hardware (`gba_malloc` & co. in `main.c`): avoids wiring up newlib's
  heap/`_sbrk` for a first prototype. `FREE` is a no-op and `RESIZE` always
  grows a fresh block — fine for a prototype that does not call any of the
  three yet, wrong for one that frees a lot. `LUGFORTH_SIM` still uses real
  `malloc`/`free`/`realloc`, already proven by every other simulator build
  in this repo.
* **`--specs=nosys.specs` and a `PROVIDE(end = .)`** in `gba.ld` satisfy
  newlib's default reentrant syscall stubs (`_sbrk`, `_write`, ...), pulled
  in transitively (nothing here calls them) by `<cmath>` (`common/floats.h`
  needs `atan2f`/`sinf`/... for the floating-point word set) — they link as
  harmless always-fail stubs, never actually called.

## Real bugs found while getting the simulator to boot (worth reading if
this ever needs debugging again; also in the repo's `SKILLS.md`)

All four were *silent*: no error text, just `SIGSEGV`. That is expected
once you know why: printing an error itself needs `type`, and in three of
the four cases `type` either was not bound yet or the very word needed to
report the problem was itself the thing missing.

1. **A local named the same as a builtin word shadows it.** Unrelated to
   this port specifically (found earlier, in `examples/sdl2_pianoroll.fs`),
   but worth restating: `{ ... to ... }` is exactly this kind of trap.

2. **`also internals definitions ... previous` vs
   `forth definitions internals ... forth`.** `default-type`/`key`/`key?`
   must be compiled into the **forth** vocabulary while *searching* through
   `internals` (for `raw-yield`, `getchar_timeout_us`, ...), not compiled
   into `internals` itself: `previous` drops `internals` from the search
   order at the end, and if the words were compiled there, `' default-type`
   later finds nothing — silently, since `type` is the very word not yet
   bound. `ports/pico-ice/platform.fs` already gets this right
   (`forth definitions internals`, a bare vocabulary word is a "search here
   additionally" context change, not a compile-target change); this file
   originally got it backwards.

3. **A nested `( paren )` inside a `( comment )` closes the comment early**
   and leaks the rest as code (the same bug chased at length in
   `examples/sdl2_pianoroll.fs`, and it recurred here independently, twice).
   `\` line comments are immune — but only once `\` itself has been defined
   by `phase1.fs` (see the next point), so during early boot even that is
   not fully safe.

4. **A `\` comment placed before the first `needs` line in a boot-chain
   *entry* file is not safe**, because `\` (and `(`) are themselves defined
   by `phase1.fs`, and are not yet available before it has been `needs`'d.
   Every other file in the boot chain can open with a `\ Copyright ...`
   header safely, because the header-generation tool
   (`tools/importation.py`) recognizes and strips any comment block
   containing the word "Copyright" — but an *additional*, non-Copyright
   explanatory comment is not stripped, and if it sits before the first
   `needs` line of the file that starts the whole chain, it ends up in the
   final boot text before comment support exists at all. Moving it after
   `needs ../../common/phase1.fs` fixes it; `gba_boot.fs` does this and
   explains why in its own comment, now safely placed after that line.

5. **Every platform must define `ok` itself; there is no default.**
   `common/fini.fs`'s very last act is `execute` on whatever xt
   `autoboot.fs` left on the stack, immediately followed by evaluating the
   bare word `ok` — and nothing in `common/*.fs` ever defines a plain `ok`;
   `ports/posix/posix.fs` and `ports/pico-ice/platform.fs` both define their own
   (`internals : ok ." <banner>" raw-ok ; forth`, `raw-ok` itself ending in
   `quit`, the real interactive loop). This file's `platform.fs` originally
   forgot this entirely, so `' ok` in `autoboot.fs` found nothing.
