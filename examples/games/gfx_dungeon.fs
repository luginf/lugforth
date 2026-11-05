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

\ A dungeon crawler in first person, square by square, in the spirit of
\ Dungeon Master, Captive or Bard's Tale: you stand on a square of a grid
\ and look along it; a step is a whole square, a turn is a quarter turn, and
\ the view is drawn in perspective with painter's algorithm (the farthest
\ squares first). Three levels to start with (the number is data: see
\ dungeon_levels.fs, made by dungeon_gen.py or by a level editor). Only
\ libSDL2 is needed (no fonts, images or sound files): the pictures are small
\ sprites made of letters in this file, the text a tiny 3x5 pixel font, the
\ sounds beeps. One file plus the levels, so that it runs the same on POSIX
\ and the web (terminal.html?run=dungeon_levels.fs,gfx_dungeon.fs).
\   w s or up down   step forward, back     a d   step aside
\   q e or left right   turn left, right    space  strike the square ahead
\   h  drink a potion   m  the map   p  pause   esc  quit
\   v or tab  switch to the roguelike view (the map from above: arrows
\   move, walking into a monster strikes it)   k  change the keys
\   (1 QWERTY, 2 AZERTY, enter then a key; saved in dungeon_keys.txt)
\   space or enter  after the end: again
\ Find the Amulet on the last level, keep away from hunger, and kill what you
\ must: monsters hunt you in real time but only hit you from the next square.
\
\ A level is 20x20 characters: # wall, T wall with a torch, . floor, D door,
\ L locked door (a key), < > stairs up and down (you walk into them), @ the
\ start of level 1, 1-5 a monster (rat, skeleton, orc, troll, lich), and on the
\ floor k key, h potion, f food, w weapon, a armor, g gold, * the Amulet.

sdl2
\ on the web the page has loaded the levels first
[undefined] dg-levels [if] include dungeon_levels.fs [then]

( ---- The screen: the view on the left, the panels around ---- )
320 constant dg-w   200 constant dg-h
8 constant dg-vx   8 constant dg-vy   192 constant dg-vw   128 constant dg-vh
dg-vx dg-vw 2/ + constant dg-cx   dg-vy dg-vh 2/ + constant dg-cy
20 constant dg-n   16 constant dg-mm        \ the map is 20x20, 16 monsters per level

( ---- Storage: every level keeps its map, its map of seen squares and its monsters ---- )
create dg-maps dg-cap dg-ns * allot
create dg-seen dg-cap dg-ns * allot
: dg-arr ( n "name" -- )   create  cells here over allot swap erase  does> ( i -- a ) swap cells + ;
dg-cap dg-mm * dg-arr mo.on   dg-cap dg-mm * dg-arr mo.x   dg-cap dg-mm * dg-arr mo.y   dg-cap dg-mm * dg-arr mo.t
dg-cap dg-mm * dg-arr mo.hp   dg-cap dg-mm * dg-arr mo.mv   dg-cap dg-mm * dg-arr mo.at   dg-cap dg-mm * dg-arr mo.fl
create dg-dist dg-ns cells allot            \ steps to the player, -1 where nobody goes
create dg-q dg-ns cells allot

0 value dg-lvl    0 value dg-px   0 value dg-py   0 value dg-dir     \ dir: 0 north, 1 east, 2 south, 3 west
0 value dg-state                            \ 0 playing, 1 paused, 2 dead, 3 won
0 value dg-hp   0 value dg-mhp   0 value dg-food   0 value dg-xp   0 value dg-plv
0 value dg-keys   0 value dg-pots   0 value dg-rats   0 value dg-wpn   0 value dg-arm
0 value dg-score   0 value dg-best   0 value dg-steps   0 value dg-tk
0 value dg-startx   0 value dg-starty   0 value dg-tend
0 value dg-rogue?   0 value dg-cfg?   0 value dg-cfg-i   0 value dg-esc
0 value dg-embed   0 value dg-leave          \ played from the level editor: Esc gives the screen back

create dg-dx 0 , 1 , 0 , -1 ,
create dg-dy -1 , 0 , 1 , 0 ,
: dg-dx@ ( d -- dx )   3 and cells dg-dx + @ ;
: dg-dy@ ( d -- dy )   3 and cells dg-dy + @ ;
: dg-fx ( -- n )   dg-dir dg-dx@ ;
: dg-fy ( -- n )   dg-dir dg-dy@ ;

: dg-map ( -- a )   dg-lvl dg-ns * dg-maps + ;
: dg-in? ( x y -- f )   dg-n u< swap dg-n u< and ;
: dg-t@ ( x y -- c )   2dup dg-in? if dg-n * + dg-map + c@ else 2drop [char] # then ;
: dg-t! ( c x y -- )   2dup dg-in? if dg-n * + dg-map + c! else 2drop drop then ;
: dg-solid? ( c -- f )   ( blocks the way: walls, doors, stairs )
  dup [char] # = over [char] T = or over [char] D = or over [char] L = or over [char] < = or
  swap [char] > = or ;
: dg-seen! ( x y -- )   2dup dg-in? if dg-n * + dg-lvl dg-ns * + dg-seen + 1 swap c! else 2drop then ;
: dg-seen? ( x y -- f )   2dup dg-in? if dg-n * + dg-lvl dg-ns * + dg-seen + c@ 0<> else 2drop 0 then ;

( ---- Monsters: 1 rat, 2 skeleton, 3 orc, 4 troll, 5 lich ---- )
: dg-table   create does> ( t -- v ) swap cells + @ ;
dg-table dg-t-hp   0 , 6 , 14 , 24 , 40 , 70 ,
dg-table dg-t-lo   0 , 1 , 2 , 3 , 5 , 6 ,
dg-table dg-t-hi   0 , 3 , 5 , 7 , 10 , 12 ,
dg-table dg-t-mv   0 , 7 , 9 , 10 , 12 , 9 ,        \ steps (50 ms) between two moves
dg-table dg-t-at   0 , 14 , 16 , 18 , 20 , 14 ,     \ steps between two blows
dg-table dg-t-xp   0 , 6 , 12 , 20 , 35 , 100 ,
dg-table dg-t-sz   0 , 10 , 12 , 13 , 16 , 13 ,     \ width on the floor, in 1/16 of a square
: dg-mname ( t -- a n )
  case 1 of s" RAT" endof 2 of s" SKELETON" endof 3 of s" ORC" endof 4 of s" TROLL" endof 5 of s" LICH" endof
    endcase ;
: dg-m0 ( -- n )   dg-lvl dg-mm * ;

0 value mn-s   0 value mn-x   0 value mn-y   0 value mn-t
: dg-mon-new ( slot x y t -- )
  to mn-t to mn-y to mn-x to mn-s
  1 mn-s mo.on !   mn-x mn-s mo.x !   mn-y mn-s mo.y !   mn-t mn-s mo.t !
  mn-t dg-t-hp mn-s mo.hp !   mn-t dg-t-mv 2 * mn-s mo.mv !   mn-t dg-t-at mn-s mo.at !   0 mn-s mo.fl ! ;

0 value ma-x   0 value ma-y
: dg-mon-at ( x y -- slot | -1 )   ( the live monster of this level on this square )
  to ma-y to ma-x  -1
  dg-m0 dg-mm + dg-m0 do
    i mo.on @ if i mo.x @ ma-x = i mo.y @ ma-y = and if drop i leave then then
  loop ;

( ---- Messages ---- )
create dg-m1 44 allot   create dg-m2 44 allot   0 value dg-m1n   0 value dg-m2n
: dg-say ( a n -- )
  dg-m1 dg-m2 44 cmove  dg-m1n to dg-m2n
  44 min dup to dg-m1n  dg-m1 swap cmove ;
create dg-bf 44 allot   0 value dg-bn
: dg-b-reset ( -- )   0 to dg-bn ;
: dg-b+ ( a n -- )   dg-bn over + 43 > if 2drop exit then  dup >r dg-bf dg-bn + swap cmove  r> +to dg-bn ;
: dg-b-num ( v -- )
  dup 99 > if dup 100 / 10 mod [char] 0 + dg-bf dg-bn + c! 1 +to dg-bn then
  dup 9 > if dup 10 / 10 mod [char] 0 + dg-bf dg-bn + c! 1 +to dg-bn then
  10 mod [char] 0 + dg-bf dg-bn + c! 1 +to dg-bn ;
: dg-b-say ( -- )   dg-bf dg-bn dg-say ;

( ---- The player ---- )
: dg-weapon ( -- a n )   dg-wpn case 0 of s" DAGGER" endof 1 of s" SWORD" endof 2 of s" AXE" endof endcase ;
: dg-armor ( -- a n )   dg-arm case 0 of s" NONE" endof 1 of s" LEATHER" endof 2 of s" CHAIN" endof 3 of s" PLATE" endof endcase ;

: dg-die ( -- )
  2 to dg-state   ticks to dg-tend   dg-score dg-best max to dg-best
  s" YOU HAVE DIED" dg-say   120 500 beep ;
: dg-hurt ( n -- )   negate +to dg-hp   dg-hp 1 < if 0 to dg-hp dg-die then ;

: dg-gain ( xp -- )
  +to dg-xp
  begin dg-xp dg-plv 25 * >= while
    dg-plv 25 * negate +to dg-xp   1 +to dg-plv   8 +to dg-mhp   dg-mhp dg-hp 8 + min to dg-hp
    s" YOU FEEL STRONGER" dg-say   880 100 beep
  repeat ;

0 value dg-pflash   0 value dg-natk   0 value dg-nact   0 value dg-mt0   0 value dg-mfwd   0 value dg-slash

0 value pk-c
: dg-pickup ( c -- )
  to pk-c
  pk-c [char] k = if 1 +to dg-keys  s" YOU FIND A KEY" dg-say  1200 80 beep then
  pk-c [char] h = if 1 +to dg-pots  s" A HEALING POTION" dg-say  1000 80 beep then
  pk-c [char] f = if 1 +to dg-rats  s" SOME FOOD" dg-say  700 80 beep then
  pk-c [char] g = if 25 dg-lvl 1+ * +to dg-score  s" A HEAP OF GOLD" dg-say  1500 60 beep then
  pk-c [char] w = if
    dg-wpn 2 < if 1 +to dg-wpn  s" A BETTER WEAPON" dg-say  else 50 +to dg-score  s" AN OLD WEAPON" dg-say  then  1000 100 beep then
  pk-c [char] a = if
    dg-arm 3 < if 1 +to dg-arm  s" BETTER ARMOR" dg-say  else 50 +to dg-score  s" OLD ARMOR" dg-say  then  1000 100 beep then
  pk-c [char] * = if
    1000 +to dg-score  3 to dg-state  ticks to dg-tend  dg-score dg-best max to dg-best  s" THE AMULET IS YOURS" dg-say  1500 300 beep then
  pk-c [char] . <> pk-c [char] @ <> and if [char] . dg-px dg-py dg-t! then ;

( ---- The automap: squares you have seen ---- )
: dg-cell ( l d -- x y )   ( the square l to the right and d ahead )
  swap >r
  dup dg-fx * dg-px +  r@ dg-fy negate * +
  swap dg-fy * dg-py +  r> dg-fx * + ;
0 value ry-d   0 value ry-x   0 value ry-y
: dg-ray ( d -- )   ( the squares that way and their two sides, up to the first wall )
  to ry-d
  8 1 do
    dg-px ry-d dg-dx@ i * +   dg-py ry-d dg-dy@ i * +   to ry-y to ry-x
    ry-x ry-y dg-seen!
    ry-x ry-d 1+ dg-dx@ +   ry-y ry-d 1+ dg-dy@ +   dg-seen!
    ry-x ry-d 3 + dg-dx@ +  ry-y ry-d 3 + dg-dy@ +  dg-seen!
    ry-x ry-y dg-t@ dg-solid? if leave then
  loop ;
: dg-see ( -- )
  2 -1 do 2 -1 do  dg-px i + dg-py j + dg-seen!  loop loop
  dg-rogue? if 4 0 do i dg-ray loop else dg-dir dg-ray then ;

( ---- Moving, doors, stairs ---- )
0 value ar-c   0 value ar-i
: dg-arrive ( n c -- )   ( on level n, next to the stairs c )
  to ar-c  to dg-lvl  0 to ar-i
  dg-ns 0 do  dg-map i + c@ ar-c = if i to ar-i leave then  loop
  4 0 do
    ar-i dg-n mod i dg-dx@ +   ar-i dg-n / i dg-dy@ +   2dup dg-t@ dg-solid? 0=  2 pick 2 pick dg-mon-at 0< and if
      to dg-py to dg-px  i to dg-dir  leave
    then 2drop
  loop
  0 to dg-mfwd   dg-see ;
: dg-descend ( -- )
  dg-lvl 1+ dg-nlev < if
    dg-lvl 1+ [char] < dg-arrive   dg-b-reset s" DEPTH " dg-b+ dg-lvl 1+ dg-b-num dg-b-say   600 200 beep
  else s" THE STAIRS ARE BLOCKED" dg-say then ;
: dg-ascend ( -- )
  dg-lvl 0> if
    dg-lvl 1- [char] > dg-arrive   dg-b-reset s" DEPTH " dg-b+ dg-lvl 1+ dg-b-num dg-b-say   600 200 beep
  else s" THE WAY BACK IS SEALED" dg-say 150 150 beep then ;

0 value pm-d   0 value pm-x   0 value pm-y   0 value pm-c
: dg-bump ( c -- )
  case
    [char] D of  [char] . pm-x pm-y dg-t!  s" THE DOOR OPENS" dg-say  300 80 beep  endof
    [char] L of
      dg-keys 0> if -1 +to dg-keys  [char] . pm-x pm-y dg-t!  s" THE KEY TURNS" dg-say  900 120 beep
      else s" LOCKED: YOU NEED A KEY" dg-say  150 100 beep then
    endof
    [char] > of dg-descend endof
    [char] < of dg-ascend endof
    150 40 beep
  endcase ;
: dg-move ( d -- )
  to pm-d
  dg-px pm-d dg-dx@ +  dg-py pm-d dg-dy@ +  to pm-y to pm-x
  pm-x pm-y dg-t@ to pm-c
  pm-x pm-y dg-mon-at 0< 0= if exit then
  pm-c dg-solid? if pm-c dg-bump exit then
  pm-x to dg-px  pm-y to dg-py
  ticks to dg-mt0   pm-d dg-dir = to dg-mfwd
  1 +to dg-steps
  pm-c dg-pickup   dg-see
  90 14 beep ;
: dg-turn ( n -- )   dg-dir + 3 and to dg-dir   0 to dg-mfwd   dg-see ;

( ---- Fighting ---- )
0 value at-s
: dg-attack ( -- )
  ticks dg-natk < if exit then
  ticks 700 dg-wpn 120 * - + to dg-natk   ticks to dg-slash
  dg-px dg-fx +  dg-py dg-fy +  dg-mon-at to at-s
  at-s 0< if 300 30 beep exit then
  100 random 85 < 0= if s" YOU MISS" dg-say  250 30 beep exit then
  2 dg-wpn 3 * + dg-plv 2/ +  3 dg-wpn 2 * + random +
  at-s mo.hp @ over - at-s mo.hp !   6 at-s mo.fl !
  dg-b-reset s" YOU HIT THE " dg-b+ at-s mo.t @ dg-mname dg-b+ s"  " dg-b+ dg-b-num dg-b-say
  500 40 beep
  at-s mo.hp @ 1 < if
    0 at-s mo.on !
    dg-b-reset s" THE " dg-b+ at-s mo.t @ dg-mname dg-b+ s"  DIES" dg-b+ dg-b-say
    at-s mo.t @ dg-t-xp dup dg-gain  2 * +to dg-score
    200 120 beep
  then ;

0 value mt-s   0 value mt-d
: dg-mon-attack ( slot -- )
  to mt-s
  mt-s mo.t @ dg-t-at mt-s mo.at !
  100 random 75 dg-arm 8 * - < 0= if s" IT MISSES YOU" dg-say exit then
  mt-s mo.t @ dup dg-t-lo swap dg-t-hi over - 1+ random +  dg-arm - 1 max to mt-d
  dg-b-reset s" THE " dg-b+ mt-s mo.t @ dg-mname dg-b+ s"  HITS YOU " dg-b+ mt-d dg-b-num dg-b-say
  8 to dg-pflash   200 80 beep
  mt-d dg-hurt ;

( ---- Monsters move: breadth first search from the player ---- )
0 value b-h   0 value b-t   0 value b-i   0 value b-x   0 value b-y
: dg-d! ( v idx -- )   cells dg-dist + ! ;
: dg-d@ ( idx -- v )   cells dg-dist + @ ;
: dg-bfs ( -- )
  dg-ns 0 do -1 i dg-d! loop
  dg-py dg-n * dg-px +  dup 0 swap dg-d!  dg-q !
  0 to b-h   1 to b-t
  begin b-h b-t < while
    b-h cells dg-q + @ to b-i   1 +to b-h
    b-i dg-n mod to b-x   b-i dg-n / to b-y
    4 0 do
      b-x i dg-dx@ +   b-y i dg-dy@ +   2dup dg-in? if
        2dup dg-t@ dg-solid? 0= if
          dg-n * +  dup dg-d@ 0< if
            b-i dg-d@ 1+ over dg-d!   b-t cells dg-q + !   1 +to b-t
          else drop then
        else 2drop then
      else 2drop then
    loop
  repeat ;

0 value mv-s   0 value mv-o   0 value mv-k   0 value mv-x   0 value mv-y
: dg-mon-move ( slot -- )
  to mv-s   4 random to mv-o
  4 0 do
    i mv-o + 3 and to mv-k
    mv-s mo.x @ mv-k dg-dx@ +   mv-s mo.y @ mv-k dg-dy@ +   to mv-y to mv-x
    mv-x mv-y dg-in? if
      mv-y dg-n * mv-x + dg-d@   mv-s mo.y @ dg-n * mv-s mo.x @ + dg-d@ 1-  = if
        mv-x mv-y dg-mon-at 0< if
          mv-x mv-s mo.x !   mv-y mv-s mo.y !
          mv-s mo.t @ dg-t-mv mv-s mo.mv !
          leave
        then
      then
    then
  loop ;

0 value ms-s   0 value ms-d
: dg-mon-step ( slot -- )
  to ms-s
  ms-s mo.fl @ 0> if -1 ms-s mo.fl +! then
  ms-s mo.mv @ 0> if -1 ms-s mo.mv +! then
  ms-s mo.at @ 0> if -1 ms-s mo.at +! then
  ms-s mo.y @ dg-n * ms-s mo.x @ + dg-d@ to ms-d
  ms-d 1 < ms-d 9 > or if exit then
  ms-d 1 = if  ms-s mo.at @ 0= if ms-s dg-mon-attack then exit  then
  ms-s mo.mv @ 0= if ms-s dg-mon-move then ;

( ---- Hunger, healing, the clock ---- )
: dg-life ( -- )
  1 +to dg-tk
  dg-tk 20 mod 0= if
    dg-food 0> if -1 +to dg-food then
    dg-food 150 < dg-rats 0> and if
      -1 +to dg-rats   dg-food 500 + 1000 min to dg-food   s" YOU EAT SOME FOOD" dg-say  400 100 beep
    then
    dg-food 0= if  s" YOU ARE STARVING" dg-say  1 dg-hurt  then
  then
  dg-tk 100 mod 0=  dg-food 0> and  dg-hp dg-mhp < and if 1 +to dg-hp then ;

: dg-step ( -- )   ( one tick of the game: 50 ms )
  dg-state 0<> if exit then
  dg-life
  dg-state 0<> if exit then
  dg-bfs
  dg-m0 dg-mm + dg-m0 do  i mo.on @ if i dg-mon-step then  loop
  dg-pflash 0> if -1 +to dg-pflash then ;

( ---- Starting ---- )
0 value sl-k   0 value sl-c
: dg-spawn-level ( n -- )
  to dg-lvl  0 to sl-k
  dg-n 0 do dg-n 0 do
    i j dg-t@ to sl-c
    sl-c [char] 1 >= sl-c [char] 5 <= and if
      sl-k dg-mm < if  sl-k dg-m0 +  i j sl-c [char] 0 -  dg-mon-new  then
      1 +to sl-k   [char] . i j dg-t!
    then
    sl-c [char] @ = if  i to dg-startx  j to dg-starty  [char] . i j dg-t!  then
  loop loop ;
: dg-new-game ( -- )
  ticks 1+ rand-seed !
  dg-levels dg-maps dg-cap dg-ns * cmove
  dg-seen dg-cap dg-ns * erase
  dg-cap dg-mm * 0 do 0 i mo.on ! loop
  dg-nlev 0 do i dg-spawn-level loop
  0 to dg-lvl   dg-startx to dg-px   dg-starty to dg-py   2 to dg-dir
  40 to dg-hp   40 to dg-mhp   800 to dg-food   0 to dg-xp   1 to dg-plv
  0 to dg-keys   0 to dg-pots   1 to dg-rats   0 to dg-wpn   0 to dg-arm
  0 to dg-score   0 to dg-steps   0 to dg-tk   0 to dg-state   0 to dg-pflash
  0 to dg-m1n  0 to dg-m2n  0 to dg-mfwd  0 to dg-natk  0 to dg-nact  -1000 to dg-slash
  dg-see
  s" K SETS THE KEYS, V THE VIEW" dg-say   s" FIND THE AMULET" dg-say ;

( ---- Keys ---- )
create dg-was 256 allot   dg-was 256 erase
: dg-just ( k -- f )   ( pressed now, not at the last look )
  dup pressed? swap 255 and dg-was + 2dup c@ 0= and >r c! r> ;
: dg-drink ( -- )
  dg-pots 0= if s" NO POTION" dg-say exit then
  dg-hp dg-mhp = if s" YOU FEEL FINE" dg-say exit then
  -1 +to dg-pots   dg-hp 20 + dg-mhp min to dg-hp   s" YOU FEEL BETTER" dg-say  900 120 beep ;
: dg-sync ( -- )   ( forget what was pressed before: no key looks new )
  256 0 do i dg-just drop loop ;
\ The keys: 11 actions, each with a key you can change (k), and the arrows that always work
create dg-kb 11 cells allot
create dg-alt 210 , 209 , 0 , 0 , 208 , 207 , 0 , 0 , 0 , 9 , 0 ,    \ arrows, and tab for the view
create dg-p-qwerty 119 , 115 , 97 , 100 , 113 , 101 , 32 , 104 , 109 , 118 , 112 ,   \ w s a d q e space h m v p
create dg-p-azerty 122 , 115 , 113 , 100 , 97 , 101 , 32 , 104 , 109 , 118 , 112 ,   \ z s q d a e space h m v p
: dg-kb@ ( action -- key )   cells dg-kb + @ ;
: dg-kb! ( key action -- )   cells dg-kb + swap 255 and swap ! ;
: dg-alt@ ( action -- key | 0 )   cells dg-alt + @ ;
: dg-preset ( a -- )   11 0 do  dup i cells + @ i dg-kb!  loop drop ;
dg-p-qwerty dg-preset
: dg-act? ( action -- f )   ( held )
  dup dg-kb@ pressed?  swap dg-alt@ dup if pressed? else drop 0 then  or ;
: dg-act-just? ( action -- f )   ( pressed now, not at the last look )
  dup dg-kb@ dg-just  swap dg-alt@ dup if dg-just else drop 0 then  or ;
\ 0 forward  1 back  2 step left  3 step right  4 turn left  5 turn right  6 hit  7 potion  8 map  9 view  10 pause
: dg-aname ( action -- a n )
  case 0 of s" FORWARD" endof 1 of s" BACK" endof 2 of s" STEP LEFT" endof 3 of s" STEP RIGHT" endof
    4 of s" TURN LEFT" endof 5 of s" TURN RIGHT" endof 6 of s" HIT" endof 7 of s" POTION" endof
    8 of s" MAP" endof 9 of s" VIEW" endof 10 of s" PAUSE" endof endcase ;
create dg-kn 4 allot
: dg-keyname ( code -- a n )
  case
    32 of s" SPC" endof   9 of s" TAB" endof   13 of s" ENTER" endof
    207 of s" RIGHT" endof   208 of s" LEFT" endof   209 of s" DOWN" endof   210 of s" UP" endof
    dup dg-kn c!  drop dg-kn 1 0
  endcase ;
: dg-kn+ ( action -- )   dg-kb@ dg-keyname dg-b+ ;

\ The keys are kept in dungeon_keys.txt (11 numbers) where the game is started, if the system can write files
0 value kp-i   0 value kp-v   0 value kp-in
: dg-parse-keys ( a n -- )
  0 to kp-i  0 to kp-v  0 to kp-in
  0 ?do
    dup i + c@  dup [char] 0 >= over [char] 9 <= and if
      [char] 0 - kp-v 10 * + to kp-v  -1 to kp-in
    else drop kp-in if
      kp-i 11 < if kp-v kp-i dg-kb! then  1 +to kp-i  0 to kp-v  0 to kp-in
    then then
  loop drop ;
[defined] dump-file [if]
create dg-ks 160 allot   0 value dg-ksn
: dg-ks-num ( v -- )
  dup 99 > if dup 100 / 10 mod [char] 0 + dg-ks dg-ksn + c! 1 +to dg-ksn then
  dup 9 > if dup 10 / 10 mod [char] 0 + dg-ks dg-ksn + c! 1 +to dg-ksn then
  10 mod [char] 0 + dg-ks dg-ksn + c! 1 +to dg-ksn
  32 dg-ks dg-ksn + c! 1 +to dg-ksn ;
: dg-save-raw ( -- )
  0 to dg-ksn   11 0 do i dg-kb@ dg-ks-num loop
  10 dg-ks dg-ksn + c! 1 +to dg-ksn
  dg-ks dg-ksn s" dungeon_keys.txt" dump-file ;
: dg-save-keys ( -- )   ['] dg-save-raw catch drop ;
: dg-load-raw ( -- )
  s" dungeon_keys.txt" r/o open-file if drop exit then
  >r  dg-ks 150 r@ read-file  r> close-file drop
  if drop exit then
  dg-ks swap dg-parse-keys ;
: dg-load-keys ( -- )   ['] dg-load-raw catch drop ;
[else]
: dg-save-keys ( -- ) ;
: dg-load-keys ( -- ) ;
[then]

0 value dg-map?
: dg-toggle-view ( -- )
  dg-rogue? 0= to dg-rogue?   0 to dg-mfwd   0 to dg-map?
  dg-see
  dg-rogue? if s" ROGUELIKE VIEW" else s" FIRST PERSON VIEW" then dg-say ;
: dg-wait ( ms -- )   ticks + to dg-nact ;
: dg-rmove ( d -- )   ( roguelike: a step in that direction of the map, or a blow if a monster is there )
  dup to dg-dir  0 to dg-mfwd
  dg-px over dg-dx@ +   dg-py rot dg-dy@ +   dg-mon-at 0< 0= if dg-attack exit then
  dg-dir dg-move  0 to dg-mfwd ;
: dg-input-rogue ( -- )
  6 dg-act? if dg-attack then
  ticks dg-nact < if exit then
  0 dg-act? key-up pressed? or if 0 dg-rmove 140 dg-wait exit then
  3 dg-act? key-right pressed? or if 1 dg-rmove 140 dg-wait exit then
  1 dg-act? key-down pressed? or if 2 dg-rmove 140 dg-wait exit then
  2 dg-act? key-left pressed? or if 3 dg-rmove 140 dg-wait exit then ;
: dg-input ( -- )
  7 dg-act-just? if dg-drink then
  8 dg-act-just? if dg-map? 0= to dg-map? then
  dg-rogue? if dg-input-rogue exit then
  6 dg-act? if dg-attack then
  ticks dg-nact < if exit then
  0 dg-act? if dg-dir dg-move  180 dg-wait exit then
  1 dg-act? if dg-dir 2 + dg-move  180 dg-wait exit then
  2 dg-act? if dg-dir 3 + dg-move  180 dg-wait exit then
  3 dg-act? if dg-dir 1 + dg-move  180 dg-wait exit then
  4 dg-act? if -1 dg-turn  150 dg-wait exit then
  5 dg-act? if 1 dg-turn  150 dg-wait exit then ;

\ The screen where the keys are changed: up and down choose, enter then a key, 1 and 2 are the presets
create dg-snap 256 allot
: dg-cfg-close ( -- )   0 to dg-cfg?  dg-save-keys  dg-sync  0 to dg-esc ;
: dg-cfg-capture ( -- )
  dg-esc if 0 to dg-esc 1 to dg-cfg? exit then
  256 0 do
    i 65 < i 90 > or  i 27 <> and  i 13 <> and  i 107 <> and  i 0<> and  if
      i pressed?  dg-snap i + c@ 0= and if  i dg-cfg-i dg-kb!  1 to dg-cfg?  leave  then
    then
  loop ;
: dg-cfg-keys ( -- )
  dg-cfg? 2 = if dg-cfg-capture exit then
  key-up dg-just if dg-cfg-i 1- dup 0< if drop 10 then to dg-cfg-i then
  key-down dg-just if dg-cfg-i 1+ dup 10 > if drop 0 then to dg-cfg-i then
  key-enter dg-just if  256 0 do i pressed? dg-snap i + c! loop  2 to dg-cfg?  then
  [char] 1 dg-just if dg-p-qwerty dg-preset then
  [char] 2 dg-just if dg-p-azerty dg-preset then
  [char] k dg-just  dg-esc or if dg-cfg-close then ;

( ---- Drawing: a tiny 3x5 pixel font ---- )
create dg-digits
  $7b6f , $2c97 , $73e7 , $73cf , $5bc9 , $79cf , $79ef , $7249 , $7bef , $7bcf ,
create dg-font   ( A to Z )
  $2bed , $6bae , $3923 , $6b6e , $79a7 , $79a4 , $396b , $5bed , $7497 , $126a , $5bad , $4927 , $5fed ,
  $6b6d , $2b6a , $6ba4 , $2b7b , $6bad , $388e , $7492 , $5b6f , $5b6a , $5bfd , $5aad , $5a92 , $72a7 ,
1 value dg-sc                                \ scale of the text: 1, or 2 for the banners
: dg-bits ( bits x y -- )
  15 0 do
    2 pick 14 i - rshift 1 and if
      over i 3 mod dg-sc * +   over i 3 / dg-sc * +   dg-sc dg-sc box
    then
  loop 2drop drop ;
: dg-glyph ( c -- bits )
  dup [char] 0 >= over [char] 9 <= and if [char] 0 - cells dg-digits + @ exit then
  dup [char] . = if drop $0002 exit then
  dup [char] ! = if drop $2482 exit then
  dup [char] - = if drop $01c0 exit then
  dup [char] / = if drop $24a4 exit then
  dup [char] : = if drop $0410 exit then
  32 or dup 97 < over 122 > or if drop 0 exit then
  97 - cells dg-font + @ ;
0 value tx-a   0 value tx-x   0 value tx-y
: dg-text ( a n x y -- )
  to tx-y to tx-x swap to tx-a
  0 ?do  tx-a i + c@ dg-glyph   tx-x i 4 * dg-sc * +   tx-y   dg-bits  loop ;
0 value dn-n   0 value dn-d   0 value dn-x   0 value dn-y
: dg-num ( n digits x y -- )   ( right aligned in that many places )
  to dn-y to dn-x to dn-d  0 max to dn-n
  dn-d 0 ?do
    dn-n 10 mod cells dg-digits + @   dn-x dn-d 1- i - 4 * dg-sc * +   dn-y   dg-bits
    dn-n 10 / to dn-n
  loop ;
0 value br-x   0 value br-y   0 value br-w   0 value br-c   0 value br-f
: dg-bar ( v max x y w rgb -- )
  to br-c to br-w to br-y to br-x
  swap br-w * swap / 0 max br-w min to br-f
  $26262e color br-x br-y br-w 3 box
  br-c color br-x br-y br-f 3 box ;

( ---- Sprites: letters stand for colors, . is clear. 16x16 for the monsters, 8x8 for the things ---- )
create dg-pal 128 cells allot   dg-pal 128 cells erase
: dg-pc! ( rgb ch -- )   cells dg-pal + ! ;
: dg-pcol ( ch -- rgb )   cells dg-pal + @ ;
: dg-pal-init ( -- )
  $101018 [char] k dg-pc!   $e8e8e0 [char] w dg-pc!   $909090 [char] g dg-pc!   $505058 [char] d dg-pc!
  $c02020 [char] r dg-pc!   $e07820 [char] o dg-pc!   $f0d020 [char] y dg-pc!   $8a5a2a [char] b dg-pc!
  $503010 [char] n dg-pc!   $30a030 [char] G dg-pc!   $206020 [char] l dg-pc!   $8030c0 [char] p dg-pc!
  $e090a0 [char] m dg-pc!   $40c0e0 [char] c dg-pc!   $3050d0 [char] B dg-pc! ;
dg-pal-init
create dg-sdata 1728 allot   0 value dg-sp
: dg-srow ( a n -- )   dup >r  dg-sdata dg-sp + swap cmove  r> +to dg-sp ;
\ 0 rat
s" ................" dg-srow   s" ................" dg-srow   s" ................" dg-srow   s" ................" dg-srow
s" ................" dg-srow   s" ................" dg-srow   s" ................" dg-srow
s" ....mm....mm...." dg-srow   s" ...mggm..mggm..." dg-srow   s" ...gggggggggg..." dg-srow
s" ..ggkggggggkgg.." dg-srow   s" ..ggrggggggrgg.." dg-srow   s" ..ggggggwwgggg.." dg-srow
s" ...ggggkkgggg..." dg-srow   s" ..dg.gggggg.gd.." dg-srow   s" .ddd.dd..dd.ddd." dg-srow
\ 1 skeleton
s" .....wwwwww....." dg-srow   s" ....wwwwwwww...." dg-srow   s" ....wkkwwkkw...." dg-srow
s" ....wkkwwkkw...." dg-srow   s" ....wwwkkwww...." dg-srow   s" .....wwwwww....." dg-srow
s" .....w.ww.w....." dg-srow   s" ...wwwwwwwwww..." dg-srow   s" ..w.wwwwwwww.w.." dg-srow
s" ..w.w.wwww.w.w.." dg-srow   s" ..w.wwwwwwww.w.." dg-srow   s" ..w..w.ww.w..w.." dg-srow
s" .....wwwwww....." dg-srow   s" .....ww..ww....." dg-srow   s" .....ww..ww....." dg-srow
s" ....www..www...." dg-srow
\ 2 orc
s" ................" dg-srow   s" .....llllll....." dg-srow   s" ....lGGGGGGl...." dg-srow
s" ...lGGGGGGGGl..." dg-srow   s" ...lGyrGGryGl..." dg-srow   s" ...lGGGGGGGGl..." dg-srow
s" ...lGwGwwGwGl..." dg-srow   s" ....lGGGGGGl...." dg-srow   s" ..bbbbbbbbbbbb.." dg-srow
s" .GGbbnbbbbnbbGG." dg-srow   s" .GGbbbbbbbbbbGG." dg-srow   s" .GG.bbbbbbbb.GG." dg-srow
s" ....bb.bb.bb...." dg-srow   s" ....bb....bb...." dg-srow   s" ...nnn....nnn..." dg-srow
s" ...nnn....nnn..." dg-srow
\ 3 troll
s" ....nnnnnnnn...." dg-srow   s" ...nbbbbbbbbn..." dg-srow   s" ..nbbbbbbbbbbn.." dg-srow
s" ..nbbkbbbbkbbn.." dg-srow   s" ..nbbyrbbyrbbn.." dg-srow   s" ..nbbbbbbbbbbn.." dg-srow
s" ..nbbwbwwbwbbn.." dg-srow   s" ...nbbbbbbbbn..." dg-srow   s" .nnbbbbbbbbbbnn." dg-srow
s" nbbnbbbnnbbbnbbn" dg-srow   s" nbbnbbbbbbbbnbbn" dg-srow   s" nbbn.bbbbbb.nbbn" dg-srow
s" .nn..bbbbbb..nn." dg-srow   s" ....bbb..bbb...." dg-srow   s" ...nbbb..bbbn..." dg-srow
s" ..nnnnn..nnnnn.." dg-srow
\ 4 lich
s" ......pppp......" dg-srow   s" .....pppppp....." dg-srow   s" ....ppwwwwpp...." dg-srow
s" ....pwkwwkwp...." dg-srow   s" ....pwwkkwwp...." dg-srow   s" ....ppwwwwpp...." dg-srow
s" .....pwkkwp....." dg-srow   s" ...pppppppppp..." dg-srow   s" ..ppcpppppppcpp." dg-srow
s" ..pp.pppppppp.pp" dg-srow   s" ..pp.ppcppppp.pp" dg-srow   s" .....pppppppp..." dg-srow
s" .....pppppppp..." dg-srow   s" ....pppppppppp.." dg-srow   s" ...pppppppppppp." dg-srow
s" ..pppppp..pppppp" dg-srow
\ 5 key, 6 potion, 7 food, 8 weapon, 9 armor, 10 gold, 11 amulet
s" ........" dg-srow   s" ..yyy..." dg-srow   s" .y...y.." dg-srow   s" .y...y.." dg-srow
s" ..yyy..." dg-srow   s" ...y...." dg-srow   s" ...yy..." dg-srow   s" ...yy..." dg-srow
s" ...kk..." dg-srow   s" ...cc..." dg-srow   s" ...kk..." dg-srow   s" ..krrk.." dg-srow
s" .krrwrk." dg-srow   s" .krrrrk." dg-srow   s" .krrrrk." dg-srow   s" ..kkkk.." dg-srow
s" ........" dg-srow   s" ..oooo.." dg-srow   s" .obbbbo." dg-srow   s" obbbbbbo" dg-srow
s" obbbbbbo" dg-srow   s" .obbbbo." dg-srow   s" ..oooo.." dg-srow   s" ...ww..." dg-srow
s" .....ww." dg-srow   s" ....www." dg-srow   s" ...www.." dg-srow   s" .w.ww..." dg-srow
s" ..www..." dg-srow   s" ..by...." dg-srow   s" .b.y...." dg-srow   s" b......." dg-srow
s" .gggggg." dg-srow   s" gwgggggg" dg-srow   s" gwggggdg" dg-srow   s" gwggggdg" dg-srow
s" gwggggdg" dg-srow   s" .gggggd." dg-srow   s" ..gggd.." dg-srow   s" ...dd..." dg-srow
s" ........" dg-srow   s" ........" dg-srow   s" ..yy.yy." dg-srow   s" .yoyyyoy" dg-srow
s" .yyyyyyy" dg-srow   s" yoyyoyyo" dg-srow   s" yyyyyyyy" dg-srow   s" .oooooo." dg-srow
s" ...yy..." dg-srow   s" ..y..y.." dg-srow   s" ..y..y.." dg-srow   s" ...yy..." dg-srow
s" ..cccc.." dg-srow   s" .cc..cc." dg-srow   s" .cwcccc." dg-srow   s" ..cccc.." dg-srow
: dg-sprite-a ( id -- a )   dup 5 < if 256 * else 5 - 64 * 1280 + then dg-sdata + ;
: dg-sprite-n ( id -- n )   5 < if 16 else 8 then ;
: dg-item-id ( c -- id | -1 )
  -1 swap  7 0 do  dup s" khfwag*" drop i + c@ = if nip i 5 + swap then  loop drop ;

0 value sp-a   0 value sp-n   0 value sp-x   0 value sp-y   0 value sp-s   0 value sp-pct   0 value sp-fl
0 value sp-i   0 value sp-k   0 value sp-ch
: dg-sh ( rgb pct -- rgb' )
  >r  dup 16 rshift $ff and r@ * 100 /  16 lshift
  over 8 rshift $ff and r@ * 100 / 8 lshift or
  swap $ff and r> * 100 / or ;
: dg-spr ( id x y s pct flash -- )   ( one sprite, s pixels wide, a bit darker when pct < 100 )
  to sp-fl to sp-pct to sp-s to sp-y to sp-x
  dup dg-sprite-n to sp-n  dg-sprite-a to sp-a
  sp-s 2 < if exit then
  sp-n 0 do
    0 to sp-i
    begin sp-i sp-n < while
      sp-a i sp-n * + sp-i + c@ to sp-ch
      sp-i 1+ to sp-k
      begin sp-k sp-n < if sp-a i sp-n * + sp-k + c@ sp-ch = else 0 then while 1 +to sp-k repeat
      sp-ch [char] . <> if
        sp-ch dg-pcol  sp-fl if $fefefe and 1 rshift $808080 + then  sp-pct dg-sh color
        sp-x sp-i sp-s * sp-n / +   sp-y i sp-s * sp-n / +
        sp-k sp-s * sp-n /  sp-i sp-s * sp-n /  -  1 max
        i 1+ sp-s * sp-n /  i sp-s * sp-n /  -  1 max   box
      then
      sp-k to sp-i
    repeat
  loop ;

( ---- The view: perspective with the farthest squares first ---- )
\ Units: 16 per square. A point at lateral x16 and distance z16 is at
\ cx + 64 * x16 / z16; half the height of a wall is 768 / z16 pixels. The eye
\ is at the back of your square: its walls are at z16 from 0 to 16.
create dg-th-wall $8c8c98 , $6f9560 , $a86058 ,
create dg-th-flr  $4a443c , $38482c , $503030 ,
create dg-th-cel  $2a2a36 , $1e2c1e , $341c1c ,
: dg-th@ ( tab -- rgb )   dg-lvl 3 mod cells + @ ;
0 value dg-eov                              \ the eye is this far behind (1/16), while you slide forward
: dg-eo ( -- n )
  dg-mfwd 0= if 0 exit then
  ticks dg-mt0 - dup 150 >= if drop 0 exit then  16 * 150 / 16 swap - ;
: dg-sx ( x16 z16 -- px )   >r 64 * r> / dg-cx + ;
: dg-hh ( z16 -- px )   768 swap / ;
: dg-br ( z16 -- pct )   100 * 120 / 100 swap - 12 max ;
: dg-wallc ( z16 -- rgb )   dg-th-wall dg-th@ swap dg-br dg-sh ;
: dg-vl ( x ya yb -- )   >r over r> line ;

0 value gf-x0   0 value gf-x1   0 value gf-y0   0 value gf-y1   0 value gf-z   0 value gf-col   0 value gf-w   0 value gf-h
: dg-fgeom ( l z16 -- )   ( the face of the square l, seen at distance z16 )
  to gf-z
  dup 16 * 8 - gf-z dg-sx to gf-x0
  16 * 8 + gf-z dg-sx to gf-x1
  gf-z dg-hh  dup dg-cy swap - to gf-y0  dg-cy + to gf-y1
  gf-x1 gf-x0 - to gf-w   gf-y1 gf-y0 - to gf-h ;
: dg-fbox ( rgb -- )   color gf-x0 gf-y0 gf-w gf-h box ;
0 value gb-y   0 value gb-yb
: dg-bricks ( -- )
  gf-col 58 dg-sh color
  5 0 do  gf-h i * 4 / gf-y0 + to gb-y   gf-x0 gb-y gf-x1 gb-y line  loop
  4 0 do
    gf-y0 gf-h i * 4 / + to gb-y   gf-y0 gf-h i 1+ * 4 / + to gb-yb
    i 1 and if  gf-x0 gf-w 2/ +  gb-y gb-yb dg-vl
    else  gf-x0 gf-w 4 / +  gb-y gb-yb dg-vl   gf-x0 gf-w 3 * 4 / +  gb-y gb-yb dg-vl  then
  loop ;

0 value sd-x   0 value sd-ax   0 value sd-lo   0 value sd-hi   0 value sd-col   0 value sd-dx   0 value sd-hh
0 value sd-door   0 value sd-z0   0 value sd-z1   0 value sd-i   0 value sd-xe   0 value sd-xm
: dg-side ( x16 z0 z1 -- )   ( a side wall at x16, from distance z0 to z1 )
  to sd-z1  2 max to sd-z0  to sd-x
  sd-z1 sd-z0 <= if exit then
  sd-x abs to sd-ax
  sd-x sd-z0 dg-sx to sd-xe   sd-x sd-z0 sd-z1 + 2/ dg-sx to sd-xm
  sd-x sd-z0 dg-sx  sd-x sd-z1 dg-sx  2dup min to sd-lo  max to sd-hi
  sd-lo dg-vx max to sd-lo   sd-hi dg-vx dg-vw + 1- min to sd-hi
  sd-lo sd-hi > if exit then
  sd-hi 1+ sd-lo do
    i to sd-i
    dg-cx sd-i - abs 1 max to sd-dx
    12 sd-dx * sd-ax / to sd-hh
    sd-door 0= if  64 sd-ax * sd-dx / dg-wallc
    else  sd-door  64 sd-ax * sd-dx / dg-br dg-sh  then  to sd-col
    sd-col color   sd-i dg-cy sd-hh -  1  sd-hh 2* 1+  box
    sd-col 58 dg-sh color
    5 0 do  sd-i  dg-cy sd-hh i 2 - * 2/ +  1 1 box  loop
    sd-i sd-xe = if sd-i dg-cy sd-hh -  1  sd-hh 2* 1+  box then
    sd-i sd-xm = if
      sd-i dg-cy sd-hh -  1  sd-hh 2/  box
      sd-i dg-cy  1  sd-hh 2/  box
    then
  loop ;

0 value c-l   0 value c-d   0 value c-c   0 value c-z   0 value c-cx   0 value c-cy
0 value st-x   0 value st-y   0 value st-w   0 value st-h   0 value st-yy   0 value st-in
: dg-door-face ( -- )
  $8a5a2a gf-z dg-br dg-sh to gf-col   gf-col dg-fbox
  gf-col 55 dg-sh color
  4 0 do  gf-x0 gf-w i 1+ * 5 / +  gf-y0 gf-y1 dg-vl  loop
  gf-x0 gf-y0 gf-x1 gf-y0 line   gf-x0 gf-y1 gf-x1 gf-y1 line
  c-c [char] L = if
    $505058 gf-z dg-br dg-sh color
    gf-x0  gf-y0 gf-h 5 / +  gf-w gf-h 12 / 1 max  box
    gf-x0  gf-y0 gf-h 3 * 4 / +  gf-w gf-h 12 / 1 max  box
    $e0c040 gf-z dg-br 20 + 100 min dg-sh color
    gf-x0 gf-w 2/ + gf-w 12 / -  gf-y0 gf-h 2/ +  gf-w 6 / 1 max  gf-h 8 / 1 max  box
  else
    $e0c040 gf-z dg-br dg-sh color
    gf-x0 gf-w 4 * 5 / +  gf-y0 gf-h 2/ +  gf-w 16 / 1 max  gf-h 12 / 1 max  box
  then ;
: dg-stairs ( -- )
  gf-x0 gf-w 5 / + to st-x   gf-y0 gf-h 6 / + to st-y   gf-w 3 * 5 / to st-w   gf-y1 st-y - to st-h
  c-c [char] > = if $08080e else $9aa8c8 gf-z dg-br dg-sh then  color   st-x st-y st-w st-h box
  c-c [char] > = if $4a4a66 else $5a6080 then  gf-z dg-br dg-sh color
  4 0 do
    st-h i 1+ * 5 / st-y + to st-yy
    c-c [char] > = if st-w i 1+ * 14 / else 0 then to st-in
    st-x st-in +  st-yy  st-x st-w + st-in -  st-yy  line
  loop ;
0 value fm-x   0 value fm-y   0 value fm-w   0 value fm-h
: dg-flame ( xc ym z16 -- )   ( a torch on the wall: a stem and a flame that flickers )
  >r  to fm-y to fm-x
  r@ dg-hh dup 6 / 2 max to fm-w   4 / 2 max to fm-h
  $503010 r> dg-br dg-sh color   fm-x fm-w 6 / 1 max 2/ -  fm-y  fm-w 3 / 1 max  fm-h  box
  ticks 90 / 1 and if $ff8020 else $ffa030 then color
  fm-x fm-w 2/ -  fm-y fm-h -  fm-w fm-h  box
  $ffe060 color   fm-x fm-w 4 / -  fm-y fm-h - fm-h 2/ +  fm-w 2/ 1 max  fm-h 2/ 1 max  box ;
: dg-torch ( -- )   gf-x0 gf-w 2/ +  gf-y0 gf-h 2/ +  gf-z dg-flame ;
: dg-face ( l z16 -- )
  dg-fgeom
  c-c [char] D = c-c [char] L = or if dg-door-face exit then
  gf-z dg-wallc to gf-col   gf-col dg-fbox   dg-bricks
  c-c [char] T = if dg-torch then
  c-c [char] > = c-c [char] < = or if dg-stairs then ;

0 value en-l   0 value en-d   0 value en-z   0 value en-xc   0 value en-yb   0 value en-sz   0 value en-s
: dg-ent ( l d -- )   ( what lies on the floor of that square: a thing, a monster )
  to en-d to en-l
  en-d 16 * 8 + dg-eov + to en-z
  en-z 14 < if exit then
  en-l 16 * en-z dg-sx to en-xc
  en-z dg-hh dg-cy + to en-yb
  en-l en-d dg-cell to c-cy to c-cx
  c-cx c-cy dg-t@ dg-item-id  dup 0< 0= if
    384 en-z / to en-sz
    en-xc en-sz 2/ -  en-yb en-sz -  en-sz  en-z dg-br 25 + 100 min  0  dg-spr
  else drop then
  c-cx c-cy dg-mon-at  dup 0< 0= if
    to en-s
    en-s mo.t @ dg-t-sz 64 * en-z / to en-sz
    en-s mo.t @ 1-
    en-xc en-sz 2/ -  en-yb en-sz -  en-sz  en-z dg-br 30 + 100 min  en-s mo.fl @ 0>  dg-spr
  else drop then ;

: dg-cell-draw ( l d -- )
  to c-d to c-l
  c-l c-d dg-cell dg-t@ to c-c
  c-c dg-solid? 0= if c-l c-d dg-ent exit then
  c-d 16 * dg-eov + to c-z
  c-z 12 >= if c-l c-z dg-face then
  c-c [char] D = if $8a5a2a else c-c [char] L = if $9a7a2a else 0 then then to sd-door
  c-l 0< if c-l 16 * 8 +  c-z c-z 16 + dg-side then
  c-l 0> if c-l 16 * 8 -  c-z c-z 16 + dg-side then
  c-c [char] T = c-l 0<> and c-z 8 + 12 >= and if
    c-l 0< if c-l 16 * 8 + else c-l 16 * 8 - then  c-z 8 + dg-sx   dg-cy  c-z 8 +  dg-flame
  then ;
0 value dl-d   0 value dl-k
: dg-layer ( d -- )   ( the squares at that distance, the outer ones first )
  to dl-d   dl-d 0 max 2 + 5 min
  begin dup 0 >= while
    dup to dl-k
    dl-k negate dl-d dg-cell-draw
    dl-k 0> if dl-k dl-d dg-cell-draw then
    1-
  repeat drop ;

0 value fl-y
: dg-backdrop ( -- )
  dg-th-cel dg-th@ color   dg-vx dg-vy dg-vw dg-vh 2/ box
  dg-th-flr dg-th@ color   dg-vx dg-cy dg-vw dg-vh 2/ box
  0 0 0 150 rgba   dg-vx dg-cy 12 - dg-vw 12 box   dg-vx dg-cy dg-vw 12 box
  0 0 0 90 rgba    dg-vx dg-cy 26 - dg-vw 14 box   dg-vx dg-cy 12 + dg-vw 14 box
  0 0 0 45 rgba    dg-vx dg-cy 44 - dg-vw 18 box   dg-vx dg-cy 26 + dg-vw 18 box
  dg-th-flr dg-th@ 60 dg-sh color
  7 0 do
    i 16 * dg-eov + dup 12 >= if dg-hh dg-cy + to fl-y  dg-vx fl-y dg-vx dg-vw + fl-y line else drop then
  loop
  8 0 do
    i 4 and if i 3 and 2* 1+ 8 * else i 3 and 2* 1+ 8 * negate then   \ x16: -8 -24 -40 -56, 8 24 40 56
    dup 12 dg-sx  dg-cy 64 +  2 pick 128 dg-sx  dg-cy 768 128 / +  line drop
  loop ;

: dg-draw-3d ( -- )
  dg-eo to dg-eov
  dg-backdrop
  4 begin dup  dg-eov 0> if -1 else 0 then  >= while  dup dg-layer 1-  repeat drop ;

( ---- The map and the panels ---- )
0 value mp-x   0 value mp-y   0 value mp-s   0 value mp-c
: dg-mapcol ( c -- rgb )
  case
    [char] # of $5a5a68 endof   [char] T of $70707e endof   [char] D of $a06a30 endof
    [char] L of $e0c040 endof   [char] < of $60a0e0 endof   [char] > of $e06060 endof
    [char] * of $40e0e0 endof
    [char] k of $a08830 endof   [char] h of $a08830 endof   [char] f of $a08830 endof
    [char] w of $a08830 endof   [char] a of $a08830 endof   [char] g of $a08830 endof
    drop $2e2e3a 0
  endcase ;
: dg-minimap ( x y s -- )
  to mp-s to mp-y to mp-x
  $0c0c12 color  mp-x mp-y dg-n mp-s * dup box
  dg-n 0 do dg-n 0 do
    i j dg-seen? if
      i j dg-t@ dg-mapcol color
      i mp-s * mp-x +  j mp-s * mp-y +  mp-s mp-s box
    then
  loop loop
  yellow color  dg-px mp-s * mp-x +  dg-py mp-s * mp-y +  mp-s mp-s box
  white color
  dg-px mp-s * mp-x + mp-s 2/ + dg-fx mp-s * 2/ +   dg-py mp-s * mp-y + mp-s 2/ + dg-fy mp-s * 2/ +   1 1 box ;

: dg-hud ( -- )
  $14141c color
  0 0 dg-w dg-vy box   0 dg-vy dg-vx dg-vh box
  dg-vx dg-vw + dg-vy dg-w dg-vx dg-vw + - dg-h dg-vy - box
  0 dg-vy dg-vh + dg-vx dg-vw + dg-h dg-vy dg-vh + - box
  $505060 color
  dg-vx 1- dg-vy 1- dg-vx dg-vw + dg-vy 1- line   dg-vx 1- dg-vy dg-vh + dg-vx dg-vw + dg-vy dg-vh + line
  dg-vx 1- dg-vy 1- dg-vx 1- dg-vy dg-vh + line   dg-vx dg-vw + dg-vy 1- dg-vx dg-vw + dg-vy dg-vh + line
  \ the panel on the right
  208 10 4 dg-minimap
  \ the compass
  dg-dir 0 = if yellow else $606070 then color  s" N" 300 12 dg-text
  dg-dir 3 = if yellow else $606070 then color  s" W" 294 20 dg-text
  dg-dir 1 = if yellow else $606070 then color  s" E" 306 20 dg-text
  dg-dir 2 = if yellow else $606070 then color  s" S" 300 28 dg-text
  $8090c0 color  s" DEPTH" 208 96 dg-text   dg-lvl 1+ 1 232 96 dg-num
  $8090c0 color  s" LV" 268 96 dg-text      dg-plv 2 282 96 dg-num
  $c0c0c0 color  s" HP" 208 104 dg-text
  dg-hp dg-mhp 230 104 48 dg-hp dg-mhp 3 / < if $e03030 else $30c030 then dg-bar
  $c0c0c0 color  dg-hp 3 282 104 dg-num
  s" FOOD" 208 112 dg-text
  dg-food 1000 230 112 48 $e08020 dg-bar
  $c0c0c0 color  s" XP" 208 120 dg-text
  dg-xp dg-plv 25 * 230 120 48 $4080e0 dg-bar
  $f0d020 color  s" KEY" 208 128 dg-text   dg-keys 1 224 128 dg-num
  $e06060 color  s" POT" 236 128 dg-text   dg-pots 1 252 128 dg-num
  $e08020 color  s" FOOD" 264 128 dg-text  dg-rats 1 284 128 dg-num
  $a0a0b0 color  s" ARMS" 208 138 dg-text   dg-weapon 232 138 dg-text
  s" ARMOR" 208 146 dg-text   dg-armor 232 146 dg-text
  $808090 color  s" SCORE" 208 156 dg-text   dg-score 6 232 156 dg-num
  s" BEST" 208 164 dg-text   dg-best 6 232 164 dg-num
  $505060 color
  dg-b-reset s" MOVE " dg-b+  0 dg-kn+ 2 dg-kn+ 1 dg-kn+ 3 dg-kn+  s"  TURN " dg-b+  4 dg-kn+ 5 dg-kn+
  dg-bf dg-bn 208 176 dg-text
  dg-b-reset s" HIT " dg-b+  6 dg-kn+  s"  POTION " dg-b+  7 dg-kn+   dg-bf dg-bn 208 184 dg-text
  dg-b-reset s" MAP " dg-b+  8 dg-kn+  s"  VIEW " dg-b+  9 dg-kn+  s"  K KEYS" dg-b+   dg-bf dg-bn 208 192 dg-text
  \ the messages
  $707080 color  dg-m2 dg-m2n 10 144 dg-text
  white color    dg-m1 dg-m1n 10 154 dg-text
  dg-hp dg-mhp 3 / < if $e03030 color s" LOW ON HEALTH" 10 170 dg-text then
  dg-food 100 < if $e08020 color s" YOU ARE HUNGRY" 10 178 dg-text then ;

: dg-banner ( a n rgb -- )   ( a word in the middle of the view, over a black bar )
  >r
  black color  dg-cx 90 -  dg-cy 12 -  180 34 box
  r> color  2 to dg-sc  dup 4 * dg-cx swap -  dg-cy 7 -  dg-text  1 to dg-sc ;

( ---- The roguelike view: the map from above, 8 pixels a square, you in the middle of the lines ---- )
0 value rg-oy   0 value rg-x   0 value rg-y   0 value rg-c   0 value rg-d
: dg-rtile ( c -- )   ( one square at rg-x rg-y )
  to rg-c
  rg-c [char] # = rg-c [char] T = or if
    $5a5a68 color rg-x rg-y 8 8 box   $3e3e4a color
    rg-x rg-y 3 + 8 1 box   rg-x rg-y 7 + 8 1 box   rg-x 3 + rg-y 3 + 1 4 box   rg-x 6 + rg-y 1 + 1 2 box
    rg-c [char] T = if $ff9020 color rg-x 3 + rg-y 2 + 2 3 box $ffe060 color rg-x 3 + rg-y 2 + 2 1 box then
    exit
  then
  rg-c [char] D = if $8a5a2a color rg-x rg-y 8 8 box  $503010 color rg-x 3 + rg-y 1 + 2 6 box exit then
  rg-c [char] L = if $9a7a2a color rg-x rg-y 8 8 box  $ffd040 color rg-x 3 + rg-y 3 + 3 3 box exit then
  rg-c [char] < = if $60a0e0 color rg-x rg-y 8 8 box  $304880 color rg-x rg-y 2 + 8 1 box rg-x rg-y 5 + 8 1 box exit then
  rg-c [char] > = if $401818 color rg-x rg-y 8 8 box  $e06060 color rg-x 1 + rg-y 2 + 6 1 box rg-x 2 + rg-y 5 + 4 1 box exit then
  $26262e color rg-x rg-y 8 8 box   $30303a color rg-x 3 + rg-y 3 + 1 1 box
  rg-c dg-item-id dup 0< 0= if rg-x rg-y 8 100 0 dg-spr else drop then ;
0 value rv-s
: dg-rvis? ( slot -- f )   ( shown in the roguelike view: near, on a square you have seen, on the rows drawn )
  to rv-s
  rv-s mo.y @ dg-n * rv-s mo.x @ + dg-d@ to rg-d
  rg-d 0< 0=  rg-d 7 <= and
  rv-s mo.x @ rv-s mo.y @ dg-seen? and
  rv-s mo.y @ rg-oy -  dup 0 >= swap 16 < and  and ;
: dg-draw-rogue ( -- )
  $06060a color dg-vx dg-vy dg-vw dg-vh box
  dg-py 8 - 0 max dg-n 16 - min to rg-oy
  16 0 do dg-n 0 do
    i j rg-oy + dg-seen? if
      i 8 * dg-vx + 16 +  to rg-x   j 8 * dg-vy + to rg-y
      i j rg-oy + dg-t@ dg-rtile
    then
  loop loop
  dg-m0 dg-mm + dg-m0 do
    i mo.on @ if i dg-rvis? if
      i mo.t @ 1-   i mo.x @ 8 * dg-vx + 16 +   i mo.y @ rg-oy - 8 * dg-vy +   8  100  i mo.fl @ 0>  dg-spr
    then then
  loop
  dg-px 8 * dg-vx + 16 +  to rg-x   dg-py rg-oy - 8 * dg-vy + to rg-y
  yellow color  rg-x 1 + rg-y 1 + 6 6 box
  white color   rg-x 3 + dg-fx 3 * +  rg-y 3 + dg-fy 3 * +  2 2 box ;

: dg-cfg-draw ( -- )
  black color dg-vx dg-vy dg-vw dg-vh box
  $ffff55 color  s" CONFIGURE THE KEYS" dg-vx 8 + dg-vy 5 + dg-text
  11 0 do
    i dg-cfg-i = if $ffff55 else $c0c0c0 then color
    i dg-cfg-i = if dg-vx 4 + dg-vy 21 + i 8 * + 3 3 box then
    i dg-aname  dg-vx 14 + dg-vy 20 + i 8 * +  dg-text
    dg-cfg? 2 = i dg-cfg-i = and if s" PRESS A KEY" else i dg-kb@ dg-keyname then
    dg-vx 110 + dg-vy 20 + i 8 * +  dg-text
  loop
  $8090c0 color
  s" ARROWS ALWAYS WORK" dg-vx 8 + dg-vy 108 + dg-text
  s" ENTER CHANGES  1 QWERTY  2 AZERTY" dg-vx 8 + dg-vy 116 + dg-text
  s" K OR ESC SAVES AND EXITS" dg-vx 8 + dg-vy 122 + dg-text ;

: dg-draw-all ( -- )
  black color cls
  dg-rogue? if dg-draw-rogue else dg-draw-3d then
  dg-pflash 0> if 255 0 0 100 rgba dg-vx dg-vy dg-vw dg-vh box then
  ticks dg-slash - 130 < dg-rogue? 0= and if
    white color
    dg-cx 50 - dg-cy 40 - dg-cx 50 + dg-cy 40 + line
    dg-cx 49 - dg-cy 40 - dg-cx 51 + dg-cy 40 + line
  then
  dg-map? if
    $06060a color  dg-vx dg-vy dg-vw dg-vh box
    dg-cx 60 - dg-cy 60 - 6 dg-minimap
  then
  dg-cfg? if dg-cfg-draw then
  dg-hud
  dg-state 1 = if s" PAUSE" yellow dg-banner then
  dg-state 2 = if s" YOU DIED" $ff5555 dg-banner then
  dg-state 3 = if s" AMULET FOUND" $55ff55 dg-banner then
  dg-state 2 >= if
    $c0c0c0 color  s" SPACE TO PLAY AGAIN" dg-cx 38 - dg-cy 36 + dg-text
  then
  flip ;

( ---- The loop ---- )
0 value dg-last
: dg-run-steps ( -- )
  dg-state 1 = dg-cfg? 0<> or if ticks to dg-last exit then
  ticks dg-last - 400 > if ticks 50 - to dg-last then
  begin ticks dg-last - 50 >= while  50 +to dg-last  dg-step  repeat ;
: dg-read-keys ( -- )
  key-esc dg-just to dg-esc
  dg-cfg? if dg-cfg-keys exit then
  [char] k dg-just if 1 to dg-cfg? 0 to dg-cfg-i exit then
  10 dg-act-just? if dg-state 0= if 1 to dg-state else dg-state 1 = if 0 to dg-state then then then
  9 dg-act-just? if dg-toggle-view then
  dg-state 0= if dg-input then
  \ the three keys are read every frame, so that space held down while you die does not restart at once
  [char] r dg-just key-space dg-just or key-enter dg-just or
  dg-state 2 >= and  ticks dg-tend - 800 > and if dg-new-game then ;
: dg-one-frame ( -- )
  events
  dg-read-keys
  dg-esc if dg-embed if -1 to dg-leave else quit! then then
  dg-run-steps dg-draw-all
  dt 33 < if 33 dt - delay then ;
: dg-run ( -- )   begin dg-one-frame quit? until close-screen ;
: dg-play ( -- )   ( the loop without closing the screen, for the level editor )
  0 to dg-leave  begin dg-one-frame quit? dg-leave or until ;

[defined] dungeon-no-run [if] [else]
3 to zoom
dg-w dg-h screen
s" Dungeon" title
dg-load-keys
dg-new-game
ticks to dg-last
dg-run
script-bye
[then]
