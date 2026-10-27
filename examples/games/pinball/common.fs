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

\ Shared by the pinball modes: fixed point helpers, a 3x5 font, seven segment digits.
\ Loaded once by each mode (see the [undefined] test at the top of raid.fs).

sdl2

: pb-common ;

: open-screen ( w h -- )   ( the window stays when the menu and a table have the same size )
  2dup height = swap width = and screen-open? and if 2drop exit then
  3 to zoom screen ;

: px ( pixels -- units ) 12 lshift ;
: u>p ( units -- pixels ) 12 arshift ;

: isqrt ( n -- r )   ( integer square root, Newton )
  dup 2 < if exit then
  dup >r begin dup r@ over / + 2/ 2dup > while nip repeat drop r> drop ;

: cos-sin ( mrad -- cos sin )   ( both times 1024 )
  s>f 1000e f/ fdup fcos 1024e f* f>s fsin 1024e f* f>s ;

( ---- Text: a 3x5 font ---- )

create glyphs 128 cells allot   glyphs 128 cells erase
: glyph ( a n c -- )   ( 5 rows of 3: # a dot, . none, spaces ignored )
  >r 0 -rot over + swap ?do
    i c@ dup [char] # = swap [char] . = or if 2* i c@ [char] # = 1 and or then
  loop r> cells glyphs + ! ;
s" ### #.# ### #.# #.#" char A glyph   s" ##. #.# ##. #.# ##." char B glyph
s" ### #.. #.. #.. ###" char C glyph   s" ##. #.# #.# #.# ##." char D glyph
s" ### #.. ##. #.. ###" char E glyph   s" ### #.. ##. #.. #.." char F glyph
s" ### #.. #.# #.# ###" char G glyph   s" #.# #.# ### #.# #.#" char H glyph
s" ### .#. .#. .#. ###" char I glyph   s" ..# ..# ..# #.# ###" char J glyph
s" #.# #.# ##. #.# #.#" char K glyph   s" #.. #.. #.. #.. ###" char L glyph
s" #.# ### ### #.# #.#" char M glyph   s" ##. #.# #.# #.# #.#" char N glyph
s" ### #.# #.# #.# ###" char O glyph   s" ### #.# ### #.. #.." char P glyph
s" ### #.# #.# ### ..#" char Q glyph   s" ### #.# ##. #.# #.#" char R glyph
s" ### #.. ### ..# ###" char S glyph   s" ### .#. .#. .#. .#." char T glyph
s" #.# #.# #.# #.# ###" char U glyph   s" #.# #.# #.# #.# .#." char V glyph
s" #.# #.# ### ### #.#" char W glyph   s" #.# #.# .#. #.# #.#" char X glyph
s" #.# #.# .#. .#. .#." char Y glyph   s" ### ..# .#. #.. ###" char Z glyph
s" ### #.# #.# #.# ###" char 0 glyph   s" .#. ##. .#. .#. ###" char 1 glyph
s" ### ..# ### #.. ###" char 2 glyph   s" ### ..# ### ..# ###" char 3 glyph
s" #.# #.# ### ..# ..#" char 4 glyph   s" ### #.. ### ..# ###" char 5 glyph
s" ### #.. ### #.# ###" char 6 glyph   s" ### ..# ..# ..# ..#" char 7 glyph
s" ### #.# ### #.# ###" char 8 glyph   s" ### #.# ### ..# ###" char 9 glyph
s" ... ... ### ... ..." char - glyph   s" .#. .#. .#. ... .#." char ! glyph
s" ... .#. ... .#. ..." char : glyph   s" ... #.# .#. #.# ..." char x glyph
s" #.. .#. ..# .#. #.." char > glyph

: draw-char ( c x y s -- )
  { c x y s }  c $7f and cells glyphs + @ { bits }
  15 0 do
    bits 14 i - rshift 1 and if  x i 3 mod s * +  y i 3 / s * +  s s box  then
  loop ;
: say ( a n x y s -- )   ( text, 4*s pixels a character )
  { a n x y s }
  n 0 ?do a i + c@ x i 4 * s * + y s draw-char loop ;
: say-number ( n x y s -- )   ( right aligned on x )
  { n x y s }
  n 0 <# #s #> { a l }  a l x l 4 * s * - y s say ;

( ---- Seven segment score ---- )

create segments $3f c, $06 c, $5b c, $4f c, $66 c, $6d c, $7d c, $07 c, $7f c, $6f c,
: seg7 ( d x y -- )   ( a digit 8 wide and 14 high )
  { d x y }  d segments + c@ { m }
  m 1 and if x 1+ y 6 2 box then
  m 2 and if x 6 + y 1+ 2 6 box then
  m 4 and if x 6 + y 7 + 2 6 box then
  m 8 and if x 1+ y 12 + 6 2 box then
  m 16 and if x y 7 + 2 6 box then
  m 32 and if x y 1+ 2 6 box then
  m 64 and if x 1+ y 6 + 6 2 box then ;
: draw-score ( n x y -- )   ( 7 digits )
  { n x y }
  7 0 do  n 10 mod  x 6 i - 11 * +  y seg7  n 10 / to n  loop ;

