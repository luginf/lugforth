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
// for platform.fs. Files: the FAT of the SD card (or virtio disk), fat.h.

#include <stdlib.h>
#include <string.h>

#define REQUIRED_MEMORY_SUPPORT \
  YV(internals, MALLOC, SET malloc(n0)) \
  YV(internals, SYSFREE, free(a0); DROP) \
  YV(internals, REALLOC, SET realloc(a1, n0); NIP)

#define REQUIRED_SYSTEM_SUPPORT \
  X("MS-TICKS", MS_TICKS, PUSH ms_ticks()) \
  XV(internals, "RAW-YIELD", RAW_YIELD, snd_pump()) \
  XV(internals, "RAW-TERMINATE", RAW_TERMINATE, rpi_terminate(n0); DROP) \
  YV(internals, getchar_timeout_us, n0 = getchar_timeout_us(n0)) \
  XV(internals, "RAW-TYPE", RAW_TYPE, uart_write(b1, n0); con_write(b1, n0); DROPn(2))

#define RPI_SUPPORT \
  XV(rpi, "US-TICKS", US_TICKS, PUSH us_ticks()) \
  XV(rpi, "UNIX-TIME", UNIX_TIME, PUSH unix_time()) \
  XV(rpi, "UNIX-TIME!", UNIX_TIME_SET, unix_time_set(n0); DROP) \
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
  XV(rpi, "FB-BLIT8", FB_BLIT8, fb_blit8(b3, n2, n1, (const uint32_t *) a0); DROPn(4)) \
  XV(rpi, "FB-CLOSE", FB_CLOSE, fb_close()) \
  XV(rpi, "FB-TEXT", FB_TEXT, fb_text(n4, n3, b2, n1, n0); DROPn(5)) \
  XV(rpi, "TEXT-LENGTH", TEXT_LENGTH, n1 = text_length(b1, n0); DROP) \
  XV(rpi, "IMAGE-LOAD", IMAGE_LOAD, n1 = image_load(c1, n0); DROP) \
  XV(rpi, "IMAGE-FREE", IMAGE_FREE, image_free(n0); DROP) \
  XV(rpi, "IMAGE-W", IMAGE_W, n0 = image_w(n0)) \
  XV(rpi, "IMAGE-H", IMAGE_H, n0 = image_h(n0)) \
  XV(rpi, "IMAGE-TINT!", IMAGE_TINT, image_tint(n1, n0); DROPn(2)) \
  XV(rpi, "IMAGE-ALPHA!", IMAGE_ALPHA, image_alpha(n1, n0); DROPn(2)) \
  XV(rpi, "IMAGE-DRAW", IMAGE_DRAW, \
     image_draw(n8, n7, n6, n5, n4, n3, n2, n1, n0); DROPn(9))

// Keyboard and mouse (input.h), and the serial line alone for the sdl2
// words' own key decoding.
#define RPI_INPUT_SUPPORT \
  XV(rpi, "INPUT-EVENTS", INPUT_EVENTS, input_snapshot()) \
  XV(rpi, "INPUT-DOWN?", INPUT_DOWN, n0 = input_down(n0)) \
  XV(rpi, "INPUT-LAST-KEY", INPUT_LAST_KEY, PUSH input_last_key()) \
  XV(rpi, "INPUT-MOUSE-X", INPUT_MOUSE_X, PUSH in_mouse_x) \
  XV(rpi, "INPUT-MOUSE-Y", INPUT_MOUSE_Y, PUSH in_mouse_y) \
  XV(rpi, "INPUT-TEXT", INPUT_TEXT, PUSH in_get_text()) \
  XV(rpi, "INPUT-WHEEL", INPUT_WHEEL, PUSH in_wheel) \
  XV(rpi, "CON-ZOOM!", CON_ZOOM_SET, con_set_zoom(n0); DROP) \
  XV(rpi, "KEYBOARD!", KEYBOARD_SET, in_azerty = n0 != 0; DROP) \
  XV(rpi, "UART-KEY", UART_KEY, PUSH uart_getc_nowait())

// Sound (sound.h), for beep, drum-hit and the tunes of sdl2-abc.
#define RPI_SOUND_SUPPORT \
  XV(rpi, "SND-BEEP", SND_BEEP, snd_beep(n3, n2, n1, n0); DROPn(4)) \
  XV(rpi, "SND-HIT", SND_HIT, snd_hit(n4, n3, n2, n1, n0); DROPn(5)) \
  XV(rpi, "SND-QUEUED", SND_QUEUED, PUSH snd_pcm_queued()) \
  XV(rpi, "SND-QUEUE", SND_QUEUE, snd_pcm_put((const int16_t *) a1, n0); DROPn(2)) \
  XV(rpi, "SND-SILENCE", SND_SILENCE, snd_silence()) \
  XV(rpi, "SND-BUSY", SND_BUSY, PUSH snd_busy()) \
  XV(rpi, "SND-OK", SND_OK, PUSH snd_ok()) \
  XV(rpi, "SND-TUNE-CLEAR", SND_TUNE_CLEAR, snd_tune_clear()) \
  XV(rpi, "SND-TUNE-ADD", SND_TUNE_ADD, snd_tune_add(n2, n1, n0); DROPn(3)) \
  XV(rpi, "SND-TUNE-RUN", SND_TUNE_RUN, snd_tune_run(n3, n2, n1, n0); DROPn(4)) \
  XV(rpi, "SND-TUNE-STOP", SND_TUNE_STOP, snd_tune_stop()) \
  XV(rpi, "SND-TUNE-PLAYING", SND_TUNE_PLAYING, PUSH snd_tune_playing())

// Files on the SD card or the virtio disk (fat.h): the standard words, with
// the stack effects of ports/esp32 (positions and sizes in one cell).
#define RPI_FILES_SUPPORT \
  X("R/O", R_O, PUSH 0) \
  X("W/O", W_O, PUSH 1) \
  X("R/W", R_W, PUSH 2) \
  Y(BIN, ) \
  X("OPEN-FILE", OPEN_FILE, cell_t mode = n0; DROP; cell_t len = n0; DROP; \
    cell_t ior; n0 = fat_open(c0, len, mode, 0, &ior); PUSH ior) \
  X("CREATE-FILE", CREATE_FILE, cell_t mode = n0; DROP; cell_t len = n0; DROP; \
    cell_t ior; n0 = fat_open(c0, len, mode, 1, &ior); PUSH ior) \
  X("CLOSE-FILE", CLOSE_FILE, n0 = fat_close(n0)) \
  X("FLUSH-FILE", FLUSH_FILE, n0 = fh_check(n0) ? FAT_EBADF : (fat_flush() ? FAT_EIO : 0)) \
  X("READ-FILE", READ_FILE, cell_t fh = n0; DROP; cell_t len = n0; DROP; \
    cell_t ior; n0 = fat_read(fh, b0, len, &ior); PUSH ior) \
  X("WRITE-FILE", WRITE_FILE, cell_t fh = n0; DROP; cell_t len = n0; DROP; \
    n0 = fat_write(fh, b0, len)) \
  X("FILE-SIZE", FILE_SIZE, cell_t ior; n0 = fat_size(n0, &ior); PUSH ior) \
  X("FILE-POSITION", FILE_POSITION, cell_t ior; n0 = fat_tell(n0, &ior); PUSH ior) \
  X("REPOSITION-FILE", REPOSITION_FILE, cell_t fh = n0; DROP; n0 = fat_seek(fh, n0)) \
  X("RESIZE-FILE", RESIZE_FILE, cell_t fh = n0; DROP; n0 = fat_resize(fh, n0)) \
  X("DELETE-FILE", DELETE_FILE, cell_t len = n0; DROP; n0 = fat_delete(c0, len)) \
  X("RENAME-FILE", RENAME_FILE, n3 = fat_rename(c3, n2, c1, n0); DROPn(3)) \
  X("OPEN-DIR", OPEN_DIR, cell_t ior; n1 = fat_opendir(c1, n0, &ior); n0 = ior) \
  X("CLOSE-DIR", CLOSE_DIR, n0 = fat_closedir(n0)) \
  YV(internals, READDIR, SET fat_readdir(n0)) \
  XV(rpi, "CHDIR", CHDIR, n1 = fat_chdir(c1, n0); DROP) \
  XV(rpi, "GETCWD", GETCWD, PUSH fs_cwd_path; PUSH strlen(fs_cwd_path)) \
  XV(rpi, "DISK?", DISK_P, PUSH fs.ok ? -1 : 0)

#define PLATFORM_OPCODE_LIST \
  REQUIRED_MEMORY_SUPPORT \
  REQUIRED_SYSTEM_SUPPORT \
  RPI_SUPPORT \
  RPI_SCREEN_SUPPORT \
  RPI_INPUT_SUPPORT \
  RPI_SOUND_SUPPORT \
  RPI_FILES_SUPPORT \
  CALLING_OPCODE_LIST \
  FLOATING_POINT_LIST

#define VOCABULARY_LIST V(forth) V(internals) V(rpi)
