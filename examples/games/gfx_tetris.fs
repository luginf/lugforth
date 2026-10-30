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

\ Tetris with SDL2, after tetris.r3 of r3forth (Pablo Reda, MIT license):
\ its score table (40, 100, 300, 1200 for 1 to 4 lines) and the falling
\ speed that grows; here with the seven pieces, wall kicks, a ghost piece,
\ levels and the end of the game. Only libSDL2 is needed.
\   left / right   move           up or x   turn
\   down           drop faster    space     drop at once
\   p              pause          esc       quit
\   r, space or enter after the end: a new game
\ Beside the well: the next piece, the score, the lines (green) and the
\ level (yellow), with a tiny 3x5 pixel font. Red tint: the game is over.
\ The bare-metal boards (ports/rpi, ports/efi) run it too.

sdl2

10 constant cols   20 constant rows   11 constant cell
8 constant well-x   10 constant well-y
200 constant w   240 constant h

create grid cols rows * allot   ( 0 empty, else the kind of piece 1..7 )

( ---- Pieces: their size of box, then four cells x y in that box ---- )
create shapes
  4 c,  0 c, 1 c,  1 c, 1 c,  2 c, 1 c,  3 c, 1 c,   ( I )
  2 c,  0 c, 0 c,  1 c, 0 c,  0 c, 1 c,  1 c, 1 c,   ( O )
  3 c,  1 c, 0 c,  0 c, 1 c,  1 c, 1 c,  2 c, 1 c,   ( T )
  3 c,  1 c, 0 c,  2 c, 0 c,  0 c, 1 c,  1 c, 1 c,   ( S )
  3 c,  0 c, 0 c,  1 c, 0 c,  1 c, 1 c,  2 c, 1 c,   ( Z )
  3 c,  0 c, 0 c,  0 c, 1 c,  1 c, 1 c,  2 c, 1 c,   ( J )
  3 c,  2 c, 0 c,  0 c, 1 c,  1 c, 1 c,  2 c, 1 c,   ( L )
create piece-colors
  $00d8e0 , $e8d000 , $a040e0 , $30c030 , $e03030 , $3060f0 , $f08020 ,

: kind-color ( k -- $rrggbb ) 1- cells piece-colors + @ ;

0 value kind   0 value turn   0 value px   0 value py   0 value next-kind
0 value cx   0 value cy

: shape ( -- a ) kind 1- 9 * shapes + ;

: cell-xy ( i -- x y )   ( cell i of the falling piece, turned, in the well )
  2* 1+ shape + dup c@ to cx  1+ c@ to cy
  turn 0 ?do  shape c@ 1- cy -  cx to cy  to cx  loop
  cx px +  cy py + ;

( ---- The well ---- )
: grid-at ( x y -- a ) cols * + grid + ;

: solid? ( x y -- f )   ( a wall, the floor or a block; above the top is free )
  over 0<  2 pick cols >= or  over rows >= or if 2drop -1 exit then
  dup 0< if 2drop 0 exit then
  grid-at c@ 0<> ;

: fits? ( -- f )
  4 0 do i cell-xy solid? if 0 unloop exit then loop -1 ;

0 value ox   0 value oy   0 value ot
: try ( dx dy dturn -- f )   ( move or turn, if the piece fits there )
  px to ox  py to oy  turn to ot
  turn + 3 and to turn   py + to py   px + to px
  fits? dup 0= if ox to px  oy to py  ot to turn then ;

: rotate ( -- )   ( turned in place, else pushed aside: the wall kicks )
  0 0 1 try if exit then   -1 0 1 try if exit then   1 0 1 try if exit then
  -2 0 1 try if exit then   2 0 1 try drop ;

: full? ( y -- f )
  cols 0 do i over grid-at c@ 0= if drop 0 unloop exit then loop drop -1 ;

: remove-row ( y -- )   ( the rows above come down one )
  grid  grid cols +  rot cols *  cmove>   grid cols erase ;

0 value cleared
: clear-rows ( -- )
  0 to cleared   rows 1-
  begin dup 0< 0= while
    dup full? if dup remove-row  1 +to cleared else 1- then
  repeat drop ;

( ---- The game ---- )
0 value score   0 value lines   0 value state   ( 0 playing, 1 paused, 2 over )
0 value last-fall   0 value best
create line-points 0 , 40 , 100 , 300 , 1200 ,

: level ( -- n ) lines 10 / ;
: fall-ms ( -- ms ) 800 level 70 * - 100 max ;

: game-over ( -- )   2 to state   score best max to best
  392 150 beep  330 150 beep  262 400 beep ;

: spawn ( -- )
  next-kind to kind   7 random 1+ to next-kind
  0 to turn   kind 2 = if 4 else 3 then to px   0 to py
  ticks to last-fall
  fits? 0= if game-over then ;

: lock ( -- )   ( the piece becomes part of the well )
  4 0 do kind i cell-xy grid-at c! loop
  clear-rows
  cleared cells line-points + @  level 1+ *  +to score
  cleared +to lines
  cleared if 523 60 beep 659 60 beep 784 90 beep else 110 25 beep then
  spawn ;

: fall ( -- ) 0 1 0 try 0= if lock then   ticks to last-fall ;

: hard-drop ( -- )
  begin 0 1 0 try while 2 +to score repeat lock ;

: new-game ( -- )
  grid cols rows * erase
  0 to score  0 to lines  0 to state
  7 random 1+ to next-kind   spawn ;

( ---- Keys: once when pressed, then repeated while held ---- )
create was 256 allot   was 256 erase
: just ( k -- f )   ( pressed now, not at the last look )
  dup pressed? swap 255 and was + 2dup c@ 0= and >r c! r> ;

0 value held-since   0 value last-repeat
: repeat? ( k -- f )   ( left and right: at once, then after 170 ms every 50 ms )
  dup just if drop ticks dup to held-since to last-repeat -1 exit then
  pressed? 0= if 0 exit then
  ticks held-since - 170 < if 0 exit then
  ticks last-repeat - 50 < if 0 exit then
  ticks to last-repeat -1 ;

: playing ( -- )
  key-left repeat? if -1 0 0 try drop then
  key-right repeat? if 1 0 0 try drop then
  key-up just [char] x just or if rotate then
  key-space just if hard-drop exit then
  ticks last-fall -  key-down pressed? if 40 else fall-ms then  >= if fall then ;

: update ( -- )
  [char] p just if state 2 < if 1 state - to state ticks to last-fall then then
  state 0 = if playing exit then
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

: block { x y c }   ( a cell of the well in color c, with a lighter top left edge )
  x cell * well-x +  y cell * well-y +  { px py }
  c color  px py cell 1- cell 1- box
  white color 110 alpha  px py cell 1- 1 box  px py 1 cell 1- box  255 alpha ;

: draw-well ( -- )
  $202838 color  well-x 2 - well-y 2 - cols cell * 4 + rows cell * 4 + box
  $0c0c14 color  well-x well-y cols cell * rows cell * box
  rows 0 do cols 0 do
    i j grid-at c@ ?dup if i j rot kind-color block then
  loop loop ;

: draw-ghost ( -- )   ( where the piece would land: its outline )
  py  begin 0 1 0 try 0= until
  kind kind-color color  4 0 do
    i cell-xy cell * well-y + swap cell * well-x + swap cell 1- cell 1- frame
  loop
  to py ;

: draw-piece ( -- ) 4 0 do i cell-xy kind kind-color block loop ;

: draw-next ( -- )   ( in the panel, with the size of box of that piece )
  kind turn px py   next-kind to kind  0 to turn
  12 to px  1 to py
  4 0 do i cell-xy kind kind-color block loop
  to py to px to turn to kind ;

: draw-panel ( -- )
  $202838 color  130 well-y w 134 - 52 box
  draw-next
  white color    score 6 130 76 2 draw-number
  $60e060 color  lines 4 130 100 3 draw-number
  $e8d000 color  level 2 130 130 3 draw-number
  $808080 color  best 6 130 222 2 draw-number ;

: draw-all ( -- )
  $101018 color cls
  draw-well
  state 2 < if draw-ghost draw-piece then
  draw-panel
  state 1 = if $000000 color 140 alpha  well-x well-y cols cell * rows cell * box 255 alpha then
  state 2 = if red color 90 alpha  0 0 w h box 255 alpha then
  flip ;

: step ( -- )
  events
  key-esc pressed? if quit! then
  update draw-all
  dt 16 < if 16 dt - delay then ;

: run ( -- ) begin step quit? until close-screen ;

[defined] tetris-no-run [if] [else]
ms-ticks 1 or rand-seed !
3 to zoom
w h screen
s" Tetris" title
new-game
run
script-bye
[then]
