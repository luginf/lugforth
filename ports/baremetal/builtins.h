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

// The opcodes shared by ports/rpi and ports/efi: the sdl2 words' screen,
// input, sound and the standard file words. The port's builtins.h defines
// BM_VOC (the vocabulary of the board's own words: rpi, pc) before including
// this file, and its main.c the functions they call (input_flush: keys hit
// before a screen opens are not the program's).

// The framebuffer (screen.h), for the sdl2 words of ports/baremetal/sdl2.fs.
#define BM_SCREEN_SUPPORT \
  XV(BM_VOC, "FB-OPEN", FB_OPEN, n2 = fb_open(n2, n1, n0); input_flush(); DROPn(2)) \
  XV(BM_VOC, "FB-COLOR", FB_COLOR, fb_set_color(n1, n0); DROPn(2)) \
  XV(BM_VOC, "FB-BOX", FB_BOX, fb_box(n3, n2, n1, n0); DROPn(4)) \
  XV(BM_VOC, "FB-LINE", FB_LINE, fb_line(n3, n2, n1, n0); DROPn(4)) \
  XV(BM_VOC, "FB-PIXEL", FB_PIXEL, n1 = fb_pixel(n1, n0); DROP) \
  XV(BM_VOC, "FB-FLIP", FB_FLIP, fb_flip()) \
  XV(BM_VOC, "FB-BLIT8", FB_BLIT8, fb_blit8(b3, n2, n1, (const uint32_t *) a0); DROPn(4)) \
  XV(BM_VOC, "FB-CLOSE", FB_CLOSE, fb_close()) \
  XV(BM_VOC, "FB-TEXT", FB_TEXT, fb_text(n4, n3, b2, n1, n0); DROPn(5)) \
  XV(BM_VOC, "TEXT-LENGTH", TEXT_LENGTH, n1 = text_length(b1, n0); DROP) \
  XV(BM_VOC, "IMAGE-LOAD", IMAGE_LOAD, n1 = image_load(c1, n0); DROP) \
  XV(BM_VOC, "IMAGE-FREE", IMAGE_FREE, image_free(n0); DROP) \
  XV(BM_VOC, "IMAGE-W", IMAGE_W, n0 = image_w(n0)) \
  XV(BM_VOC, "IMAGE-H", IMAGE_H, n0 = image_h(n0)) \
  XV(BM_VOC, "IMAGE-TINT!", IMAGE_TINT, image_tint(n1, n0); DROPn(2)) \
  XV(BM_VOC, "IMAGE-ALPHA!", IMAGE_ALPHA, image_alpha(n1, n0); DROPn(2)) \
  XV(BM_VOC, "IMAGE-DRAW", IMAGE_DRAW, \
     image_draw(n8, n7, n6, n5, n4, n3, n2, n1, n0); DROPn(9))

// Keyboard and mouse (input.h), and the serial line alone for the sdl2
// words' own key decoding.
#define BM_INPUT_SUPPORT \
  XV(BM_VOC, "INPUT-EVENTS", INPUT_EVENTS, input_snapshot()) \
  XV(BM_VOC, "INPUT-DOWN?", INPUT_DOWN, n0 = input_down(n0)) \
  XV(BM_VOC, "INPUT-LAST-KEY", INPUT_LAST_KEY, PUSH input_last_key()) \
  XV(BM_VOC, "INPUT-MOUSE-X", INPUT_MOUSE_X, PUSH in_mouse_x) \
  XV(BM_VOC, "INPUT-MOUSE-Y", INPUT_MOUSE_Y, PUSH in_mouse_y) \
  XV(BM_VOC, "INPUT-TEXT", INPUT_TEXT, PUSH in_get_text()) \
  XV(BM_VOC, "INPUT-WHEEL", INPUT_WHEEL, PUSH in_wheel) \
  XV(BM_VOC, "CON-ZOOM!", CON_ZOOM_SET, con_set_zoom(n0); DROP) \
  XV(BM_VOC, "CON-ROWS", CON_ROWS_OP, PUSH con_on ? CON_ROWS : 0) \
  XV(BM_VOC, "KEYBOARD!", KEYBOARD_SET, in_azerty = n0 != 0; DROP) \
  XV(BM_VOC, "UART-KEY", UART_KEY, PUSH uart_getc_nowait())

// Sound (sound.h), for beep, drum-hit and the tunes of sdl2-abc.
#define BM_SOUND_SUPPORT \
  XV(BM_VOC, "SND-BEEP", SND_BEEP, snd_beep(n3, n2, n1, n0); DROPn(4)) \
  XV(BM_VOC, "SND-HIT", SND_HIT, snd_hit(n4, n3, n2, n1, n0); DROPn(5)) \
  XV(BM_VOC, "SND-QUEUED", SND_QUEUED, PUSH snd_pcm_queued()) \
  XV(BM_VOC, "SND-QUEUE", SND_QUEUE, snd_pcm_put((const int16_t *) a1, n0); DROPn(2)) \
  XV(BM_VOC, "SND-SILENCE", SND_SILENCE, snd_silence()) \
  XV(BM_VOC, "SND-BUSY", SND_BUSY, PUSH snd_busy()) \
  XV(BM_VOC, "SND-OK", SND_OK, PUSH snd_ok()) \
  XV(BM_VOC, "SND-TUNE-CLEAR", SND_TUNE_CLEAR, snd_tune_clear()) \
  XV(BM_VOC, "SND-TUNE-ADD", SND_TUNE_ADD, snd_tune_add(n2, n1, n0); DROPn(3)) \
  XV(BM_VOC, "SND-TUNE-RUN", SND_TUNE_RUN, snd_tune_run(n3, n2, n1, n0); DROPn(4)) \
  XV(BM_VOC, "SND-TUNE-STOP", SND_TUNE_STOP, snd_tune_stop()) \
  XV(BM_VOC, "SND-TUNE-PLAYING", SND_TUNE_PLAYING, PUSH snd_tune_playing())

// Files (fat.h on the Pi, files.h on the PC): the standard words, with
// the stack effects of ports/esp32 (positions and sizes in one cell).
#define BM_FILES_SUPPORT \
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
  XV(BM_VOC, "MKDIR", MKDIR, n1 = fat_mkdir(c1, n0); DROP) \
  XV(BM_VOC, "RMDIR", RMDIR, n1 = fat_rmdir(c1, n0); DROP) \
  XV(BM_VOC, "CHDIR", CHDIR, n1 = fat_chdir(c1, n0); DROP) \
  XV(BM_VOC, "GETCWD", GETCWD, PUSH fs_cwd_path; PUSH strlen(fs_cwd_path)) \
  XV(BM_VOC, "DISK?", DISK_P, PUSH fs.ok ? -1 : 0)
