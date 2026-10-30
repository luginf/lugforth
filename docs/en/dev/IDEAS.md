# Ideas, open questions and other Forths

Decisions pending and things not done. Part of
[README.md](../../../README.md). The to-do list of the editors is in
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
- turtle (examples/graphics/gfx_turtle.fs): `back`, `turtle@` /
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



## Ports to retro platforms (analysis of 2026-10-29)

Made by reading the repository, nothing was compiled. What matters: a
32-bit C compiler for the target, enough RAM, the byte order. The kernel
is C/C++ with pointer-sized cells (32 bits at least); the GBA port fits
in 288 KB of RAM on a 16.8 MHz ARM7; `lugforth-mini` is 150 KB on a PC.
`common/*.h` does not deal with byte order: everything was tested
little-endian.

Realistic, from most to least useful:

1. **Atari ST** (68000, 8 MHz, 512 KB to 4 MB): the best candidate.
   Compiler `m68k-atari-mint-gcc`; files through GEMDOS, console and
   keyboard through BIOS/XBIOS, screen through video memory. Risks: the
   68000 is big-endian (byte access to a cell, `w@`, `l@`, strings, the
   dictionary) and refuses word accesses at odd addresses (`@` and `,`
   if they assume it is allowed). Hatari is installed on this PC; the
   cross compiler is missing.
2. **Amiga** (68000, 512 KB or more): same risks, `m68k-amigaos-gcc`
   toolchain; FS-UAE is installed here.
3. **DOS 386** (DJGPP, 32-bit protected mode): the easiest, 32-bit
   cells, little-endian, VGA mode 13h.
4. **Nintendo DS** (ARM9, 4 MB): very close to the GBA (devkitARM).
5. **PlayStation 1, N64, Dreamcast, PSP** (MIPS, SH4, GCC): possible but
   heavy in drivers for little interest.

Not realistic:

- **C64, Atari 8-bit, ZX Spectrum, Amstrad** (6502, Z80, 64 KB): 32-bit
  cells are too slow, a 150 KB kernel is too big for 64 KB, the C
  toolchains are limited (cc65 is a partial C89, llvm-mos is more
  complete). It would take another Forth with 16-bit cells written in
  assembly, like DurexForth (quoted above): a separate project.
- **Sega Mega Drive**: decent 68000 but 64 KB of RAM, only with a
  reduced kernel.
- **Atari Falcon, TT**: a bigger Atari ST.

Another way for very old machines: the Uxn/Varvara virtual machine that
Lugforth already runs (`examples/system/varvara`); several old platforms
have a Uxn implementation (which ones: to be checked).

For an Atari ST: install the 68k cross compiler; build the kernel as a
POSIX simulator with a fake reversed byte order, or under `qemu-m68k` /
`hatari`, to find the non-portable accesses; write `crt0` and the
input/output and screen words on the model of the GBA port.

## Unifying the graphics words, away from the `sdl2` name (2026-10-29)

Decision of principle, renaming later. The short words (`dot`, `box`,
`line`, `flip`, `pressed?`...) already run where there is no SDL: the
bare-metal framebuffer (`ports/baremetal/sdl2.fs`, `screen.h`), the web
canvas (`ports/web/sdl2.fs`), next to the real SDL2 loaded by `dlopen`
on Linux, Windows and macOS. The name `sdl2` and the files `sdl2_*.fs`
are misleading. Only `sdl2-image`, `sdl2-ttf` and `sdl2-mixer` are
really tied to SDL2.

Done on 2026-10-30 for the examples: the programs `examples/*/sdl2_*.fs`
(games, graphics, music, the FM synth, the tile sheets, the web pages)
are now `gfx_*.fs`, with every reference changed; no alias was kept (the
URLs of the web demo pages changed, and the `.ini` file of the desktop
that names an old path must be edited). The `sdl2` vocabulary and the
library files (`ports/posix/sdl2*.fs`, `ports/web/sdl2.fs`) keep their
names. Decided:

- Words directly inspired by SDL2 keep an SDL2-compatible API (names and
  arguments), for people who port C code.
- The rest moves to the `gfx_` prefix: example files `gfx_*.fs` instead
  of `sdl2_*.fs`, and a neutral vocabulary (`gfx`; `gfx-on` already
  exists for `graphic`). `sdl2` stays as an alias that loads the new
  layer, so no example or doc breaks; old file names are kept as aliases
  for a while.
- Sound goes under the same umbrella: `gfx_` everywhere (decision of
  2026-10-29, the `snd` vocabulary idea is dropped).
- `graphic` (drawing at the prompt) is already half of this neutral
  layer; reconcile them.
- The docs (`SDL2.md`, `SDL2-words.md`, `SDL2-web.md`) become graphics
  docs with a section for the SDL2 bindings.

Open, for later: where the screen size is defined. Programs assume a
size (Space Invaders 224x240, magnoquill 800x800), but the GBA is
240x160, the EFI console 640x480 and the web canvas is chosen by the
page. The contract should say how a program asks for the size
(`screen-w`, `screen-h`?) and whether it must adapt (magnoquill already
has a small-screen mode) or the layer scales.
