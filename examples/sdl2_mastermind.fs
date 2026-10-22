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

\ Mastermind with SDL2: find the four hidden colors (out of six, a color may
\ come more than once) in ten tries. After each try, small pegs: a black one
\ for each color at its right place, a white one for each right color at
\ another place. The tries go up from the bottom. Only libSDL2 is needed.
\   1 to 6                  put that color in the frame, which moves on
\   left / right            move the frame      up / down   change its color
\   backspace               take the color away
\   space or enter          check the row (when it is full)
\   mouse                   click a color below, a hole of the row, or the green box
\   r                       a new game          esc         quit
\ At the top, the hidden row (shown at the end, green won, red lost) and the
\ games won and played, in a tiny 3x5 pixel font. The bare-metal boards run it too.

sdl2

4 constant holes   6 constant ncolors   10 constant tries
180 constant w   240 constant h
22 constant top   18 constant row-h   212 constant palette-y

create peg-colors
  $e03030 , $f0d020 , $30b040 , $3070f0 , $f08020 , $b050e0 ,
: peg-color ( c -- $rrggbb ) 1- cells peg-colors + @ ;   ( colors 1..6, 0 empty )

create secret holes allot
create guesses tries holes * allot
create marks tries 2* allot   ( per row: right places, right colors elsewhere )
0 value row   0 value slot   0 value state   ( 0 playing, 1 won, 2 lost )
0 value won   0 value played

: guess ( row -- a ) holes * guesses + ;
: exact@ ( row -- n ) 2* marks + c@ ;
: near@ ( row -- n ) 2* marks + 1+ c@ ;

( ---- Scoring a row against the hidden one ---- )
create counts ncolors 1+ 2* cells allot   ( per color: in the secret, in the guess )
: score ( a -- exact near )   ( the guess at a )
  counts ncolors 1+ 2* cells erase
  0  holes 0 do
    over i + c@  secret i + c@ = if 1+ then
    over i + c@ 2* cells counts + cell+ 1 swap +!
    secret i + c@ 2* cells counts + 1 swap +!
  loop nip
  0  ncolors 1+ 1 do  i 2* cells counts + dup @ swap cell+ @ min +  loop
  over - ;

: full? ( -- f ) -1  holes 0 do row guess i + c@ 0= if drop 0 then loop ;

: new-game ( -- )
  holes 0 do ncolors random 1+ secret i + c! loop
  guesses tries holes * erase   marks tries 2* erase
  0 to row  0 to slot  0 to state ;

: check-row ( -- )
  full? 0= if 110 40 beep exit then
  row guess score  row 2* marks + 1+ c!  dup row 2* marks + c!
  1 +to played
  holes = if 1 to state  1 +to won
    523 80 beep  659 80 beep  784 80 beep  1047 250 beep  exit then
  row 1+ to row  0 to slot
  row tries = if 2 to state  tries 1- to row  262 150 beep  196 400 beep exit then
  440 30 beep  330 30 beep ;

: put ( c -- )   ( a color in the frame, then the frame goes to the next hole )
  row guess slot + c!  slot 1+ holes 1- min to slot  660 15 beep ;

: turn-color ( d -- )   ( the color of the frame, one up or down )
  row guess slot + dup c@ rot + ncolors 1+ + ncolors 1+ mod  swap c! ;

: take-away ( -- )
  row guess slot + dup c@ if 0 swap c! exit then drop
  slot 1- 0 max to slot  0 row guess slot + c! ;

( ---- Places on the screen ---- )
: row-y ( row -- y ) tries 1- swap - row-h * top + row-h 2 / + 2 + ;   ( middle of a row, from the bottom )
: hole-x ( i -- x ) 20 * 16 + ;
: color-x ( c -- x ) 1- 28 * 20 + ;   ( middle of the color c below )
: ok-x ( -- x ) 132 ;   ( the green box of the row, 36 wide )

( ---- Keys and the mouse ---- )
create was 256 allot   was 256 erase
: just ( k -- f )   ( pressed now, not at the last look )
  dup pressed? swap 255 and was + 2dup c@ 0= and >r c! r> ;

: click ( -- )
  mouse-y palette-y - abs 12 < if
    ncolors 1+ 1 do mouse-x i color-x - abs 12 < if i put then loop exit
  then
  mouse-y row row-y - abs row-h 2 / < if
    mouse-x ok-x >= mouse-x ok-x 36 + < and if check-row exit then
    holes 0 do mouse-x i hole-x - abs 9 < if i to slot then loop
  then ;

: update ( -- )
  [char] r just if new-game exit then
  state if
    key-space just key-enter just or LEFT-BUTTON just or if new-game then exit
  then
  ncolors 1+ 1 do [char] 0 i + just if i put then loop
  key-left just if slot 1- 0 max to slot then
  key-right just if slot 1+ holes 1- min to slot then
  key-up just if 1 turn-color then
  key-down just if -1 turn-color then
  key-backspace just if take-away then
  key-space just key-enter just or if check-row then
  LEFT-BUTTON just if click then ;

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

: draw-peg ( c x y -- )   ( a color, or an empty hole )
  rot ?dup if peg-color color 6 disc else $2a1a0c color 3 disc then ;

: draw-marks { r }   ( small pegs, two by two, right of the row )
  r row-y { y }
  r exact@ r near@ + 0 ?do
    i r exact@ < if $101010 else $f0f0f0 then color
    i 2 mod 6 * 98 +   i 2 / 6 * y + 3 -   2 disc
  loop ;

: draw-rows ( -- )
  tries 0 do
    i row = state 0= and if $8a6038 else $6b4a2b then color
    6 i row-y row-h 2 / - 1+  w 12 - row-h 2 - box
    holes 0 do  j guess i + c@  i hole-x  j row-y  draw-peg  loop
    i row < i row = state 0<> and or if i draw-marks then
  loop ;

: draw-frame ( -- )   ( the hole the keys fill, and the box that checks )
  state if exit then
  yellow color  slot hole-x 8 -  row row-y 8 -  17 17 frame
  full? if $30b040 else $404040 then color  ok-x row row-y 6 -  36 12 box ;

: draw-secret ( -- )
  state 1 = if $30b040 else state 2 = if $c03030 else $303030 then then color
  6 2 w 12 - top 4 - box
  holes 0 do
    state if secret i + c@ else 0 then  i hole-x  top 2 / 1+  draw-peg
  loop
  white color  won 2 106 6 2 draw-number
  $909090 color  played 2 142 6 2 draw-number ;

: draw-palette ( -- )
  ncolors 1+ 1 do i dup color-x palette-y draw-peg loop ;

: draw-all ( -- )
  $3a2814 color cls
  draw-secret draw-rows draw-frame draw-palette
  flip ;

: step ( -- )
  events
  key-esc pressed? if quit! then
  update draw-all
  dt 16 < if 16 dt - delay then ;

: run ( -- ) begin step quit? until close-screen ;

[defined] mastermind-no-run [if] [else]
ms-ticks 1 or rand-seed !
3 to zoom
w h screen
s" Mastermind" title
new-game
run
script-bye
[then]
