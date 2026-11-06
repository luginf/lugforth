/* Copyright 2026 Luginfo10

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.

   The boot code of the MBR of tools/dos_image.py (its bytes are copied there,
   so the image needs no assembler): the BIOS loads it at 0:7C00; it moves to
   0:0600, finds the active partition, loads its first sector at 0:7C00 (LBA
   through INT 13h AH=42h, else CHS from the table) and jumps there with DL,
   the BIOS drive, and DS:SI on the partition entry, as DOS's own MBR does.

     as --32 -o mbr.o mbr.S && ld -m elf_i386 -Ttext 0x600 --oformat binary -o mbr.bin mbr.o
*/
        .code16
        .text
        .globl _start
_start:
        cli
        xor     %ax, %ax
        mov     %ax, %ds
        mov     %ax, %es
        mov     %ax, %ss
        mov     $0x7c00, %sp
        sti
        cld
        mov     $0x7c00, %si
        mov     $0x0600, %di
        mov     $256, %cx
        rep movsw
        ljmp    $0, $moved
moved:
        mov     $0x0600 + 446, %si
        mov     $4, %cx
find:   testb   $0x80, (%si)
        jnz     found
        add     $16, %si
        loop    find
        jmp     fail
found:  mov     %si, %bp                /* the entry, for the jump */
        mov     $0x41, %ah              /* the LBA calls? */
        mov     $0x55aa, %bx
        int     $0x13
        jc      chs
        cmp     $0xaa55, %bx
        jne     chs
        mov     8(%bp), %ax             /* the first sector, 32 bits */
        mov     %ax, dap_lba
        mov     10(%bp), %ax
        mov     %ax, dap_lba + 2
        mov     $dap, %si
        mov     $0x42, %ah
        int     $0x13
        jnc     loaded
chs:    mov     1(%bp), %dh             /* head */
        mov     2(%bp), %cx             /* sector and cylinder */
        mov     $0x7c00, %bx
        mov     $0x0201, %ax
        int     $0x13
        jc      fail
loaded: cmpw    $0xaa55, 0x7dfe
        jne     fail
        mov     %bp, %si
        ljmp    $0, $0x7c00
fail:   mov     $msg, %si
puts:   lodsb
        test    %al, %al
        jz      stop
        mov     $0x0e, %ah
        mov     $7, %bx
        int     $0x10
        jmp     puts
stop:   int     $0x18                   /* the BIOS's next boot device */
        hlt
        jmp     stop

msg:    .asciz  "No bootable partition"
dap:    .byte   16, 0
        .word   1                       /* one sector */
        .word   0x7c00, 0               /* to 0:7C00 */
dap_lba: .long  0, 0
