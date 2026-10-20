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

// Terminals, the scheduler. Each terminal is a Forth of its own: another
// forth_init on another heap (the whole state of the interpreter is in
// g_sys and that heap), with a screen of its own: the console (con_shadow,
// cursor, colors), the graphics layer or the program's screen (fb_back...)
// and the images. They are cooperative: forth_run returns to the main loop at
// each pause (key and ms wait with it), where term_next swaps the screen
// variables of the terminal that ran for those of the next one. What stands
// for the one physical screen (con_front, fb_front, fb_mem) is shared and
// written by the terminal that has the focus only (TERM_FOCUSED); a switch of
// focus redraws the screen of the new one. The keyboard, the mouse and the
// serial output belong to the focused terminal too.
// A computation that never pauses blocks the others.

#define TERM_HEAP (32 * 1024 * 1024)      // the heap of the terminals after the first

#define TERM_FIELDS(F) \
  F(uint32_t *, fb_back) F(int, fb_w) F(int, fb_h) F(int, fb_zoom) F(uint32_t, fb_color) F(int, fb_alpha) \
  F(int, fb_gfx) F(int, fb_gfx_dirty) F(int, con_on) F(int, con_col) F(int, con_row) F(int, con_shown) \
  F(int, con_fgi) F(int, con_bgi) F(int, con_bold) F(int, con_rev) F(int, con_ul) \
  F(int64_t, con_fgc) F(int64_t, con_bgc) F(int, con_esc) F(int, con_npar) F(int, con_d0) F(int, con_d1) \
  F(uint32_t *, con_shadow) F(int, con_nparts)

static struct term {
  int live, dying, zoom_at;
  G_SYS *sys;
#define F(t, n) t n;
  TERM_FIELDS(F)
#undef F
  int con_par[8];
  unsigned char con_part[4];
  __typeof__(images) images;
} terms[TERM_MAX];

static void term_save(int i) {
  struct term *t = &terms[i];
  t->sys = g_sys;
#define F(ty, n) t->n = n;
  TERM_FIELDS(F)
#undef F
  memcpy(t->con_par, con_par, sizeof con_par);
  memcpy(t->con_part, con_part, sizeof con_part);
  memcpy(t->images, images, sizeof images);
  t->zoom_at = con_zoom;
}

static void term_load(int i) {
  struct term *t = &terms[i];
  term_cur = i;
  g_sys = t->sys;
#define F(ty, n) n = t->n;
  TERM_FIELDS(F)
#undef F
  memcpy(con_par, t->con_par, sizeof con_par);
  memcpy(con_part, t->con_part, sizeof con_part);
  memcpy(images, t->images, sizeof images);
  con_map_w = 0;
  if (con_on && t->zoom_at != con_zoom) con_init();     // another terminal changed the console zoom
}

// A blank screen state, for a terminal that starts.
static void term_blank(void) {
  fb_back = 0; fb_w = fb_h = fb_zoom = 0; fb_color = 0; fb_alpha = 255; fb_gfx = fb_gfx_dirty = 0;
  con_on = 0; con_col = con_row = con_shown = 0; con_fgi = 7; con_bgi = 0; con_bold = con_rev = con_ul = 0;
  con_fgc = con_bgc = -1; con_esc = con_npar = 0; con_d0 = 1 << 30; con_d1 = -1; con_shadow = 0; con_nparts = 0;
  memset(con_par, 0, sizeof con_par);
  memset(con_part, 0, sizeof con_part);
  memset(images, 0, sizeof images);
}

// The running terminal gets the screen: draw it all again.
static void term_present(void) {
  con_map_w = 0;
  fb_front_ok = 0;
  input_flush();
  in_chars_tail = in_chars_head;
  if (con_on) {
    if (fb_hw_open(CON_W, CON_H)) return;
    con_force = 1; con_d0 = 0; con_d1 = CON_H - 1; fb_gfx_dirty = 1;
    con_sync();
  } else if (fb_w && fb_back) {
    if (fb_hw_open(fb_w * fb_zoom, fb_h * fb_zoom)) return;
    fb_flip();
  }
}

// A new terminal: it takes the focus, shows its prompt when it runs.
static int term_make(int n) {
  void *heap = malloc(TERM_HEAP);
  if (!heap) return -1;
  term_save(term_cur);
  term_blank();
  term_cur = term_foc = n;
  con_init();
  static char arg0[] = "lugforth";
  static char *argv[] = { arg0, 0 };
  forth_init(1, argv, heap, TERM_HEAP, boot, sizeof(boot));
  terms[n].live = 1; terms[n].dying = 0;
  term_nlive++;
  return 0;
}

// The first terminal, once its Forth exists.
static void term_boot(void) {
  terms[0].live = 1;
  term_nlive = 1;
  term_cur = term_foc = 0;
}

// BYE in another terminal than the first closes it (the first one powers off).
static int term_bye(void) {
  if (term_cur == 0) return 0;
  terms[term_cur].dying = 1;
  return 1;
}

static void term_free(int i) {
  struct term *t = &terms[i];
  free(t->con_shadow);
  free(t->fb_back);
  free(t->sys);            // g_sys is the start of the heap
  memset(t, 0, sizeof *t);
  term_nlive--;
}

// Between two slices of Forth: close a terminal that ended, switch the focus,
// and give the next terminal its turn.
static void term_next(void) {
  if (term_nlive < 2 && term_want < 0 && !terms[term_cur].dying) return;
  int want = term_want;
  term_want = -1;
  if (terms[term_cur].dying) {
    int was_foc = term_foc == term_cur;
    term_save(term_cur);                            // its last screen buffers, to free them
    term_free(term_cur);
    term_load(0);
    if (was_foc) { term_foc = 0; term_present(); }
    return;
  }
  term_save(term_cur);
  if (want >= 0 && want != term_foc) {
    if (!terms[want].live) {
      if (term_make(want) == 0) return;             // it runs now, and has the screen
      want = -1;
    }
    if (want >= 0) {
      term_load(want);
      term_foc = want;
      term_present();
      return;
    }
  }
  int n = term_cur;                                 // the next live terminal
  do { n = (n + 1) % TERM_MAX; } while (!terms[n].live);
  term_load(n);
}

// At each pause: a program that never reads the keyboard must not hold the screen for ever.
static void term_poll(void) {
  if (TERM_FOCUSED()) input_poll();
}
