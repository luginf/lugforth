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

// Lugforth as a WebAssembly module for the TIC-80 fantasy console, which runs
// it in its wasm3 interpreter: a Forth cartridge is then a script in a
// sandbox, as Lua or JavaScript ones are. Every address it can make is an
// offset in the linear memory of the module; the only ways out are the
// imports of the module "tic80" (ports/tic80/builtins.h, include/math.h).
//
// The host drives it through the exports below:
//   lf_init()                boots the dictionary; 0 when ready
//   lf_alloc(n) / lf_free(a) a buffer in the module, for a text to evaluate
//   lf_eval(a, n)            evaluates a text; returns the throw code
//   lf_find(a, n)            the execution token of a word, or 0
//   lf_call(xt)              runs a word; returns the throw code
//   lf_call1(xt, n)          the same with n on the stack (SCN, BDR, MENU)
//   lf_depth()               depth of the data stack after the last command
// The stack is emptied before each command, so a word that leaves or takes
// too much shows up in lf_depth() and does not pile up from frame to frame.

#include "ports/tic80/libc.h"

// arm64 and wasm do not raise SIGFPE: throw -10 by hand, as the norm says.
#define DIV0_CHECK if (tos == 0) { THROWIT(-10); NEXT; }

#include "common/tier0_opcodes.h"
#include "common/tier1_opcodes.h"
#include "common/tier2_opcodes.h"
#include "common/floats.h"

#define HEAP_SIZE (1024 * 1024)
#define STACK_CELLS (4 * 1024)

#include "ports/tic80/builtins.h"
#include "common/bits.h"
#include "common/core.h"
#include "common/calling.h"
#include "ports/tic80/interp.h"
#include "gen/tic80_boot.h"

#define EXPORT(n) __attribute__((export_name(#n)))

enum { CMD_EVAL = 1, CMD_CALL = 2, CMD_CALL1 = 3 };

static cell_t run_command(cell_t cmd, cell_t a, cell_t b) {
  host_area.cmd = cmd;
  host_area.a = a;
  host_area.b = b;
  host_area.result = 0;
  g_sys->rp = forth_run(g_sys->rp);
  return host_area.result;
}

EXPORT(lf_init) cell_t lf_init(void) {
  void *heap = malloc(HEAP_SIZE);
  if (!heap) return -1;
  forth_init(0, 0, heap, HEAP_SIZE, boot, sizeof(boot));
  // The boot runs until the loop of platform.fs yields for its first command.
  g_sys->rp = forth_run(g_sys->rp);
  return host_area.ready ? 0 : -1;
}

EXPORT(lf_alloc) void *lf_alloc(cell_t n) { return malloc(n); }
EXPORT(lf_free) void lf_free(void *a) { free(a); }

EXPORT(lf_eval) cell_t lf_eval(const char *a, cell_t n) {
  return run_command(CMD_EVAL, (cell_t) a, n);
}

EXPORT(lf_find) cell_t lf_find(const char *a, cell_t n) {
  return find(a, n);
}

EXPORT(lf_call) cell_t lf_call(cell_t xt) {
  return run_command(CMD_CALL, xt, 0);
}

EXPORT(lf_call1) cell_t lf_call1(cell_t xt, cell_t n) {
  return run_command(CMD_CALL1, xt, n);
}

EXPORT(lf_depth) cell_t lf_depth(void) {
  return host_area.depth;
}
