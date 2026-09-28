#! /usr/bin/env ueforth

\ Copyright 2026 luginf
\
\ Licensed under the Apache License, Version 2.0 (the "License");
\ you may not use this file except in compliance with the License.
\ You may obtain a copy of the License at
\
\     http://www.apache.org/licenses/LICENSE-2.0
\
\ Unless required by applicable law or agreed to in writing, software
\ distributed under the License is distributed on an "AS IS" BASIS,
\ WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
\ See the License for the specific language governing permissions and
\ limitations under the License.

\ A pure-computation benchmark (no graphics, no I/O in the timed part): the
\ sieve of Eratosthenes up to 32767, run 200 times back to back. LIMIT is kept
\ small enough to fit, together with the code, in uxn's single 64K memory
\ bank, so the same benchmark also runs unmodified in uxntal. Meant to be
\ compared with the same algorithm, same LIMIT, same REPEATS, in plain C
\ (bench_sieve.c) and in uxntal for the uxn virtual machine (bench_sieve.tal):
\ same work, three very different execution engines (a native-threaded Forth
\ interpreter calling straight into libc, a compiled C loop, and a small
\ software stack machine). Expected result: 3512 primes below 32768.
\   out/posix/ueforth examples/bench_sieve.fs
\   cc -O2 -o bench_sieve examples/bench_sieve.c && ./bench_sieve
\   uxnasm examples/bench_sieve.tal bench_sieve.rom && uxncli bench_sieve.rom

also posix   ( ms-ticks )

32768 constant limit   ( fits, with code, in uxn's single 64K bank: keep the three versions in step )
200 constant repeats

create sieve limit allot

0 value bs-count
: mark-multiples ( p -- )
  { p }
  limit p dup * ?do
    1 i sieve + c!
  p +loop ;
: run-sieve ( -- n )
  0 to bs-count
  limit 2 ?do
    i sieve + c@ 0= if
      1 +to bs-count
      ( only mark when i*i is still below limit: nothing to mark past that,
        and ?do only skips cleanly when start = limit, not when start > limit )
      i dup * limit < if i mark-multiples then
    then
  loop
  bs-count ;

0 value bs-t0   0 value bs-total
: bench ( reps -- )
  { reps }
  0 to bs-total
  reps 0 ?do
    sieve limit erase
    ms-ticks to bs-t0
    run-sieve drop
    ms-ticks bs-t0 - +to bs-total
  loop
  cr ." primes below " limit . ." : " bs-count . cr
  reps . ." runs: " bs-total . ." ms total, avg "
  bs-total 1000 * reps /  . ." (ms x1000)" cr ;

repeats bench
bye
