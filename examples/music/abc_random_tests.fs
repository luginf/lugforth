#! /usr/bin/env lugforth

\ Copyright 2026 Luginfo10
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

\ Tests of abc_random.fs: the tunes it makes are read by the ABC reader of
\ common/abc.fs (no sound needed). Run with:  lugforth examples/music/abc_random_tests.fs

needs ../../common/testing.fs

also internals
vocabulary sdl2
forth definitions also internals
abc-parser evaluate
only forth definitions

: abc-play ( a n -- ) 2drop ;     \ no player in the tests: abc_random.fs would load the SDL one
include abc_random.fs

also sdl2 also abc-int
-1 to voices?
: tune-ok ( -- )                          \ the tune just made: two voices of 12 bars
  new-tune abc
  n-voices 2 =assert
  tune-end 12 4 * 480 * =assert
  vcur cell+ @ 12 4 * 480 * =assert   cur 12 4 * 480 * =assert     \ both voices last 12 bars
  tune-notes 100 > assert ;
: test-200-tunes-are-read
  200 0 ?do i 7 * 1+ rand-seed !  tune-ok loop ;

create keep 1700 allot   0 value keep-n
: test-the-same-seed-makes-the-same-tune
  99 rand-seed !  new-tune dup to keep-n  keep swap cmove
  99 rand-seed !  new-tune  keep-n =assert
  keep-n 0 ?do dup i + c@ keep i + c@ =assert loop drop ;

: test-the-title-shows-the-seed
  1234 rand-seed !  new-tune drop 18 s" X:1
T:Random 1234" ( not expanded ) 2drop 2drop
  1234 rand-seed ! new-tune  s" T:Random 1234" search assert 2drop ;

forth definitions
sdl2
run-tests
bye
