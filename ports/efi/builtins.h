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

// The opcodes of the PC port, in the vocabulary pc, which platform.fs also names
// rpi. The screen, input, sound and file words are those of the Pi, shared in
// ports/baremetal/builtins.h (so ports/baremetal/sdl2.fs and the programs
// written for the Pi run here).

#define REQUIRED_MEMORY_SUPPORT \
  YV(internals, MALLOC, SET malloc(n0)) \
  YV(internals, SYSFREE, free(a0); DROP) \
  YV(internals, REALLOC, SET realloc(a1, n0); NIP)

#define REQUIRED_SYSTEM_SUPPORT \
  X("MS-TICKS", MS_TICKS, PUSH ms_ticks()) \
  XV(internals, "RAW-YIELD", RAW_YIELD, snd_pump(); term_poll()) \
  XV(internals, "RAW-TERMINATE", RAW_TERMINATE, if (term_bye()) { DROP; PARK; return rp; } efi_terminate(n0); DROP) \
  YV(internals, getchar_timeout_us, n0 = getchar_timeout_us(n0)) \
  XV(internals, "RAW-TYPE", RAW_TYPE, if (TERM_FOCUSED()) uart_write(b1, n0); con_write(b1, n0); DROPn(2))

#define PC_SUPPORT \
  XV(pc, "US-TICKS", US_TICKS, PUSH us_ticks()) \
  XV(pc, "UNIX-TIME", UNIX_TIME, PUSH unix_time()) \
  XV(pc, "UNIX-TIME!", UNIX_TIME_SET, unix_time_set(n0); DROP) \
  XV(pc, "MOUSE-SPEED!", MOUSE_SPEED_SET, ptr_gain = n0 < 1 ? 1 : n0 > 1000 ? 1000 : (int) n0; DROP) \
  XV(pc, "CNTFRQ", CNTFRQ, PUSH timer_freq()) \
  XV(pc, "BOARD", BOARD, PUSH BOARD_NAME; PUSH sizeof(BOARD_NAME) - 1) \
  XV(pc, "SCREEN-SIZE", SCREEN_SIZE, PUSH gop_w; PUSH gop_h) \
  XV(pc, "VOLUME-COUNT", VOLUME_COUNT, PUSH vol_count()) \
  XV(pc, "VOLUME-NOW", VOLUME_NOW, PUSH vol_now()) \
  XV(pc, "VOLUME-LABEL", VOLUME_LABEL, n2 = vol_label(n2, c1, n0); DROPn(2)) \
  XV(pc, "VOLUME-SIZE", VOLUME_SIZE, n0 = vol_size_mb(n0)) \
  XV(pc, "VOLUME-MOUNT", VOLUME_MOUNT, n0 = vol_mount(n0)) \
  XV(pc, "LOAD-DRIVER", LOAD_DRIVER, n1 = efi_load_driver(c1, n0); DROP) \
  XV(pc, "SND-ENABLE", SND_ENABLE, snd_enable(n0); DROP) \
  XV(pc, "INB", INB_OP, n0 = inb((uint16_t) n0)) \
  XV(pc, "OUTB", OUTB_OP, outb((uint16_t) n0, (uint8_t) n1); DROPn(2))

#define BM_VOC pc
#include "ports/baremetal/builtins.h"

#define PLATFORM_OPCODE_LIST \
  REQUIRED_MEMORY_SUPPORT \
  REQUIRED_SYSTEM_SUPPORT \
  PC_SUPPORT \
  BM_SCREEN_SUPPORT \
  BM_INPUT_SUPPORT \
  BM_SOUND_SUPPORT \
  BM_FILES_SUPPORT \
  CALLING_OPCODE_LIST \
  FLOATING_POINT_LIST

#define VOCABULARY_LIST V(forth) V(internals) V(pc)
