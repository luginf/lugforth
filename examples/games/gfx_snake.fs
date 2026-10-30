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

\ Snake with SDL2, after snake.r3 of r3forth (Pablo Reda, MIT license): the
\ snake goes through the edges and comes back on the other side, grows by
\ one for each fruit and goes faster; here it also dies when it bites
\ itself. Only libSDL2 is needed.
\   arrows or wasd   turn        p     pause
\   esc              quit        r, space or enter after the end: again
\ At the top: the score (white) and the best one (gray), in a tiny 3x5 pixel
\ font. Red tint: the game is over. The bare-metal boards run it too.

sdl2

32 constant cols   22 constant rows   10 constant cell   20 constant top
cols cell * constant w   rows cell * top + constant h
cols rows * constant ncells

create body ncells cells allot   ( the cells of the snake, a ring: head at head )
create taken ncells allot        ( 1 where the snake is )
0 value head   0 value len   0 value grow
0 value dir   0 value next-dir   ( 0 right, 1 down, 2 left, 3 up )
0 value fruit   0 value score   0 value best
0 value state   ( 0 playing, 1 paused, 2 over )
0 value last-move
5 constant start-len

: cell-x ( c -- x ) cols mod ;
: cell-y ( c -- y ) cols / ;
: at ( n -- c )   ( the cell n steps behind the head )
  head swap - ncells + ncells mod cells body + @ ;

: place-fruit ( -- )   ( on a free cell )
  begin ncells random dup taken + c@ while drop repeat to fruit ;

: new-game ( -- )
  taken ncells erase
  start-len to len   0 to head   0 to grow
  start-len 0 do
    rows 2 / cols * 4 + i +   dup taken + 1 swap c!
    i cells body + !
  loop
  start-len 1- to head
  0 to dir  0 to next-dir  0 to score  0 to state
  place-fruit  ticks to last-move ;

: step-ms ( -- ms ) 140 score 3 * - 60 max ;

: ahead ( -- c )   ( the next cell of the head, through the edges )
  0 at dup cell-x swap cell-y   ( x y )
  dir 0 = if swap 1+ swap then
  dir 1 = if 1+ then
  dir 2 = if swap 1- swap then
  dir 3 = if 1- then
  rows + rows mod cols * swap cols + cols mod + ;

: die ( -- )   2 to state  score best max to best
  330 120 beep  262 120 beep  196 400 beep ;

: move-to { c }   ( the head goes on cell c )
  grow if -1 +to grow 1 +to len
  else len 1- at taken + 0 swap c! then   ( the tail leaves its cell )
  c taken + c@ if die exit then
  head 1+ ncells mod to head   c head cells body + !   1 c taken + c!
  c fruit = if 1 +to score  3 +to grow  880 30 beep
    len grow + ncells < if place-fruit then
  then ;

: advance ( -- ) next-dir to dir  ahead move-to ;

( ---- Keys ---- )
create was 256 allot   was 256 erase
: just ( k -- f )   ( pressed now, not at the last look )
  dup pressed? swap 255 and was + 2dup c@ 0= and >r c! r> ;

: turn-to ( d -- )   ( never straight back into the neck )
  dup dir 2 + 3 and = if drop exit then  to next-dir ;

: steer ( -- )
  key-right pressed? [char] d pressed? or if 0 turn-to then
  key-down pressed? [char] s pressed? or if 1 turn-to then
  key-left pressed? [char] a pressed? or if 2 turn-to then
  key-up pressed? [char] w pressed? or if 3 turn-to then ;

: update ( -- )
  [char] p just if state 2 < if 1 state - to state ticks to last-move then then
  state 0 = if
    steer
    ticks last-move - step-ms >= if advance ticks to last-move then exit
  then
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

: cell-box ( c inset -- )   ( a square on cell c, inset pixels in from its edge )
  >r dup cell-x cell * r@ +  swap cell-y cell * top + r@ +
  cell r@ 2* - dup  r> drop box ;

: draw-snake ( -- )
  len 0 do
    i 0= if $f0f040 else i 2 mod if $30b040 else $40c850 then then color
    i at 1 cell-box
  loop
  0 0 0 rgb  0 at   ( the eyes, on the side the head goes to )
  dup cell-x cell * 4 +  swap cell-y cell * top + 4 +  2 2 box ;

: draw-all ( -- )
  $182028 color 0 0 w top box
  $0c140c color 0 top w h top - box
  $e03030 color fruit cell-x cell * cell 2 / +  fruit cell-y cell * top + cell 2 / +  4 disc
  draw-snake
  white color   score 3 4 4 3 draw-number
  $808080 color best 3 w 48 - 4 3 draw-number
  state 1 = if black color 140 alpha 0 top w h top - box then
  state 2 = if red color 90 alpha 0 0 w h box then
  flip ;

: step ( -- )
  events
  key-esc pressed? if quit! then
  update draw-all
  dt 16 < if 16 dt - delay then ;

: run ( -- ) begin step quit? until close-screen ;

[defined] snake-no-run [if] [else]
ms-ticks 1 or rand-seed !
3 to zoom
w h screen
s" Snake" title
new-game
run
script-bye
[then]
