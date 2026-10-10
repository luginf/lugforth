// Copyright 2021 Bradley D. Nelson
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
#include <errno.h>
#include <fcntl.h>
#include <signal.h>
#include <stdlib.h>
#include <termios.h>
#include <unistd.h>
#include <sys/mman.h>

#include "common/tier0_opcodes.h"
#include "common/tier1_opcodes.h"
#include "common/tier2_opcodes.h"
#include "common/floats.h"
#include "common/calls.h"

#define HEAP_SIZE (10 * 1024 * 1024)
#define STACK_CELLS (8 * 1024)

// NOTE: errno implemented as opcode to avoid a Linux platform dependency.

#define PLATFORM_OPCODE_LIST \
  Y(DLSYM, tos = (cell_t) dlsym(a1 ? a1 : RTLD_DEFAULT, c0); NIP) \
  XV(internals, "errno", ERRNO_INTERNAL, DUP; tos = (cell_t) errno) \
  CALLING_OPCODE_LIST \
  FLOATING_POINT_LIST

#define VOCABULARY_LIST V(forth) V(internals)

#include "common/bits.h"
#include "common/core.h"
#include "ports/posix/faults.h"
#include "common/calling.h"
#include "common/interp.h"

#include "gen/posix_boot.h"

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

int main(int argc, char *argv[]) {
  if (isatty(0) && tcgetattr(0, &g_saved_termios) == 0) {
    g_have_termios = 1;
    atexit(restore_terminal);
    signal(SIGTERM, restore_and_die);
    signal(SIGHUP, restore_and_die);
  }
  void *heap = mmap(
      (void *) 0x8000000, HEAP_SIZE,
      PROT_EXEC | PROT_READ | PROT_WRITE, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
  forth_init(argc, argv, heap, HEAP_SIZE, boot, sizeof(boot));
  for (;;) { g_sys->rp = forth_run(g_sys->rp); }
  return 1;
}
