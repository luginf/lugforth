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

\ The level editor of gfx_dungeon.fs (POSIX, SDL2): it reads dungeon_levels.fs,
\ shows a level from above, 20x20 squares, lets you draw it, check it, play it
\ and writes the file back (the old one is kept once as dungeon_levels.fs.bak).
\ It loads the game (without running it): the pictures and the rules are the
\ game's own.
\
\   lugforth examples/games/gfx_dungeon_edit.fs
\
\ Mouse: the left button paints with the chosen tile (drag), the right button
\ picks the tile under it; click a tile of the palette, or a button.
\ Keys: arrows move the cursor, space paints, e picks, Tab and Backspace choose
\ the next and the previous tile, l and b the next and previous level, n a new
\ level, x deletes the level (twice), u undoes the last stroke, c checks the
\ level (the way to everything, keys for the locked doors, stairs), t plays
\ it (Esc comes back), s saves, Esc quits (twice if there are unsaved changes).
\ Up to 32 levels; the game keeps room for what the file says (dg-cap).
\ The @ (where you start) is only for level 1; the other levels start next to
\ their < stairs. Letters of the tiles: see the top of gfx_dungeon.fs.

gfx
: dungeon-no-run ;
include gfx_dungeon.fs

32 constant de-cap
create de-lv de-cap dg-ns * allot   de-lv de-cap dg-ns * erase
create de-undo dg-ns allot
0 value de-n   0 value de-cur   0 value de-dirty   0 value de-sel   0 value de-cx   0 value de-cy
0 value de-undo-ok   0 value de-undo-lv   0 value de-nmove   0 value de-del-t   -10000 value de-quit-t   0 value de-bak-done
20 constant de-nt                          \ the tiles of the palette
: de-tc ( i -- c )   s" #T.DL<>@12345khfwag*" drop + c@ ;
: de-l ( n -- a )   dg-ns * de-lv + ;
: de-cell ( x y -- a )   dg-n * + de-cur de-l + ;

( ---- Messages ---- )
create de-m1 44 allot   create de-m2 44 allot   create de-m3 44 allot
0 value de-m1n   0 value de-m2n   0 value de-m3n
: de-say ( a n -- )
  de-m2 de-m3 44 cmove  de-m2n to de-m3n   de-m1 de-m2 44 cmove  de-m1n to de-m2n
  44 min dup to de-m1n  de-m1 swap cmove ;
: de-b-say ( -- )   dg-bf dg-bn de-say ;
: de-cname ( c -- a n )
  case
    [char] # of s" WALL" endof   [char] T of s" WALL WITH A TORCH" endof   [char] . of s" FLOOR" endof
    [char] D of s" DOOR" endof   [char] L of s" LOCKED DOOR" endof   [char] < of s" STAIRS UP" endof
    [char] > of s" STAIRS DOWN" endof   [char] @ of s" START (LEVEL 1)" endof
    [char] 1 of s" RAT" endof   [char] 2 of s" SKELETON" endof   [char] 3 of s" ORC" endof
    [char] 4 of s" TROLL" endof   [char] 5 of s" LICH" endof
    [char] k of s" KEY" endof   [char] h of s" POTION" endof   [char] f of s" FOOD" endof
    [char] w of s" WEAPON" endof   [char] a of s" ARMOR" endof   [char] g of s" GOLD" endof
    [char] * of s" THE AMULET" endof
  endcase ;

( ---- The file ---- )
create de-path 100 allot   0 value de-pn
create de-bn 110 allot   0 value de-bnn
create de-out 26000 allot   0 value de-on
create de-bak 26000 allot
0 value de-fid   0 value de-rn   0 value bk-a   0 value bk-n
: de-exists? ( a n -- f )   r/o open-file if drop 0 else close-file drop -1 then ;
: de-try ( a n -- f )   ( this file exists: it is the one to write )
  2dup de-exists? if dup to de-pn de-path swap cmove -1 else 2drop 0 then ;
: de-find-path ( -- )
  s" dungeon_levels.fs" de-try if exit then
  s" examples/games/dungeon_levels.fs" de-try if exit then
  s" games/dungeon_levels.fs" de-try if exit then
  s" dungeon_levels.fs" dup to de-pn de-path swap cmove ;
: de-load ( -- )
  dg-nlev de-cap min to de-n
  de-n 0 ?do  i dg-ns * dg-levels +  i de-l  dg-ns cmove  loop ;

: de-o+ ( a n -- )   dup >r de-out de-on + swap cmove  r> +to de-on ;
: de-oc ( c -- )   de-out de-on + c!  1 +to de-on ;
: de-onl ( -- )   10 de-oc ;
: de-onum ( n -- )   dup 9 > if dup 10 / 10 mod [char] 0 + de-oc then  10 mod [char] 0 + de-oc ;
: de-build ( -- )
  0 to de-on
  s" \ Levels of gfx_dungeon.fs, written by gfx_dungeon_edit.fs (or made by dungeon_gen.py)." de-o+ de-onl
  s" \ 20 rows of 20 characters per level, in order; dg-row stores one row." de-o+ de-onl
  s" \ Legend: see the top of gfx_dungeon.fs." de-o+ de-onl
  de-n 8 max de-onum  s"  constant dg-cap   400 constant dg-ns" de-o+ de-onl
  s" create dg-levels dg-cap dg-ns * allot   dg-levels dg-cap dg-ns * erase" de-o+ de-onl
  s" 0 value dg-lp   0 value dg-nlev" de-o+ de-onl
  s" : dg-row ( a n -- )   ( refuses a row past the room for dg-cap levels )" de-o+ de-onl
  s"   drop  dg-lp dg-cap dg-ns * 20 - > if drop exit then  dg-levels dg-lp + 20 cmove   20 +to dg-lp ;" de-o+ de-onl
  de-n 0 do
    s" \ level " de-o+ i 1+ de-onum de-onl
    dg-n 0 do
      [char] s de-oc  [char] " de-oc  32 de-oc
      j de-l i dg-n * + dg-n de-o+
      [char] " de-oc  32 de-oc  s" dg-row" de-o+ de-onl
    loop
  loop
  s" dg-lp dg-ns / to dg-nlev" de-o+ de-onl ;
: de-backup ( -- )   ( once: the file as it was, beside it )
  de-path de-pn de-exists? 0= if exit then
  s" .bak" to bk-n to bk-a
  de-path de-bn de-pn cmove   bk-a de-bn de-pn + bk-n cmove   de-pn bk-n + to de-bnn
  de-path de-pn r/o open-file if drop exit then  to de-fid
  de-bak 25999 de-fid read-file drop to de-rn   de-fid close-file drop
  de-bn de-bnn w/o create-file if drop exit then  to de-fid
  de-bak de-rn de-fid write-file drop   de-fid close-file drop ;
: de-save ( -- )
  de-bak-done 0= if de-backup -1 to de-bak-done then
  de-build
  de-path de-pn w/o create-file if drop s" SAVE FAILED" de-say exit then  to de-fid
  de-out de-on de-fid write-file drop   de-fid close-file drop
  0 to de-dirty   dg-b-reset s" SAVED " dg-b+ de-n dg-b-num s"  LEVELS" dg-b+ de-b-say ;

( ---- Painting ---- )
: de-snap ( -- )   de-cur de-l de-undo dg-ns cmove   de-cur to de-undo-lv   -1 to de-undo-ok ;
: de-undo-do ( -- )
  de-undo-ok 0= if s" NOTHING TO UNDO" de-say exit then
  de-undo-lv de-n < if  de-undo de-undo-lv de-l dg-ns cmove  de-undo-lv to de-cur  -1 to de-dirty  then
  0 to de-undo-ok ;
0 value pa-c   0 value pa-x   0 value pa-y
: de-paint ( c x y -- )
  to pa-y to pa-x to pa-c
  pa-c [char] @ = if
    de-cur 0<> if s" THE START IS ONLY ON LEVEL 1" de-say exit then
    dg-ns 0 do  de-cur de-l i + c@ [char] @ = if [char] . de-cur de-l i + c! then  loop
  then
  pa-x pa-y de-cell c@ pa-c = if exit then
  pa-c pa-x pa-y de-cell c!   -1 to de-dirty ;
: de-pick ( x y -- )
  de-cell c@  de-nt 0 do  dup i de-tc = if i to de-sel then  loop drop ;
: de-put ( c x y n -- )   de-l >r  dg-n * + r> + c! ;
: de-new ( -- )
  de-n de-cap >= if s" NO ROOM FOR MORE LEVELS" de-say exit then
  dg-ns 0 do  [char] # de-n de-l i + c!  loop
  3 1 do 3 1 do  [char] . i j de-n de-put  loop loop
  de-n 0= if [char] @ 1 1 0 de-put else [char] < 1 0 de-n de-put then
  1 +to de-n   de-n 1- to de-cur   0 to de-undo-ok   -1 to de-dirty
  dg-b-reset s" NEW LEVEL " dg-b+ de-n dg-b-num de-b-say ;
: de-delete ( -- )
  de-n 2 < if s" CANNOT DELETE THE LAST LEVEL" de-say exit then
  de-del-t 0= ticks de-del-t - 3000 > or if  ticks to de-del-t  s" DELETE AGAIN TO CONFIRM" de-say exit  then
  de-cur 1+ de-n < if  de-cur 1+ de-l  de-cur de-l  de-n de-cur 1+ - dg-ns *  cmove  then
  -1 +to de-n   de-cur de-n >= if de-n 1- to de-cur then
  0 to de-undo-ok   -1 to de-dirty   0 to de-del-t   s" LEVEL DELETED" de-say ;
: de-goto ( n -- )   dup 0< over de-n >= or if drop exit then  to de-cur   0 to de-undo-ok ;

( ---- Check: the same rules as the game ---- )
0 value ck-bad   0 value ck-c   0 value ck-nx   0 value ck-ny   0 value ck-problems
: de-find ( c -- i )   ( in the level copied in dg-maps: its place, or -1 )
  -1 swap  dg-ns 0 do  dg-map i + c@ over = if nip i swap leave then  loop drop ;
: de-care? ( c -- f )   ( things, monsters and stairs that must be reachable )
  0 swap  14 0 do  dup s" khfwag*12345<>" drop i + c@ = if nip -1 swap then  loop drop ;
: de-dist? ( x y -- f )   2dup dg-in? if dg-n * + dg-d@ 0< 0= else 2drop 0 then ;
: de-reach? ( x y -- f )   ( the square or one next to it can be walked to )
  to ck-ny to ck-nx
  ck-nx ck-ny de-dist?
  4 0 do  ck-nx i dg-dx@ +  ck-ny i dg-dy@ +  de-dist? or  loop ;
: de-lcount ( c -- n )   ( how many in the level being edited )
  0 swap  dg-ns 0 do  de-cur de-l i + c@ over = if swap 1+ swap then  loop drop ;
: de-problem ( -- )   1 +to ck-problems   de-b-say ;
: de-check ( -- )
  0 to ck-problems
  de-cur de-l dg-maps dg-ns cmove   0 to dg-lvl
  [char] @ de-find dup 0< 0= if  dup dg-n mod to dg-px  dg-n / to dg-py
  else drop  [char] < de-find 0< if
      dg-b-reset s" NO START: A < OR THE @ OF LEVEL 1" dg-b+ de-problem  exit
    then
    0 [char] < dg-arrive
  then
  dg-ns 0 do  dg-map i + c@ dup [char] D = swap [char] L = or if [char] . dg-map i + c! then  loop
  dg-bfs   0 to ck-bad
  dg-n 0 do dg-n 0 do
    i j dg-t@ to ck-c
    ck-c de-care? if i j de-reach? 0= if 1 +to ck-bad then then
  loop loop
  ck-bad 0> if dg-b-reset ck-bad dg-b-num s"  THINGS CANNOT BE REACHED" dg-b+ de-problem then
  [char] k de-lcount [char] L de-lcount < if
    dg-b-reset s" ONLY " dg-b+ [char] k de-lcount dg-b-num s"  KEYS FOR " dg-b+ [char] L de-lcount dg-b-num
    s"  LOCKED DOORS" dg-b+ de-problem then
  de-cur 0> if [char] < de-lcount 0= if dg-b-reset s" NO < STAIRS UP" dg-b+ de-problem then then
  de-cur 0= if [char] @ de-lcount 0= if dg-b-reset s" NO @ START" dg-b+ de-problem then then
  de-cur de-n 1- < if [char] > de-lcount 0= if dg-b-reset s" NO > STAIRS DOWN" dg-b+ de-problem then then
  de-cur de-n 1- = if [char] * de-lcount 0= if dg-b-reset s" NO * AMULET ON THE LAST LEVEL" dg-b+ de-problem then then
  ck-problems 0= if dg-b-reset s" LEVEL " dg-b+ de-cur 1+ dg-b-num s"  IS FINE" dg-b+ de-b-say then ;

( ---- Play the level ---- )
0 value pl-save
: de-play ( -- )
  de-cur de-l dg-levels dg-ns cmove
  dg-nlev to pl-save   1 to dg-nlev
  [char] @ dg-ns 0 do dg-levels i + c@ over = if drop 0 leave then loop  ( 0 when there is an @ )
  if
    \ no @: start next to a <, or on the first floor square
    dg-ns 0 do dg-levels i + c@ [char] . = if [char] @ dg-levels i + c! leave then loop
  then
  1 to dg-embed   dg-new-game   ticks to dg-last   dg-play
  0 to dg-embed   pl-save to dg-nlev   dg-sync   0 to dg-esc   0 to dg-leave ;

( ---- Drawing ---- )
0 value dt-c   0 value fr-x   0 value fr-y   0 value fr-w   0 value fr-h
: de-tile ( c x y -- )   ( 8 by 8 pixels )
  to rg-y to rg-x  to dt-c
  dt-c [char] 1 >= dt-c [char] 5 <= and if
    [char] . dg-rtile   dt-c [char] 1 -  rg-x rg-y 8 100 0 dg-spr  exit
  then
  dt-c [char] @ = if
    [char] . dg-rtile   yellow color rg-x 1 + rg-y 1 + 6 6 box   white color rg-x 3 + rg-y 3 + 2 2 box  exit
  then
  dt-c dg-rtile ;
: de-frame ( x y w h c -- )   ( an outline )
  color  to fr-h to fr-w to fr-y to fr-x
  fr-x fr-y fr-w 1 box   fr-x fr-y fr-h + 1- fr-w 1 box   fr-x fr-y 1 fr-h box   fr-x fr-w + 1- fr-y 1 fr-h box ;
8 constant de-gx   14 constant de-gy   184 constant de-px   14 constant de-py
create de-btn-names 0 , 0 ,
: de-btn-name ( i -- a n )
  case 0 of s" PREV" endof 1 of s" NEXT" endof 2 of s" NEW" endof 3 of s" DEL" endof
    4 of s" UNDO" endof 5 of s" SAVE" endof 6 of s" CHECK" endof 7 of s" TEST" endof endcase ;
: de-draw ( -- )
  black color cls
  $14141c color  0 0 dg-w dg-h box
  \ the level
  $06060a color  de-gx de-gy 160 160 box
  dg-n 0 do dg-n 0 do
    i j de-cell c@  i 8 * de-gx +  j 8 * de-gy +  de-tile
  loop loop
  $505060 color  de-gx 1- de-gy 1- 162 162 $505060 de-frame
  de-cx 8 * de-gx +  de-cy 8 * de-gy +  8 8 white de-frame
  \ the palette
  de-nt 0 do
    i 8 mod 16 * de-px +  i 8 / 16 * de-py +  to rg-y to rg-x   \ top left of the cell
    i de-tc  rg-x 4 + rg-y 4 +  de-tile
    i de-sel = if rg-x rg-y 16 16 yellow de-frame else rg-x rg-y 16 16 $303040 de-frame then
  loop
  \ the buttons
  8 0 do
    i 4 mod 35 * de-px +  i 4 / 14 * 70 +  to rg-y to rg-x
    $303048 color rg-x rg-y 33 11 box
    $c0c0d0 color i de-btn-name  rg-x 3 +  rg-y 3 +  dg-text
  loop
  \ the texts
  $f0d020 color  s" LEVEL" 8 4 dg-text   de-cur 1+ 2 36 4 dg-num   s" OF" 46 4 dg-text   de-n 2 58 4 dg-num
  de-dirty if $ff5555 color s" UNSAVED" 80 4 dg-text then
  $808090 color  de-path de-pn 110 4 dg-text
  $c0c0c0 color  de-sel de-tc de-cname de-px 100 dg-text
  $808090 color  s" X" de-px 108 dg-text   de-cx 2 de-px 8 + 108 dg-num   s" Y" de-px 28 + 108 dg-text   de-cy 2 de-px 36 + 108 dg-num
  $707080 color  de-m3 de-m3n de-px 120 dg-text
  $a0a0b0 color  de-m2 de-m2n de-px 128 dg-text
  white color    de-m1 de-m1n de-px 136 dg-text
  $505060 color
  s" CLICK OR SPACE PAINTS" de-px 154 dg-text
  s" RIGHT CLICK OR E PICKS" de-px 162 dg-text
  s" TAB TILE  L B LEVELS" de-px 170 dg-text
  s" N NEW X DEL U UNDO" de-px 178 dg-text
  s" C CHECK T TEST S SAVE" de-px 186 dg-text
  s" ESC QUIT" de-px 194 dg-text
  flip ;

( ---- Input ---- )
0 value mc-x   0 value mc-y
: de-grid? ( mx my -- x y f )
  to mc-y to mc-x
  mc-x de-gx >= mc-x de-gx 160 + < and  mc-y de-gy >= and  mc-y de-gy 160 + < and if
    mc-x de-gx - 8 /  mc-y de-gy - 8 /  -1
  else 0 0 0 then ;
: de-pal? ( mx my -- i | -1 )
  to mc-y to mc-x
  mc-x de-px >= mc-x de-px 128 + < and  mc-y de-py >= and  mc-y de-py 48 + < and if
    mc-y de-py - 16 / 8 *  mc-x de-px - 16 / +  dup de-nt < 0= if drop -1 then
  else -1 then ;
: de-btn? ( mx my -- i | -1 )
  to mc-y to mc-x
  mc-x de-px >= mc-x de-px 140 + < and  mc-y 70 >= and  mc-y 98 < and if
    mc-y 70 - 14 /  4 *  mc-x de-px - 35 / +
    mc-y 70 - 14 mod 11 < 0= if drop -1 then
  else -1 then ;
: de-do ( button -- )
  case
    0 of de-cur 1- de-goto endof   1 of de-cur 1+ de-goto endof
    2 of de-new endof   3 of de-delete endof   4 of de-undo-do endof
    5 of de-save endof   6 of de-check endof   7 of de-play endof
  endcase ;
0 value mi-x   0 value mi-y   0 value mi-i
: de-mouse ( -- )
  mouse-x mouse-y de-grid? if
    to de-cy to de-cx
    LEFT-BUTTON dg-just if de-snap then
    LEFT-BUTTON pressed? if de-sel de-tc de-cx de-cy de-paint then
    RIGHT-BUTTON pressed? if de-cx de-cy de-pick then
  else 2drop then
  \ the palette and the buttons only on a click
  mouse-x to mi-x  mouse-y to mi-y
  LEFT-BUTTON pressed? if
    mi-x mi-y de-pal? dup 0< 0= if to de-sel else drop then
  then ;
: de-keys ( -- )
  \ the cursor moves with the arrows, a little slowly
  ticks de-nmove >= if
    key-left pressed? if de-cx 1- 0 max to de-cx  ticks 90 + to de-nmove then
    key-right pressed? if de-cx 1+ dg-n 1- min to de-cx  ticks 90 + to de-nmove then
    key-up pressed? if de-cy 1- 0 max to de-cy  ticks 90 + to de-nmove then
    key-down pressed? if de-cy 1+ dg-n 1- min to de-cy  ticks 90 + to de-nmove then
  then
  key-space dg-just if de-snap then
  key-space pressed? if de-sel de-tc de-cx de-cy de-paint then
  [char] e dg-just if de-cx de-cy de-pick then
  key-tab dg-just if de-sel 1+ de-nt mod to de-sel then
  key-backspace dg-just if de-sel 1- de-nt + de-nt mod to de-sel then
  [char] l dg-just if de-cur 1+ de-goto then
  [char] b dg-just if de-cur 1- de-goto then
  [char] n dg-just if de-new then
  [char] x dg-just if de-delete then
  [char] u dg-just if de-undo-do then
  [char] c dg-just if de-check then
  [char] t dg-just if de-play then
  [char] s dg-just if de-save then ;
0 value de-click
: de-frame-once ( -- )
  events
  LEFT-BUTTON pressed? 0= if 0 to de-click then
  \ a click on a button counts once
  LEFT-BUTTON pressed? de-click 0= and if
    mouse-x mouse-y de-btn? dup 0< 0= if -1 to de-click de-do else drop then
  then
  de-mouse de-keys
  key-esc dg-just if
    de-dirty 0= ticks de-quit-t - 3000 < or if quit! else
      ticks to de-quit-t  s" UNSAVED CHANGES: ESC AGAIN TO QUIT" de-say then
  then
  de-draw
  dt 16 < if 16 dt - delay then ;

: de-title ( -- )   dg-b-reset s" Dungeon editor - " dg-b+ de-path de-pn dg-b+ dg-bf dg-bn title ;
: de-start ( -- )
  de-find-path   de-load   0 to de-cur   0 to de-sel   0 to de-dirty
  dg-b-reset s" LOADED " dg-b+ de-n dg-b-num s"  LEVELS" dg-b+ de-b-say
  s" CHECK THE LEVEL (C) BEFORE SAVING" de-say ;
: dungeon-edit ( -- )
  de-start
  3 to zoom  dg-w dg-h screen  de-title
  begin de-frame-once quit? until close-screen ;

[defined] dungeon-edit-no-run [if] [else]
dungeon-edit
script-bye
[then]
