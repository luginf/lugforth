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

\ A wireframe rail shooter in 3D, in the spirit of Star Fox on a vector
\ screen: you fly down a canyon of towers toward a red sun, dodge pillars
\ and bars, shoot the fighters that come at you. Only lines: the 3D is a
\ perspective division and a clip against the near plane. Only libSDL2 is
\ needed; the letters are a tiny 3x5 pixel font and the sounds are beeps.
\ One file, so that it runs the same on POSIX and the web
\ (terminal.html?run=gfx_corridor.fs).
\   arrows or wasd   fly               space or f   fire (hold it for a burst)
\   p                pause             esc          quit
\   space or enter   after the end: again

gfx

( ---- The screen and the camera ---- )
320 constant cr-w   220 constant cr-h
160 constant cr-cx   92 constant cr-hy      \ the middle column and the horizon row
120 constant cr-f                           \ focal length, in pixels
20 constant cr-near   2000 constant cr-far  \ the clip plane and the far end
150 constant cr-sz                          \ the ship is this far in front of the camera
-100 constant cr-floor
300 constant cr-half                        \ half width of the corridor
$55ffff constant cr-cyan   $ff5555 constant cr-red

0 value cr-sx   0 value cr-sy               \ the ship, in world units
0 value cr-camx   0 value cr-camy
: cr-camera ( -- )   cr-sx 3 * 4 /  to cr-camx   cr-sy 2/ 90 +  to cr-camy ;

: cr-px ( x z -- col )   >r cr-camx - cr-f * r> / cr-cx + ;
: cr-py ( y z -- row )   >r cr-camy - cr-f * r> / negate cr-hy + ;

( ---- Lines in space, clipped against the near plane ---- )
0 value wx1   0 value wy1   0 value wz1   0 value wx2   0 value wy2   0 value wz2
: cr-lerp ( a b n d -- a' )   >r >r over - r> * r> / + ;
: cr-wline ( x1 y1 z1 x2 y2 z2 -- )
  to wz2  to wy2  to wx2  to wz1  to wy1  to wx1
  wz1 cr-near <  wz2 cr-near < and if exit then
  wz1 cr-near < if
    wx1 wx2 cr-near wz1 - wz2 wz1 - cr-lerp
    wy1 wy2 cr-near wz1 - wz2 wz1 - cr-lerp
    to wy1 to wx1  cr-near to wz1
  then
  wz2 cr-near < if
    wx2 wx1 cr-near wz2 - wz1 wz2 - cr-lerp
    wy2 wy1 cr-near wz2 - wz1 wz2 - cr-lerp
    to wy2 to wx2  cr-near to wz2
  then
  wx1 wz1 cr-px  wy1 wz1 cr-py  wx2 wz2 cr-px  wy2 wz2 cr-py  line ;

0 value t1   0 value t2   0 value t3   0 value t4   0 value t5
: cr-rz ( x0 y0 x1 y1 z -- )   ( a rectangle across the view )
  to t5 to t4 to t3 to t2 to t1
  t1 t2 t5  t3 t2 t5 cr-wline
  t3 t2 t5  t3 t4 t5 cr-wline
  t3 t4 t5  t1 t4 t5 cr-wline
  t1 t4 t5  t1 t2 t5 cr-wline ;
: cr-rx ( x y0 z0 y1 z1 -- )   ( a rectangle along the view )
  to t5 to t4 to t3 to t2 to t1
  t1 t2 t3  t1 t2 t5 cr-wline
  t1 t2 t5  t1 t4 t5 cr-wline
  t1 t4 t5  t1 t4 t3 cr-wline
  t1 t4 t3  t1 t2 t3 cr-wline ;

( ---- Things: arrays of slots ---- )
28 constant cr-nb   8 constant cr-no   6 constant cr-ne   20 constant cr-nq   12 constant cr-nl   10 constant cr-nx
: arr ( n "name" -- )   create cells allot  does> ( i -- a ) swap cells + ;
cr-nb arr bd.on   cr-nb arr bd.z   cr-nb arr bd.s   cr-nb arr bd.d   cr-nb arr bd.h
cr-no arr ob.on   cr-no arr ob.z   cr-no arr ob.x   cr-no arr ob.w   cr-no arr ob.y0   cr-no arr ob.y1   cr-no arr ob.d
cr-ne arr en.on   cr-ne arr en.z   cr-ne arr en.x   cr-ne arr en.y   cr-ne arr en.b   cr-ne arr en.t   cr-ne arr en.p
cr-nq arr bo.on   cr-nq arr bo.x   cr-nq arr bo.y   cr-nq arr bo.z   cr-nq arr bo.vx   cr-nq arr bo.vy
cr-nl arr la.on   cr-nl arr la.x   cr-nl arr la.y   cr-nl arr la.z
cr-nx arr ex.on   cr-nx arr ex.x   cr-nx arr ex.y   cr-nx arr ex.z   cr-nx arr ex.a
: cr-free ( xt n -- i | -1 )   ( the first free slot of an array of flags: xt is its word, n its size )
  -1 swap 0 do
    i 2 pick execute @ 0= if drop i leave then
  loop nip ;

( ---- State ---- )
0 value cr-state    \ 0 playing, 1 paused, 2 over
0 value cr-score    0 value cr-best    0 value cr-shield   0 value cr-level
0 value cr-speed    0 value cr-travel  0 value cr-frame    0 value cr-inv
0 value cr-bank   0 value cr-fire-t   0 value cr-flash   0 value cr-spawn-o  0 value cr-spawn-e
create cr-edge 0 , 0 ,                  \ the far end of the last tower, left and right
6 constant cr-maxshield

: cr-clear ( -- )
  cr-nb 0 do 0 i bd.on ! loop   cr-no 0 do 0 i ob.on ! loop   cr-ne 0 do 0 i en.on ! loop
  cr-nq 0 do 0 i bo.on ! loop   cr-nl 0 do 0 i la.on ! loop   cr-nx 0 do 0 i ex.on ! loop ;

( ---- Towers along the two sides ---- )
: cr-edge@ ( side -- a )   0< if cr-edge else cr-edge cell+ then ;
: cr-tower ( side -- )   ( one more tower at the far end of that side )
  >r  ['] bd.on cr-nb cr-free  dup 0< if drop r> drop exit then
  dup 1 swap bd.on !
  r@ over bd.s !
  r@ cr-edge@ @  20 random + 10 +  over bd.z !
  150 random 150 + over bd.d !
  300 random 220 + over bd.h !
  dup bd.z @  over bd.d @ +  r> cr-edge@ !  drop ;
: cr-towers ( -- )
  -1 cr-edge@ @ cr-far < if -1 cr-tower then
  1 cr-edge@ @ cr-far < if 1 cr-tower then ;
: cr-fill-towers ( -- )   -150 cr-edge !  -100 cr-edge cell+ !  30 0 do cr-towers loop ;

( ---- Things in the corridor ---- )
: cr-pillar ( i -- )   ( a tall or a low block )
  dup 1 swap ob.on !  cr-far over ob.z !
  400 random 200 - over ob.x !   55 random 40 + over ob.w !   100 over ob.d !
  cr-floor over ob.y0 !
  2 random if 260 else 70 then cr-floor + swap ob.y1 ! ;
: cr-bar ( i -- )   ( a floating bar across the whole corridor )
  dup 1 swap ob.on !  cr-far over ob.z !
  0 over ob.x !   cr-half over ob.w !   60 over ob.d !
  50 random 10 + over ob.y0 !   dup ob.y0 @ 50 + swap ob.y1 ! ;
: cr-new-obstacle ( -- )
  ['] ob.on cr-no cr-free dup 0< if drop exit then
  3 random 0= if cr-bar else cr-pillar then ;

: cr-new-enemy ( -- )
  ['] en.on cr-ne cr-free dup 0< if drop exit then
  dup 1 swap en.on !   cr-far 100 - over en.z !
  300 random 150 - over en.b !   dup en.b @ over en.x !
  150 random 20 - over en.y !   16 random over en.p !
  60 random 40 + swap en.t ! ;

: cr-new-explosion ( x y z -- )
  ['] ex.on cr-nx cr-free dup 0< if drop drop drop drop exit then
  dup 1 swap ex.on !  >r  r@ ex.z !  r@ ex.y !  r@ ex.x !  0 r> ex.a ! ;

: cr-damage ( -- )
  cr-inv 0> if exit then
  -1 +to cr-shield  50 to cr-inv  10 to cr-flash  150 100 beep
  cr-shield 0= if
    2 to cr-state  cr-score cr-best max to cr-best
    cr-sx cr-sy cr-sz cr-new-explosion  100 300 beep
  then ;

( ---- Our shots ---- )
: cr-shoot ( -- )
  cr-fire-t 0> if exit then
  ['] la.on cr-nl cr-free dup 0< if drop exit then
  dup 1 swap la.on !  cr-sx 14 - over la.x !  cr-sy over la.y !  cr-sz over la.z !  drop
  ['] la.on cr-nl cr-free dup 0< if drop exit then
  dup 1 swap la.on !  cr-sx 14 + over la.x !  cr-sy over la.y !  cr-sz over la.z !  drop
  6 to cr-fire-t  880 20 beep ;

0 value cr-q   0 value cr-e   0 value cr-steps
: cr-enemy-shoots ( i -- )
  ['] bo.on cr-nq cr-free dup 0< if 2drop exit then
  to cr-q  to cr-e
  1 cr-q bo.on !
  cr-e en.x @ cr-q bo.x !   cr-e en.y @ cr-q bo.y !   cr-e en.z @ cr-q bo.z !
  cr-e en.z @ 30 / 1 max to cr-steps
  cr-sx cr-e en.x @ - cr-steps / cr-q bo.vx !
  cr-sy cr-e en.y @ - cr-steps / cr-q bo.vy ! ;

( ---- Keys ---- )
create cr-was 256 allot   cr-was 256 erase
: cr-just ( k -- f )   ( pressed now, not at the last look )
  dup pressed? swap 255 and cr-was + 2dup c@ 0= and >r c! r> ;

: cr-input ( -- )
  key-left pressed? [char] a pressed? or if -7 +to cr-sx then
  key-right pressed? [char] d pressed? or if 7 +to cr-sx then
  key-up pressed? [char] w pressed? or if 5 +to cr-sy then
  key-down pressed? [char] s pressed? or if -5 +to cr-sy then
  key-left pressed? [char] a pressed? or if -4 else
    key-right pressed? [char] d pressed? or if 4 else 0 then then
  cr-bank - dup 0< if drop -1 else 0> 1 and then +to cr-bank   \ the ship leans, one notch a step
  cr-sx -230 max 230 min to cr-sx
  cr-sy -70 max 130 min to cr-sy
  key-space pressed? [char] f pressed? or if cr-shoot then ;

( ---- Moving the world toward the camera ---- )
create cr-sintab  0 , 38 , 71 , 92 , 100 , 92 , 71 , 38 , 0 , -38 , -71 , -92 , -100 , -92 , -71 , -38 ,
: cr-sin ( i -- n )   15 and cells cr-sintab + @ ;   \ 100 * sin, 16 steps a turn
: cr-cos ( i -- n )   4 + cr-sin ;

: cr-move-things ( -- )
  cr-nb 0 do
    i bd.on @ if
      cr-speed negate i bd.z +!
      i bd.z @ i bd.d @ + 0< if 0 i bd.on ! then
    then
  loop
  cr-no 0 do
    i ob.on @ if
      cr-speed negate i ob.z +!
      i ob.z @ i ob.d @ + 0< if 0 i ob.on ! then
    then
  loop
  cr-ne 0 do
    i en.on @ if
      cr-speed 3 + negate i en.z +!
      cr-frame 6 / i en.p @ + cr-sin 70 * 100 /  i en.b @ +  i en.x !
      i en.z @ 30 < if 0 i en.on ! else
        -1 i en.t +!
        i en.t @ 0= if  i en.z @ 1500 < if i cr-enemy-shoots then  70 random 60 + i en.t !  then
      then
    then
  loop
  cr-nq 0 do
    i bo.on @ if
      cr-speed 16 + negate i bo.z +!
      i bo.vx @ i bo.x +!   i bo.vy @ i bo.y +!
      i bo.z @ 0< if 0 i bo.on ! then
    then
  loop
  cr-nl 0 do
    i la.on @ if
      45 i la.z +!
      i la.z @ cr-far > if 0 i la.on ! then
    then
  loop
  cr-nx 0 do
    i ex.on @ if
      cr-speed negate i ex.z +!   1 i ex.a +!
      i ex.a @ 18 > i ex.z @ cr-near < or if 0 i ex.on ! then
    then
  loop ;

: cr-move ( -- )
  cr-speed +to cr-travel
  cr-speed negate cr-edge +!   cr-speed negate cr-edge cell+ +!
  cr-move-things  cr-towers ;

( ---- Hits ---- )
0 value cr-li
: cr-laser-vs-enemies ( -- f )
  0  cr-ne 0 do
    i en.on @ if
      i en.z @ cr-li la.z @ - abs 50 <
      i en.x @ cr-li la.x @ - abs 40 < and
      i en.y @ cr-li la.y @ - abs 35 < and if
        i en.x @ i en.y @ i en.z @ cr-new-explosion
        0 i en.on !  100 +to cr-score  200 80 beep
        drop -1 leave
      then
    then
  loop ;
: cr-laser-vs-obstacles ( -- f )
  0  cr-no 0 do
    i ob.on @ if
      cr-li la.z @ i ob.z @ >=   cr-li la.z @ i ob.z @ i ob.d @ + <= and
      i ob.x @ cr-li la.x @ - abs i ob.w @ < and
      cr-li la.y @ i ob.y0 @ >= and   cr-li la.y @ i ob.y1 @ <= and if
        cr-li la.x @ cr-li la.y @ i ob.z @ cr-new-explosion
        0 i ob.on !  50 +to cr-score  200 60 beep
        drop -1 leave
      then
    then
  loop ;
: cr-lasers ( -- )
  cr-nl 0 do
    i la.on @ if
      i to cr-li
      cr-laser-vs-enemies  cr-laser-vs-obstacles  or if 0 cr-li la.on ! then
    then
  loop ;

: cr-ship-hits ( -- )
  cr-no 0 do
    i ob.on @ if
      i ob.z @ cr-sz <=   i ob.z @ i ob.d @ + cr-sz >= and
      i ob.x @ cr-sx - abs i ob.w @ 18 + < and
      cr-sy 16 + i ob.y0 @ > and   cr-sy 16 - i ob.y1 @ < and if
        i ob.x @ cr-sy i ob.z @ cr-new-explosion  0 i ob.on !  cr-damage
      then
    then
  loop
  cr-ne 0 do
    i en.on @ if
      i en.z @ cr-sz - abs 45 <   i en.x @ cr-sx - abs 45 < and   i en.y @ cr-sy - abs 40 < and if
        i en.x @ i en.y @ i en.z @ cr-new-explosion  0 i en.on !  cr-damage
      then
    then
  loop
  cr-nq 0 do
    i bo.on @ if
      i bo.z @ cr-sz - abs 30 <   i bo.x @ cr-sx - abs 24 < and   i bo.y @ cr-sy - abs 24 < and if
        0 i bo.on !  cr-damage
      then
    then
  loop ;

( ---- Timers, difficulty ---- )
: cr-timers ( -- )
  cr-fire-t 0> if -1 +to cr-fire-t then
  cr-inv 0> if -1 +to cr-inv then
  cr-flash 0> if -1 +to cr-flash then
  cr-frame 4 mod 0= if 1 +to cr-score then
  cr-travel 12000 / 1+ 8 min to cr-level
  10 cr-level 2 * + to cr-speed
  -1 +to cr-spawn-o   cr-spawn-o 0< if  cr-new-obstacle  170 cr-level 12 * - 60 max random 50 + to cr-spawn-o  then
  -1 +to cr-spawn-e   cr-spawn-e 0< if  cr-new-enemy     200 cr-level 15 * - 80 max random 60 + to cr-spawn-e  then ;

: cr-new-game ( -- )
  cr-clear  cr-fill-towers
  0 to cr-score  cr-maxshield to cr-shield  1 to cr-level  0 to cr-travel  12 to cr-speed
  0 to cr-sx  0 to cr-sy  0 to cr-bank  0 to cr-inv  0 to cr-flash  0 to cr-fire-t
  60 to cr-spawn-o  150 to cr-spawn-e   0 to cr-state ;

: cr-playing ( -- )   cr-input cr-camera cr-move cr-lasers cr-ship-hits cr-timers ;

: cr-step ( -- )
  1 +to cr-frame
  [char] p cr-just if
    cr-state 0 = if 1 to cr-state else cr-state 1 = if 0 to cr-state then then
  then
  cr-state case
    0 of cr-playing endof
    2 of cr-camera cr-move
         [char] r cr-just key-space cr-just or key-enter cr-just or if cr-new-game then endof
  endcase ;

( ---- Drawing: a tiny 3x5 pixel font ---- )
create cr-digits
  $7b6f , $2c97 , $73e7 , $73cf , $5bc9 , $79cf , $79ef , $7249 , $7bef , $7bcf ,
create cr-font   ( A to Z )
  $2bed , $6bae , $3923 , $6b6e , $79a7 , $79a4 , $396b , $5bed , $7497 , $126a , $5bad , $4927 , $5fed ,
  $6b6d , $2b6a , $6ba4 , $2b7b , $6bad , $388e , $7492 , $5b6f , $5b6a , $5bfd , $5aad , $5a92 , $72a7 ,
: cr-bits { bits x y s }
  15 0 do
    bits 14 i - rshift 1 and if
      x i 3 mod s * +   y i 3 / s * +   s s box
    then
  loop ;
: cr-number { n digits x y s }   ( right aligned in digits places )
  digits 0 do
    n 10 mod cells cr-digits + @   x digits 1- i - s 4 * * +   y s cr-bits
    n 10 / to n
  loop ;
: cr-glyph ( c -- bits )
  32 or dup 97 < over 122 > or if drop 0 exit then
  97 - cells cr-font + @ ;
: cr-text { a n x y s }   n 0 do  a i + c@ cr-glyph   x i 4 s * * +   y s cr-bits  loop ;
: cr-banner { a n rgb }   ( a word in the middle, over a black bar )
  black color   cr-w 2 / 60 -  cr-h 2 / 8 -  120 16 box
  rgb color  a n  cr-w n 8 * 2 - - 2 /  cr-h 2 / 5 -  2 cr-text ;

( ---- Drawing: the sun, the floor, the towers ---- )
: cr-isqrt ( v -- w )   0 begin dup 1+ dup * 2 pick <= while 1+ repeat nip ;
0 value sn-dy   0 value sn-half
: cr-sun ( -- )
  cr-red color
  27 0 do
    i 13 - to sn-dy
    sn-dy 4 >= sn-dy 5 <= and  sn-dy 9 >= sn-dy 10 <= and or 0= if
      sn-dy dup * 169 swap - 0 max cr-isqrt to sn-half
      cr-cx sn-half -   cr-hy 38 - sn-dy +   sn-half 2* 1+ 1 box
    then
  loop ;

: cr-fline ( x1 z1 x2 z2 -- )   ( a line on the floor )
  to t4 to t3 to t2 to t1   t1 cr-floor t2  t3 cr-floor t4  cr-wline ;
: cr-floor-lines ( -- )
  cr-cyan color
  9 0 do
    i 4 - 150 *  dup abs cr-half = 0= if  dup cr-near swap cr-far cr-fline  else drop then
  loop
  10 1 do
    -700  i 200 * cr-travel 200 mod -  700  over  cr-fline
  loop
  cr-red color
  cr-half cr-near  cr-half cr-far  cr-fline
  cr-half negate cr-near  cr-half negate cr-far  cr-fline ;

0 value td-in   0 value td-out   0 value td-z0   0 value td-z1   0 value td-top   0 value td-y
: cr-tower-draw ( i -- )
  dup bd.s @ cr-half * to td-in
  dup bd.s @ cr-half 260 + * to td-out
  dup bd.z @ to td-z0
  dup bd.z @ over bd.d @ + to td-z1
  bd.h @ cr-floor + to td-top
  td-z1 cr-near < if exit then
  cr-cyan color
  td-in cr-floor td-out td-top td-z0 cr-rz
  td-in cr-floor td-z0 td-top td-z1 cr-rx
  td-out td-top td-z0  td-out td-top td-z1  cr-wline
  cr-red color
  cr-floor 70 +  begin dup td-top < while
    to td-y
    td-in td-y td-z0  td-in td-y td-z1  cr-wline
    td-in td-y td-z0  td-out td-y td-z0  cr-wline
    td-y 80 +
  repeat drop ;
: cr-towers-draw ( -- )   cr-nb 0 do i bd.on @ if i cr-tower-draw then loop ;

0 value ob-i
: cr-box-draw ( i -- )
  to ob-i
  ob-i ob.x @ ob-i ob.w @ -  ob-i ob.y0 @   ob-i ob.x @ ob-i ob.w @ +  ob-i ob.y1 @   ob-i ob.z @   cr-rz
  ob-i ob.x @ ob-i ob.w @ -  ob-i ob.y0 @   ob-i ob.x @ ob-i ob.w @ +  ob-i ob.y1 @   ob-i ob.z @ ob-i ob.d @ +   cr-rz
  ob-i ob.x @ ob-i ob.w @ -  ob-i ob.y0 @ ob-i ob.z @   ob-i ob.x @ ob-i ob.w @ -  ob-i ob.y0 @ ob-i ob.z @ ob-i ob.d @ +  cr-wline
  ob-i ob.x @ ob-i ob.w @ +  ob-i ob.y0 @ ob-i ob.z @   ob-i ob.x @ ob-i ob.w @ +  ob-i ob.y0 @ ob-i ob.z @ ob-i ob.d @ +  cr-wline
  ob-i ob.x @ ob-i ob.w @ -  ob-i ob.y1 @ ob-i ob.z @   ob-i ob.x @ ob-i ob.w @ -  ob-i ob.y1 @ ob-i ob.z @ ob-i ob.d @ +  cr-wline
  ob-i ob.x @ ob-i ob.w @ +  ob-i ob.y1 @ ob-i ob.z @   ob-i ob.x @ ob-i ob.w @ +  ob-i ob.y1 @ ob-i ob.z @ ob-i ob.d @ +  cr-wline
  cr-red color
  ob-i ob.x @ ob-i ob.w @ -  ob-i ob.y0 @ ob-i ob.y1 @ + 2/ ob-i ob.z @
  ob-i ob.x @ ob-i ob.w @ +  ob-i ob.y0 @ ob-i ob.y1 @ + 2/ ob-i ob.z @  cr-wline ;
: cr-obstacles-draw ( -- )
  cr-no 0 do
    i ob.on @ if
      i ob.w @ cr-half = if cr-red else cr-cyan then color
      i ob.z @ i ob.d @ + cr-near > if i cr-box-draw then
    then
  loop ;

( ---- Drawing: fighters, shots, explosions, the ship ---- )
\ an octahedron: six points, twelve edges
create cr-vx  35 , -35 , 0 , 0 , 0 , 0 ,
create cr-vy  0 , 0 , 22 , -22 , 0 , 0 ,
create cr-vz  0 , 0 , 0 , 0 , 35 , -35 ,
create cr-ex  0 , 0 , 0 , 0 , 1 , 1 , 1 , 1 , 2 , 2 , 3 , 3 ,
create cr-ey  2 , 3 , 4 , 5 , 2 , 3 , 4 , 5 , 4 , 5 , 4 , 5 ,
create cr-rxs 6 cells allot   create cr-rys 6 cells allot   create cr-rzs 6 cells allot
0 value en-i   0 value en-a   0 value fe-a   0 value fe-b
: cr-edge-draw ( -- )
  fe-a cells cr-rxs + @  fe-a cells cr-rys + @  fe-a cells cr-rzs + @
  fe-b cells cr-rxs + @  fe-b cells cr-rys + @  fe-b cells cr-rzs + @  cr-wline ;
: cr-fighter-draw ( i -- )
  to en-i  cr-frame 3 / en-i en.p @ + to en-a
  6 0 do
    i cells cr-vx + @  en-a cr-cos * i cells cr-vz + @ en-a cr-sin * +  100 /  en-i en.x @ +  i cells cr-rxs + !
    i cells cr-vy + @  en-i en.y @ +  i cells cr-rys + !
    i cells cr-vz + @  en-a cr-cos * i cells cr-vx + @ en-a cr-sin * -  100 /  en-i en.z @ +  i cells cr-rzs + !
  loop
  12 0 do  i cells cr-ex + @ to fe-a   i cells cr-ey + @ to fe-b   cr-edge-draw  loop ;
: cr-fighters-draw ( -- )
  white color
  cr-ne 0 do i en.on @ if i cr-fighter-draw then loop ;

: cr-bolts-draw ( -- )
  cr-red color
  cr-nq 0 do
    i bo.on @ if
      i bo.x @ i bo.y @ i bo.z @   i bo.x @ i bo.y @ i bo.z @ 40 +   cr-wline
      i bo.z @ cr-near > if
        white color
        i bo.x @ i bo.z @ cr-px 1-   i bo.y @ i bo.z @ cr-py 1-   3 3 box
        cr-red color
      then
    then
  loop ;

: cr-lasers-draw ( -- )
  white color
  cr-nl 0 do
    i la.on @ if  i la.x @ i la.y @ i la.z @   i la.x @ i la.y @ i la.z @ 45 +   cr-wline  then
  loop ;

create cr-rdx  10 , 7 , 0 , -7 , -10 , -7 , 0 , 7 ,
create cr-rdy  0 , 7 , 10 , 7 , 0 , -7 , -10 , -7 ,
0 value xp-x   0 value xp-y   0 value xp-r   0 value xp-a
: cr-explosion-draw ( i -- )
  dup ex.a @ to xp-a
  dup ex.x @ over ex.z @ cr-px to xp-x
  dup ex.y @ over ex.z @ cr-py to xp-y
  ex.z @ cr-near max   xp-a 5 * 8 +  cr-f * swap / to xp-r
  8 0 do
    i 1 and if cr-red else white then color
    xp-x  i cells cr-rdx + @ xp-r * 20 / +   xp-y  i cells cr-rdy + @ xp-r * 20 / +
    xp-x  i cells cr-rdx + @ xp-r * 10 / +   xp-y  i cells cr-rdy + @ xp-r * 10 / +   line
  loop ;
: cr-explosions-draw ( -- )   cr-nx 0 do i ex.on @ if i cr-explosion-draw then loop ;

0 value sh-x   0 value sh-y
: cr-sp ( x y -- col row )   ( a point of the ship, leaning by the bank )
  over cr-bank * 12 / +  sh-y +  swap sh-x +  swap ;
: cr-sline ( x1 y1 x2 y2 -- )   cr-sp 2swap cr-sp 2swap line ;
: cr-ship-draw ( -- )
  cr-inv 0> cr-frame 4 and and if exit then
  cr-sx cr-sz cr-px to sh-x   cr-sy cr-sz cr-py to sh-y
  white color
  0 -9  -22 6 cr-sline    0 -9  22 6 cr-sline
  -22 6  -7 4 cr-sline    22 6  7 4 cr-sline
  -7 4  0 7 cr-sline      7 4  0 7 cr-sline ;

: cr-sight-draw ( -- )
  cr-cyan color
  cr-cx  cr-sy 700 cr-py  to sh-y to sh-x      \ the cross stays on the middle column, the vanishing line
  sh-x 6 - sh-y  sh-x 6 + sh-y  line   sh-x sh-y 6 -  sh-x sh-y 6 +  line
  $601818 color  cr-cx 30 -  cr-h 12 -  cr-maxshield 10 *  3 box     \ the shield gauge
  cr-red color   cr-cx 30 -  cr-h 12 -  cr-shield 0 max 10 *  3 box ;

: cr-hud-draw ( -- )
  white color  cr-score 6 4 4 2 cr-number
  $808080 color  s" LV" 62 4 2 cr-text   cr-level 1 80 4 2 cr-number
  $808080 color  cr-best 6 cr-w 70 - 12 1 cr-number ;

: cr-draw-all ( -- )
  cr-camera
  black color cls
  cr-sun cr-floor-lines cr-towers-draw cr-obstacles-draw
  cr-fighters-draw cr-bolts-draw cr-lasers-draw cr-explosions-draw
  cr-sight-draw cr-ship-draw cr-hud-draw
  cr-flash 0> cr-frame 2 and and if cr-red color  0 0 cr-w 2 box  0 cr-h 2 - cr-w 2 box then
  cr-state 1 = if s" PAUSE" yellow cr-banner then
  cr-state 2 = if s" GAME OVER" cr-red cr-banner then
  flip ;

( ---- The loop ---- )
0 value cr-last
: cr-run-steps ( -- )
  cr-state 1 = if ticks to cr-last then
  ticks cr-last - 160 > if ticks 16 - to cr-last then
  begin ticks cr-last - 16 >= while  16 +to cr-last  cr-step  repeat ;
: cr-one-frame ( -- )
  events
  key-esc pressed? if quit! then
  cr-run-steps cr-draw-all
  dt 16 < if 16 dt - delay then ;
: cr-run ( -- )   begin cr-one-frame quit? until close-screen ;

[defined] corridor-no-run [if] [else]
3 to zoom
cr-w cr-h screen
s" Corridor" title
cr-new-game
ticks to cr-last
cr-run
script-bye
[then]
