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

\ A Doom-like walk in a maze, by raycasting: one ray for each column of the
\ screen goes through the map (DDA) and the height of the wall is drawn.
\ After the raycasting demo of JS-Forth (Chris Curl, MIT license), rewritten in
\ integers (16.16 fixed point; the products go through */, so 32-bit cells are enough: the
\ browser has them), so the bare-metal boards and the web page run it too.
\   mouse        turn: the further from the middle, the faster; up and down
\                look up and down
\   w a s d, arrows   w/s or up/down walk, a/d slide, left/right turn
\   m   mouse on/off      esc   quit
\ Top left: the map, the red dot is you. Only libSDL2 is needed.

sdl2

320 constant sw   200 constant sh
65536 constant one
4096 constant circle-units        ( an angle is 0 to 4095 )
24 constant mw   24 constant mh

\ ---- sines, in 16.16 ----
create sins circle-units cells allot
: build-sins ( -- )
  circle-units 0 do
    i s>f 6.283185307e f* circle-units s>f f/ fsin 65536e f*
    fdup f0< if 0.5e f- else 0.5e f+ then f>s
    sins i cells + !
  loop ;
build-sins
: sin@ ( angle -- s )  circle-units 1- and cells sins + @ ;
: cos@ ( angle -- c )  circle-units 4 / + sin@ ;

\ ---- the map: . is empty, 1 to 4 are walls of different colors ----
create world mw mh * allot
: row ( y a n -- )   rot mw * world + swap cmove ;
0  s" 111111111111111111111111" row
1  s" 1..........1...........1" row
2  s" 1..........1...........1" row
3  s" 1....2222..1....3333...1" row
4  s" 1....2..........3......1" row
5  s" 1....2..........3......1" row
6  s" 1....2222..1....3333...1" row
7  s" 1..........1...........1" row
8  s" 1..........1...........1" row
9  s" 111111.1111111111.111111" row
10 s" 1.......4......1.......1" row
11 s" 1.......4......1.......1" row
12 s" 1.......4..............1" row
13 s" 1..............1..444..1" row
14 s" 1..............1.......1" row
15 s" 1..333.........1.......1" row
16 s" 1..3...................1" row
17 s" 1..3...............22..1" row
18 s" 1.....4.....4..........1" row
19 s" 1...................2..1" row
20 s" 1.....4.....4..........1" row
21 s" 1......................1" row
22 s" 1......................1" row
23 s" 111111111111111111111111" row

: wall@ ( cx cy -- char )   ( outside the map is a wall )
  over mw u< over mh u< and 0= if 2drop 49 exit then
  mw * + world + c@ ;
: empty? ( cx cy -- f )   wall@ 46 = ;

\ ---- the player: position in 16.16, angle, and where the horizon is ----
0 value px   0 value py   0 value ang   0 value horizon
0 value dirx   0 value diry   0 value plx   0 value ply   ( direction and camera plane )
0 value mouse-on   0 value moved   0 value m-down

: spot-free? ( x y -- f )   16 rshift swap 16 rshift swap empty? ;
: room? { x y -- f }   ( a square of 0.2 cell around the point is free )
  x 13000 - y 13000 - spot-free?
  x 13000 + y 13000 - spot-free? and
  x 13000 - y 13000 + spot-free? and
  x 13000 + y 13000 + spot-free? and ;

: aim ( -- )
  ang cos@ to dirx   ang sin@ to diry
  diry negate 66 * 100 / to plx   dirx 66 * 100 / to ply ;

: new-game ( -- )
  2 one * one 2/ + to px   2 one * one 2/ + to py   0 to ang
  sh 2/ to horizon   -1 to mouse-on   0 to moved
  aim ;

: move ( dx dy -- )   ( slides along walls: each axis alone )
  swap px + dup py room? if to px else drop then
  py + px over room? if to py else drop then ;

\ ---- one ray ----
0 value col   0 value camx   0 value rdx   0 value rdy
0 value mx   0 value my   0 value sdx   0 value sdy
0 value ddx   0 value ddy   0 value stx   0 value sty
0 value side   0 value hit   0 value dist

: delta ( rd -- dd )   ( distance between two lines of the grid, along the ray: 2^32 / rd )
  abs dup 16 < if drop 1 28 lshift exit then   ( parallel to an axis: very far, but below 2^31 )
  65536 65536 rot */ ;

: ray-setup ( -- )
  px 16 rshift to mx   py 16 rshift to my
  rdx delta to ddx   rdy delta to ddy
  rdx 0< if
    -1 to stx   px mx 16 lshift - ddx 65536 */ to sdx
  else
    1 to stx   mx 1+ 16 lshift px - ddx 65536 */ to sdx
  then
  rdy 0< if
    -1 to sty   py my 16 lshift - ddy 65536 */ to sdy
  else
    1 to sty   my 1+ 16 lshift py - ddy 65536 */ to sdy
  then ;

: dda ( -- )
  0 to hit
  begin hit 0= while
    sdx sdy < if
      ddx sdx + to sdx   stx mx + to mx   0 to side
    else
      ddy sdy + to sdy   sty my + to my   1 to side
    then
    mx my wall@ dup 46 <> if to hit else drop then
  repeat
  side if sdy ddy - else sdx ddx - then  1 max to dist ;

: cast ( x -- )   ( the ray of column x: hit, side and dist )
  dup to col
  2* sw - 65536 * sw / to camx
  dirx plx camx 65536 */ + to rdx
  diry ply camx 65536 */ + to rdy
  ray-setup dda ;

\ ---- drawing ----
: wall-color ( char -- rgb )
  dup 49 = if drop $8898a8 exit then
  dup 50 = if drop $b04838 exit then
  dup 51 = if drop $48a058 exit then
  drop $c0a840 ;

: shade ( rgb b -- rgb' )   ( each component times b / 256 )
  >r dup 16 rshift $ff and r@ * 8 rshift 16 lshift
  over 8 rshift $ff and r@ * 8 rshift 8 lshift or
  swap $ff and r> * 8 rshift or ;

: light ( -- b )   ( 256 close to the player, darker far away, darker on the sides )
  65536 256 dist 8 rshift 4 / + /  256 min
  side if 3 * 2/ 2/ then ;

: wall-u ( -- u )   ( where on the wall it hit, 0 to 63 )
  side if px dist rdx 65536 */ + else py dist rdy 65536 */ + then
  $ffff and 10 rshift ;

0 value y0   0 value y1   0 value lh

: draw-wall ( -- )
  sh 16 lshift dist / to lh
  horizon lh 2/ - 0 max to y0
  horizon lh 2/ + sh min to y1
  y1 y0 <= if exit then
  hit wall-color
  light  wall-u dup 2 < swap 61 > or if 2* 3 / then   ( the joints between blocks )
  shade color
  col y0 1 y1 y0 - box ;

: draw-room ( -- )   ( ceiling and floor in eight bands each, lighter near the horizon / the player )
  8 0 do
    horizon i * 8 /  to y0   horizon i 1+ * 8 /  to y1
    16 i 3 * +  24 i 3 * +  40 i 4 * +  rgb   0 y0 sw y1 y0 - box
    sh horizon - i * 8 / horizon +  to y0   sh horizon - i 1+ * 8 / horizon +  to y1
    30 i 6 * +  30 i 6 * +  38 i 6 * +  rgb   0 y0 sw y1 y0 - box
  loop ;

: draw-map ( -- )
  $404040 color
  mh 0 do
    mw 0 do
      i j empty? 0= if  i 2* 2 +  j 2* 2 +  2 2 box  then
    loop
  loop
  red color  px 15 rshift 2 +  py 15 rshift 2 +  2 2 box
  yellow color
  px 15 rshift 3 +  py 15 rshift 3 +
  2dup  swap dirx 4096 / +  swap diry 4096 / +  line ;

: draw-all ( -- )
  draw-room
  sw 0 do i cast draw-wall loop
  draw-map
  flip ;

\ ---- keys and mouse ----
: held? ( char scancode -- f )   scan? swap pressed? or ;

: walk ( speed -- )   ( forward, or backward if negative )
  dup dirx * 65536 /  swap diry * 65536 /  move ;
: slide ( speed -- )   ( to the right, or left if negative )
  dup diry negate * 65536 /  swap dirx * 65536 /  move ;
: turn-by ( n -- )   ang + circle-units 1- and to ang ;

: mouse-look ( -- )
  mouse-x mouse-y or 0<> moved or to moved   ( no mouse seen yet: do not turn )
  moved 0= mouse-on 0= or if exit then
  mouse-x sw 2/ -  dup abs 24 < if drop 0 then   8 /  turn-by
  sh 2/  sh 2/ mouse-y - 2/ +  20 max  sh 20 - min to horizon ;

: keys ( -- )
  [char] w 26 held?  key-up pressed? or  if 3900 walk then
  [char] s 22 held?  key-down pressed? or  if -3900 walk then
  [char] a 4 held?  if -3000 slide then
  [char] d 7 held?  if 3000 slide then
  key-left pressed? if -14 turn-by then
  key-right pressed? if 14 turn-by then
  [char] m 16 held?  dup m-down 0= and if mouse-on 0= to mouse-on then  to m-down
  mouse-look aim ;

: step ( -- )
  events
  key-esc pressed? if quit! then
  keys draw-all
  dt 16 < if 16 dt - delay then ;

: run ( -- ) begin step quit? until close-screen ;

[defined] raycaster-no-run [if] [else]
3 to zoom
sw sh screen
s" Raycaster" title
new-game
run
script-bye
[then]
