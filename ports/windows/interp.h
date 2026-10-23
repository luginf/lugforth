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

#define NEXT goto next
#define JMPW goto work
#define ADDROF(x) ((void *) OP_ ## x)

enum {
#define Z(flags, name, op, code) OP_ ## op,
  PLATFORM_OPCODE_LIST
  TIER2_OPCODE_LIST
  TIER1_OPCODE_LIST
  TIER0_OPCODE_LIST
#undef Z
};

#ifndef _MSC_VER
// MinGW has no __try / __except. A vectored exception handler jumps back into forth_run,
// which does what the __except block does under MSVC.
#include <setjmp.h>
static jmp_buf *forth_jb;
static DWORD forth_exc_code;
static LONG CALLBACK forth_veh(EXCEPTION_POINTERS *e) {
  DWORD c = e->ExceptionRecord->ExceptionCode;
  if (!forth_jb) return EXCEPTION_CONTINUE_SEARCH;
  if (c != EXCEPTION_INT_DIVIDE_BY_ZERO && c != EXCEPTION_ACCESS_VIOLATION &&
      c != EXCEPTION_INT_OVERFLOW && c != EXCEPTION_ILLEGAL_INSTRUCTION &&
      c != EXCEPTION_PRIV_INSTRUCTION && c != EXCEPTION_IN_PAGE_ERROR) {
    return EXCEPTION_CONTINUE_SEARCH;
  }
  forth_exc_code = c;
  longjmp(*forth_jb, 1);
}
static cell_t *forth_run_inner(cell_t *init_rp);
static cell_t *forth_run(cell_t *init_rp) {
  jmp_buf *saved = forth_jb;
  cell_t *r = forth_run_inner(init_rp);
  forth_jb = saved;
  return r;
}
#define FORTH_RUN forth_run_inner
#else
#define FORTH_RUN forth_run
#endif

static cell_t *FORTH_RUN(cell_t *init_rp) {
  static const BUILTIN_WORD builtins[] = {
#define Z(flags, name, op, code) \
    name, ((VOC_ ## flags >> 8) & 0xff) | BUILTIN_MARK, sizeof(name) - 1, \
    (VOC_ ## flags & 0xff), (void *) OP_ ## op,
    PLATFORM_OPCODE_LIST
    TIER2_OPCODE_LIST
    TIER1_OPCODE_LIST
    TIER0_OPCODE_LIST
#undef Z
    0, 0, 0,
  };

  if (!init_rp) {
    g_sys->DOCREATE_OP = ADDROF(DOCREATE);
    g_sys->builtins = builtins;
    return 0;
  }
  register cell_t *ip, *rp, *sp, tos, w;
  register float *fp, ft;
  rp = init_rp; UNPARK;
#ifndef _MSC_VER
  static int veh_added;
  if (!veh_added) { veh_added = 1; AddVectoredExceptionHandler(1, forth_veh); }
  jmp_buf jb;
  forth_jb = &jb;
  if (setjmp(jb)) {
    DWORD code = forth_exc_code;
    forth_jb = &jb;
    switch (code) {
      case EXCEPTION_INT_DIVIDE_BY_ZERO: THROWIT(-10); break;
      case EXCEPTION_ACCESS_VIOLATION: THROWIT(-9); break;
      default: THROWIT(code); break;
    }
  }
  for (;;) {
    {
      for (;;) {
#else
  for (;;) {
    __try {
      for (;;) {
#endif
next:
        w = *ip++;
work:
        switch (*(cell_t *) w & 0xff) {
#define Z(flags, name, op, code) case OP_ ## op: { code; } NEXT;
  PLATFORM_OPCODE_LIST
  TIER2_OPCODE_LIST
  TIER1_OPCODE_LIST
  TIER0_OPCODE_LIST
#undef Z
        }
      }
#ifdef _MSC_VER
    } __except (EXCEPTION_EXECUTE_HANDLER) {
      DWORD code = GetExceptionCode();
      switch (code) {
        case EXCEPTION_INT_DIVIDE_BY_ZERO: THROWIT(-10); break;
        case EXCEPTION_ACCESS_VIOLATION: THROWIT(-9); break;
        default: THROWIT(code); break;
      }
    }
#else
    }
#endif
  }
}
