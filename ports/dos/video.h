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

// The screen of the sdl2 words on DOS: a VESA mode of 32 bits per pixel with
// a linear framebuffer, the smallest that holds the program's screen (zoom
// included; centered in it, the rest black), drawn by ports/baremetal/screen.h
// as on the boards. When no 32-bit mode is large enough (DOSBox's S3, 2 MB,
// has 1024x768 in 16 bits only), a 16-bit one (5-6-5): screen.h then draws in
// a copy in RAM, and each flip converts the rows that changed. While it is open the keyboard is read by interrupt (IRQ
// 1, chained to the handler that was there, so DOS and DJGPP still see the
// keys and Ctrl+Break) for the keys' downs and ups, which input.h turns into
// the state of pressed? and into characters; the mouse through the INT 33h
// driver. close-screen gives the text mode back.

#define FB_HW_EXTERNAL
#include "ports/baremetal/term.h"
#include "ports/baremetal/screen.h"

static int con_on;                       // no console drawn on the framebuffer here
static void con_init(void);
static void con_sync(void) {}

#include "ports/baremetal/input.h"

static int vid_graphics;                 // a VESA mode is set
static int vid_bpp;                      // 32, or 16 with the copy below
static uint32_t *vid_shadow;             // 16 bits: what screen.h draws, 32 bits a pixel
static uint32_t *vid_prev;               // 16 bits: the copy as it was at the last flip
static uint8_t *vid_out;                 // 16 bits: the program's screen in the video memory
static uint32_t vid_pitch16, vid_pw, vid_ph;
static int vid_all;                      // 16 bits: the next flip writes every pixel
static int vid_allow16;                  // dos_fb_open: no 32-bit mode holds the screen, even at zoom 1
static unsigned long vid_phys, vid_lin;  // the framebuffer mapped: its physical address, linear address

struct __attribute__((packed)) vbe_info {
  char sig[4]; uint16_t version; uint32_t oem, caps, modes; uint16_t memory;
  uint8_t rest[512 - 20];
};
struct __attribute__((packed)) vbe_mode {
  uint16_t attr; uint8_t wa, wb; uint16_t gran, wsize, sega, segb; uint32_t winfunc; uint16_t pitch;
  uint16_t w, h; uint8_t cw, ch, planes, bpp, banks, model, banksize, pages, res1;
  uint8_t rmask, rpos, gmask, gpos, bmask, bpos, xmask, xpos, dcm;
  uint32_t phys, res2; uint16_t res3, linpitch;
  uint8_t rest[256 - 52];
};

static int vbe_mode_info(int mode, struct vbe_mode *mi) {
  __dpmi_regs r;
  memset(&r, 0, sizeof r);
  r.x.ax = 0x4F01; r.x.cx = (unsigned short) mode;
  r.x.es = __tb >> 4; r.x.di = __tb & 15;
  __dpmi_int(0x10, &r);
  if (r.x.ax != 0x004F) return -1;
  dosmemget(__tb, sizeof *mi, mi);
  return 0;
}

// The VESA mode for pw x ph: bpp bits (32, or 16 as 5-6-5), linear framebuffer, the smallest area that holds it.
static int vbe_find(uint32_t pw, uint32_t ph, int bpp, struct vbe_mode *best) {
  struct vbe_info vi;
  __dpmi_regs r;
  memset(&vi, 0, sizeof vi);
  memcpy(vi.sig, "VBE2", 4);
  dosmemput(&vi, sizeof vi, __tb);
  memset(&r, 0, sizeof r);
  r.x.ax = 0x4F00; r.x.es = __tb >> 4; r.x.di = __tb & 15;
  __dpmi_int(0x10, &r);
  if (r.x.ax != 0x004F) return -1;
  dosmemget(__tb, sizeof vi, &vi);
  if (memcmp(vi.sig, "VESA", 4) || vi.version < 0x200) return -1;
  uint16_t modes[256];
  unsigned long at = ((vi.modes >> 16) << 4) + (vi.modes & 0xFFFF);
  int n = 0;
  while (n < 255) {
    uint16_t m = _farpeekw(_dos_ds, at + 2 * n);
    if (m == 0xFFFF) break;
    modes[n++] = m;
  }
  int found = -1;
  uint64_t area = ~0ull;
  for (int i = 0; i < n; i++) {
    struct vbe_mode mi;
    if (vbe_mode_info(modes[i], &mi)) continue;
    if ((mi.attr & 0x99) != 0x99 || mi.bpp != bpp || mi.model != 6) continue;   // supported, color, graphics, linear
    if (bpp == 16 && (mi.rmask != 5 || mi.gmask != 6 || mi.bmask != 5 || mi.rpos != 11)) continue;
    if (mi.w < pw || mi.h < ph) continue;
    uint64_t a = (uint64_t) mi.w * mi.h;
    if (a < area) { area = a; found = modes[i]; *best = mi; }
  }
  return found;
}

static void kbd_on(void);
static void kbd_off(void);
static void mouse_on(int w, int h);

static int fb_hw_open(uint32_t pw, uint32_t ph) {
  struct vbe_mode mi;
  int bpp = 32, mode = vbe_find(pw, ph, 32, &mi);
  if (mode < 0 && vid_allow16) { bpp = 16; mode = vbe_find(pw, ph, 16, &mi); }
  if (mode < 0) return -1;
  uint32_t *shadow = 0;
  uint32_t *prev = 0;
  if (bpp == 16) {                                                         // the copy in RAM, before the mode changes
    shadow = calloc((size_t) pw * ph, 4);
    prev = malloc((size_t) pw * ph * 4);
    if (!shadow || !prev) { free(shadow); free(prev); return -1; }
  }
  __dpmi_regs r;
  memset(&r, 0, sizeof r);
  r.x.ax = 0x4F02; r.x.bx = (unsigned short) (mode | 0x4000);              // with the linear framebuffer
  __dpmi_int(0x10, &r);
  if (r.x.ax != 0x004F) return -1;
  uint32_t pitch = mi.pitch;                                               // bytes per row
  if (vid_phys != mi.phys) {
    __dpmi_meminfo m;
    m.handle = 0; m.address = mi.phys; m.size = 16u << 20;               // the most any mode here needs
    if (__dpmi_physical_address_mapping(&m)) return -1;
    vid_phys = mi.phys; vid_lin = m.address;
  }
  if (!__djgpp_nearptr_enable()) return -1;
  uint8_t *base = (uint8_t *) (vid_lin + __djgpp_conventional_base);
  for (uint32_t y = 0; y < mi.h; y++) memset(base + (size_t) y * pitch, 0, (size_t) mi.w * bpp / 8);
  uint8_t *at = base + (size_t) ((mi.h - ph) / 2) * pitch + (size_t) (mi.w - pw) / 2 * (bpp / 8);
  free(vid_shadow); free(vid_prev);
  vid_shadow = shadow; vid_prev = prev; vid_bpp = bpp;
  if (bpp == 16) {
    vid_out = at; vid_pitch16 = pitch; vid_pw = pw; vid_ph = ph; vid_all = 1;
    fb_pitch = pw * 4;
    fb_mem = (volatile uint32_t *) shadow;
    fb_rgb = 0;
  } else {
    fb_pitch = pitch;
    fb_mem = (volatile uint32_t *) at;
    fb_rgb = mi.rpos == 0;                                                  // byte 0 is red
  }
  vid_graphics = 1;
  kbd_on();
  mouse_on((int) pw, (int) ph);
  return 0;
}

// fb_close (CLOSE-SCREEN): the text mode again, as the prompt left it.
static void con_init(void) {
  if (!vid_graphics) return;
  kbd_off();
  __dpmi_regs r;
  memset(&r, 0, sizeof r);
  r.x.ax = 0x0003;
  __dpmi_int(0x10, &r);
  vid_graphics = 0;
  fb_mem = 0;
  free(vid_shadow); free(vid_prev); vid_shadow = 0; vid_prev = 0; vid_bpp = 0;
  if (con_tty) { gppconio_init(); con_attr(); }
}

// ---- the keyboard by interrupt: the scan codes are kept in a ring by the
// handler, and turned into Linux key codes for input.h by usb_poll ----
static volatile uint8_t kbd_ring[256];
static volatile unsigned kbd_head, kbd_tail;
static int kbd_installed, kbd_e0, kbd_skip;
static _go32_dpmi_seginfo kbd_old, kbd_new;

static void kbd_isr(void) { kbd_ring[kbd_head++ & 255] = inportb(0x60); }
static void kbd_isr_end(void) {}

static void kbd_on(void) {
  if (kbd_installed) return;
  _go32_dpmi_lock_code((void *) kbd_isr, (char *) kbd_isr_end - (char *) kbd_isr);
  _go32_dpmi_lock_data((void *) kbd_ring, sizeof kbd_ring);
  _go32_dpmi_lock_data((void *) &kbd_head, sizeof kbd_head);
  _go32_dpmi_get_protected_mode_interrupt_vector(9, &kbd_old);
  kbd_new.pm_offset = (unsigned long) kbd_isr;
  kbd_new.pm_selector = _go32_my_cs();
  if (_go32_dpmi_chain_protected_mode_interrupt_vector(9, &kbd_new)) return;
  kbd_tail = kbd_head; kbd_e0 = kbd_skip = 0;
  kbd_installed = 1;
}

static void kbd_off(void) {
  if (!kbd_installed) return;
  _go32_dpmi_set_protected_mode_interrupt_vector(9, &kbd_old);
  kbd_installed = 0;
  while (kbhit()) getch();                     // what the BIOS kept meanwhile was the game's
}

// Set 1 scan codes are Linux's key codes for the main block; the keys after
// E0 are the others.
static int kbd_e0_code(int s) {
  switch (s) {
    case 0x1C: return 28;                      // keypad Enter: Enter
    case 0x1D: return 97;                      // right Ctrl
    case 0x35: return 53;                      // keypad /
    case 0x38: return 100;                     // right Alt (AltGr)
    case 0x47: return 102; case 0x48: return 103; case 0x49: return 104;
    case 0x4B: return 105; case 0x4D: return 106;
    case 0x4F: return 107; case 0x50: return 108; case 0x51: return 109;
    case 0x52: return 110; case 0x53: return 111;
  }
  return 0;
}

// ---- the mouse: the INT 33h driver, its range set to the screen ----
static int mouse_ok, mouse_x = -1, mouse_y = -1, mouse_b;

static void mouse_on(int w, int h) {
  __dpmi_regs r;
  memset(&r, 0, sizeof r);
  r.x.ax = 0;
  __dpmi_int(0x33, &r);
  mouse_ok = r.x.ax == 0xFFFF;
  if (!mouse_ok) return;
  memset(&r, 0, sizeof r); r.x.ax = 7; r.x.cx = 0; r.x.dx = (unsigned short) (w - 1); __dpmi_int(0x33, &r);
  memset(&r, 0, sizeof r); r.x.ax = 8; r.x.cx = 0; r.x.dx = (unsigned short) (h - 1); __dpmi_int(0x33, &r);
  mouse_x = mouse_y = -1; mouse_b = 0;
}

static void usb_init(void) {}

static void usb_poll(void) {
  while (kbd_installed && kbd_tail != kbd_head) {
    int s = kbd_ring[kbd_tail++ & 255];
    if (kbd_skip) { kbd_skip--; continue; }
    if (s == 0xE1) { kbd_skip = 5; continue; }  // Pause
    if (s == 0xE0) { kbd_e0 = 1; continue; }
    int up = s & 0x80, code = s & 0x7F;
    if (kbd_e0) {
      kbd_e0 = 0;
      if (code == 0x2A || code == 0x36) continue;   // the shifts the keyboard adds around some keys
      code = kbd_e0_code(code);
    }
    if (!code) continue;
    int k = key_sdl_code(code);
    input_key(code, up ? 0 : k && in_down[k] ? 2 : 1);
  }
  if (kbd_installed) while (kbhit()) getch();   // the BIOS's copy of the same keys
  if (!mouse_ok || !vid_graphics) return;
  __dpmi_regs r;
  memset(&r, 0, sizeof r);
  r.x.ax = 3;
  __dpmi_int(0x33, &r);
  int w = fb_w * fb_zoom, h = fb_h * fb_zoom;
  if ((int) r.x.cx != mouse_x || (int) r.x.dx != mouse_y) {
    mouse_x = r.x.cx; mouse_y = r.x.dx;
    if (w && h) { input_abs(0, (int64_t) mouse_x * 32768 / w); input_abs(1, (int64_t) mouse_y * 32768 / h); }
  }
  int b = r.x.bx;
  if ((b ^ mouse_b) & 1) input_key(BTN_LEFT, b & 1);
  if ((b ^ mouse_b) & 2) input_key(BTN_RIGHT, (b & 2) != 0);
  if ((b ^ mouse_b) & 4) input_key(BTN_MIDDLE, (b & 4) != 0);
  mouse_b = b;
}

static int uart_getc_nowait(void) { return -1; }   // no serial line: the keys come through input.h

// A screen opened: the keys hit before are not the new program's.
static void input_flush(void) {
  usb_poll();
  memset(in_down, 0, sizeof in_down);
  memset(in_hit, 0, sizeof in_hit);
  memset(in_snap, 0, sizeof in_snap);
  in_last_key = 0;
  in_chars_tail = in_chars_head;
}

// FB-FLIP: as on the boards, then in a 16-bit mode the rows of the copy that
// changed since the last flip go to the video memory, 5-6-5.
static void dos_fb_flip(void) {
  fb_flip();
  if (vid_bpp != 16 || !vid_shadow || !vid_graphics) return;
  for (uint32_t y = 0; y < vid_ph; y++) {
    const uint32_t *src = vid_shadow + (size_t) y * vid_pw;
    uint32_t *old = vid_prev + (size_t) y * vid_pw;
    if (!vid_all && !memcmp(src, old, (size_t) vid_pw * 4)) continue;
    memcpy(old, src, (size_t) vid_pw * 4);
    uint16_t *dst = (uint16_t *) (vid_out + (size_t) y * vid_pitch16);
    for (uint32_t x = 0; x < vid_pw; x++) {
      uint32_t c = src[x];
      dst[x] = (uint16_t) (((c >> 8) & 0xF800) | ((c >> 5) & 0x07E0) | ((c >> 3) & 0x001F));
    }
  }
  vid_all = 0;
}
#define fb_flip dos_fb_flip

// FB-OPEN: a 16-bit mode only when no 32-bit mode holds the screen even at
// zoom 1 (2048's 484x640 on DOSBox's S3); else ports/baremetal/sdl2.fs lowers
// the zoom until a 32-bit mode holds it: true colors, and no conversion at
// each flip, which is slow at a large zoom.
static cell_t dos_fb_open(cell_t w, cell_t h, cell_t zoom) {
  struct vbe_mode mi;
  vid_allow16 = w > 0 && h > 0 && vbe_find((uint32_t) w, (uint32_t) h, 32, &mi) < 0;
  return fb_open(w, h, zoom);
}
#define fb_open dos_fb_open
