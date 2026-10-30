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

\ Mouse test for the SDL2 words (also on the bare-metal PC and Raspberry Pi,
\ where the pointer is drawn by the system): position, buttons, wheel.
\   move          the cross follows the pointer, a trail of dots stays behind
\   left button   drops a green square, right button a red one, middle a blue one
\   c             clears the trail and the squares
\   esc           quit
\ A speed figure (pixels per second) shows whether the pointer is too fast:
\ cross the screen at an even pace, it should take about a second.

sdl2 sdl2-ttf

320 constant w   240 constant h
3 to zoom
w h screen
s" Mouse test" title
s" /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf" 10 load-font font   ( the file name is ignored on the bare-metal boots )

create trail-x 64 cells allot   create trail-y 64 cells allot
0 value trail-n   0 value moves   0 value clicks   0 value wheel-total
0 value last-x   0 value last-y   0 value path   0 value speed
0 value mark-n   0 value was-l   0 value was-m   0 value was-r
create mark-x 32 cells allot   create mark-y 32 cells allot   create mark-c 32 cells allot
0 value t0
create numbuf 16 allot

: num ( n -- a u )   ( the digits of n, in a small buffer )
  dup >r abs numbuf 16 +
  begin 1 - >r 10 /mod swap [char] 0 + r@ c! r> over 0= until nip
  r> 0< if 1 - [char] - over c! then
  dup numbuf 16 + swap - ;
: say ( x y a u -- )   white color text ;
: field ( x y n -- )   num say ;

: mark ( c -- )   ( a coloured square where the pointer is )
  mark-n 32 mod cells { o }  mark-c o + !  mouse-x mark-x o + !  mouse-y mark-y o + !
  mark-n 1+ to mark-n  clicks 1+ to clicks ;
: buttons ( -- )
  LEFT-BUTTON pressed?   dup was-l 0= and if green mark then   to was-l
  MIDDLE-BUTTON pressed? dup was-m 0= and if blue mark then    to was-m
  RIGHT-BUTTON pressed?  dup was-r 0= and if red mark then     to was-r ;

: track ( -- )
  mouse-x last-x <> mouse-y last-y <> or if
    moves 1+ to moves
    mouse-x last-x - abs mouse-y last-y - abs + path + to path
    mouse-x to last-x  mouse-y to last-y
    mouse-x trail-x trail-n 64 mod cells + !  mouse-y trail-y trail-n 64 mod cells + !
    trail-n 1+ to trail-n
  then
  wheel wheel-total + to wheel-total ;

: speed-update ( -- )   ( pixels travelled during the last second )
  ticks t0 - 1000 >= if path to speed  0 to path  ticks to t0 then ;

: paint ( -- )
  black color cls
  gray color  trail-n 64 min 0 ?do trail-x i cells + @  trail-y i cells + @ dot loop
  mark-n 32 min 0 ?do
    mark-c i cells + @ color  mark-x i cells + @ 3 -  mark-y i cells + @ 3 -  7 7 box
  loop
  white color
  mouse-x 6 - mouse-y mouse-x 6 + mouse-y line
  mouse-x mouse-y 6 - mouse-x mouse-y 6 + line
  4 2  s" MOUSE TEST (esc quits, c clears)" say
  4 16 s" x / y" say    90 16 mouse-x field   130 16 mouse-y field
  4 28 s" left" say     90 28 was-l if s" DOWN" else s" up" then say
  4 40 s" middle" say   90 40 was-m if s" DOWN" else s" up" then say
  4 52 s" right" say    90 52 was-r if s" DOWN" else s" up" then say
  4 64 s" moves" say    90 64 moves field
  4 76 s" clicks" say   90 76 clicks field
  4 88 s" wheel" say    90 88 wheel-total field
  4 100 s" px per second" say  130 100 speed field ;

: step ( -- )
  events
  [char] c pressed? if 0 to trail-n 0 to mark-n then
  key-esc pressed? if quit! then
  track buttons speed-update paint flip  16 delay ;

: run ( -- ) ticks to t0 begin step quit? until close-screen ;
0 cursor      \ the cross is drawn here, not the system arrow
run
script-bye
