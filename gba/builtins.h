// Copyright 2026 luginf
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

// The GBA needs far less C glue than pico-ice: almost everything on the
// console (video modes, VRAM, palette, OAM, input, DMA...) is a fixed
// memory-mapped address, so once the kernel can peek/poke memory (it
// already can: @ ! c@ c! ...) most of "the platform" is plain Forth
// constants in platform.fs, not opcodes here. What's left is: the pieces
// C really has to do (an mGBA debug-console print, for development without
// any display) and the required memory/system opcodes every platform needs.

#include <stdio.h>

#ifndef UEFORTH_SIM
# include <string.h>
#endif

// Real libc malloc/free/realloc under UEFORTH_SIM (an ordinary POSIX
// process); a small bump allocator on real hardware (gba_malloc & co in
// main.c), since that needs no newlib heap/_sbrk wiring. This is a
// separate, small heap from the main Forth dictionary heap the kernel
// itself manages (HEAP_SIZE in main.c); ALLOCATE/FREE/RESIZE (see
// allocation.fs) are the only things that use it.
#ifdef UEFORTH_SIM
# define REQUIRED_MEMORY_SUPPORT \
  YV(internals, MALLOC, SET malloc(n0)) \
  YV(internals, SYSFREE, free(a0); DROP) \
  YV(internals, REALLOC, SET realloc(a1, n0); NIP)
#else
# define REQUIRED_MEMORY_SUPPORT \
  XV(internals, "MALLOC", MALLOC, SET gba_malloc(n0)) \
  XV(internals, "SYSFREE", SYSFREE, gba_free(a0); DROP) \
  XV(internals, "REALLOC", REALLOC, SET gba_realloc(a1, n0); NIP)
#endif

// Real hardware addresses on real hardware, ordinary heap memory under
// UEFORTH_SIM (see gba_reg_*/gba_vram in main.c) -- platform.fs builds its
// register/VRAM constants from these instead of hardcoding literals, so the
// same Forth graphics code can be developed against the simulator.
#define GBA_ADDRESS_SUPPORT \
  YV(internals, gba_reg_dispcnt, PUSH gba_reg_dispcnt()) \
  YV(internals, gba_reg_vcount, PUSH gba_reg_vcount()) \
  YV(internals, gba_reg_keyinput, PUSH gba_reg_keyinput()) \
  YV(internals, gba_vram, PUSH gba_vram()) \
  YV(internals, gba_tiles, PUSH gba_tiles())

// A native tile blit and rect fill (gba_blit_tile/gba_fill_rect in main.c):
// the original all-Forth per-pixel loop in platform.fs worked but was
// unplayably slow on real mgba (confirmed by the user: ~20s per full
// redraw), since every single pixel cost several interpreted word
// dispatches. These two opcodes move just the hot inner loop into compiled
// C; platform.fs's draw-tile-tinted/gba-fill-rect wrap them with the same
// Forth-level names/signatures as before, so nothing above platform.fs had
// to change.
#define GBA_FAST_BLIT_SUPPORT \
  XV(internals, "GBA-BLIT-TILE", GBA_BLIT_TILE, \
     gba_blit_tile((int) n3, (int) n2, (int) n1, (unsigned) n0); DROPn(4)) \
  XV(internals, "GBA-FILL-RECT", GBA_FILL_RECT, \
     gba_fill_rect((int) n4, (int) n3, (int) n2, (int) n1, (unsigned) n0); DROPn(5))

// No filesystem on a cartridge, so unlike pico-ice/esp32 this platform does
// not define REQUIRED_FILES_SUPPORT at all: nothing in the boot chain
// (phase1/phase2/fini) calls OPEN-FILE et al, only the test suite does,
// which this platform does not run.

#ifndef UEFORTH_SIM
// REG_VCOUNT (the current scanline, 0..227, wrapping ~59.7 times/second) is
// the only free-running counter available without setting up a hardware
// timer register; good enough for a first MS-TICKS. RAW-YIELD waits for the
// next VBlank, which doubles as "the game loop's frame boundary" the way
// `pause`/cooperative tasks expect a yield point to be. There is no way to
// "exit" a cartridge, so RAW-TERMINATE just halts.
# define REQUIRED_SYSTEM_SUPPORT \
  X("MS-TICKS", MS_TICKS, PUSH gba_ms_ticks()) \
  XV(internals, "RAW-YIELD", RAW_YIELD, gba_vblank_wait()) \
  XV(internals, "RAW-TERMINATE", RAW_TERMINATE, DROP; gba_halt_forever()) \
  YV(internals, getchar_timeout_us, DROP; PUSH -1) \
  XV(internals, "RAW-TYPE", RAW_TYPE, gba_mgba_print(b1, n0); DROPn(2))
#else
# define REQUIRED_SYSTEM_SUPPORT \
  X("MS-TICKS", MS_TICKS, PUSH (time(0) * 1000)) \
  XV(internals, "RAW-YIELD", RAW_YIELD, sleep(0)) \
  XV(internals, "RAW-TERMINATE", RAW_TERMINATE, exit(n0); DROP) \
  YV(internals, getchar_timeout_us, DROP; PUSH fgetc(stdin)) \
  XV(internals, "RAW-TYPE", RAW_TYPE, fwrite(b1, 1, n0, stdout); fflush(stdout); DROPn(2))
#endif

#define PLATFORM_OPCODE_LIST \
  REQUIRED_MEMORY_SUPPORT \
  REQUIRED_SYSTEM_SUPPORT \
  GBA_ADDRESS_SUPPORT \
  GBA_FAST_BLIT_SUPPORT \
  CALLING_OPCODE_LIST \
  FLOATING_POINT_LIST

// Only the two flags actually used by X()/XV()/Y()/YV() above; the Forth
// vocabulary `gba` (in platform.fs) is plain Forth and needs no C-side
// registration since no opcode is tagged with it.
#define VOCABULARY_LIST V(forth) V(internals)
