# lugforth for the Game Boy Advance (prototype)

Same kernel (`common/*.h`) and boot chain shape as `ports/pico-ice/`, built with
`arm-none-eabi-gcc` (288K RAM: 256K EWRAM + 32K IWRAM). Hand-written `crt0.s` and
`gba.ld`, no libgba or devkitARM. Boot files: `gba_boot.fs` (tile demo),
`gba_roguelike_boot.fs` (game), `gba_magnoquill_boot.fs` (the point cloud of
`examples/graphics/sdl2_magnoquill.fs`).

## Simulator

`LUGFORTH_SIM` builds an ordinary POSIX program that boots to the `ok` prompt
and runs the demo gradient:

```sh
python3 ../../tools/importation.py -i gba_boot.fs -o /tmp/gba/gen/gba_boot.h \
  -I ../.. -I /tmp/gba --name boot --header cpp
g++ -DLUGFORTH_SIM=1 -O2 -I ../.. -I /tmp/gba -std=c++11 main.c -o /tmp/gba/sim
/tmp/gba/sim
```

There is no key input in the simulator, so the games cannot be played there.

## ROM

Tested under `mgba` 0.10.2 and on a real machine.

```sh
arm-none-eabi-as -mcpu=arm7tdmi crt0.s -o /tmp/gba/crt0.o
arm-none-eabi-g++ -c -mcpu=arm7tdmi -marm -O2 -fno-exceptions -std=c++11 \
  -I ../.. -I /tmp/gba main.c -o /tmp/gba/main.o
arm-none-eabi-g++ -mcpu=arm7tdmi -marm -nostartfiles --specs=nosys.specs \
  -T gba.ld /tmp/gba/crt0.o /tmp/gba/main.o -o /tmp/gba/gba.elf
arm-none-eabi-objcopy -O binary /tmp/gba/gba.elf gba.gba
python3 fix_header.py gba.gba
mgba gba.gba
```

For the roguelike, use `gba_roguelike_boot.fs` and write `roguelike.gba`; for
the point cloud, `gba_magnoquill_boot.fs` and `magnoquill.gba`. (The `.gba`
files are not in git.)

## Magnoquill

`magnoquill.fs`: the demo `examples/graphics/sdl2_magnoquill.fs` in mode 3,
160x160 centered in the 240x160 screen. UP/DOWN speed, LEFT/RIGHT number of
points (100 to 1500, 400 at start), A rainbow or white, B pause, START time
back to 0. The first version, with the formulas in Forth floats, needed more
than ten seconds per picture for 250 points (no FPU: software sin and cos).
Now the formulas run in C in 16.16 fixed point with a 1024-entry sine table
(`gba_magno` in `main.c`, opcode `GBA-MAGNO`, word `gba-magno`): about 50 ms
per picture for 600 points under mgba, measured with a hardware timer. What
made the difference, in order: no 64-bit division (a library call of about a
thousand cycles: replaced by multiplications by `2^32/n`; 24000 cycles a
point went down to 7400), then the code and the sine table in IWRAM
(`.iwram_code`, `gba.ld`, copied by `crt0.s`: 1400 cycles a point), and the
cartridge wait states (`REG_WAITCNT` = `$4317` in `crt0.s`). `.text` and
`.data` now end on a multiple of 8 in the ROM: `crt0.s` copies by words, and
an unaligned `ldr` would read garbage. Mode 3 has no additive blending and a
single buffer: each point erases the pixel it drew in the previous frame
instead of the screen being cleared. Checked under mgba 0.10.2 (screenshots
under Xvfb); not on a real console.

## Tiles

`python3 ports/gba/convert_tiles.py` turns `examples/games/sdl2_tiles.png` into
`tiles_data.h`: plain RGB15 pixels (mode 3 has no palette), 64 per 8x8 tile;
`$ffff` marks a transparent pixel. `platform.fs` has `draw-tile ( tile x y -- )`
and `tile-pixel ( tile px py -- color )`.

## Roguelike

`roguelike.fs` is `examples/games/sdl2_roguelike.fs` ported, without music: the same
game logic, a scrolling 30x18 viewport over the 40x24 map, d-pad and A/B/START
for input. Drawing goes through two C opcodes (`GBA-BLIT-TILE`,
`GBA-FILL-RECT`), because a per-pixel Forth loop took about 20 seconds per
redraw. It only redraws when something changed (`dirty` flag), otherwise it
flickers: mode 3 is a single framebuffer.

## Known limits

* Boot is slow: there is no saved image, so the whole boot chain is interpreted
  from source at each power-on (16.78 MHz ARM7TDMI, no cache).
* Mode 3 software blits, not the tile and sprite hardware.
* No sound.
* `FREE` is a no-op and `RESIZE` always allocates a new block (bump allocator).

## Design notes

* Registers and VRAM are memory-mapped, so `builtins.h` only has opcodes for
  what needs C: a debug print, `MS-TICKS`, the allocator and the blits.
* The cell is 4 bytes on the ARM (8 in the simulator), so 16-bit registers are
  written with `h!`/`h@`.
* The platform file must define `ok` itself: `common/fini.fs` runs it last.

## Pitfalls met while porting (more in `docs/en/dev/PITFALLS.md`)

* `crt0.s` must copy `.data` to RAM as well as clear `.bss`. Missing, newlib's
  state was garbage and the screen stayed white.
* In an emulator without a GPU the demo can take 20-30 seconds to draw; do not
  conclude too early that the screen is blank.
* A boolean chain needs one `or`/`and` per pair of terms, or a flag stays on
  the stack.
* A `{ local }` declared inside a `do ... loop` body corrupts the loop: locals
  use the return stack and are only cleaned at `;`.
* A nested `( paren )` inside a `( comment )` ends the comment early.
* A non-Copyright comment before the first `needs` of a boot entry file ends up
  in the boot text before comments exist.
