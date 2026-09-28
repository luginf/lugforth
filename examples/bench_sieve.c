// Reference C version of examples/bench_sieve.fs and examples/bench_sieve.tal:
// same algorithm, same LIMIT, same REPEATS, so the three are comparable.
// Build:  cc -O2 -o bench_sieve examples/bench_sieve.c
// Run:    ./bench_sieve

#include <stdio.h>
#include <string.h>
#include <time.h>

#define LIMIT 32768  // fits, with the code, in uxn's single 64K bank: keep the three versions in step
#define REPEATS 200

static unsigned char sieve[LIMIT];

static long run_sieve(void) {
  long count = 0;
  memset(sieve, 0, LIMIT);
  for (long i = 2; i < LIMIT; i++) {
    if (!sieve[i]) {
      count++;
      for (long j = i * i; j < LIMIT; j += i) sieve[j] = 1;
    }
  }
  return count;
}

static double ms_now(void) {
  struct timespec t;
  clock_gettime(CLOCK_MONOTONIC, &t);
  return t.tv_sec * 1000.0 + t.tv_nsec / 1e6;
}

int main(void) {
  long count = 0;
  double t0 = ms_now();
  for (int r = 0; r < REPEATS; r++) count = run_sieve();
  double t1 = ms_now();
  double total = t1 - t0;
  printf("primes below %d: %ld\n", LIMIT, count);
  printf("%d runs: %.3f ms total, %.4f ms/run\n", REPEATS, total, total / REPEATS);
  return 0;
}
