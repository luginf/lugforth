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

// Lugforth for DOS: a 32-bit protected mode program built with DJGPP
// (i586-pc-msdosdjgpp-gcc), run with a DPMI host (CWSDPMI.EXE beside it on
// plain DOS; DOSBox, FreeDOS with HDPMI and Windows 9x have their own).
// Needs a 386 and a few megabytes of memory. The prompt in text mode: the
// console is the screen through conio, the ANSI codes of the line editor,
// the pager and the editors are turned into conio calls here, so no
// ANSI.SYS is needed; with stdin or stdout redirected (lugforth < in > out)
// the bytes go through as they are. Files: files.h. Graphics, the keyboard
// by interrupt and the mouse while a screen is open: video.h. Sound: sb16.h.

#include <conio.h>
#include <crt0.h>
#include <dir.h>
#include <dos.h>
#include <dpmi.h>
#include <errno.h>
#include <go32.h>
#include <fcntl.h>
#include <io.h>
#include <math.h>
#include <pc.h>
#include <setjmp.h>
#include <signal.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/exceptn.h>
#include <sys/farptr.h>
#include <sys/movedata.h>
#include <sys/nearptr.h>
#include <sys/stat.h>
#include <time.h>
#include <unistd.h>

// DJGPP's libm has no fmin and fmax (C99), which common/floats.h uses.
#define fmin(a, b) dos_fmin(a, b)
#define fmax(a, b) dos_fmax(a, b)
static double dos_fmin(double a, double b) { return a != a ? b : b != b ? a : a < b ? a : b; }
static double dos_fmax(double a, double b) { return a != a ? b : b != b ? a : a > b ? a : b; }

#include "common/tier0_opcodes.h"
#include "common/tier1_opcodes.h"
#include "common/tier2_opcodes.h"
#include "common/floats.h"
#include "common/calls.h"

#define HEAP_SIZE (4 * 1024 * 1024)
#define STACK_CELLS (8 * 1024)
#define BOARD_NAME "dos"

int _crt0_startup_flags = _CRT0_FLAG_NONMOVE_SBRK;   // the heap stays where it is: video.h keeps near pointers
unsigned _stklen = 512 * 1024;                     // the C stack (DJGPP's default is 256K)
char **__crt0_glob_function(char *arg) { (void) arg; return 0; }   // no wildcard expansion of the arguments

// ---- time: uclock counts the PC's timer at 1.19 MHz ----
static uint64_t us_ticks(void) { return (uint64_t) uclock() * 1000000ull / UCLOCKS_PER_SEC; }
static uint64_t ms_ticks(void) { return us_ticks() / 1000; }

// ---- the console: conio on the text screen, with an ANSI interpreter ----
static int con_tty, key_tty;
static int con_state, con_np, con_par[8], con_priv;
static int con_fg = 7, con_bg = 0, con_bold, con_rev;

static void con_attr(void) {
  int fg = con_fg | (con_bold ? 8 : 0), bg = con_bg;
  if (con_rev) { int t = fg & 7; fg = bg; bg = t; }
  textattr((bg << 4) | fg);
}

// ANSI's color order (black red green yellow blue magenta cyan white) in the PC's.
static const int ansi_pc[8] = { 0, 4, 2, 6, 1, 5, 3, 7 };

static int color256(int n) {                       // xterm's 256 colors to the 16 of the PC
  if (n < 8) return ansi_pc[n];
  if (n < 16) return ansi_pc[n - 8] | 8;
  if (n >= 232) { int g = n - 232; return g < 6 ? 0 : g < 12 ? 8 : g < 20 ? 7 : 15; }
  n -= 16;
  int r = n / 36, g = (n / 6) % 6, b = n % 6, hi = r > 3 || g > 3 || b > 3;
  int c = (r > 1 ? 4 : 0) | (g > 1 ? 2 : 0) | (b > 1 ? 1 : 0);
  if (!c) return r + g + b > 2 ? 8 : 0;
  return c | (hi ? 8 : 0);
}

static void con_sgr(void) {
  if (con_np == 0) con_par[con_np++] = 0;
  for (int i = 0; i < con_np; i++) {
    int p = con_par[i];
    if (p == 0) { con_fg = 7; con_bg = 0; con_bold = con_rev = 0; }
    else if (p == 1) con_bold = 1;
    else if (p == 22) con_bold = 0;
    else if (p == 7) con_rev = 1;
    else if (p == 27) con_rev = 0;
    else if (p >= 30 && p <= 37) con_fg = ansi_pc[p - 30];
    else if (p == 39) con_fg = 7;
    else if (p >= 40 && p <= 47) con_bg = ansi_pc[p - 40];
    else if (p == 49) con_bg = 0;
    else if (p >= 90 && p <= 97) con_fg = ansi_pc[p - 90] | 8;
    else if (p >= 100 && p <= 107) con_bg = ansi_pc[p - 100];
    else if ((p == 38 || p == 48) && i + 2 < con_np && con_par[i + 1] == 5) {
      int c = color256(con_par[i + 2]);
      if (p == 38) con_fg = c; else con_bg = c & 7;
      i += 2;
    }
  }
  con_attr();
}

static void con_csi(int c) {
  int n = con_np ? con_par[0] : 0, n1 = n ? n : 1;
  int x = wherex(), y = wherey(), w = ScreenCols(), h = ScreenRows();
  switch (c) {
    case 'A': gotoxy(x, y - n1 < 1 ? 1 : y - n1); break;
    case 'B': gotoxy(x, y + n1 > h ? h : y + n1); break;
    case 'C': gotoxy(x + n1 > w ? w : x + n1, y); break;
    case 'D': gotoxy(x - n1 < 1 ? 1 : x - n1, y); break;
    case 'H': case 'f': {
      int r = con_np > 0 && con_par[0] ? con_par[0] : 1, col = con_np > 1 && con_par[1] ? con_par[1] : 1;
      gotoxy(col > w ? w : col, r > h ? h : r);
      break;
    }
    case 'J':
      if (n == 2 || n == 3) { clrscr(); break; }
      clreol();
      for (int r = y + 1; r <= h; r++) { gotoxy(1, r); clreol(); }
      gotoxy(x, y);
      break;
    case 'K': clreol(); break;
    case 'm': con_sgr(); break;
    case 'h': case 'l':
      if (con_priv && n == 25) _setcursortype(c == 'h' ? _NORMALCURSOR : _NOCURSOR);
      break;
  }
}

static void con_put(int c) {
  switch (con_state) {
    case 0:
      if (c == 27) { con_state = 1; return; }
      if (c == '\n') { putch('\r'); putch('\n'); return; }
      if (c == 7) return;
      if (c == 8) { if (wherex() > 1) gotoxy(wherex() - 1, wherey()); return; }
      putch(c);
      return;
    case 1:
      if (c == '[') { con_state = 2; con_np = 0; con_par[0] = 0; con_priv = 0; return; }
      if (c == ']') { con_state = 3; return; }
      con_state = 0;                               // ESC D, ESC M...: left out
      return;
    case 2:
      if (c == '?') { con_priv = 1; return; }
      if (c >= '0' && c <= '9') {
        if (con_np == 0) con_np = 1;
        con_par[con_np - 1] = con_par[con_np - 1] * 10 + (c - '0');
        return;
      }
      if (c == ';') { if (con_np == 0) con_np = 1; if (con_np < 8) con_par[con_np++] = 0; return; }
      con_state = 0;
      con_csi(c);
      return;
    case 3:                                        // a title (ESC ] ... BEL): left out
      if (c == 7 || c == 27) con_state = 0;
      return;
  }
}

static void con_write(const uint8_t *b, cell_t n) {
  if (!con_tty) { if (write(1, b, n) < 0) { /* nowhere to say it */ } return; }
  for (cell_t i = 0; i < n; i++) con_put(b[i]);
}

static cell_t screen_char(cell_t col, cell_t row) {   // the character at col row (0-based) of the text screen
  int ch = 0, attr = 0;
  if (col >= 0 && row >= 0 && col < ScreenCols() && row < ScreenRows()) ScreenGetChar(&ch, &attr, col, row);
  return ch;
}

#include "ports/dos/video.h"
#include "ports/dos/sb16.h"

// ---- the keyboard: BIOS keys, the special ones turned into the ANSI
// sequences of a terminal; or stdin when it is a file ----
static char key_queue[16];
static int key_head, key_tail;

static void key_push(const char *s) {
  for (; *s; s++) { key_queue[key_tail] = *s; key_tail = (key_tail + 1) % sizeof key_queue; }
}

static void key_special(int scan) {
  switch (scan) {
    case 72: key_push("\033[A"); break;
    case 80: key_push("\033[B"); break;
    case 77: key_push("\033[C"); break;
    case 75: key_push("\033[D"); break;
    case 71: key_push("\033[H"); break;
    case 79: key_push("\033[F"); break;
    case 73: key_push("\033[5~"); break;
    case 81: key_push("\033[6~"); break;
    case 82: key_push("\033[2~"); break;
    case 83: key_push("\033[3~"); break;
    case 115: key_push("\033[1;5D"); break;        // Ctrl+Left, Ctrl+Right, Ctrl+Home, Ctrl+End
    case 116: key_push("\033[1;5C"); break;
    case 119: key_push("\033[1;5H"); break;
    case 117: key_push("\033[1;5F"); break;
    case 59: key_push("\033OP"); break;            // F1..F4
    case 60: key_push("\033OQ"); break;
    case 61: key_push("\033OR"); break;
    case 62: key_push("\033OS"); break;
  }
}

static int key_get(void) {                         // a byte, or -1
  if (key_head != key_tail) {
    int c = (unsigned char) key_queue[key_head];
    key_head = (key_head + 1) % sizeof key_queue;
    return c;
  }
  if (kbd_installed) { usb_poll(); return in_get_char(); }   // a screen is open: input.h has the keys
  if (!key_tty) {
    unsigned char c;
    int r = read(0, &c, 1);
    if (r == 1) return c;
    exit(0);                                       // the end of the file given as input: as bye
  }
  if (!kbhit()) return -1;
  int c = getch();
  if (c == 0 || c == 0xe0) { key_special(getch()); return key_get(); }
  return c;
}

static int getchar_timeout_us(cell_t us) {
  uint64_t end = us_ticks() + (uint64_t) us;
  for (;;) {
    int c = key_get();
    if (c >= 0) return c;
    if (us_ticks() >= end) return -1;
    snd_pump();
    __dpmi_yield();
  }
}

static void dos_cleanup(void) {                    // at exit: the text mode, the keyboard, the sound card as they were
  con_init();
  sb_stop();
  if (con_tty) { textattr(7); _setcursortype(_NORMALCURSOR); }
}

static void dos_terminate(cell_t code) { exit((int) code); }

// ---- faults: DJGPP turns the processor's exceptions into signals ----
static jmp_buf g_forth_fault;
static volatile int g_forth_signal;

#define FAULT_ENTRY \
  if (setjmp(g_forth_fault)) { THROWIT(g_forth_signal); }

static void forth_signal_handler(int sig) {
  signal(sig, forth_signal_handler);
  switch (sig) {
    case SIGSEGV: g_forth_signal = -9; break;
    case SIGFPE: g_forth_signal = -10; break;
    case SIGINT: g_forth_signal = -28; break;
    default: g_forth_signal = -256 - sig; break;
  }
  longjmp(g_forth_fault, 1);
}

static void forth_faults_setup(void) {
  signal(SIGSEGV, forth_signal_handler);
  signal(SIGFPE, forth_signal_handler);
  signal(SIGILL, forth_signal_handler);
  signal(SIGINT, forth_signal_handler);           // Ctrl+Break stops a running word
}

#include "ports/dos/files.h"
#include "ports/dos/builtins.h"

#include "common/bits.h"
#include "common/core.h"
#include "common/calling.h"
#include "common/interp.h"
#include "gen/dos_boot.h"

int main(int argc, char *argv[]) {
  con_tty = isatty(1);
  key_tty = isatty(0);
  __djgpp_set_ctrl_c(0);                           // Ctrl+C is a key (the editors copy with it)
  if (con_tty) { gppconio_init(); con_attr(); }
  else setmode(1, O_BINARY);                       // the CR LF of cr as they are, not CR CR LF
  fs_init();
  atexit(dos_cleanup);
  void *heap = malloc(HEAP_SIZE);
  if (!heap) { fputs("lugforth: not enough memory (4 MB needed)\n", stderr); return 1; }
  forth_init(argc, argv, heap, HEAP_SIZE, boot, sizeof(boot));
  for (;;) { g_sys->rp = forth_run(g_sys->rp); }
  return 0;
}
