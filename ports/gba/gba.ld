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

/* GBA memory map (GBATEK): code and the 192-byte header go in ROM; the
   150K malloc-less heap (heap_mem[], see main.c) and everything else
   .data/.bss lives in EWRAM, which is 256K, so it fits with room to
   spare. IWRAM (32K, faster) is used only for the stack, set up by hand
   in crt0.s, not declared here. */
MEMORY
{
  rom   (rx)  : ORIGIN = 0x08000000, LENGTH = 4M
  ewram (rwx) : ORIGIN = 0x02000000, LENGTH = 256K
  iwram (rwx) : ORIGIN = 0x03000000, LENGTH = 24K   /* the top 8K stay for the stack, see crt0.s */
}

SECTIONS
{
  .header : { *(.header) } > rom
  .text   : { *(.text) *(.text.*) *(.rodata) *(.rodata.*) *(.glue_7) *(.glue_7t) . = ALIGN(8); } > rom   /* crt0.s copies .data and .iwram by words */

  . = ORIGIN(ewram);
  .data : {
    __data_start = .;
    *(.data) *(.data.*)
    __data_end = .;
  } > ewram AT > rom
  __data_load = LOADADDR(.data);

  /* Hot code and tables (gba_magno and its sine table, main.c): IWRAM is a
     32-bit bus with no wait state, a lot faster than code run from the
     cartridge or from EWRAM. Loaded after .data, copied by crt0.s. */
  .iwram : AT(ALIGN(LOADADDR(.data) + SIZEOF(.data), 8)) {
    __iwram_start = .;
    *(.iwram_code) *(.iwram_data)
    __iwram_end = .;
  } > iwram
  __iwram_load = LOADADDR(.iwram);
  .bss (NOLOAD) : {
    __bss_start = .;
    *(.bss) *(.bss.*) *(COMMON)
    __bss_end = .;
  } > ewram

  /* newlib's default _sbrk (pulled in even though nothing here calls
     malloc: see gba_malloc & co in main.c) wants this name for "end of
     BSS, start of free memory" -- it will never actually be called
     (there is no heap to grow), but it still has to link. */
  PROVIDE(end = .);
}
