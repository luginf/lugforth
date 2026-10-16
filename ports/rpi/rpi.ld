/* Copyright 2026 Luginfo10
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

/* One RAM region, loaded whole (ELF or flat kernel8.img): code, data and
   the TLS template are in place, .tbss and .bss are zeroed by start.S.
   The Makefile gives __load_addr (where the image is loaded) and
   __ram_end (end of the RAM the C heap may use) with --defsym.

   picolibc keeps errno and its stdio state in thread-local storage: the
   one TLS block is .tdata + .tbss in place, set by _set_tls(__tls_base)
   in main.c, and the __tls_* / __arm64_tls_tcb_offset names below are the
   ones its picolibc.ld defines. The C heap (malloc, sbrk) runs from
   __heap_start to __heap_end. */

ENTRY(_start)

SECTIONS
{
  . = __load_addr;
  .text : {
    KEEP(*(.text.boot))
    *(.text .text.*)
  }
  .rodata : { *(.rodata .rodata.*) }
  . = ALIGN(16);
  .init_array : {
    PROVIDE_HIDDEN(__preinit_array_start = .);
    KEEP(*(.preinit_array))
    PROVIDE_HIDDEN(__preinit_array_end = .);
    PROVIDE_HIDDEN(__init_array_start = .);
    KEEP(*(SORT_BY_INIT_PRIORITY(.init_array.*)))
    KEEP(*(.init_array))
    PROVIDE_HIDDEN(__init_array_end = .);
    PROVIDE_HIDDEN(__fini_array_start = .);
    KEEP(*(.fini_array .fini_array.*))
    PROVIDE_HIDDEN(__fini_array_end = .);
  }
  .data : { *(.data .data.*) *(.got .got.plt) }
  .tdata : ALIGN(16) {
    *(.tdata .tdata.*)
  }
  __tls_base = ADDR(.tdata);
  .tbss : ALIGN(16) {
    *(.tbss .tbss.*) *(.tcommon)
  }
  __tls_end = ADDR(.tbss) + SIZEOF(.tbss);
  __tls_align = MAX(ALIGNOF(.tdata), ALIGNOF(.tbss));
  __tls_size = __tls_end - __tls_base;
  __tls_size_align = (__tls_size + __tls_align - 1) & ~(__tls_align - 1);
  __arm64_tls_tcb_offset = MAX(16, __tls_align);
  __tdata_source = LOADADDR(.tdata);
  __tdata_size = SIZEOF(.tdata);
  __tbss_size = SIZEOF(.tbss);
  __tbss_offset = ADDR(.tbss) - ADDR(.tdata);
  __image_end = ADDR(.tbss);

  /* ld.bfd does not advance the location counter over .tbss. */
  .tbss_space (NOLOAD) : {
    . = ADDR(.tbss) + SIZEOF(.tbss);
  }
  __zero_start = ADDR(.tbss);
  .bss (NOLOAD) : ALIGN(16) {
    *(.bss .bss.*) *(COMMON)
    . = ALIGN(16);
    __bss_end = .;
  }

  /* 64K stack for the C code and the exception handler (the Forth stacks
     live in the Forth heap). */
  . = ALIGN(16);
  . = . + 0x10000;
  __stack = .;
  __heap_start = .;
  __heap_end = __ram_end;

  /DISCARD/ : {
    *(.note .note.*) *(.comment) *(.eh_frame .eh_frame.*)
  }
}
