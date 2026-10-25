// Copyright 2021 Bradley D. Nelson
// Modifications Copyright 2026 Luginfo10
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

#include <dlfcn.h>
#include <signal.h>
#if defined(__aarch64__)
// arm64 does not trap an integer division by zero: raise the signal that x86 gets (see faults.h).
# define DIV0_CHECK if (tos == 0) raise(SIGFPE);
#endif
#include <errno.h>
#include <fcntl.h>
#include <signal.h>
#include <stdlib.h>
#include <termios.h>
#include <unistd.h>
#include <sys/mman.h>

#ifdef LUGFORTH_STATIC
// A static executable (musl) has no dynamic symbol table: dlsym(RTLD_DEFAULT, "open") finds nothing.
// The C functions that the Forth looks up by name (z" open" 3 sysfunc open...) are listed here.
// A new sysfunc in a .fs file needs its name here too (the static tests fail at load: -38).
#include <dirent.h>
#include <netdb.h>
#include <stdio.h>
#include <sys/stat.h>
#include <poll.h>
#include <string.h>
#include <time.h>
#include <sys/ioctl.h>
#include <sys/socket.h>
#include <sys/wait.h>

// The table is generated from the .fs sources by tools/static_syms.py (out/gen/static_syms.h).
#ifdef LUGFORTH_STATIC_SDL
#include "gen/static_syms_sdl.h"
#else
#include "gen/static_syms.h"
#endif
#ifdef LUGFORTH_STATIC_SDL
// SDL2 and the X11 libraries are linked in (ninja posix-static-sdl). The Forth still "opens" libSDL2 by name:
// give it a handle that means "this executable". SDL2_image, SDL2_ttf and SDL2_mixer are linked in too. The other libraries (pthread...)
// are not linked in: dlopen fails (0) and the Forth falls back (bitmap font, no sound).
static void *lug_static_dlopen(const char *name, int flags) {
  static char self;
  return name && !strncmp(name, "libSDL2", 7) ? (void *) &self : 0;
}
#endif
static const struct { const char *name; void *addr; } lug_syms[] = {
#ifdef LUGFORTH_STATIC_SDL
  {"dlopen", (void *) lug_static_dlopen},
#endif
  LUG_SYMS
};
static void *lug_static_dlsym(void *h, const char *n) {
  for (unsigned i = 0; lug_syms[i].name; ++i)
    if (!strcmp(n, lug_syms[i].name)) return lug_syms[i].addr;
  return h && h != RTLD_DEFAULT ? 0 : dlsym(h, n);
}
#define LUG_DLSYM lug_static_dlsym
#endif

#include "common/tier0_opcodes.h"
#include "common/tier1_opcodes.h"
#include "common/tier2_opcodes.h"
#include "common/floats.h"
#include "common/calls.h"

#define HEAP_SIZE (10 * 1024 * 1024)
#define STACK_CELLS (8 * 1024)

// NOTE: errno implemented as opcode to avoid a Linux platform dependency.

// Apple arm64 passes the variadic arguments of open, fcntl and ioctl on the stack, not in registers
// like the other arguments: called through a plain function pointer, as sysfunc does, they read
// garbage (the start looped on a failing fcntl). These wrappers take fixed arguments and make the
// real variadic call.
#if defined(__APPLE__) && defined(__aarch64__)
#include <stdio.h>
#include <string.h>
#include <sys/ioctl.h>
static long lug_open(const char *p, long f, long m) { return open(p, (int) f, (int) m); }
static long lug_fcntl(long fd, long c, long a) { return fcntl((int) fd, (int) c, a); }
static long lug_ioctl(long fd, unsigned long r, void *a) { return ioctl((int) fd, r, a); }
static void *lug_dlsym(void *h, const char *n) {
  if (!strcmp(n, "open")) return (void *) lug_open;
  if (!strcmp(n, "fcntl")) return (void *) lug_fcntl;
  if (!strcmp(n, "ioctl")) return (void *) lug_ioctl;
  return dlsym(h, n);
}
#define LUG_DLSYM lug_dlsym
#elif !defined(LUGFORTH_STATIC)
#define LUG_DLSYM dlsym
#endif
#ifdef __APPLE__
#define LUG_DARWIN 1
#else
#define LUG_DARWIN 0
#endif

#define PLATFORM_OPCODE_LIST \
  Y(DLSYM, tos = (cell_t) LUG_DLSYM(a1 ? a1 : RTLD_DEFAULT, c0); NIP) \
  XV(internals, "host-mac?", DARWIN_P, DUP; tos = LUG_DARWIN ? -1 : 0) \
  XV(internals, "errno", ERRNO_INTERNAL, DUP; tos = (cell_t) errno) \
  CALLING_OPCODE_LIST \
  FLOATING_POINT_LIST

#define VOCABULARY_LIST V(forth) V(internals)

#include "common/bits.h"
#include "common/core.h"
#include "ports/posix/faults.h"
#include "common/calling.h"
#include "common/interp.h"

#ifdef LUGFORTH_MINI
#include "gen/posix_mini_boot.h"
#else
#include "gen/posix_boot.h"
#endif

// Leave the terminal as found, however we exit (a library calling exit(), as
// Xlib does when its window is closed, bypasses the Forth terminate hook).
static struct termios g_saved_termios;
static int g_have_termios;

static void restore_terminal(void) {
  if (!g_have_termios) return;
  fcntl(0, F_SETFL, fcntl(0, F_GETFL) & ~O_NONBLOCK);
  tcsetattr(0, TCSAFLUSH, &g_saved_termios);
}

static void restore_and_die(int sig) {
  restore_terminal();
  signal(sig, SIG_DFL);
  raise(sig);
}

#ifdef LUGFORTH_STATIC_SDL
// A static alsa-lib cannot load plugins: the "default" device of a Pulse or PipeWire desktop is such a plugin
// (libasound_module_pcm_pulse.so) and fails. plughw:0,0 (card 0) is the default here, tested on a PipeWire desktop; AUDIODEV (the variable SDL reads
// for ALSA) chooses another device.
// The ALSA error lines (one for each plugin tried) are not printed: SDL says "Couldn't open audio device".
extern void snd_lib_error_set_handler(void (*handler)(const char *, int, const char *, int, const char *, ...));
static void lug_alsa_quiet(const char *f, int l, const char *fn, int e, const char *fmt, ...) {}
#endif

int main(int argc, char *argv[]) {
#ifdef LUGFORTH_STATIC_SDL
  setenv("AUDIODEV", "plughw:0,0", 0);
  snd_lib_error_set_handler(lug_alsa_quiet);
#endif
  // lugforth --gui & : a background job is stopped by SIGTTOU/SIGTTIN as soon as it touches the
  // terminal (tcsetattr, a read), and the window never opens. The window does not need the terminal.
  if (argc >= 2 && strcmp(argv[1], "--gui") == 0) {
    signal(SIGTTOU, SIG_IGN);
    signal(SIGTTIN, SIG_IGN);
  }
  if (isatty(0) && tcgetattr(0, &g_saved_termios) == 0) {
    g_have_termios = 1;
    atexit(restore_terminal);
    signal(SIGTERM, restore_and_die);
    signal(SIGHUP, restore_and_die);
  }
  void *heap = mmap(
      (void *) 0x8000000, HEAP_SIZE,
      PROT_EXEC | PROT_READ | PROT_WRITE, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
  if (heap == MAP_FAILED) {
    // Some systems (macOS on Apple silicon) refuse a writable and executable
    // mapping, or the address hint. Threaded code never runs from the heap.
    heap = mmap(0, HEAP_SIZE, PROT_READ | PROT_WRITE,
                MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
  }
  forth_init(argc, argv, heap, HEAP_SIZE, boot, sizeof(boot));
  for (;;) { g_sys->rp = forth_run(g_sys->rp); }
  return 1;
}
