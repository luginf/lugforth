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

// The screen behind ports/rpi/sdl2.fs: the HDMI framebuffer the Pi firmware
// hands out through the mailbox (QEMU's raspi3b emulates it and shows it in
// its window). Drawing goes to a back buffer of width x height logical
// pixels, $rrggbb words; FB-FLIP copies it to the framebuffer, each pixel
// as a zoom x zoom square, in the pixel order the firmware chose. Included
// by main.c, after the UART and the MMU code.

#ifdef PERIPHERAL_BASE
# define MBOX_BASE (PERIPHERAL_BASE + 0xB880UL)
#endif

static uint32_t *fb_back;         // logical pixels, $rrggbb
static int fb_w, fb_h, fb_zoom;   // logical size, scale
static volatile uint32_t *fb_mem; // the framebuffer
static uint32_t fb_pitch;         // bytes per framebuffer row
static int fb_rgb;                // 1: byte 0 is red; 0: byte 0 is blue
static uint32_t fb_color;         // pen, $rrggbb
static int fb_alpha = 255;

static void dcache_clean(const void *p, uint64_t n) {
  uint64_t a = (uint64_t) p & ~63UL, end = (uint64_t) p + n;
  for (; a < end; a += 64) __asm__ volatile("dc civac, %0" :: "r"(a) : "memory");
  __asm__ volatile("dsb sy" ::: "memory");
}

#ifdef MBOX_BASE
// One property-channel call (channel 8). The buffer is cleaned from the
// data cache before and after: the GPU reads and writes memory directly.
// 0xC0000000 is the uncached alias of RAM as the GPU sees it.
static int mbox_call(volatile uint32_t *buf) {
  uint32_t a = (uint32_t) (uint64_t) buf;
  dcache_clean((const void *) buf, buf[0]);
  while (REG(MBOX_BASE + 0x38) & 0x80000000u) {}        // MBOX1 STATUS: full
  REG(MBOX_BASE + 0x20) = (a | 0xC0000000u) | 8;        // MBOX1 WRITE
  for (;;) {
    while (REG(MBOX_BASE + 0x18) & 0x40000000u) {}      // MBOX0 STATUS: empty
    uint32_t r = REG(MBOX_BASE + 0x00);                 // MBOX0 READ
    if ((r & 15) == 8) break;
  }
  dcache_clean((const void *) buf, buf[0]);
  return buf[1] == 0x80000000u;
}
#endif

// 0 on success, -1 if there is no framebuffer (QEMU virt, or a refusal).
static cell_t fb_open(cell_t w, cell_t h, cell_t zoom) {
  if (w <= 0 || h <= 0 || zoom <= 0) return -1;
#ifndef MBOX_BASE
  return -1;
#else
  static volatile uint32_t m[36] __attribute__((aligned(16)));
  uint32_t pw = (uint32_t) (w * zoom), ph = (uint32_t) (h * zoom);
  uint32_t i = 0;
  m[i++] = 0;                  // size, below
  m[i++] = 0;                  // request
  m[i++] = 0x48003; m[i++] = 8; m[i++] = 0; m[i++] = pw; m[i++] = ph;  // physical size
  m[i++] = 0x48004; m[i++] = 8; m[i++] = 0; m[i++] = pw; m[i++] = ph;  // virtual size
  m[i++] = 0x48009; m[i++] = 8; m[i++] = 0; m[i++] = 0; m[i++] = 0;    // virtual offset
  m[i++] = 0x48005; m[i++] = 4; m[i++] = 0; m[i++] = 32;               // depth
  m[i++] = 0x48006; m[i++] = 4; m[i++] = 0; m[i++] = 1;                // pixel order: RGB
  m[i++] = 0x40001; m[i++] = 8; m[i++] = 0; m[i++] = 16; m[i++] = 0;   // allocate
  m[i++] = 0x40008; m[i++] = 4; m[i++] = 0; m[i++] = 0;                // pitch
  m[i++] = 0;                  // end tag
  m[0] = i * 4;
  if (!mbox_call(m)) return -1;
  if (m[20] != 32 || m[28] == 0) return -1;              // depth, address
  uint32_t *back = (uint32_t *) realloc(fb_back, (size_t) (w * h) * 4);
  if (!back) return -1;
  fb_back = back;
  fb_rgb = m[24];
  fb_mem = (volatile uint32_t *) (uint64_t) (m[28] & 0x3FFFFFFFu);
  fb_pitch = m[33];
  fb_w = (int) w; fb_h = (int) h; fb_zoom = (int) zoom;
  for (int k = 0; k < fb_w * fb_h; k++) fb_back[k] = 0;
  return 0;
#endif
}

static void fb_set_color(cell_t rgb, cell_t alpha) {
  fb_color = (uint32_t) rgb & 0xFFFFFF;
  fb_alpha = alpha < 0 ? 0 : alpha > 255 ? 255 : (int) alpha;
}

static uint32_t fb_blend(uint32_t d) {
  uint32_t a = (uint32_t) fb_alpha, b = 255 - a, s = fb_color;
  uint32_t r = (((s >> 16) & 255) * a + ((d >> 16) & 255) * b) / 255;
  uint32_t g = (((s >> 8) & 255) * a + ((d >> 8) & 255) * b) / 255;
  uint32_t l = ((s & 255) * a + (d & 255) * b) / 255;
  return (r << 16) | (g << 8) | l;
}

static void fb_box(cell_t x, cell_t y, cell_t w, cell_t h) {
  if (!fb_back) return;
  cell_t x2 = x + w, y2 = y + h;
  if (x < 0) x = 0;
  if (y < 0) y = 0;
  if (x2 > fb_w) x2 = fb_w;
  if (y2 > fb_h) y2 = fb_h;
  for (cell_t j = y; j < y2; j++) {
    uint32_t *row = fb_back + j * fb_w;
    if (fb_alpha == 255) {
      for (cell_t i = x; i < x2; i++) row[i] = fb_color;
    } else if (fb_alpha) {
      for (cell_t i = x; i < x2; i++) row[i] = fb_blend(row[i]);
    }
  }
}

// Lines: Bresenham, the all-octant form of ports/web/sdl2.fs, clipped to
// the screen without changing which pixels it draws. Along the main axis
// it moves one pixel per step; after k steps it has moved
//   minor(k) = floor((2*k*small + big) / (2*big))
// pixels on the other axis (big, small: the two |deltas|). minor(k) only
// grows, so the visible steps are one interval, found by binary search:
// a line far off the screen costs a few dozen steps, not its length.
// Horizontal and vertical lines are boxes. A Python copy of this code drew
// the same pixels as the plain loop for 120000 random lines, ends on and
// off a 40x30 screen; tools/rpi_qemu_test.py compares the real screen too.
static void fb_plot(cell_t x, cell_t y) {
  uint32_t *p = fb_back + y * fb_w + x;
  *p = fb_alpha == 255 ? fb_color : fb_blend(*p);
}

static cell_t line_minor(cell_t k, cell_t big, cell_t small) {
  return (cell_t) (((__int128) 2 * k * small + big) / ((__int128) 2 * big));
}

// The steps k in [lo, hi] at which start + dir * f(k) is in [0, n - 1],
// f non-decreasing: f(k) = k (major) or line_minor (minor).
static void line_window(cell_t start, cell_t dir, cell_t n, cell_t steps,
                        int major, cell_t big, cell_t small,
                        cell_t *lo, cell_t *hi) {
  __int128 fmin = dir > 0 ? -(__int128) start : (__int128) start - (n - 1);
  __int128 fmax = dir > 0 ? (__int128) n - 1 - start : (__int128) start;
  if (major) {
    if (fmin > *lo) *lo = fmin <= steps ? (cell_t) fmin : steps + 1;
    if (fmax < *hi) *hi = fmax < 0 ? -1 : (cell_t) fmax;
    return;
  }
  cell_t a = 0, b = steps + 1;          // first k with f(k) >= fmin
  while (a < b) {
    cell_t m = a + (b - a) / 2;
    if (line_minor(m, big, small) >= fmin) b = m; else a = m + 1;
  }
  if (a > *lo) *lo = a;
  a = -1; b = steps;                    // last k with f(k) <= fmax
  while (a < b) {
    cell_t m = b - (b - a) / 2;
    if (line_minor(m, big, small) <= fmax) a = m; else b = m - 1;
  }
  if (a < *hi) *hi = a;
}

static void fb_line(cell_t x1, cell_t y1, cell_t x2, cell_t y2) {
  if (!fb_back) return;
  if (y1 == y2) { fb_box(x1 < x2 ? x1 : x2, y1, (x1 < x2 ? x2 - x1 : x1 - x2) + 1, 1); return; }
  if (x1 == x2) { fb_box(x1, y1 < y2 ? y1 : y2, 1, (y1 < y2 ? y2 - y1 : y1 - y2) + 1); return; }
  cell_t dx = x2 > x1 ? x2 - x1 : x1 - x2, sx = x1 < x2 ? 1 : -1;
  cell_t dy = y2 > y1 ? y1 - y2 : y2 - y1, sy = y1 < y2 ? 1 : -1;   // dy <= 0
  int xmajor = dx >= -dy;
  cell_t big = xmajor ? dx : -dy, small = xmajor ? -dy : dx;
  cell_t lo = 0, hi = big;
  line_window(x1, sx, fb_w, big, xmajor, big, small, &lo, &hi);
  line_window(y1, sy, fb_h, big, !xmajor, big, small, &lo, &hi);
  if (lo > hi) return;
  // The state of the loop at step lo: err = dx + dy + (x steps)*dy + (y steps)*dx.
  cell_t m = line_minor(lo, big, small);
  cell_t xs = xmajor ? lo : m, ys = xmajor ? m : lo;
  cell_t x = x1 + sx * xs, y = y1 + sy * ys;
  cell_t err = (cell_t) ((__int128) dx + dy + (__int128) xs * dy + (__int128) ys * dx);
  for (cell_t k = lo; ; k++) {
    fb_plot(x, y);
    if (k == hi) break;
    cell_t e2 = 2 * err;
    if (e2 >= dy) { err += dy; x += sx; }
    if (e2 <= dx) { err += dx; y += sy; }
  }
}

static cell_t fb_pixel(cell_t x, cell_t y) {
  if (!fb_back || x < 0 || y < 0 || x >= fb_w || y >= fb_h) return 0;
  return fb_back[y * fb_w + x];
}

static void fb_flip(void) {
  if (!fb_back) return;
  for (int y = 0; y < fb_h; y++) {
    const uint32_t *src = fb_back + y * fb_w;
    for (int zy = 0; zy < fb_zoom; zy++) {
      volatile uint32_t *dst = (volatile uint32_t *)
          ((volatile uint8_t *) fb_mem + (uint64_t) (y * fb_zoom + zy) * fb_pitch);
      for (int x = 0; x < fb_w; x++) {
        uint32_t c = src[x];
        if (fb_rgb) c = ((c & 255) << 16) | (c & 0xFF00) | ((c >> 16) & 255);
        for (int zx = 0; zx < fb_zoom; zx++) *dst++ = c;
      }
    }
  }
  dcache_clean((const void *) fb_mem, (uint64_t) fb_h * fb_zoom * fb_pitch);
}
