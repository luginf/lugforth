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

\ Minesweeper with SDL2, after minesweeper.r3 of r3forth (Pablo Reda, MIT
\ license): its three levels and its way of opening the empty cells with a
\ list rather than by recursion; here the first cell opened is never a
\ mine, a click on a number opens around it when its flags are all there,
\ and the keyboard works as well as the mouse. Only libSDL2 is needed.
\   left click, space or enter   open (on a number: open around it)
\   right click or f             put or take away a flag
\   arrows                       move the yellow frame
\   1 2 3   a new game: 9x9 and 10 mines, 16x16 and 40, 30x16 and 99
\   r or a click on the face     a new game, same size      esc   quit
\ At the top: the mines left to find, the face (green: won, red: lost) and
\ the seconds. The bare-metal boards run it too.

sdl2

12 constant cell   24 constant top
368 constant w   220 constant h
30 16 * constant max-cells

create board max-cells allot   ( per cell: 1 a mine, 2 open, 4 a flag )
1 constant mine   2 constant opened   4 constant flag
9 value cols   9 value rows   10 value mines
0 value placed   0 value state   ( 0 playing, 1 lost, 2 won )
0 value open-count   0 value flags   0 value hit
0 value start-ms   0 value end-ms
0 value kx   0 value ky   ( the frame moved with the keys )

: idx ( x y -- i ) cols * + ;
: in? ( x y -- f ) dup 0< 0= swap rows < and  swap dup 0< 0= swap cols < and  and ;
: has? ( i bit -- f ) swap board + c@ and 0<> ;
: set ( i bit -- ) swap board + dup c@ rot or swap c! ;

: mines-near { x y }   ( -- n )
  0  2 -1 do 2 -1 do
    x i + y j + 2dup in? if idx mine has? if 1+ then else 2drop then
  loop loop ;

: flags-near { x y }   ( -- n )
  0  2 -1 do 2 -1 do
    x i + y j + 2dup in? if idx flag has? if 1+ then else 2drop then
  loop loop ;

( ---- The mines, laid at the first cell opened: none on it or around it ---- )
: near? { c x y }   ( -- f ) c cols mod x - abs 2 <  c cols / y - abs 2 < and ;

: place-mines { x y }
  mines 0 ?do
    begin cols rows * random  dup mine has?  over x y near? or while drop repeat
    mine set
  loop
  -1 to placed   ticks to start-ms ;

: new-game ( -- )
  board max-cells erase
  0 to placed  0 to state  0 to open-count  0 to flags  -1 to hit
  0 to start-ms  0 to end-ms
  kx cols 1- min to kx   ky rows 1- min to ky ;

: level ( cols rows mines -- ) to mines to rows to cols  new-game ;

: seconds ( -- n )
  placed 0= if 0 exit then
  state if end-ms else ticks then  start-ms - 1000 /  999 min ;

( ---- Opening: the cells with no mine around go in a list, whose
  neighbours open in turn ---- )
create todo max-cells cells allot   0 value ntodo
: push ( i -- ) todo ntodo cells + !  1 +to ntodo ;
: pop ( -- i ) -1 +to ntodo  todo ntodo cells + @ ;

: reveal ( i -- )   ( a cell without mine opens, and goes in the list if none is near )
  dup opened has? over flag has? or if drop exit then
  dup opened set  1 +to open-count
  dup cols mod over cols / mines-near 0= if push else drop then ;

: spread-xy { x y }   ( the neighbours of the cell x y open )
  2 -1 do 2 -1 do
    x i + y j + 2dup in? if idx reveal else 2drop then
  loop loop ;

: spread ( i -- ) dup cols mod swap cols / spread-xy ;
: flood ( -- ) begin ntodo while pop spread repeat ;

: lose ( i -- )   to hit  1 to state  ticks to end-ms
  196 120 beep  147 120 beep  98 400 beep ;

: check-win ( -- )
  state 0= open-count cols rows * mines - = and if
    2 to state  ticks to end-ms  mines to flags
    cols rows * 0 do i mine has? if i flag set then loop
    523 80 beep  659 80 beep  784 80 beep  1047 250 beep
  then ;

: open-one ( i -- )
  dup flag has? over opened has? or if drop exit then
  dup mine has? if lose exit then
  reveal ;

: open-around { x y }   ( on a number with as many flags around: the rest opens )
  x y mines-near  x y flags-near  <> if exit then
  2 -1 do 2 -1 do
    x i + y j + 2dup in? if idx open-one else 2drop then
  loop loop ;

: open-cell { x y }
  state if exit then
  placed 0= if x y place-mines then
  x y idx opened has? if x y open-around else x y idx open-one then
  flood  check-win
  state 0= if 660 15 beep then ;

: toggle-flag { x y }
  state if exit then
  x y idx opened has? if exit then
  x y idx board + dup c@ flag xor dup rot c!
  flag and if 1 else -1 then +to flags   440 15 beep ;

( ---- Keys and the mouse ---- )
create was 256 allot   was 256 erase
: just ( k -- f )   ( pressed now, not at the last look )
  dup pressed? swap 255 and was + 2dup c@ 0= and >r c! r> ;

: board-x ( -- x ) w cols cell * - 2 / ;
: board-y ( -- y ) h top - rows cell * - 2 / top + ;

: mouse-cell ( -- x y )   ( the cell under the mouse, maybe outside the board )
  mouse-x board-x - dup 0< if drop -1 else cell / then
  mouse-y board-y - dup 0< if drop -1 else cell / then ;

: on-face? ( -- f ) mouse-x w 2 / - abs 10 <  mouse-y top 2 / - abs 10 < and ;

0 value last-mx   0 value last-my
: follow-mouse ( -- )   ( the frame goes where the mouse moves )
  mouse-x last-mx <> mouse-y last-my <> or if
    mouse-x to last-mx  mouse-y to last-my
    mouse-cell 2dup in? if to ky to kx else 2drop then
  then ;

: move-frame ( -- )
  key-left just if kx 1- 0 max to kx then
  key-right just if kx 1+ cols 1- min to kx then
  key-up just if ky 1- 0 max to ky then
  key-down just if ky 1+ rows 1- min to ky then ;

: update ( -- )
  [char] 1 just if 9 9 10 level then
  [char] 2 just if 16 16 40 level then
  [char] 3 just if 30 16 99 level then
  [char] r just if new-game then
  follow-mouse  move-frame
  LEFT-BUTTON just if
    on-face? if new-game exit then
    mouse-cell 2dup in? if open-cell else 2drop then
  then
  RIGHT-BUTTON just if mouse-cell 2dup in? if toggle-flag else 2drop then then
  key-space just key-enter just or if kx ky open-cell then
  [char] f just if kx ky toggle-flag then ;

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

create number-colors
  $2040e0 , $208020 , $e02020 , $102080 , $802010 , $108080 , $000000 , $606060 ,

: draw-closed ( px py -- )
  $a8a8b0 color 2dup cell 1- cell 1- box
  $e0e0e8 color 2dup cell 1- 1 box  2dup 1 cell 1- box
  $606068 color 2dup swap cell 2 - + swap 1+ 1 cell 2 - box
  swap 1+ swap cell 2 - + cell 2 - 1 box ;

: draw-flag ( px py -- )
  black color  2dup swap 6 + swap 2 + 1 8 box
  2dup swap 3 + swap 8 + 6 2 box
  red color  swap 2 + swap 2 + 4 4 box ;

: draw-mine ( px py -- )
  black color  swap 6 + swap 6 + 2dup 3 disc
  white color  swap 1- swap 1- 1 1 box ;

: draw-cross { px py }   ( a flag that was wrong )
  red color  px 1+ py 1+ px 9 + py 9 + line   px 9 + py 1+ px 1+ py 9 + line ;

: draw-at { x y px py c }   ( c: the index of the cell x y, drawn at px py )
  c opened has? if
    c hit = if red else $d0d0d8 then color  px py cell 1- cell 1- box
    c mine has? if px py draw-mine exit then
    x y mines-near ?dup if
      dup 1- cells number-colors + @ color  px 3 + py 1+ 2 draw-digit
    then exit
  then
  state 1 = c mine has? and if
    $d0d0d8 color px py cell 1- cell 1- box
    c flag has? if px py draw-flag else px py draw-mine then exit
  then
  px py draw-closed
  c flag has? if px py draw-flag then
  state 1 = c flag has? and c mine has? 0= and if px py draw-cross then ;

: draw-cell { x y } x y  board-x x cell * +  board-y y cell * +  x y idx  draw-at ;

: draw-face ( -- )
  state 0= if $f0d020 then  state 1 = if $e04040 then  state 2 = if $40c040 then  color
  w 2 / top 2 / 9 disc
  black color  w 2 / 4 - top 2 / 3 - 2 2 box  w 2 / 2 + top 2 / 3 - 2 2 box
  w 2 / 3 - top 2 / 3 + 7 1 box ;

: draw-all ( -- )
  $303038 color cls
  $181820 color 0 0 w top box
  $e04040 color  mines flags - 0 max 3 6 5 3 draw-number
  $e0e0e0 color  seconds 3 w 54 - 5 3 draw-number
  draw-face
  rows 0 do cols 0 do i j draw-cell loop loop
  state 0= if
    yellow color  board-x kx cell * + 1-  board-y ky cell * + 1-  cell 1+ cell 1+ frame
  then
  flip ;

: step ( -- )
  events
  key-esc pressed? if quit! then
  update draw-all
  dt 16 < if 16 dt - delay then ;

: run ( -- ) begin step quit? until close-screen ;

[defined] minesweeper-no-run [if] [else]
ms-ticks 1 or rand-seed !
3 to zoom
w h screen
s" Minesweeper" title
new-game
run
script-bye
[then]
