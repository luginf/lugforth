@ Copyright 2026 luginf
@
@ Licensed under the Apache License, Version 2.0 (the "License");
@ you may not use this file except in compliance with the License.
@ You may obtain a copy of the License at
@
@     http://www.apache.org/licenses/LICENSE-2.0
@
@ Unless required by applicable law or agreed to in writing, software
@ distributed under the License is distributed on an "AS IS" BASIS,
@ WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
@ See the License for the specific language governing permissions and
@ limitations under the License.

@ Minimal GBA startup: the 192-byte cartridge header the BIOS checks before
@ running anything, then just enough setup (IWRAM stack, .data copy, .bss
@ clear) to call the plain C main() in main.c. No libgba/devkitARM: written
@ by hand against the GBA memory map (GBATEK), the same way builtins.h
@ avoids a vendor SDK.
@
@ The .data copy is not optional, and was missing for a while: newlib's own
@ internal state (_impure_ptr and what it points at -- errno, malloc's
@ bookkeeping, ...) lives in .data, and without copying it from its ROM
@ image to its EWRAM runtime address, _impure_ptr is whatever garbage was
@ already in EWRAM, not a valid pointer -- the first newlib call that
@ dereferences it (almost anything touching errno or malloc) writes through
@ that garbage pointer instead. This was chased down to a uniform white
@ GBA screen (see ../README.md's "Tried under mgba"), reproduced with
@ forth_init() alone, before any Forth text ever runs: forth_init() itself
@ doesn't call anything newlib-shaped, but something pulled in alongside it
@ (a static local array of computed &&label addresses, resolved by the
@ linker into ROM directly, no copy needed for that part) apparently does.
@
@ The Nintendo logo bytes below are still zeroed, not the real logo a
@ licensed cartridge or a strict boot check needs; a real devkitARM/libgba
@ install has the real bytes (crt0.s / gba_crt0.s) to copy in if this needs
@ to boot on real hardware or in an emulator stricter than mgba's default.

	.section .header, "ax"
	.arm
	.global _start
_start:
	b	_boot                   @ ROM entry point: skip over the header
	.fill 156, 1, 0                @ Nintendo logo (real hardware needs the real bytes)
	.ascii "UEFORTHGBA  "          @ game title, 12 bytes
	.ascii "AUEE"                  @ game code, 4 bytes
	.byte 0, 0                     @ maker code
	.byte 0x96                     @ fixed value
	.byte 0                        @ main unit code
	.byte 0                        @ device type
	.fill 7, 1, 0                  @ reserved
	.byte 0                        @ software version
	.byte 0                        @ complement check: patched by fix_header.py after linking
	.fill 2, 1, 0                  @ reserved

	.section .text, "ax"
	.arm
_boot:
	@ IWRAM stack (top of the 32K IWRAM region minus a little headroom).
	ldr	sp, =0x03007f00

	@ Copy .data from its ROM image to its EWRAM runtime address (see the
	@ comment above: this is the one that matters, newlib's _impure_ptr
	@ and friends live here).
	ldr	r0, =__data_load
	ldr	r1, =__data_start
	ldr	r2, =__data_end
1:	cmp	r1, r2
	bge	2f
	ldr	r3, [r0], #4
	str	r3, [r1], #4
	b	1b
2:
	@ Clear .bss (sim_vram-equivalent statics live in EWRAM/main.c's own
	@ heap array, not here, but plain C globals still need zeroing).
	ldr	r0, =__bss_start
	ldr	r1, =__bss_end
	mov	r2, #0
3:	cmp	r0, r1
	bge	4f
	str	r2, [r0], #4
	b	3b
4:
	bl	main
5:	b	5b                      @ main() never returns (raw_terminate halts too)
