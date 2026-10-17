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

( The Varvara Screen device: two layers of four colors in 8-bit pixels )
\
\ Shared by varvara.fs (SDL2 window) and varvara_rpi.fs (the bare-metal board).
\ A pixel byte is bg + 4 * fg. The front end supplies RESIZE (the screen size
\ written to the System device changed) and SET-COLORS (the palette changed), by
\ IS, and shows the pixels when DIRTY.

needs uxn.fs
also uxn definitions

0 value pixels
0 value pitch
0 value scr-w
0 value scr-h
0 value dirty
defer resize
defer set-colors

: nib ( port c -- n ) swap dev2@ 12 rot 4 * - rshift $f and ;
: system-color ( c -- $rgb )   ( System/r g b hold one nibble per color )
  >r $08 r@ nib 8 lshift  $0a r@ nib 4 lshift or  $0c r> nib or ;
: nibble>byte ( n -- b ) $f and 17 * ;

0 value fg?   ( drawing on the foreground layer )
: plot ( c x y -- )
  dup scr-h < 0= if drop 2drop exit then
  over scr-w < 0= if drop 2drop exit then
  pitch * + pixels +
  fg? if dup c@ 3 and rot 2 lshift or else dup c@ $0c and rot or then swap c! ;

: fill-rect ( c x1 y1 x2 y2 -- )
  { c x1 y1 x2 y2 }
  x2 scr-w min to x2  y2 scr-h min to y2
  y2 y1 > x2 x1 > and if
    y2 y1 do x2 x1 do c i j plot loop loop
  then -1 to dirty ;

: screen-x ( -- x ) $28 dev2@ ;
: screen-y ( -- y ) $2a dev2@ ;
: screen-x! ( x -- ) $ffff and dup 8 rshift $28 dev! $29 dev! ;
: screen-y! ( y -- ) $ffff and dup 8 rshift $2a dev! $2b dev! ;
: auto ( -- b ) $26 dev@ ;

: pixel ( -- )
  $2e dev@ { ctrl }
  ctrl $40 and 0<> to fg?
  ctrl $80 and if
    ctrl 3 and
    ctrl $10 and if 0 screen-x else screen-x scr-w then
    ctrl $20 and if 0 screen-y else screen-y scr-h then
    rot swap ( c x1 y1 x2 y2 ) fill-rect
  else
    ctrl 3 and screen-x screen-y plot  -1 to dirty
    auto 1 and if screen-x 1+ screen-x! then
    auto 2 and if screen-y 1+ screen-y! then
  then ;

create blending
  0 c, 0 c, 0 c, 0 c, 1 c, 0 c, 1 c, 1 c, 2 c, 2 c, 0 c, 2 c, 3 c, 3 c, 3 c, 0 c,
  0 c, 1 c, 2 c, 3 c, 0 c, 1 c, 2 c, 3 c, 0 c, 1 c, 2 c, 3 c, 0 c, 1 c, 2 c, 3 c,
  1 c, 2 c, 3 c, 1 c, 1 c, 2 c, 3 c, 1 c, 1 c, 2 c, 3 c, 1 c, 1 c, 2 c, 3 c, 1 c,
  2 c, 3 c, 1 c, 2 c, 2 c, 3 c, 1 c, 2 c, 2 c, 3 c, 1 c, 2 c, 2 c, 3 c, 1 c, 2 c,

0 value spr-color
0 value spr-flipx
0 value spr-flipy
0 value spr-2bpp
0 value spr-opaque
0 value spr-bits
0 value cv-ready
0 value keepm    ( bits of a pixel byte kept: the other layer )
create cv 4 cells allot   ( by pixel value 0 to 3: the new byte for the layer, shifted, or -1 )
: set-colors-table ( -- )
  4 0 do
    i spr-opaque or if
      i 16 * spr-color + blending + c@  fg? if 2 lshift then
    else -1 then
    cv i cells + !
  loop
  fg? if 3 else $0c then to keepm ;

$0101010101010101 constant ones
0 value row-p
0 value row-x
: draw-row ( x y -- )   ( the 8 pixels of spr-bits on one row )
  $ffff and dup scr-h u< 0= if 2drop exit then
  pitch * pixels + to row-p  to row-x
  8 0 do
    spr-bits 1 and spr-bits 7 rshift 2 and or cells cv + @
    dup 0< 0= if
      row-x  spr-flipx if i else 7 i - then +  $ffff and
      dup scr-w u< if row-p + dup c@ keepm and 2 pick or swap c! drop else 2drop then
    else drop then
    spr-bits 1 rshift to spr-bits
  loop ;

: zero-tile? ( a -- f )   ( all the bytes of the tile are 0 )
  spr-2bpp if 16 else 8 then 0
  ?do dup i + ram@ if drop 0 unloop exit then loop drop -1 ;

: fill-tile ( x y -- )   ( an opaque 8x8 tile of 0 bits: one 8 byte write for each row )
  { x y }
  cv @ ones * { vv }  keepm ones * { km }
  x $ffff and 8 + scr-w > if
    8 0 do 0 to spr-bits  x y i + draw-row loop exit
  then
  x $ffff and { xm }
  8 0 do
    y i + $ffff and dup scr-h u< if pitch * pixels + xm + dup @ km and vv or swap ! else drop then
  loop ;

: blit ( addr x y -- )
  { a x y }
  a zero-tile? if
    spr-opaque 0= if exit then
    cv-ready 0= if set-colors-table -1 to cv-ready then
    x y fill-tile exit
  then
  cv-ready 0= if set-colors-table -1 to cv-ready then
  8 0 do
    a i + ram@  spr-2bpp if a i + 8 + ram@ 8 lshift or then  to spr-bits
    spr-bits spr-opaque or if
      x  y spr-flipy if 7 i - else i then +  draw-row
    then
  loop ;

: sprite ( -- )
  $2f dev@ { ctrl }  auto { move }  $2c dev2@ { a }
  ctrl $40 and 0<> to fg?
  ctrl $0f and to spr-color
  ctrl $10 and 0<> to spr-flipx
  ctrl $20 and 0<> to spr-flipy
  ctrl $80 and 0<> to spr-2bpp
  spr-color 5 mod 0<> spr-color 0= or to spr-opaque
  0 to cv-ready
  spr-flipx if -1 else 1 then { fx }
  spr-flipy if -1 else 1 then { fy }
  move 1 and 3 lshift { dx }  move 2 and 2 lshift { dy }
  move 4 and if spr-2bpp if 16 else 8 then else 0 then { da }
  move 4 rshift 1+ 0 do
    a  screen-x dy fx * i * +  screen-y dx fy * i * +  blit
    da +to a
  loop
  move 1 and if screen-x dx fx * + screen-x! then
  move 2 and if screen-y dy fy * + screen-y! then
  move 4 and if a $ffff and dup 8 rshift $2c dev! $2d dev! then
  -1 to dirty ;

: screen-deo ( p -- )
  $0f and case
    $03 of resize endof
    $05 of resize endof
    $0e of pixel endof
    $0f of sprite endof
  endcase ;

: varvara-system-deo ( p -- )
  dup system-deo  $0f and dup $08 >= swap $0d <= and if set-colors then ;


( ---- Controller and Mouse devices: what a front end calls when something happens ---- )

: controller ( -- ) $80 dev2@ eval ;
: buttons ( b f -- )   ( set or clear button bits )
  $82 dev@ swap if or else swap invert and then $82 dev! controller ;
: send-key ( c -- ) $83 dev! controller 0 $83 dev! ;

: mouse ( -- ) $90 dev2@ eval ;
: mouse-xy ( x y -- ) $ffff and dup 8 rshift $94 dev! $95 dev!
                      $ffff and dup 8 rshift $92 dev! $93 dev! ;
: mouse-button ( n f -- )
  >r 1 swap 1- lshift  $96 dev@ r> if or else swap invert and then $96 dev! ;
: scroll ( x y -- )
  $ffff and dup 8 rshift $9c dev! $9d dev!
  $ffff and dup 8 rshift $9a dev! $9b dev!
  mouse  0 0 $9a dev! $9b dev!  0 0 $9c dev! $9d dev! ;

( ---- Datetime device ---- )
\ TM@ ( n -- v ) is the front end's: the fields of C's struct tm, n = 0 sec, 1 min, 2 hour,
\ 3 day of the month, 4 month (0 to 11), 5 year - 1900, 6 weekday, 7 day of the year, 8 dst.

defer tm@
: datetime-dei ( p -- b )
  dup $0f and case
    $00 of drop 5 tm@ 1900 + 8 rshift endof
    $01 of drop 5 tm@ 1900 + $ff and endof
    $02 of drop 4 tm@ endof
    $03 of drop 3 tm@ endof
    $04 of drop 2 tm@ endof
    $05 of drop 1 tm@ endof
    $06 of drop 0 tm@ endof
    $07 of drop 6 tm@ endof
    $08 of drop 7 tm@ 8 rshift endof
    $09 of drop 7 tm@ $ff and endof
    $0a of drop 8 tm@ 0<> 1 and endof
    drop dev@ 0
  endcase ;

previous definitions
