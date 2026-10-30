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

\ Space Invaders with SDL2, in plain Forth: 55 aliens that march faster as they
\ die, a cannon, three bombs at most, four shields eaten pixel by pixel, the
\ flying saucer, waves that start lower. Only libSDL2 is needed (the bare-metal
\ boards run it too).
\   left, right or a, d   move       space, up or w   fire (one shot at a time)
\   p                     pause      esc              quit
\   r, space or enter after the end: again
\ At the top: the score (white), the wave (gray) and the best score (gray), in a
\ tiny 3x5 pixel font. Spare cannons at the bottom. Red tint: the game is over.

sdl2

224 constant w   240 constant h
11 constant cols   5 constant rows   cols rows * constant nal
16 constant pit        ( an alien lives in a cell of 16x16 pixels )
208 constant py        ( top of the cannon )
184 constant sy        ( top of the shields )
22 constant sw   16 constant sh   sw sh * constant ssize

create alive nal allot                  ( 1 for a living alien, row after row )
create shields 4 ssize * allot          ( four shields of sw x sh pixels, 1 where there is stuff )
create bomb-x 3 cells allot   create bomb-y 3 cells allot   create bomb-on 3 cells allot

0 value ax   0 value ay   1 value dir   0 value alive-n   0 value anim
0 value last-move   0 value state   ( 0 playing, 1 paused, 2 over, 3 cannon hit )
0 value score   0 value best   3 value lives   0 value level
0 value px   0 value bullet-on   0 value bx   0 value by
0 value ufo-on   0 value ufo-x   2 value ufo-dir   0 value ufo-next
0 value hit-until   0 value boom-until   0 value boom-x   0 value boom-y
0 value next-bomb   0 value lc   0 value rc   0 value lr   0 value tone-i   0 value nx

( ---- Sprites: one byte per pixel, rows written as pictures ---- )
: row, ( "picture" -- )   ( 1 for each X; parse-name: s" would sit at HERE, where c, writes )
  parse-name 0 do dup i + c@ [char] X = 1 and c, loop drop ;

create aliens   ( 3 kinds, 2 frames each, 12x8 )
  row, .....XX.....  row, ....XXXX....  row, ...XXXXXX...  row, ..XX.XX.XX..
  row, ..XXXXXXXX..  row, ....X..X....  row, ...X.XX.X...  row, ..X.X..X.X..
  row, .....XX.....  row, ....XXXX....  row, ...XXXXXX...  row, ..XX.XX.XX..
  row, ..XXXXXXXX..  row, ...X.XX.X...  row, ..X......X..  row, ...X....X...
  row, ..X.....X...  row, ...X...X....  row, ..XXXXXXX...  row, .XX.XXX.XX..
  row, XXXXXXXXXXX.  row, X.XXXXXXX.X.  row, X.X.....X.X.  row, ...XX.XX....
  row, ..X.....X...  row, X..X...X..X.  row, X.XXXXXXX.X.  row, XXX.XXX.XXX.
  row, XXXXXXXXXXX.  row, .XXXXXXXXX..  row, ..X.....X...  row, .X.......X..
  row, ....XXXX....  row, .XXXXXXXXXX.  row, XXXXXXXXXXXX  row, XXX..XX..XXX
  row, XXXXXXXXXXXX  row, ...XX..XX...  row, ..XX.XX.XX..  row, XX........XX
  row, ....XXXX....  row, .XXXXXXXXXX.  row, XXXXXXXXXXXX  row, XXX..XX..XXX
  row, XXXXXXXXXXXX  row, ..XXX..XXX..  row, .XX..XX..XX.  row, ..XX....XX..

create cannon   ( 13x6 )
  row, ......X......  row, .....XXX.....  row, .XXXXXXXXXXX.
  row, XXXXXXXXXXXXX  row, XXXXXXXXXXXXX  row, XXXXXXXXXXXXX

create ufo      ( 16x7 )
  row, ......XXXX......  row, ....XXXXXXXX....  row, ...XXXXXXXXXX...
  row, ..XX.XX.XX.XX.XX  row, XXXXXXXXXXXXXXXX  row, ...XXX..XX..XXX.
  row, ....X........X..

create boom     ( 12x8, an alien that blows up )
  row, .X..X..X..X.  row, ..X..XX..X..  row, ...X.XX.X...  row, XX.XX..XX.XX
  row, XX.XX..XX.XX  row, ...X.XX.X...  row, ..X..XX..X..  row, .X..X..X..X.

create kcolor  $e070e0 , $60e0e0 , $60e060 ,

: spr ( n -- a )   96 * aliens + ;     ( kind * 2 + frame )
: kind ( row -- 0 squid, 1 crab, 2 octopus )   dup 0= if exit then  3 < if 1 else 2 then ;
: pts ( row -- n )   kind 2 swap - 1+ 10 * ;

0 value rs
: draw-row { a n x y }   ( a row of n bytes: a box for each run of 1s )
  -1 to rs
  n 1+ 0 do
    i n < if a i + c@ else 0 then
    if rs 0< if i to rs then
    else rs 0< 0= if x rs +  y  i rs -  1 box  -1 to rs then then
  loop ;
: draw-spr { a n m x y }   ( n x m pixels )
  m 0 do a i n * +  n  x  y i +  draw-row loop ;

( ---- The formation ---- )
: alive@ ( c r -- a )   cols * + alive + ;
: ax@ ( c -- x )   pit * ax + 2 + ;
: ay@ ( r -- y )   pit * ay + ;

: scan ( -- )   ( alive-n and the columns and the row at the edges )
  cols to lc  -1 to rc  -1 to lr  0 to alive-n
  rows 0 do cols 0 do
    i j alive@ c@ if  1 +to alive-n  i lc min to lc  i rc max to rc  j lr max to lr  then
  loop loop ;

( ---- Shields ---- )
: shield-at ( s x y -- a )   sw * + swap ssize * + shields + ;
: shape-on? { x y }   ( the shape of a shield: cut corners and a notch below )
  y 4 < if  x 4 y - <  x sw 1- 4 y - - > or  if 0 exit then then
  y 10 >= x 8 14 within and if 0 exit then
  -1 ;
: new-shield { s }   sh 0 do sw 0 do  i j shape-on?  s i j shield-at c!  loop loop ;
: new-shields ( -- )   4 0 do i new-shield loop ;

: erode { s rx ry }   ( a bite of 5x4 pixels, ragged )
  2 -2 do  3 -2 do
    rx i + 0 sw within  ry j + 0 sh within and
    4 random 0<>  i j or 0=  or and if  0  s rx i + ry j + shield-at  c!  then
  loop loop ;

0 value shit   0 value rx   0 value ry
: shield-hit? { x y }   ( a pixel at x y on a shield: eats a bite of it )
  0 to shit
  4 0 do
    x 24 i 48 * + - to rx   y sy - to ry
    shit 0=  rx 0 sw within and  ry 0 sh within and if
      i rx ry shield-at c@ if  i rx ry erode  -1 to shit then
    then
  loop shit ;

( ---- Shots ---- )
: boom-at ( c r -- )   ay@ to boom-y  ax@ to boom-x  ticks 160 + to boom-until ;
: zap ( c r -- )   ( kill the alien )
  2dup alive@ 0 swap c!
  dup pts +to score  -1 +to alive-n
  boom-at  120 40 beep ;

0 value tc   0 value tr
: shoot-alien ( x y -- f )   ( hits the alien under the point, if any )
  ay -  dup 0< if 2drop 0 exit then
  pit /mod swap 8 >= if 2drop 0 exit then  to tr
  ax - 2 -  dup 0< if drop 0 exit then
  pit /mod swap 12 >= if drop 0 exit then  to tc
  tc cols <  tr rows < and 0= if 0 exit then
  tc tr alive@ c@ 0= if 0 exit then
  tc tr zap -1 ;

create ufo-pts 50 , 100 , 150 , 300 ,
: shoot-ufo ( x y -- f )
  ufo-on 0= if 2drop 0 exit then
  24 31 within 0= if drop 0 exit then
  ufo-x - 0 16 within 0= if 0 exit then
  0 to ufo-on  ticks 15000 + to ufo-next
  4 random cells ufo-pts + @ +to score  600 80 beep  900 80 beep  -1 ;

: bullet-1 ( -- )   ( one pixel up, and what it meets )
  -1 +to by
  by 14 < if 0 to bullet-on exit then
  bx by shoot-alien if 0 to bullet-on exit then
  bx by shoot-ufo if 0 to bullet-on exit then
  bx by shield-hit? if 0 to bullet-on then ;
: bullet-move ( -- )   4 0 do bullet-on if bullet-1 then loop ;

: player-hit ( -- )
  state 0 <> if exit then
  3 to state  ticks 1400 + to hit-until  -1 +to lives
  200 150 beep  150 150 beep  100 300 beep ;

0 value bi   0 value bc
: bomb-kill ( -- )   0 bi cells bomb-on + ! ;
: bomb-1 ( -- )   ( one pixel down )
  1 bi cells bomb-y + +!
  bi cells bomb-x + @   bi cells bomb-y + @ 4 +        ( x y of the tip )
  dup h 24 - >= if 2drop bomb-kill exit then
  2dup shield-hit? if 2drop bomb-kill exit then
  dup py >= if over px - 0 13 within if 2drop bomb-kill player-hit exit then then
  2drop ;
: bombs-move ( -- )
  3 0 do
    i to bi
    bi cells bomb-on + @ if
      2 0 do bi cells bomb-on + @ if bomb-1 then loop
    then
  loop ;

: free-bomb ( -- i | -1 )   -1  3 0 do i cells bomb-on + @ 0= if drop i leave then loop ;
: low-alien ( c -- r | -1 )   -1 rows 0 do over i alive@ c@ if drop i then loop nip ;
: drop-bomb ( -- )   ( from the lowest alien of a random column )
  free-bomb dup 0< if drop exit then
  cols random to bc  bc low-alien dup 0< if 2drop exit then       ( slot row )
  ay@ 8 +  over cells bomb-y + !
  bc ax@ 6 +  over cells bomb-x + !
  1 swap cells bomb-on + ! ;

( ---- The march ---- )
create tones 110 , 98 , 87 , 82 ,
: step-ms ( -- ms )   alive-n 15 * 20 + level 50 * - 16 max ;

: march ( -- )
  scan  alive-n 0= if exit then
  1 anim xor to anim
  dir 4 * ax + to nx
  dir 0> if nx rc pit * + 14 + w 2 - >  else nx lc pit * + 2 + 2 <  then
  if  8 +to ay  dir negate to dir  else  nx to ax  then
  ay lr pit * + 8 + py 8 - >= if  2 to state  score best max to best  then
  tone-i 3 and cells tones + @ 30 beep  1 +to tone-i ;

: clear-bombs ( -- )   3 0 do 0 i cells bomb-on + ! loop  0 to bullet-on ;

: new-wave ( -- )
  nal 0 do 1 alive i + c! loop  nal to alive-n
  24 to ax   40 level 4 min 8 * + to ay   1 to dir  0 to anim
  new-shields  clear-bombs  ticks to last-move  ticks 1500 + to next-bomb ;

: new-game ( -- )
  0 to score  3 to lives  0 to level  105 to px  0 to ufo-on  0 to boom-until
  ticks 12000 + to ufo-next  0 to state  new-wave ;

: next-wave ( -- )   1 +to level  new-wave  880 60 beep ;

: ufo-update ( -- )
  ufo-on if
    ufo-dir +to ufo-x
    ufo-x -20 <  ufo-x w 4 + > or if  0 to ufo-on  ticks 15000 + to ufo-next  then
  else
    alive-n 8 >=  ticks ufo-next >= and if
      1 to ufo-on  2 random if 2 else -2 then to ufo-dir
      ufo-dir 0> if -16 else w then to ufo-x
    then
  then ;

( ---- Keys ---- )
create was 256 allot   was 256 erase
: just ( k -- f )   ( pressed now, not at the last look )
  dup pressed? swap 255 and was + 2dup c@ 0= and >r c! r> ;

: steer ( -- )
  key-left pressed? [char] a pressed? or if -2 +to px then
  key-right pressed? [char] d pressed? or if 2 +to px then
  px 2 max w 15 - min to px ;

: fire ( -- )
  bullet-on 0= if
    key-space pressed? key-up pressed? or [char] w pressed? or if
      1 to bullet-on  px 6 + to bx  py 4 - to by  1000 25 beep
    then
  then ;

: after-hit ( -- )
  lives 0= if  2 to state  score best max to best  exit  then
  0 to state  105 to px  clear-bombs  ticks to last-move ;

: playing ( -- )
  alive-n 0= if next-wave exit then
  steer fire bullet-move bombs-move
  ticks last-move - step-ms >= if march ticks to last-move then
  ticks next-bomb >= if  drop-bomb  ticks 300 + 700 level 60 * - 200 max random + to next-bomb  then
  ufo-update ;

: update ( -- )
  [char] p just if
    state 0 = if 1 to state else state 1 = if 0 to state ticks to last-move then then
  then
  state 0 = if playing exit then
  state 3 = if ticks hit-until >= if after-hit then exit then
  state 2 = if
    [char] r just key-space just or key-enter just or if new-game then
  then ;

( ---- Drawing ---- )
create digit-bits
  $7b6f , $2c97 , $73e7 , $73cf , $5bc9 , $79cf , $79ef , $7249 , $7bef , $7bcf ,

: draw-digit { d x y s }
  d cells digit-bits + @ { bits }
  15 0 do
    bits 14 i - rshift 1 and if
      x i 3 mod s * +   y i 3 / s * +   s s box
    then
  loop ;

: draw-number { n digits x y s }   ( right aligned in digits places )
  digits 0 do
    n 10 mod   x digits 1- i - s 4 * * +   y s draw-digit
    n 10 / to n
  loop ;

: draw-aliens ( -- )
  rows 0 do
    i kind cells kcolor + @ color
    cols 0 do
      i j alive@ c@ if
        j kind 2* anim + spr  12 8  i ax@  j ay@  draw-spr
      then
    loop
  loop ;

: draw-shields ( -- )
  $40c040 color
  4 0 do  shields i ssize * +  sw sh  24 i 48 * +  sy  draw-spr  loop ;

: draw-bombs ( -- )
  $f0f0f0 color
  3 0 do
    i cells bomb-on + @ if  i cells bomb-x + @  i cells bomb-y + @  1 4 box  then
  loop ;

: draw-cannon ( x y -- )   >r >r cannon 13 6 r> r> draw-spr ;

: draw-all ( -- )
  black color  0 0 w h box
  draw-aliens draw-shields draw-bombs
  ticks boom-until < if  white color  boom 12 8 boom-x boom-y draw-spr  then
  ufo-on if  $e04040 color  ufo 16 7 ufo-x 24 draw-spr  then
  bullet-on if  white color  bx by 1 4 box  then
  state 3 = if ticks 100 / 1 and if red else yellow then else $40e040 color then
  px py draw-cannon
  $40e040 color  0 216 w 1 box
  lives 1- 0 max 0 ?do  8 i 16 * +  228 draw-cannon  loop
  white color    score 5 8 4 2 draw-number
  $808080 color  level 1+ 2 100 4 2 draw-number   best 5 w 48 - 4 2 draw-number
  state 1 = if black color 140 alpha 0 0 w h box then
  state 2 = if red color 90 alpha 0 0 w h box then
  flip ;

: step ( -- )
  events
  key-esc pressed? if quit! then
  update draw-all
  dt 16 < if 16 dt - delay then ;

: run ( -- )   begin step quit? until close-screen ;

[defined] invaders-no-run [if] [else]
ms-ticks 1 or rand-seed !
3 to zoom
w h screen
s" Space Invaders" title
new-game
run
script-bye
[then]
