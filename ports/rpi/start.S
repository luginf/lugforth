// Copyright 2026 Luginfo10
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

// AArch64 entry point, the same for every board: QEMU's `virt` enters at
// EL1, QEMU's `raspi3b` and the Raspberry Pi firmware at EL2, some boot
// loaders at EL3. Drop to EL1, park the other cores, enable the FPU (the
// kernel uses floats: the first FP instruction traps otherwise), set the
// stack, zero .tbss/.bss and the vector table, then call rpi_start() in
// main.c (MMU, TLS, UART, main).
//
// The image is loaded whole into RAM (ELF or flat kernel8.img), so .data
// is already in place: no copy from a ROM image as on the GBA.

	.section .text.boot, "ax"
	.global _start
_start:
	mrs	x1, mpidr_el1
	and	x1, x1, #0xff
	cbnz	x1, park

	mrs	x1, CurrentEL
	lsr	x1, x1, #2
	cmp	x1, #3
	b.ne	1f
	// EL3 -> EL2: non-secure, EL2 is AArch64, HVC on, SMC off.
	mov	x2, #0x5b1
	msr	scr_el3, x2
	mov	x2, #0x3c9              // EL2h, DAIF masked
	msr	spsr_el3, x2
	adr	x2, 1f
	msr	elr_el3, x2
	eret
1:	mrs	x1, CurrentEL
	lsr	x1, x1, #2
	cmp	x1, #2
	b.ne	el1
	// EL2 -> EL1: EL1 is AArch64, timers and FP not trapped.
	mov	x2, #(1 << 31)
	msr	hcr_el2, x2
	mrs	x2, cnthctl_el2
	orr	x2, x2, #3
	msr	cnthctl_el2, x2
	msr	cntvoff_el2, xzr
	mov	x2, #0x33ff
	msr	cptr_el2, x2
	msr	hstr_el2, xzr
	ldr	x2, =0x30d00800         // SCTLR_EL1 RES1 bits, MMU and caches off
	msr	sctlr_el1, x2
	mov	x2, #0x3c5              // EL1h, DAIF masked
	msr	spsr_el2, x2
	adr	x2, el1
	msr	elr_el2, x2
	eret

el1:
	mov	x2, #(3 << 20)          // CPACR_EL1.FPEN: FP/SIMD at EL0 and EL1
	msr	cpacr_el1, x2
	isb
	ldr	x2, =__stack
	mov	sp, x2
	ldr	x2, =rpi_vectors
	msr	vbar_el1, x2

	// Zero from .tbss to the end of .bss (both 16-byte aligned in rpi.ld):
	// aligned stores only, the MMU is still off (all memory is Device).
	ldr	x2, =__zero_start
	ldr	x3, =__bss_end
2:	cmp	x2, x3
	b.hs	3f
	stp	xzr, xzr, [x2], #16
	b	2b
3:
	// The other cores wait in the boot stub (the Pi firmware's armstub,
	// or QEMU's own for raspi3b) on the spin table at 0xd8 + 8 * core,
	// spinning on WFE: send them to park, where WFI really sleeps (no
	// interrupt is routed to them). Under QEMU this frees 3 host cores.
#ifndef RPI_BOARD_VIRT
	adr	x2, park
	mov	x3, #0xe0
	str	x2, [x3], #8
	str	x2, [x3], #8
	str	x2, [x3]
	dsb	sy
	sev
#endif
	bl	rpi_start
park:	wfi
	b	park

// Exception vectors: 16 entries of 0x80 bytes, the table 2K aligned. Every
// entry saves x0-x30 and calls rpi_exception(kind, esr, elr, far), which
// prints a line and returns the address to resume at (rpi_fault_resume,
// which turns the fault into a Forth THROW, as signals do in
// ports/posix/faults.h) or does not return.
	.macro vector kind
	.balign 0x80
	sub	sp, sp, #256
	stp	x0, x1, [sp, #0]
	mov	x0, #\kind
	b	exception_common
	.endm

	.balign 0x800
	.global rpi_vectors
rpi_vectors:
	vector 0
	vector 1
	vector 2
	vector 3
	vector 4
	vector 5
	vector 6
	vector 7
	vector 8
	vector 9
	vector 10
	vector 11
	vector 12
	vector 13
	vector 14
	vector 15

exception_common:
	stp	x2, x3, [sp, #16]
	stp	x4, x5, [sp, #32]
	stp	x6, x7, [sp, #48]
	stp	x8, x9, [sp, #64]
	stp	x10, x11, [sp, #80]
	stp	x12, x13, [sp, #96]
	stp	x14, x15, [sp, #112]
	stp	x16, x17, [sp, #128]
	stp	x18, x19, [sp, #144]
	stp	x20, x21, [sp, #160]
	stp	x22, x23, [sp, #176]
	stp	x24, x25, [sp, #192]
	stp	x26, x27, [sp, #208]
	stp	x28, x29, [sp, #224]
	str	x30, [sp, #240]
	mrs	x1, esr_el1
	mrs	x2, elr_el1
	mrs	x3, far_el1
	bl	rpi_exception
	msr	elr_el1, x0
	add	sp, sp, #256
	eret
