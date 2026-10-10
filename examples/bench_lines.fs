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

\ A drawing-throughput benchmark, the display-path twin of bench_sieve.*: 100
\ lines, their endpoints picked by a 16-bit LCG, redrawn 50 times through real
\ SDL2 rendering (one dot per pixel, Bresenham's line algorithm, no built-in
\ SDL2 "line" primitive, so all three versions do the same per-pixel work).
\ Same LCG, same line count, same Bresenham, same redraws in bench_lines.c
\ (real SDL2 too) and bench_lines.tal (uxn's Screen device); a checksum of
\ every plotted (x,y), reset each frame, cross-checks the geometry is the
\ same everywhere: expect 6942 (hex). After the timed part, the window stays
\ open on the last frame so the drawing can be checked by eye too (esc or
\ close it to quit); set BENCH_HEADLESS to skip straight to bye instead (used
\ for timing runs, see the Makefile).
\   out/posix/lugforth examples/bench_lines.fs
\   cc -O2 -o bench_lines examples/bench_lines.c $(pkg-config --cflags --libs sdl2)
\   uxnasm examples/bench_lines.tal bench_lines.rom && uxncli bench_lines.rom

sdl2
also posix   ( ms-ticks )

256 constant width   256 constant height
100 constant nlines   50 constant frames

0 value lcg-state
: lcg-next ( -- ) lcg-state 25173 * 13849 + $ffff and to lcg-state ;
: gen-point ( -- x y ) lcg-next  lcg-state $ff and  lcg-state 8 rshift $ff and ;

create lx0 nlines cells allot   create ly0 nlines cells allot
create lx1 nlines cells allot   create ly1 nlines cells allot
: gen-lines ( -- )
  1 to lcg-state
  nlines 0 ?do
    gen-point  i cells ly0 + !  i cells lx0 + !
    gen-point  i cells ly1 + !  i cells lx1 + !
  loop ;

0 value checksum
: bl-plot ( x y -- )
  { x y }
  checksum x 3 * + y 7 * + $ffff and to checksum
  x y dot ;

0 value bh-dx   0 value bh-sx   0 value bh-dy   0 value bh-sy
0 value bh-err  0 value bh-e2   0 value bh-x    0 value bh-y
: draw-line ( x0 y0 x1 y1 -- )
  { X0 Y0 X1 Y1 }
  X1 X0 - abs to bh-dx
  X0 X1 < if 1 else -1 then to bh-sx
  Y1 Y0 - abs negate to bh-dy
  Y0 Y1 < if 1 else -1 then to bh-sy
  bh-dx bh-dy + to bh-err
  X0 to bh-x   Y0 to bh-y
  begin
    bh-x bh-y bl-plot
    bh-x X1 <>  bh-y Y1 <>  or
  while
    bh-err 2 * to bh-e2
    bh-e2 bh-dy >= if bh-dy +to bh-err  bh-sx +to bh-x then
    bh-e2 bh-dx <= if bh-dx +to bh-err  bh-sy +to bh-y then
  repeat ;

: draw-frame ( -- )
  0 to checksum
  nlines 0 ?do
    i cells lx0 + @  i cells ly0 + @  i cells lx1 + @  i cells ly1 + @  draw-line
  loop
  flip ;

: bench ( -- )
  gen-lines
  black color cls   ( cleared once; the timed loop below only draws and presents )
  white color        ( set after the clear, and after screen exists: it stays current for every dot below )
  ms-ticks { t0 }
  frames 0 ?do draw-frame loop
  ms-ticks t0 - { total }
  cr ." checksum (per frame): " checksum . cr
  frames . ." frames of " nlines . ." lines: " total . ." ms total, avg "
  total 1000 * frames /  . ." (ms x1000)/frame" cr ;

z" getenv" 1 sysfunc c-getenv ( z -- z )
: headless? ( -- f ) z" BENCH_HEADLESS" c-getenv 0<> ;
: maybe-watch ( -- )   ( if/then belongs inside a definition: at top level it does not behave )
  headless? if exit then
  cr ." showing the last frame -- esc or close the window to quit" cr
  begin events key-esc pressed? quit? or until
  close-screen ;

width height screen
s" bench_lines" title
bench
maybe-watch
bye
