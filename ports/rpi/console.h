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

// The Forth console on the screen: a text terminal of 80x30 cells (the 8x8
// font, each glyph doubled in height, then CON_ZOOM times larger) drawn
// straight into the framebuffer of screen.h, 1280x960. What RAW-TYPE writes to the UART goes here too, and
// the keys of the keyboard (input.h) already reach KEY, so the QEMU window
// (or an HDMI screen with a USB keyboard) is a complete console. Without a
// framebuffer (virt without -device ramfb) nothing changes: serial only.
// The sdl2 word SCREEN takes the framebuffer over (the console goes quiet);
// CLOSE-SCREEN gives it back. Included by main.c, after screen.h.
//
// Escape sequences understood (what common/lineedit.fs and ansi.fs send):
// CSI A B C D H f J K m (colors 30-37 40-47 90-97 100-107, 0 1 7 27 39 49).

#ifndef CON_ZOOM
# define CON_ZOOM 2                  // the default: 1 is 640x480, 2 is 1280x960
#endif
static int con_zoom = CON_ZOOM;      // CONSOLE-ZOOM changes it
#define CON_W (640 * con_zoom)
#define CON_H (480 * con_zoom)
#define CON_CW (8 * con_zoom)
#define CON_CH (16 * con_zoom)
#define CON_COLS (CON_W / CON_CW)
#define CON_ROWS (CON_H / CON_CH)

static int con_col, con_row, con_shown;      // cursor cell, drawn (XORed) or not
static int con_fgi = 7, con_bgi = 0, con_bold, con_rev;
static int con_esc, con_npar, con_par[4];    // 0, 1: after ESC, 2: in a CSI
static int con_d0 = 1 << 30, con_d1 = -1;      // pixel rows written, not yet cleaned
static uint64_t con_synced;

static const uint32_t con_pal[16] = {
  0x000000, 0xAA0000, 0x00AA00, 0xAA5500, 0x0000AA, 0xAA00AA, 0x00AAAA, 0xC0C0C0,
  0x555555, 0xFF5555, 0x55FF55, 0xFFFF55, 0x5555FF, 0xFF55FF, 0x55FFFF, 0xFFFFFF,
};

static uint32_t con_hw(uint32_t c) {
  return fb_rgb ? ((c & 255) << 16) | (c & 0xFF00) | ((c >> 16) & 255) : c;
}

static volatile uint32_t *con_px(int x, int y) {
  return (volatile uint32_t *) ((volatile uint8_t *) fb_mem + (uint64_t) y * fb_pitch) + x;
}

static void con_mark(int y0, int y1) {
  if (y0 < con_d0) con_d0 = y0;
  if (y1 > con_d1) con_d1 = y1;
}

// Makes the pixels written visible: the GPU / QEMU read memory, not our cache.
static void con_sync(void) {
  if (!con_on || con_d1 < con_d0) return;
  dcache_clean((const void *) con_px(0, con_d0), (uint64_t) (con_d1 - con_d0 + 1) * fb_pitch);
  con_d0 = 1 << 30; con_d1 = -1;
  con_synced = ms_ticks();
}

static void con_colors(uint32_t *fg, uint32_t *bg) {
  int f = con_fgi + (con_bold && con_fgi < 8 ? 8 : 0), b = con_bgi;
  if (con_rev) { int t = f; f = b; b = t; }
  *fg = con_hw(con_pal[f]);
  *bg = con_hw(con_pal[b]);
}

static void con_fill(int x, int y, int w, int h, uint32_t c) {
  for (int j = y; j < y + h; j++) {
    volatile uint32_t *p = con_px(x, j);
    for (int i = 0; i < w; i++) p[i] = c;
  }
  con_mark(y, y + h - 1);
}

static void con_cursor(void) {      // toggles the block at the cursor
  if (con_col >= CON_COLS) return;
  for (int j = 0; j < CON_CH; j++) {
    volatile uint32_t *p = con_px(con_col * CON_CW, con_row * CON_CH + j);
    for (int i = 0; i < CON_CW; i++) p[i] ^= 0x00FFFFFF;
  }
  con_mark(con_row * CON_CH, con_row * CON_CH + CON_CH - 1);
  con_shown = !con_shown;
}

static void con_glyph(const unsigned char *g) {
  uint32_t fg, bg;
  con_colors(&fg, &bg);
  for (int j = 0; j < CON_CH; j++) {
    volatile uint32_t *p = con_px(con_col * CON_CW, con_row * CON_CH + j);
    unsigned bits = g[j / (2 * con_zoom)];
    for (int i = 0; i < CON_CW; i++) p[i] = (bits >> (i / con_zoom)) & 1 ? fg : bg;
  }
  con_mark(con_row * CON_CH, con_row * CON_CH + CON_CH - 1);
}

static void con_newline(void) {
  con_col = 0;
  if (++con_row < CON_ROWS) return;
  con_row = CON_ROWS - 1;
  memmove((void *) fb_mem, (const void *) con_px(0, CON_CH), (size_t) (CON_H - CON_CH) * fb_pitch);
  uint32_t fg, bg;
  con_colors(&fg, &bg);
  con_fill(0, CON_H - CON_CH, CON_W, CON_CH, bg);
  con_mark(0, CON_H - 1);
}

// Clears from cell (c0, r0) to cell (c1, r1), in reading order.
static void con_clear(int c0, int r0, int c1, int r1) {
  uint32_t fg, bg;
  con_colors(&fg, &bg);
  for (int r = r0; r <= r1; r++) {
    int a = r == r0 ? c0 : 0, b = r == r1 ? c1 : CON_COLS - 1;
    if (b >= a) con_fill(a * CON_CW, r * CON_CH, (b - a + 1) * CON_CW, CON_CH, bg);
  }
}

static int con_arg(int i, int def) { return i < con_npar && con_par[i] > 0 ? con_par[i] : def; }
static int con_clamp(int v, int hi) { return v < 0 ? 0 : v > hi ? hi : v; }

static void con_sgr(int p) {
  if (p == 0) { con_fgi = 7; con_bgi = 0; con_bold = con_rev = 0; }
  else if (p == 1) con_bold = 1;
  else if (p == 7) con_rev = 1;
  else if (p == 22) con_bold = 0;
  else if (p == 27) con_rev = 0;
  else if (p >= 30 && p <= 37) con_fgi = p - 30;
  else if (p == 39) con_fgi = 7;
  else if (p >= 40 && p <= 47) con_bgi = p - 40;
  else if (p == 49) con_bgi = 0;
  else if (p >= 90 && p <= 97) { con_fgi = p - 90 + 8; con_bold = 0; }
  else if (p >= 100 && p <= 107) con_bgi = p - 100 + 8;
}

static void con_csi(int final) {
  int c = con_clamp(con_col, CON_COLS - 1), mode = con_npar ? con_par[0] : 0;
  switch (final) {
    case 'A': con_row = con_clamp(con_row - con_arg(0, 1), CON_ROWS - 1); break;
    case 'B': con_row = con_clamp(con_row + con_arg(0, 1), CON_ROWS - 1); break;
    case 'C': con_col = con_clamp(c + con_arg(0, 1), CON_COLS - 1); break;
    case 'D': con_col = con_clamp(c - con_arg(0, 1), CON_COLS - 1); break;
    case 'H': case 'f':
      con_row = con_clamp(con_arg(0, 1) - 1, CON_ROWS - 1);
      con_col = con_clamp(con_arg(1, 1) - 1, CON_COLS - 1);
      break;
    case 'J':
      if (mode == 0) con_clear(c, con_row, CON_COLS - 1, CON_ROWS - 1);
      else if (mode == 1) con_clear(0, 0, c, con_row);
      else con_clear(0, 0, CON_COLS - 1, CON_ROWS - 1);
      break;
    case 'K':
      if (mode == 0) con_clear(c, con_row, CON_COLS - 1, con_row);
      else if (mode == 1) con_clear(0, con_row, c, con_row);
      else con_clear(0, con_row, CON_COLS - 1, con_row);
      break;
    case 'm':
      if (!con_npar) con_sgr(0);
      for (int i = 0; i < con_npar; i++) con_sgr(con_par[i]);
      break;
  }
}

static void con_write(const unsigned char *s, cell_t n) {
  if (!con_on) return;
  if (con_shown) con_cursor();
  const unsigned char *end = s + n;
  while (s < end) {
    unsigned c = *s;
    if (con_esc == 1) {
      s++;
      if (c == '[') { con_esc = 2; con_npar = 0; con_par[0] = 0; } else con_esc = 0;
    } else if (con_esc == 2) {
      s++;
      if (c >= '0' && c <= '9') { if (con_par[con_npar] < 10000) con_par[con_npar] = con_par[con_npar] * 10 + (int) c - '0'; }
      else if (c == ';') { if (con_npar < 3) con_par[++con_npar] = 0; }
      else if (c >= 0x40 && c <= 0x7E) { con_npar++; con_csi((int) c); con_esc = 0; }
      else if (c < 0x20 || c > 0x7E) con_esc = 0;
    } else if (c < 0x20 || c == 127) {
      s++;
      switch (c) {
        case 27: con_esc = 1; break;
        case '\n': con_newline(); break;
        case '\r': con_col = 0; break;
        case 8: case 127: if (con_col > 0) con_col--; break;
        case 9: con_col = (con_col + 8) & ~7; if (con_col >= CON_COLS) con_newline(); break;
      }
    } else {
      const unsigned char *g = text_glyph(&s, end);
      if (con_col >= CON_COLS) con_newline();
      con_glyph(g);
      con_col++;
    }
  }
  con_cursor();
  if (ms_ticks() - con_synced >= 20) con_sync();   // else when the console waits for a key
}

// Opens the framebuffer for the console, if there is one.
static void con_init(void) {
  con_on = 0;
  if (fb_hw_open(CON_W, CON_H)) return;
  con_on = 1;
  con_esc = con_shown = 0;
  con_fgi = 7; con_bgi = 0; con_bold = con_rev = 0;
  con_col = con_row = 0;
  con_clear(0, 0, CON_COLS - 1, CON_ROWS - 1);
  con_cursor();
  con_sync();
}

// CONSOLE-ZOOM ( n -- ): 1 (640x480) to 3 (1920x1440); the screen is cleared.
static void con_set_zoom(cell_t n) {
  if (n < 1 || n > 3) return;
  con_zoom = (int) n;
  con_d0 = 1 << 30; con_d1 = -1;
  if (con_on) con_init();
}
