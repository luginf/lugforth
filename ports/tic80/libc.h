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

// The C library of the tic80 port: memory words and an allocator over the
// linear memory of the module, which grows with memory.grow. Plain loops: the
// module is built with -ffreestanding, so clang does not turn them back into
// calls to memset or memcpy. Included by main.c, once.

#include <stddef.h>
#include <stdint.h>
#include <string.h>
#include <stdlib.h>

void *memcpy(void *d, const void *s, size_t n) {
  uint8_t *a = d; const uint8_t *b = s;
  while (n--) *a++ = *b++;
  return d;
}

void *memmove(void *d, const void *s, size_t n) {
  uint8_t *a = d; const uint8_t *b = s;
  if (a <= b || a >= b + n) return memcpy(d, s, n);
  a += n; b += n;
  while (n--) *--a = *--b;
  return d;
}

void *memset(void *d, int c, size_t n) {
  uint8_t *a = d;
  while (n--) *a++ = (uint8_t) c;
  return d;
}

int memcmp(const void *x, const void *y, size_t n) {
  const uint8_t *a = x, *b = y;
  for (; n; n--, a++, b++) if (*a != *b) return *a - *b;
  return 0;
}

size_t strlen(const char *s) {
  size_t n = 0;
  while (s[n]) n++;
  return n;
}

// First fit over a free list kept in address order, so that free can merge
// a block with its neighbours. A block starts with its size (header included).
typedef struct block { size_t size; struct block *next; } block;
#define BLOCK_ALIGN 8
#define PAGE (64 * 1024)

extern unsigned char __heap_base;
static uintptr_t brk_at = (uintptr_t) &__heap_base;
static block *free_list;

static void *more_core(size_t n) {
  uintptr_t start = (brk_at + BLOCK_ALIGN - 1) & ~(uintptr_t) (BLOCK_ALIGN - 1);
  uintptr_t end = start + n;
  uintptr_t have = __builtin_wasm_memory_size(0) * PAGE;
  if (end > have) {
    if (__builtin_wasm_memory_grow(0, (end - have + PAGE - 1) / PAGE) == (size_t) -1) return 0;
  }
  brk_at = end;
  return (void *) start;
}

void *malloc(size_t n) {
  if (n == 0) n = 1;
  size_t need = (n + sizeof(block) + BLOCK_ALIGN - 1) & ~(size_t) (BLOCK_ALIGN - 1);
  if (need < n) return 0;
  for (block **at = &free_list; *at; at = &(*at)->next) {
    block *b = *at;
    if (b->size < need) continue;
    if (b->size - need >= 2 * sizeof(block)) {
      block *rest = (block *) ((uint8_t *) b + need);
      rest->size = b->size - need;
      rest->next = b->next;
      *at = rest;
      b->size = need;
    } else {
      *at = b->next;
    }
    return b + 1;
  }
  block *b = more_core(need);
  if (!b) return 0;
  b->size = need;
  return b + 1;
}

void free(void *p) {
  if (!p) return;
  block *b = (block *) p - 1;
  block *prev = 0, *next = free_list;
  while (next && next < b) { prev = next; next = next->next; }
  b->next = next;
  if (next && (uint8_t *) b + b->size == (uint8_t *) next) {
    b->size += next->size;
    b->next = next->next;
  }
  if (prev) {
    prev->next = b;
    if ((uint8_t *) prev + prev->size == (uint8_t *) b) {
      prev->size += b->size;
      prev->next = b->next;
    }
  } else {
    free_list = b;
  }
}

void *realloc(void *p, size_t n) {
  if (!p) return malloc(n);
  block *b = (block *) p - 1;
  size_t have = b->size - sizeof(block);
  if (n <= have) return p;
  void *q = malloc(n);
  if (!q) return 0;
  memcpy(q, p, have);
  free(p);
  return q;
}

// clang turns fmin and fmax of a float into these.
float fminf(float a, float b) { return a < b ? a : b; }
float fmaxf(float a, float b) { return a > b ? a : b; }
