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

( Flood fill, for the paint of the drawing words: the scanline algorithm. The
  picture is read through flood-get and filled through flood-span, which each
  sdl2.fs points at its own pixel@ and hline. A bitmap of the pixels already
  filled stops it, whatever the pen color. The runs waiting are kept on a stack
  of fl-max; past it, some are dropped and the fill stays partial. )

also internals  internals definitions
defer flood-get ( x y -- $rrggbb )
defer flood-span ( x y w -- )
0 value fl-w   0 value fl-h   0 value fl-target
0 value fl-seen   0 value fl-stack   0 value fl-sp
16384 constant fl-max
: fl-bit ( x y -- a mask ) fl-w * + dup 3 rshift fl-seen +  swap 7 and 1 swap lshift ;
: fl-ok? ( x y -- f )   ( not filled yet, and of the color found at the start )
  2dup fl-bit swap c@ and if 2drop 0 exit then  flood-get fl-target = ;
: fl-mark ( x y -- ) fl-bit over c@ or swap c! ;
: fl-push ( x y -- )
  fl-sp fl-max < if fl-sp 2* cells fl-stack + tuck cell+ ! !  1 +to fl-sp else 2drop then ;
: fl-pop ( -- x y ) -1 +to fl-sp  fl-sp 2* cells fl-stack + dup @ swap cell+ @ ;
: fl-row ( lx rx y -- )   ( the start of each run to fill on row y, between lx and rx )
  0 { lx rx y in }
  y 0< y fl-h >= or if exit then
  rx 1+ lx ?do
    i y fl-ok? if in 0= if i y fl-push then -1 to in else 0 to in then
  loop ;
: fl-left ( x y -- lx ) { x y } begin x 0> if x 1- y fl-ok? else 0 then while x 1- to x repeat x ;
: fl-right ( x y -- rx ) { x y } begin x 1+ fl-w < if x 1+ y fl-ok? else 0 then while x 1+ to x repeat x ;
: fl-fill ( lx rx y -- )   ( a run of row y, then the rows above and below )
  { lx rx y }
  rx 1+ lx ?do i y fl-mark loop
  lx y rx lx - 1+ flood-span
  lx rx y 1- fl-row  lx rx y 1+ fl-row ;
: fl-run ( x y -- )   ( the whole run around x on row y )
  { x y }  x y fl-left  x y fl-right  y fl-fill ;
: fl-step ( x y -- ) 2dup fl-ok? if fl-run else 2drop then ;

( still in internals: the sdl2 words reach it with also internals )
: flood-fill ( x y w h -- )   ( the area of the color at x y, inside w h, through flood-get and flood-span )
  to fl-h to fl-w
  over 0< over 0< or  2 pick fl-w >= or  over fl-h >= or if 2drop exit then
  2dup flood-get to fl-target
  fl-w fl-h * 8 / 1+ dup allocate throw to fl-seen  fl-seen swap 0 fill
  fl-max 2* cells allocate throw to fl-stack  0 to fl-sp
  fl-push
  begin fl-sp while fl-pop fl-step repeat
  fl-stack free throw  fl-seen free throw ;
forth definitions previous
