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

// x86-64 pieces in assembly: setjmp and longjmp (the System V ABI gcc
// compiles in: rbx rbp r12-r15 rsp and the return address), and the entry
// points of the processor exceptions that main.c puts in a copy of the
// firmware's IDT. An entry pushes its vector (and a 0 where the CPU pushes
// no error code), calls efi_exception(vector, error, rip, cr2), which
// returns where to resume (efi_fault_resume, which throws, as signals do
// in ports/posix/faults.h), puts that in the frame and returns from the
// interrupt there.

	.text
	.globl setjmp, _setjmp, longjmp, _longjmp
setjmp:
_setjmp:
	movq	%rbx, 0(%rdi)
	movq	%rbp, 8(%rdi)
	movq	%r12, 16(%rdi)
	movq	%r13, 24(%rdi)
	movq	%r14, 32(%rdi)
	movq	%r15, 40(%rdi)
	leaq	8(%rsp), %rdx
	movq	%rdx, 48(%rdi)
	movq	(%rsp), %rdx
	movq	%rdx, 56(%rdi)
	xorl	%eax, %eax
	ret

longjmp:
_longjmp:
	movl	%esi, %eax
	testl	%eax, %eax
	jnz	1f
	incl	%eax
1:	movq	0(%rdi), %rbx
	movq	8(%rdi), %rbp
	movq	16(%rdi), %r12
	movq	24(%rdi), %r13
	movq	32(%rdi), %r14
	movq	40(%rdi), %r15
	movq	48(%rdi), %rsp
	jmpq	*56(%rdi)

	.macro exc_noerr n
	.globl exc_\n
exc_\n:
	pushq	$0
	pushq	$\n
	jmp	exc_common
	.endm

	.macro exc_err n
	.globl exc_\n
exc_\n:
	pushq	$\n
	jmp	exc_common
	.endm

	exc_noerr 0		// divide error
	exc_noerr 6		// invalid opcode
	exc_err 12		// stack fault
	exc_err 13		// general protection
	exc_err 14		// page fault
	exc_noerr 16		// x87 error
	exc_err 17		// alignment check
	exc_noerr 19		// SIMD error

	// Stack: vector, error, rip, cs, rflags, rsp, ss.
exc_common:
	movq	0(%rsp), %rdi
	movq	8(%rsp), %rsi
	movq	16(%rsp), %rdx
	movq	%cr2, %rcx
	movq	%rsp, %rbx
	andq	$-16, %rsp
	call	efi_exception
	movq	%rbx, %rsp
	movq	%rax, 16(%rsp)		// resume there
	addq	$16, %rsp
	iretq

	.section .note.GNU-stack,"",@progbits
