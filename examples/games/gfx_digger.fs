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

\ A Digger-like game with SDL2 (the 1983 digging game for the PC), written
\ from the rules as they are widely described; the levels are drawn by chance
\ and the pictures are our own boxes. Only libSDL2 is needed (no fonts, images
\ or sound files); the letters are a tiny 3x5 pixel font and the sounds are
\ beeps. One file, so that it runs the same on POSIX and the web
\ (terminal.html?run=gfx_digger.fs).
\   arrows or wasd   dig and walk       space or f      fire (it takes a while to load again)
\   p                pause              esc             quit
\   space or enter   after the end: again
\ Dig through the earth to eat the emeralds (25; eight in a row 250) and the gold
\ of the bags that break (500). Dig under a bag and it falls: one cell and it
\ stays, more and it breaks and kills what it hits. You can push a bag along a
\ tunnel. Nobbins come out of the top right corner and follow your tunnels; after
\ a while they turn into hobbins, which dig their own way. Shoot them with the
\ fire ball, drop a bag on them (250 each) or eat the cherry that comes now and
\ then: for a while you eat the monsters (200, 400, 800...). Level done when all
\ the emeralds are eaten or all the monsters are dead. A free life at 20000.
\ All the positions are in pixels of the field, 2 pixels a step, a step every
\ 16 ms whatever the speed of the screen.

gfx
\ on the web the page has loaded both files first (?run=gfx_spr.fs,gfx_digger_sprites.fs,gfx_digger.fs)
[undefined] spr-draw [if] include ../graphics/gfx_spr.fs [then]
[undefined] emerald [if] include gfx_digger_sprites.fs [then]

( ---- The field: 15 x 10 cells of 20 x 18 pixels, the earth in sub-cells of half a cell ---- )
15 constant dg-cols   10 constant dg-rows
20 constant dg-cw     18 constant dg-ch
10 constant dg-sw      9 constant dg-sh
30 constant dg-scols  20 constant dg-srows
10 constant dg-ox     20 constant dg-oy      \ top left of the field on the screen
300 constant dg-fw   180 constant dg-fh
320 constant dg-w    200 constant dg-h
6 constant dg-maxmon  10 constant dg-maxbag

create dg-dirt dg-scols dg-srows * allot     \ 1 where there is earth
create dg-item dg-cols dg-rows * allot       \ 0 nothing, 1 emerald, 2 gold, 3 cherry
create dg-dist dg-cols dg-rows * allot       \ steps to the digger along the tunnels
create dg-queue dg-cols dg-rows * allot

: arr ( "name" -- )   create dg-maxbag cells allot  does> ( i -- a ) swap cells + ;
arr bag.st    \ 0 none, 1 rests, 2 wobbles, 3 falls
arr bag.x   arr bag.y   arr bag.t   arr bag.n
arr mon.t     \ 0 none, 1 nobbin, 2 hobbin
arr mon.x   arr mon.y   arr mon.d   arr mon.a   arr mon.m

\ directions: 0 right, 1 down, 2 left, 3 up
create dg-dx  1 , 0 , -1 , 0 ,
create dg-dy  0 , 1 , 0 , -1 ,
: dx@ ( d -- n )   cells dg-dx + @ ;
: dy@ ( d -- n )   cells dg-dy + @ ;
: opposite ( d -- d' )   2 + 3 and ;

0 value dg-state     \ 0 playing, 1 paused, 2 over, 3 digger dying, 4 level done
0 value dg-timer
0 value dg-score   0 value dg-best   3 value dg-lives   1 value dg-level   0 value dg-next-life
0 value dg-gx   0 value dg-gy   0 value dg-gd   -1 value dg-push
0 value dg-left   0 value dg-streak   0 value dg-streak-t
0 value dg-bonus   0 value dg-chain
0 value dg-fon   0 value dg-fx   0 value dg-fy   0 value dg-fd   0 value dg-fire-t
0 value dg-spawned   0 value dg-quota   0 value dg-spawn-t   0 value dg-cherry
0 value dg-frame

( ---- The earth ---- )
: dirt-at ( sx sy -- a )   dg-scols * + dg-dirt + ;
: dirt? ( sx sy -- f )   ( outside the field counts as earth )
  over 0 dg-scols within 0= if 2drop -1 exit then
  dup 0 dg-srows within 0= if 2drop -1 exit then
  dirt-at c@ 0<> ;

0 value rx0   0 value rx1   0 value ry0   0 value ry1
: clear-rect { x y w h }   ( takes the earth away from every sub-cell the rectangle touches )
  x 0 max dg-sw / to rx0   x w + 1- dg-sw / dg-scols 1- min to rx1
  y 0 max dg-sh / to ry0   y h + 1- dg-sh / dg-srows 1- min to ry1
  rx0 rx1 > ry0 ry1 > or if exit then
  ry1 1+ ry0 do rx1 1+ rx0 do  0 i j dirt-at c!  loop loop ;

: item-at ( cx cy -- a )   dg-cols * + dg-item + ;
: dist-at ( cx cy -- a )   dg-cols * + dg-dist + ;
: in-field? ( cx cy -- f )   0 dg-rows within swap 0 dg-cols within and ;
: aligned? ( x y -- f )   dg-ch mod 0=  swap dg-cw mod 0=  and ;
: cell-clear? { cx cy }   ( the four sub-cells of the cell are free of earth )
  cx 2* cy 2* dirt? 0=   cx 2* 1+ cy 2* dirt? 0= and
  cx 2* cy 2* 1+ dirt? 0= and   cx 2* 1+ cy 2* 1+ dirt? 0= and ;
: step-cell ( cx cy d -- nx ny )   >r r@ dy@ + swap r> dx@ + swap ;
: link? { cx cy d }   ( a walk from the cell to the next one in the direction d, along tunnels )
  cx cy cell-clear?  cx cy d step-cell cell-clear? and ;

( ---- Bags ---- )
: bag-cell { cx cy -- i }   ( the bag that rests or wobbles in the cell, or -1 )
  -1
  dg-maxbag 0 do
    i bag.st @ 1 3 within
    i bag.x @ cx dg-cw * = and
    i bag.y @ cy dg-ch * = and if drop i leave then
  loop ;
: can-drop? { cx cy -- f }   ( the cell under is free for a bag )
  cy 1+ dg-rows < 0= if 0 exit then
  cx cy 1+ cell-clear? 0= if 0 exit then
  cx cy 1+ bag-cell 0< ;

( ---- Scoring and sounds ---- )
: score+ ( n -- )
  +to dg-score
  dg-score dg-next-life >= if 1 +to dg-lives  20000 +to dg-next-life  523 80 beep  784 120 beep then ;

( ---- Making a level ---- )
0 value ccx   0 value ccy
: free-for-item? ( cx cy -- f )   ( earth, no item, no bag )
  2dup cell-clear? if 2drop 0 exit then
  2dup item-at c@ if 2drop 0 exit then
  bag-cell 0< ;
: cluster ( -- )   ( a few emeralds in a bunch )
  dg-cols random to ccx   7 random 2 + to ccy
  3 random 3 + 0 do
    ccx ccy free-for-item? if 1 ccx ccy item-at c!  1 +to dg-left then
    2 random if 1 +to ccx else 1 +to ccy then
    ccx dg-cols 1- min to ccx   ccy dg-rows 1- min to ccy
  loop ;
: place-bags ( n -- )
  0 do
    begin  dg-cols random to ccx   6 random 1+ to ccy   ccx ccy free-for-item?  until
    1 i bag.st !   ccx dg-cw * i bag.x !   ccy dg-ch * i bag.y !   0 i bag.t !   0 i bag.n !
  loop ;

: start-digger ( -- )
  7 dg-cw * to dg-gx   9 dg-ch * to dg-gy   0 to dg-gd   -1 to dg-push
  0 to dg-fon   0 to dg-bonus   0 to dg-streak
  dg-maxmon 0 do 0 i mon.t ! loop
  120 to dg-spawn-t ;

: new-level ( -- )
  dg-dirt dg-scols dg-srows * 1 fill
  dg-item dg-cols dg-rows * erase
  dg-maxbag 0 do 0 i bag.st ! loop
  0 0 dg-fw dg-ch clear-rect                              \ the corridor along the top
  6 dg-cw *  9 dg-ch *  3 dg-cw *  dg-ch clear-rect       \ where the digger starts
  0 to dg-left
  8 0 do cluster loop
  4 dg-level 2/ + 8 min place-bags
  dg-level 4 + 12 min to dg-quota   0 to dg-spawned   0 to dg-cherry   0 to dg-fire-t
  start-digger ;

: new-game ( -- )
  0 to dg-score   3 to dg-lives   1 to dg-level   20000 to dg-next-life
  new-level  0 to dg-state ;

: next-level ( -- )   1 +to dg-level  new-level  0 to dg-state ;

( ---- Keys ---- )
create dg-was 256 allot   dg-was 256 erase
: just ( k -- f )   ( pressed now, not at the last look )
  dup pressed? swap 255 and dg-was + 2dup c@ 0= and >r c! r> ;

: key-dir ( -- d )   ( the direction asked for, or -1 )
  key-right pressed? [char] d pressed? or if 0 exit then
  key-down pressed? [char] s pressed? or if 1 exit then
  key-left pressed? [char] a pressed? or if 2 exit then
  key-up pressed? [char] w pressed? or if 3 exit then
  -1 ;

( ---- The digger ---- )
: dg-cx ( -- cx )   dg-gx 10 + dg-cw / ;
: dg-cy ( -- cy )   dg-gy 9 + dg-ch / ;
0 value ex   0 value ey
: can-enter? ( -- f )   ( at a cell: may the digger set off to dg-gd? a bag on the way is pushed )
  dg-gx dg-cw /  dg-gy dg-ch /  dg-gd step-cell to ey to ex
  ex ey in-field? 0= if 0 exit then
  ex ey bag-cell dup 0< if drop -1 exit then
  dg-gd 1 and if drop 0 exit then                         \ a bag does not go up or down
  dup bag.st @ 1 = 0= if drop 0 exit then                 \ one that wobbles stays
  ex dg-gd dx@ + ey cell-clear? 0= if drop 0 exit then    \ it needs a tunnel to go on
  ex dg-gd dx@ + ey bag-cell 0< 0= if drop 0 exit then
  to dg-push -1 ;

: eat ( -- )   ( what is in the cell where the digger is )
  dg-cx dg-cy item-at dup c@ ( a v )
  dup 1 = if
    drop 0 swap c!  -1 +to dg-left  25 score+  880 30 beep
    1 +to dg-streak  70 to dg-streak-t
    dg-streak 8 = if 250 score+ 0 to dg-streak  1320 100 beep then exit
  then
  dup 2 = if drop 0 swap c!  500 score+  1100 60 beep 1480 100 beep exit then
  3 = if 0 swap c!  100 score+  625 to dg-bonus  0 to dg-chain  660 80 beep 880 80 beep 1100 100 beep exit then
  drop ;

: walk-to ( kd -- )   ( one step toward kd, or on in the direction already taken; -1: no key )
  dg-gx dg-gy aligned? if
    dup 0< if drop exit then
    to dg-gd
    can-enter? 0= if exit then
  else
    dup -1 > over dg-gd opposite = and dg-push 0< and if to dg-gd else drop then
  then
  dg-gd dx@ 2 * +to dg-gx   dg-gd dy@ 2 * +to dg-gy
  dg-push -1 > if
    dg-gd dx@ 2 * dg-push bag.x +!
    dg-gx dg-gy aligned? if -1 to dg-push then
  then
  dg-gx 2 +  dg-gy 2 +  16 14 clear-rect
  eat ;
: walk ( -- )   key-dir walk-to ;

( ---- The fire ball ---- )
: under-bag? ( -- f )   ( facing up with a bag right above )
  dg-gd 3 =  dg-gx dg-gy aligned? and
  dg-cy 0> and  dg-cx dg-cy 1- bag-cell 0< 0= and ;
: fire! ( -- )   ( set the ball off, if it is loaded )
  dg-fon 0= dg-fire-t 0= and 0= if exit then
  1 to dg-fon   under-bag? if 1 else dg-gd then to dg-fd
  dg-gx 10 + dg-fd dx@ 10 * + to dg-fx
  dg-gy 9 + dg-fd dy@ 9 * + to dg-fy
  300 to dg-fire-t   330 50 beep ;
: fire-input ( -- )   key-space pressed? [char] f pressed? or if fire! then ;

: mon-die ( i pts -- )   ( the monster is dead )
  score+  0 swap mon.t !  200 60 beep ;

: fire-hit ( -- )
  dg-maxmon 0 do
    i mon.t @ if
      i mon.x @ 10 + dg-fx - abs 10 <   i mon.y @ 9 + dg-fy - abs 9 < and if
        i 250 mon-die  0 to dg-fon  leave
      then
    then
  loop ;

: fire-step ( -- )
  dg-fon 0= if exit then
  2 0 do
    dg-fon 0= if leave then
    2 dg-fd dx@ * +to dg-fx   2 dg-fd dy@ * +to dg-fy
    dg-fx 0<  dg-fx dg-fw >= or  dg-fy 0< or  dg-fy dg-fh >= or if 0 to dg-fon leave then
    dg-fx dg-sw /  dg-fy dg-sh /  dirt? if 0 to dg-fon leave then
    fire-hit
  loop ;

( ---- The bags ---- )
0 value bi
: bag-cx ( -- cx )   bi bag.x @ dg-cw / ;
: bag-cy ( -- cy )   bi bag.y @ dg-ch / ;
: bag-rest ( -- )   ( it stands on earth, or it begins to wobble )
  bag-cx bag-cy can-drop? if 2 bi bag.st !  36 bi bag.t ! then ;
: held? ( -- f )   ( the digger stands right under the bag and pushes up: it props it )
  key-dir 3 =  dg-gx dg-gy aligned? and
  dg-cx bag-cx =  dg-cy bag-cy 1+ = and  and ;
: bag-wobble ( -- )
  held? if 10 bi bag.t ! exit then                         \ propped: it shakes on, and keeps a delay
  -1 bi bag.t +!  bi bag.t @ 0> if exit then
  bag-cx bag-cy can-drop? 0= if 1 bi bag.st ! exit then
  3 bi bag.st !  0 bi bag.n ! ;
: bag-fall ( -- )
  3 bi bag.y +!
  bi bag.y @ dg-ch mod 0<> if exit then
  1 bi bag.n +!
  bag-cx bag-cy can-drop? if exit then
  bi bag.n @ 2 >= if                                       \ more than one cell: it breaks
    2 bag-cx bag-cy item-at c!  0 bi bag.st !  150 90 beep
  else 1 bi bag.st ! then ;

: bags-update ( -- )
  dg-maxbag 0 do
    i to bi
    bi dg-push <> if
      bi bag.st @ case
        1 of bag-rest endof
        2 of bag-wobble endof
        3 of bag-fall endof
      endcase
    then
  loop ;

( ---- The monsters ---- )
: mon-cx ( i -- cx )   mon.x @ dg-cw / ;
: mon-cy ( i -- cy )   mon.y @ dg-ch / ;

0 value qh   0 value qt
: q+ ( idx -- )   dg-queue qt + c!  1 +to qt ;
: bfs-expand { cx cy }
  4 0 do
    cx cy i link? if
      cx cy i step-cell
      2dup bag-cell 0< if
        2dup dist-at c@ 255 = if
          cx cy dist-at c@ 1+ >r
          2dup dist-at r> swap c!
          dg-cols * + q+
        else 2drop then
      else 2drop then
    then
  loop ;
: bfs ( -- )   ( the distance to the digger along the tunnels )
  dg-dist dg-cols dg-rows * 255 fill
  0 dg-cx dg-cy dist-at c!
  dg-cx dg-cy dg-cols * + dg-queue c!   1 to qt   0 to qh
  begin qh qt < while
    dg-queue qh + c@ dup dg-cols mod swap dg-cols /  bfs-expand
    1 +to qh
  repeat ;

: ok-dir? ( cx cy d -- f )   ( inside the field and no bag in the next cell )
  step-cell 2dup in-field? if bag-cell 0< else 2drop 0 then ;

: open-dir? { cx cy d -- f }   ( along a tunnel, and no bag in the next cell )
  cx cy d link? if cx cy d step-cell bag-cell 0< else 0 then ;

0 value nbest   0 value nbv
: nob-dir { cx cy cur -- d }   ( a nobbin at a cell: along the tunnels, toward the digger )
  -1 to nbest   999 to nbv
  4 0 do
    i cur opposite <>  cx cy i open-dir? and if
      cx cy i step-cell dist-at c@  dup nbv < if to nbv i to nbest else drop then
    then
  loop
  nbest 0< if
    cx cy cur opposite open-dir? if cur opposite exit then
    -1 exit
  then
  nbv 255 = if                                             \ the digger is out of reach: wander
    4 random 4 0 do
      dup i + 3 and  dup cur opposite <>  cx cy 3 pick open-dir? and if nip unloop exit then drop
    loop drop
  then
  nbest ;

: sgn ( n -- -1|0|1 )   dup 0< if drop -1 exit then  0> 1 and ;
: dir-of ( sx sy -- d )   ( the direction of a sign pair, or -1 )
  over 0> if 2drop 0 exit then
  over 0< if 2drop 2 exit then
  dup 0> if 2drop 1 exit then
  0< if drop 3 exit then
  drop -1 ;
0 value hx   0 value hy   0 value hd1   0 value hd2
: hob-dir { cx cy cur -- d }   ( a hobbin: straight toward the digger, digging )
  dg-cx cx - to hx   dg-cy cy - to hy
  hx abs hy abs < if
    0 hy sgn dir-of to hd1   hx sgn 0 dir-of to hd2
  else
    hx sgn 0 dir-of to hd1   0 hy sgn dir-of to hd2
  then
  hd1 -1 > if cx cy hd1 ok-dir? if hd1 exit then then
  hd2 -1 > if cx cy hd2 ok-dir? if hd2 exit then then
  4 0 do cx cy i ok-dir?  i cur opposite <> and if i unloop exit then loop
  cur ;

0 value mi
: morph ( -- )
  mi mon.t @ 1 = if
    2 mi mon.t !   400 random 400 + mi mon.m !
  else
    1 mi mon.t !   500 random 500 + mi mon.m !
  then ;

: mon-speed ( -- n )   ( quarters of the speed of the digger )
  dg-level 2/ 2 + 4 min   mi mon.t @ 2 = if 1+ 4 min then ;

: mon-step ( i -- )
  to mi
  mi mon.t @ 0= if exit then
  -1 mi mon.m +!  mi mon.m @ 0= if morph then
  mi mon.a @ mon-speed + dup 4 >= if 4 - mi mon.a ! else mi mon.a ! exit then
  mi mon.x @ mi mon.y @ aligned? if
    mi mon-cx mi mon-cy mi mon.d @
    mi mon.t @ 1 = if nob-dir else hob-dir then
    dup 0< if drop exit then
    mi mon.d !
  then
  mi mon.d @ dx@ 2 * mi mon.x +!   mi mon.d @ dy@ 2 * mi mon.y +!
  mi mon.t @ 2 = if mi mon.x @ 2 + mi mon.y @ 2 +  16 14 clear-rect then ;

0 value mn
: mon-free ( -- i )   ( a free slot, or -1 )
  -1  dg-maxmon 0 do i mon.t @ 0= if drop i leave then loop ;
: mon-count ( -- n )   0 dg-maxmon 0 do i mon.t @ 0<> 1 and + loop ;

: spawn ( -- )
  dg-spawned dg-quota < 0= if exit then
  -1 +to dg-spawn-t  dg-spawn-t 0> if exit then
  mon-count dg-level 2 + 5 min >= if 20 to dg-spawn-t exit then
  mon-free dup 0< if drop exit then to mn
  1 mn mon.t !  14 dg-cw * mn mon.x !  0 mn mon.y !  2 mn mon.d !  0 mn mon.a !
  500 random 400 + mn mon.m !
  1 +to dg-spawned
  220 dg-level 15 * - 100 max to dg-spawn-t
  dg-spawned 3 = dg-cherry 0= and if                       \ a cherry in the top corridor
    1 to dg-cherry   3  9 random 3 +  0 item-at c!
  then ;

: monsters-update ( -- )
  bfs
  dg-maxmon 0 do i mon-step loop
  spawn ;

( ---- Death, collisions ---- )
: kill-digger ( -- )
  dg-state 0 = 0= if exit then
  3 to dg-state   100 to dg-timer   -1 +to dg-lives   0 to dg-fon
  392 150 beep  330 150 beep  262 300 beep ;

: hit? { ax ay bx by -- f }
  ax bx - abs 14 <   ay by - abs 12 < and ;

: bag-crush ( b -- )   ( a falling bag: the digger or a monster under it )
  dup bag.x @ over bag.y @ dg-gx dg-gy hit? if kill-digger then
  dg-maxmon 0 do
    i mon.t @ if
      dup bag.x @ over bag.y @ i mon.x @ i mon.y @ hit? if i 250 mon-die then
    then
  loop drop ;

: monsters-vs-digger ( -- )
  dg-maxmon 0 do
    i mon.t @ if
      i mon.x @ i mon.y @ dg-gx dg-gy hit? if
        dg-bonus 0> if
          dg-chain 1+ 4 min to dg-chain
          i  100 dg-chain 1 lshift *  mon-die
        else kill-digger then
      then
    then
  loop ;

: collisions ( -- )
  dg-maxbag 0 do i bag.st @ 3 = if i bag-crush then loop
  dg-state 0 = if monsters-vs-digger then ;

( ---- One step of the game ---- )
: timers ( -- )
  dg-fire-t 0> if -1 +to dg-fire-t then
  dg-bonus 0> if -1 +to dg-bonus then
  dg-streak-t 0> if -1 +to dg-streak-t  dg-streak-t 0= if 0 to dg-streak then then ;

: all-done? ( -- f )
  dg-left 0= if -1 exit then
  dg-spawned dg-quota >=  mon-count 0=  and ;

: after-death ( -- )
  dg-lives 0= if 2 to dg-state  dg-score dg-best max to dg-best exit then
  start-digger  0 to dg-state ;

: playing ( -- )
  walk fire-input fire-step bags-update monsters-update collisions timers
  dg-state 0 = if
    all-done? if 4 to dg-state  120 to dg-timer  523 80 beep 659 80 beep 784 80 beep 1046 200 beep then
  then ;

: dg-step ( -- )
  1 +to dg-frame
  [char] p just if
    dg-state 0 = if 1 to dg-state else dg-state 1 = if 0 to dg-state then then
  then
  dg-state case
    0 of playing endof
    3 of bags-update  -1 +to dg-timer  dg-timer 0= if after-death then endof
    4 of -1 +to dg-timer  dg-timer 0= if next-level then endof
    2 of [char] r just key-space just or key-enter just or if new-game then endof
  endcase ;

( ---- Drawing: a tiny 3x5 pixel font ---- )
create dg-digits
  $7b6f , $2c97 , $73e7 , $73cf , $5bc9 , $79cf , $79ef , $7249 , $7bef , $7bcf ,
create dg-font   ( A to Z )
  $2bed , $6bae , $3923 , $6b6e , $79a7 , $79a4 , $396b , $5bed , $7497 , $126a , $5bad , $4927 , $5fed ,
  $6b6d , $2b6a , $6ba4 , $2b7b , $6bad , $388e , $7492 , $5b6f , $5b6a , $5bfd , $5aad , $5a92 , $72a7 ,

: draw-bits { bits x y s }
  15 0 do
    bits 14 i - rshift 1 and if
      x i 3 mod s * +   y i 3 / s * +   s s box
    then
  loop ;

: draw-number { n digits x y s }   ( right aligned in digits places )
  digits 0 do
    n 10 mod cells dg-digits + @   x digits 1- i - s 4 * * +   y s draw-bits
    n 10 / to n
  loop ;

: glyph ( c -- bits )
  32 or dup 97 < over 122 > or if drop 0 exit then
  97 - cells dg-font + @ ;

: draw-text { a n x y s }
  n 0 do  a i + c@ glyph   x i 4 s * * +   y s draw-bits  loop ;

: banner { a n rgb }   ( a word in the middle, over a black bar )
  black color   dg-w 2 / 60 -  dg-h 2 / 8 -  120 16 box
  rgb color  a n  dg-w n 8 * 2 - - 2 /  dg-h 2 / 5 -  2 draw-text ;

( ---- Drawing: the field and the characters ---- )
: draw-earth ( -- )
  black color  dg-ox dg-oy dg-fw dg-fh box
  $9a4a1c color
  dg-srows 0 do dg-scols 0 do
    i j dirt-at c@ if  i dg-sw * dg-ox +  j dg-sh * dg-oy +  dg-sw dg-sh box  then
  loop loop
  $c4742a color
  dg-srows 0 do dg-scols 0 do
    i j dirt-at c@ if
      i 5 * j 11 * + i j * + 7 mod  dup 3 < if
        dup 3 * 1+ i dg-sw * + dg-ox +   swap 2* 2 + j dg-sh * + dg-oy +  2 2 box
      else drop then
    then
  loop loop ;

: draw-emerald { x y }   emerald x y spr-draw ;
: draw-gold { x y }   gold x y spr-draw ;
: draw-cherry { x y }   cherry x y spr-draw ;

: draw-item { v x y }
  v 1 = if x y draw-emerald then
  v 2 = if x y draw-gold then
  v 3 = if x y draw-cherry then ;

: draw-items ( -- )
  dg-rows 0 do dg-cols 0 do
    i j item-at c@  i dg-cw * dg-ox +  j dg-ch * dg-oy +  draw-item
  loop loop ;

: draw-bag-at { x y }   bag x y spr-draw ;
: wob ( b -- dx )   bag.st @ 2 = if dg-frame 3 and 2 < if 1 else -1 then else 0 then ;
: draw-bag ( b -- )   dup wob over bag.x @ + dg-ox +   swap bag.y @ dg-oy +   draw-bag-at ;
: draw-bags ( -- )   dg-maxbag 0 do i bag.st @ if i draw-bag then loop ;

: draw-mon-at { x y t }   t 1 = if nobbin else hobbin then x y spr-draw ;
: draw-mon ( i -- )   dup mon.x @ dg-ox +  over mon.y @ dg-oy +  rot mon.t @  draw-mon-at ;
: draw-mons ( -- )   dg-maxmon 0 do i mon.t @ if i draw-mon then loop ;

: body-color ( -- rgb )   ( red, or flashing while the digger eats monsters )
  dg-bonus 0= if $e02020 exit then
  dg-frame 4 / 1 and if yellow else $e02020 then ;

: draw-digger-at { x y }
  dg-state 3 = if
    dg-timer 6 / 1 and if white else red then
    [char] R spr-pal   cross x y spr-draw
    exit
  then
  body-color [char] R spr-pal
  dg-gd case
    0 of digger-r endof
    1 of digger-d endof
    2 of digger-l endof
    digger-u swap
  endcase
  x y spr-draw ;
: draw-digger ( -- )   dg-gx dg-ox +  dg-gy dg-oy +  draw-digger-at ;

: draw-fire ( -- )
  dg-fon 0= if exit then
  $ff8000 color  dg-fx dg-ox + 3 -  dg-fy dg-oy + 3 -  6 6 box
  yellow color   dg-fx dg-ox + 1 -  dg-fy dg-oy + 1 -  3 3 box ;

: draw-hud ( -- )
  white color  dg-score 6 6 6 2 draw-number
  $808080 color  s" LV" 62 6 2 draw-text   dg-level 2 80 6 2 draw-number
  red color  dg-lives 5 min 0 ?do  108 i 12 * +  6  8 8 box  loop
  $808080 color  dg-best 6 dg-w 56 - 6 2 draw-number
  $503010 color  190 8  32 4 box
  dg-fire-t 0= if $ff8000 color 190 8 32 4 box
  else $c06000 color  190 8  300 dg-fire-t - 32 * 300 /  4 box then ;

: draw-all ( -- )
  $101830 color  0 0 dg-w dg-h box
  draw-earth draw-items draw-bags draw-mons draw-digger draw-fire draw-hud
  dg-state 1 = if s" PAUSE" yellow banner then
  dg-state 2 = if red color 70 alpha 0 0 dg-w dg-h box  s" GAME OVER" white banner then
  dg-state 4 = if s" LEVEL DONE" yellow banner then
  flip ;

( ---- The loop ---- )
0 value dg-last
: run-steps ( -- )
  dg-state 1 = if ticks to dg-last then
  ticks dg-last - 160 > if ticks 16 - to dg-last then      \ a slow screen: the game slows down, it does not jump
  begin ticks dg-last - 16 >= while  16 +to dg-last  dg-step  repeat ;

: one-frame ( -- )
  events
  key-esc pressed? if quit! then
  run-steps draw-all
  dt 16 < if 16 dt - delay then ;

: run ( -- )   begin one-frame quit? until close-screen ;

[defined] digger-no-run [if] [else]
ms-ticks 1 or rand-seed !
3 to zoom
dg-w dg-h screen
s" Digger" title
new-game
ticks to dg-last
run
script-bye
[then]
