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

#define TIER2_OPCODE_LIST \
  X(">flags", TOFLAGS, tos = *TOFLAGS(tos)) \
  X(">flags&", TOFLAGSAT, tos = (cell_t) TOFLAGS(tos)) \
  X(">params", TOPARAMS, tos = *TOPARAMS(tos)) \
  X(">size", TOSIZE, tos = TOSIZE(tos)) \
  X(">link&", TOLINKAT, tos = (cell_t) TOLINK(tos)) \
  X(">link", TOLINK, tos = *TOLINK(tos)) \
  X(">name", TONAME, DUP; *sp = (cell_t) TONAME(tos); tos = *TONAMELEN(tos)) \
  Y(aligned, tos = CELL_ALIGNED(tos)) \
  Y(align, g_sys->heap = (cell_t *) CELL_ALIGNED(g_sys->heap)) \
  YV(internals, fill32, cell_t c = tos; DROP; cell_t n = tos; DROP; \
                        uint32_t *a = (uint32_t *) tos; DROP; \
                        for (;n;--n) *a++ = c) \
  DO_LOOP_OPCODE_LIST

// DO..LOOP in C: the return stack holds the limit, then the index (I is R@).
// A loop ends when the index crosses from limit-1 to limit, either way, as the
// Forth versions in boot.fs (used where a port has no tier 2, the web).
// With x = index - limit + MIN_INT, that crossing is the signed overflow of
// x + step (a change of sign of index - limit alone would also stop the loop
// half a turn away from the limit when the step is large).
#define LOOP_MIN ((ucell_t) 1 << (sizeof(cell_t) * 8 - 1))
#define LOOP_X ((ucell_t) *rp - (ucell_t) rp[-1] + LOOP_MIN)
#define LOOP_ENDS(s) ((cell_t) ((LOOP_X ^ (LOOP_X + (ucell_t) (s))) & \
                                ((ucell_t) (s) ^ (LOOP_X + (ucell_t) (s)))) < 0)
#define DO_LOOP_OPCODE_LIST \
  XV(internals, "(DO)", PDO, *++rp = *sp--; *++rp = tos; DROP) \
  XV(internals, "(?DO)", PQDO, if (tos == *sp) { --sp; DROP; ip = (cell_t *) *ip; } \
                               else { *++rp = *sp--; *++rp = tos; DROP; ++ip; }) \
  XV(internals, "(LOOP)", PLOOP, if (LOOP_ENDS(1)) { rp -= 2; ++ip; } \
                                 else { *rp = (cell_t) ((ucell_t) *rp + 1); ip = (cell_t *) *ip; }) \
  XV(internals, "(+LOOP)", PPLOOP, if (LOOP_ENDS(tos)) { rp -= 2; ++ip; } \
                                   else { *rp = (cell_t) ((ucell_t) *rp + tos); ip = (cell_t *) *ip; } DROP)
