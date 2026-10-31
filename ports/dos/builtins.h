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

// The opcodes of the DOS port, in the vocabulary dos (also named rpi by
// platform.fs, for the shared Forth of ports/baremetal and common/pager.fs).
// The file words are those of the boards (BM_FILES_SUPPORT), over files.h.

#define REQUIRED_MEMORY_SUPPORT \
  YV(internals, MALLOC, SET malloc(n0)) \
  YV(internals, SYSFREE, free(a0); DROP) \
  YV(internals, REALLOC, SET realloc(a1, n0); NIP)

#define REQUIRED_SYSTEM_SUPPORT \
  X("MS-TICKS", MS_TICKS, PUSH ms_ticks()) \
  XV(internals, "RAW-YIELD", RAW_YIELD, snd_pump(); __dpmi_yield()) \
  XV(internals, "RAW-TERMINATE", RAW_TERMINATE, dos_terminate(n0); DROP) \
  YV(internals, getchar_timeout_us, n0 = getchar_timeout_us(n0)) \
  XV(internals, "RAW-TYPE", RAW_TYPE, con_write(b1, n0); DROPn(2))

#define DOS_SUPPORT \
  XV(dos, "US-TICKS", US_TICKS, PUSH us_ticks()) \
  XV(dos, "UNIX-TIME", UNIX_TIME, PUSH time(0)) \
  XV(dos, "BOARD", BOARD, PUSH BOARD_NAME; PUSH sizeof(BOARD_NAME) - 1) \
  XV(dos, "CON-ROWS", CON_ROWS_OP, PUSH con_tty ? ScreenRows() : 0) \
  XV(dos, "CON-COLS", CON_COLS_OP, PUSH con_tty ? ScreenCols() : 80) \
  XV(dos, "SCREEN-CHAR", SCREEN_CHAR, n1 = screen_char(n1, n0); DROP) \
  XV(dos, "INB", INB_OP, n0 = inportb((unsigned short) n0)) \
  XV(dos, "OUTB", OUTB_OP, outportb((unsigned short) n0, (unsigned char) n1); DROPn(2))

#define BM_VOC dos
#include "ports/baremetal/builtins.h"

// The input words of ports/baremetal/sdl2.fs; the terminals and the console
// on the framebuffer of the boards have no counterpart here.
#define DOS_INPUT_SUPPORT \
  XV(dos, "INPUT-EVENTS", INPUT_EVENTS, input_snapshot()) \
  XV(dos, "INPUT-DOWN?", INPUT_DOWN, n0 = input_down(n0)) \
  XV(dos, "INPUT-LAST-KEY", INPUT_LAST_KEY, PUSH input_last_key()) \
  XV(dos, "INPUT-MOUSE-X", INPUT_MOUSE_X, PUSH in_mouse_x) \
  XV(dos, "INPUT-MOUSE-Y", INPUT_MOUSE_Y, PUSH in_mouse_y) \
  XV(dos, "INPUT-TEXT", INPUT_TEXT, PUSH in_get_text()) \
  XV(dos, "INPUT-WHEEL", INPUT_WHEEL, PUSH in_wheel) \
  XV(dos, "KEYBOARD!", KEYBOARD_SET, in_azerty = n0 != 0; DROP) \
  XV(dos, "UART-KEY", UART_KEY, PUSH uart_getc_nowait()) \
  XV(dos, "SND-ENABLE", SND_ENABLE, sb_off = !n0; if (!n0) sb_stop(); DROP)

#define PLATFORM_OPCODE_LIST \
  REQUIRED_MEMORY_SUPPORT \
  REQUIRED_SYSTEM_SUPPORT \
  DOS_SUPPORT \
  BM_SCREEN_SUPPORT \
  DOS_INPUT_SUPPORT \
  BM_SOUND_SUPPORT \
  BM_FILES_SUPPORT \
  CALLING_OPCODE_LIST \
  FLOATING_POINT_LIST

#define VOCABULARY_LIST V(forth) V(internals) V(dos)
