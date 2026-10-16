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

// Peripherals are memory-mapped (UL@ and L! reach them), so only what needs
// C is here: memory, time, the console, leaving, and the board's addresses
// for platform.fs. No filesystem, like ports/gba: nothing in the boot chain
// opens a file.

#include <stdlib.h>
#include <string.h>

#define REQUIRED_MEMORY_SUPPORT \
  YV(internals, MALLOC, SET malloc(n0)) \
  YV(internals, SYSFREE, free(a0); DROP) \
  YV(internals, REALLOC, SET realloc(a1, n0); NIP)

#define REQUIRED_SYSTEM_SUPPORT \
  X("MS-TICKS", MS_TICKS, PUSH ms_ticks()) \
  XV(internals, "RAW-YIELD", RAW_YIELD, ) \
  XV(internals, "RAW-TERMINATE", RAW_TERMINATE, rpi_terminate(n0); DROP) \
  YV(internals, getchar_timeout_us, n0 = getchar_timeout_us(n0)) \
  XV(internals, "RAW-TYPE", RAW_TYPE, uart_write(b1, n0); DROPn(2))

#define RPI_SUPPORT \
  XV(rpi, "US-TICKS", US_TICKS, PUSH us_ticks()) \
  XV(rpi, "CNTFRQ", CNTFRQ, PUSH timer_freq()) \
  XV(rpi, "UART-BASE", UART_BASE_OP, PUSH UART_BASE) \
  XV(rpi, "GPIO-BASE", GPIO_BASE_OP, PUSH GPIO_BASE) \
  XV(rpi, "BOARD", BOARD, PUSH BOARD_NAME; PUSH sizeof(BOARD_NAME) - 1)

// The framebuffer (screen.h), for the sdl2 words of ports/rpi/sdl2.fs.
#define RPI_SCREEN_SUPPORT \
  XV(rpi, "FB-OPEN", FB_OPEN, n2 = fb_open(n2, n1, n0); DROPn(2)) \
  XV(rpi, "FB-COLOR", FB_COLOR, fb_set_color(n1, n0); DROPn(2)) \
  XV(rpi, "FB-BOX", FB_BOX, fb_box(n3, n2, n1, n0); DROPn(4)) \
  XV(rpi, "FB-LINE", FB_LINE, fb_line(n3, n2, n1, n0); DROPn(4)) \
  XV(rpi, "FB-PIXEL", FB_PIXEL, n1 = fb_pixel(n1, n0); DROP) \
  XV(rpi, "FB-FLIP", FB_FLIP, fb_flip()) \
  XV(rpi, "FB-TEXT", FB_TEXT, fb_text(n4, n3, b2, n1, n0); DROPn(5)) \
  XV(rpi, "TEXT-LENGTH", TEXT_LENGTH, n1 = text_length(b1, n0); DROP)

#define PLATFORM_OPCODE_LIST \
  REQUIRED_MEMORY_SUPPORT \
  REQUIRED_SYSTEM_SUPPORT \
  RPI_SUPPORT \
  RPI_SCREEN_SUPPORT \
  CALLING_OPCODE_LIST \
  FLOATING_POINT_LIST

#define VOCABULARY_LIST V(forth) V(internals) V(rpi)
