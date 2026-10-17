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

// The bits of a C library the kernel and the shared ports/rpi headers use,
// for a UEFI application, which has none: strings, memory (UEFI pool
// allocations), and the few math functions of common/floats.h, done with
// the x87 instructions (80-bit, so as precise as a libm for doubles).
// setjmp and longjmp are in start.S. Included by main.c, once; not static,
// as gcc may call memcpy and memset by itself.

#include <stddef.h>
#include <string.h>
#include <stdlib.h>
#include <math.h>

// Eight bytes at a time (rep movs): the console scrolls by moving the whole
// framebuffer, and reading video memory byte by byte is very slow on a real GPU.
void *memcpy(void *d, const void *s, size_t n) {
  void *r = d;
  size_t q = n >> 3;
  __asm__ volatile("rep movsq" : "+D"(d), "+S"(s), "+c"(q) : : "memory");
  n &= 7;
  __asm__ volatile("rep movsb" : "+D"(d), "+S"(s), "+c"(n) : : "memory");
  return r;
}

void *memmove(void *d, const void *s, size_t n) {
  uint8_t *a = d; const uint8_t *b = s;
  if (a < b || a >= b + n) return memcpy(d, s, n);
  a += n; b += n;
  for (size_t t = n & 7; t; t--) *--a = *--b;
  size_t q = n >> 3;
  if (q) {
    a -= 8; b -= 8;
    __asm__ volatile("std\n\trep movsq\n\tcld" : "+D"(a), "+S"(b), "+c"(q) : : "memory");
  }
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

size_t strlen(const char *s) { size_t n = 0; while (s[n]) n++; return n; }

int strncmp(const char *a, const char *b, size_t n) {
  for (; n; n--, a++, b++) {
    if (*a != *b) return (unsigned char) *a - (unsigned char) *b;
    if (!*a) return 0;
  }
  return 0;
}

int strcmp(const char *a, const char *b) { return strncmp(a, b, (size_t) -1); }

char *strchr(const char *s, int c) {
  for (;; s++) {
    if (*s == (char) c) return (char *) s;
    if (!*s) return 0;
  }
}

// ---- memory: UEFI pool allocations, the size kept in front ----
static EFI_BOOT_SERVICES *libc_bs;

void *malloc(size_t n) {
  uint64_t *p;
  if (!libc_bs || libc_bs->AllocatePool(EfiLoaderData, n + 16, (void **) &p) != EFI_SUCCESS) return 0;
  p[0] = n;
  return p + 2;
}

void free(void *p) {
  if (p) libc_bs->FreePool((uint64_t *) p - 2);
}

void *realloc(void *p, size_t n) {
  void *q = malloc(n);
  if (q && p) {
    size_t old = ((uint64_t *) p)[-2];
    memcpy(q, p, old < n ? old : n);
    free(p);
  }
  return q;
}

void *calloc(size_t a, size_t b) {
  void *p = malloc(a * b);
  if (p) memset(p, 0, a * b);
  return p;
}

// ---- math ----
double fabs(double x) { return x < 0 ? -x : x; }
double fmin(double a, double b) { return a < b ? a : b; }
double fmax(double a, double b) { return a > b ? a : b; }

double sqrt(double x) { double r; __asm__("sqrtsd %1, %0" : "=x"(r) : "x"(x)); return r; }

double floor(double x) {
  if (!(x > -4503599627370496.0 && x < 4503599627370496.0)) return x;   // big, inf, nan
  double t = (double) (int64_t) x;
  return t > x ? t - 1 : t;
}

double sin(double x) { double r; __asm__("fsin" : "=t"(r) : "0"(x)); return r; }
double cos(double x) { double r; __asm__("fcos" : "=t"(r) : "0"(x)); return r; }
void sincos(double x, double *s, double *c) { *s = sin(x); *c = cos(x); }

double atan2(double y, double x) {
  double r;
  __asm__("fpatan" : "=t"(r) : "0"(x), "u"(y) : "st(1)");   // atan(st1 / st0), popped
  return r;
}

// 2^x for any x: the integer part by fscale, the fraction by f2xm1.
static double exp2_x87(long double x) {
  long double i, f, r;
  __asm__("frndint" : "=t"(i) : "0"(x));
  f = x - i;
  __asm__("f2xm1" : "=t"(r) : "0"(f));
  r += 1;
  __asm__("fscale" : "=t"(r) : "0"(r), "u"(i));
  return (double) r;
}

static long double log2_x87(long double x) {    // y * log2(x) with y = 1
  long double r;
  __asm__("fyl2x" : "=t"(r) : "0"(x), "u"(1.0L) : "st(1)");
  return r;
}

double exp(double x) { return exp2_x87((long double) x * 1.4426950408889634073599L); }
double log(double x) { return (double) (log2_x87(x) * 0.6931471805599453094172L); }

double pow(double x, double y) {
  if (y == 0) return 1;
  if (x == 0) return y > 0 ? 0 : 1.0 / 0.0;
  if (x < 0) {
    double t = floor(y);
    if (t != y) return 0.0 / 0.0;                    // a negative number, a fraction
    double r = exp2_x87(y * log2_x87(-x));
    return fmod(t, 2) == 0 ? r : -r;
  }
  return exp2_x87(y * log2_x87(x));
}

double fmod(double x, double y) {
  long double r;
  unsigned short sw;
  __asm__("1: fprem\n\tfnstsw %%ax\n\ttestw $0x400, %%ax\n\tjnz 1b"
          : "=t"(r), "=a"(sw) : "0"((long double) x), "u"((long double) y));
  (void) sw;
  return (double) r;
}

// Float versions, should gcc pick them.
float sinf(float x) { return (float) sin(x); }
float cosf(float x) { return (float) cos(x); }
float sqrtf(float x) { return (float) sqrt(x); }
float floorf(float x) { return (float) floor(x); }
float fabsf(float x) { return x < 0 ? -x : x; }
float expf(float x) { return (float) exp(x); }
float logf(float x) { return (float) log(x); }
float powf(float x, float y) { return (float) pow(x, y); }
float atan2f(float y, float x) { return (float) atan2(y, x); }
float fminf(float a, float b) { return a < b ? a : b; }
float fmaxf(float a, float b) { return a > b ? a : b; }
