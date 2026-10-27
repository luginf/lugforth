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

// The words of the tic80 port. There is no CALL, DLSYM or file word: the only
// ways out of the module are the imports below, and the host checks every
// address it is handed against the bounds of the linear memory.

#include "ports/tic80/api.h"

#define TIC80_IMPORT_FN(n) __attribute__((import_module("tic80"), import_name(#n)))

// Runs API word id on its n arguments, base[0] .. base[n-1], and writes its
// results from base[0]: returns how many.
TIC80_IMPORT_FN(api) cell_t tic80_api(cell_t id, cell_t *base, cell_t n);
// Text of TYPE, written to the console of TIC-80 (trace) at each line end.
TIC80_IMPORT_FN(type) void tic80_type(const char *a, cell_t n);
TIC80_IMPORT_FN(ticks) cell_t tic80_ticks(void);

// A command from the host (main.c), read by the loop of ports/tic80/platform.fs.
typedef struct {
  cell_t cmd, a, b, c;    // command and its arguments
  cell_t result, depth;   // throw code, stack depth after
  cell_t ready;           // set by the loop when the boot is over
} HOST_AREA;
static HOST_AREA host_area;

// After DUP the arguments are the top n cells, from sp - n + 1.
#define API(id, name, op, nargs, nres) \
  X(name, TIC80_ ## op, DUP; w = tic80_api(id, sp - (nargs) + 1, nargs); \
                        sp = sp - (nargs) + w; DROP)

#define PLATFORM_OPCODE_LIST \
  REQUIRED_MEMORY_SUPPORT \
  REQUIRED_SYSTEM_SUPPORT \
  TIC80_API_LIST \
  FLOATING_POINT_LIST

#define REQUIRED_MEMORY_SUPPORT \
  YV(internals, MALLOC, SET malloc(n0)) \
  YV(internals, SYSFREE, free(a0); DROP) \
  YV(internals, REALLOC, SET realloc(a1, n0); NIP)

#define REQUIRED_SYSTEM_SUPPORT \
  X("MS-TICKS", MS_TICKS, PUSH tic80_ticks()) \
  XV(internals, "HOST-TYPE", HOST_TYPE, tic80_type(c1, n0); DROPn(2)) \
  XV(internals, "'HOST", THOST, PUSH &host_area)

#define VOCABULARY_LIST V(forth) V(internals)
