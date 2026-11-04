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

\ A Lode Runner-like game with SDL2 (the 1983 platform game: gold, ladders,
\ ropes, guards, and holes dug in the bricks), written from the rules as they
\ are widely described; the levels are our own. Only libSDL2 is needed (no
\ fonts, images or sound files); the pictures are sprites made with
\ examples/graphics/gfx_spr.fs (gfx_loderunner_sprites.fs, edit them with
\ gfx_sprited.fs), the letters a tiny 3x5 pixel font, the sounds beeps. On the
\ web (terminal.html?run=gfx_spr.fs,gfx_loderunner_sprites.fs,gfx_loderunner.fs).
\   arrows or wasd   run, climb, hang on the ropes
\   z or q           dig the brick on the left (down)    x or e   on the right
\   p                pause    k  give up a life (stuck)   esc      quit
\   space or enter   after the end: again
\ Take all the gold, the ladders to the top show, climb out. A hole in the
\ bricks closes again: a guard in it is stuck for a while, then killed.

sdl2
\ on the web the page has loaded both files first
[undefined] spr-draw [if] include ../graphics/gfx_spr.fs [then]
[undefined] lr-brick [if] include gfx_loderunner_sprites.fs [then]

( ---- The field: 28 x 16 tiles of 10 x 10 pixels ---- )
28 constant lr-cols   16 constant lr-rows   10 constant lr-t
20 constant lr-ox   30 constant lr-oy       \ top left of the field on the screen
320 constant lr-w   200 constant lr-h
6 constant lr-maxg   16 constant lr-maxh   7 constant lr-ne   \ entity 0 is the runner, 1 to 6 the guards
: lr-arr ( n "name" -- )   create  cells here over allot swap erase  does> ( i -- a ) swap cells + ;
create lr-first lr-cols lr-rows * allot       \ for the guards: first step toward the runner
create lr-queue lr-cols lr-rows * cells allot
create lr-map lr-cols lr-rows * allot
lr-ne lr-arr le.on   lr-ne lr-arr le.x   lr-ne lr-arr le.y   lr-ne lr-arr le.f   lr-ne lr-arr le.d
lr-ne lr-arr le.s   lr-ne lr-arr le.t   lr-ne lr-arr le.c   lr-ne lr-arr le.v   lr-ne lr-arr le.a   lr-ne lr-arr le.m
lr-maxh lr-arr lh.on   lr-maxh lr-arr lh.x   lr-maxh lr-arr lh.y   lr-maxh lr-arr lh.t

\ ---- Levels: 16 rows of 28 characters each ----
\ space nothing, # brick (can be dug), @ solid, H ladder, - rope, $ gold,
\ S a ladder that shows when all the gold is taken, & the runner, 0 a guard
5 constant lr-nlev   lr-cols lr-rows * constant lr-lsize
create lr-levels lr-nlev lr-lsize * allot
0 value lr-lp
: lr-row ( a n -- )   ( the next row, 28 characters; s" may use the space after here, so no allot )
  drop  lr-levels lr-lp + lr-cols cmove  lr-cols +to lr-lp ;
\ level 1
s"               S             " lr-row
s"               S             " lr-row
s"               S             " lr-row
s"      H   $    S   $   H   0 " lr-row
s" #####H################H#####" lr-row
s"      H############### H     " lr-row
s"      H                H     " lr-row
s"  0$  H       H   H    H  $  " lr-row
s" #############H###H##########" lr-row
s"  ########### H   H          " lr-row
s"              H   H          " lr-row
s"    H  $   $  H   H      H $ " lr-row
s" ###H####################H###" lr-row
s"    H ################## H   " lr-row
s"    H    $ $ &      $    H   " lr-row
s" @@@@@@@@@@@@@@@@@@@@@@@@@@@@" lr-row
\ level 2
s"                     S       " lr-row
s"                     S       " lr-row
s"                     S       " lr-row
s"   H   $     -$--    S   0H  " lr-row
s" ##H#########    #########H##" lr-row
s"   H########              H  " lr-row
s"   H                      H  " lr-row
s"   H  H   ----$--$-- 0 H  H $" lr-row
s" #####H###          ###H#####" lr-row
s" #####H###             H     " lr-row
s"      H                H     " lr-row
s"   0H H  --$--  H      H $   " lr-row
s" ###H####     ##H############" lr-row
s"    H         ##H########### " lr-row
s"    H  $   &    H    $     $ " lr-row
s" ############################" lr-row
\ level 3
s"             S               " lr-row
s"             S               " lr-row
s"             S               " lr-row
s"  H $ 0  H   S  $   H  0   $ " lr-row
s" #H######H##########H########" lr-row
s"  H$     H          H        " lr-row
s" #H##### H          H        " lr-row
s"  H  H   H $  -$-   H  $  H 0" lr-row
s" ####H########   #########H##" lr-row
s"     H               ####$H##" lr-row
s"     H                ####H##" lr-row
s"     H    H $    0   H  $ H  " lr-row
s" #########H##########H#######" lr-row
s" #########H########  H       " lr-row
s"    $     H   $& $   H     $ " lr-row
s" @@@@@@@@@@@@@@@@@@@@@@@@@@@@" lr-row
\ level 4
s"               S             " lr-row
s"               S             " lr-row
s"               S             " lr-row
s"  H0 --$---H $ S  H--$---  0 " lr-row
s" #H##      H######H      ##H#" lr-row
s"  H        H######H        H " lr-row
s"  H        H      H        H " lr-row
s"  H--$-- H H --$--H H --$0-HH" lr-row
s" ##     #H###     ##H#     #H" lr-row
s" ##      H          H       H" lr-row
s"         H          H       H" lr-row
s"   H$  --H-0-$H  --$H--  H$ H" lr-row
s" ##H###      #H##      ##H###" lr-row
s"   H         #H##        H   " lr-row
s"   H   $ $    H &  $  $  H   " lr-row
s" ############################" lr-row
\ level 5
s"               S             " lr-row
s"               S             " lr-row
s"         ######S#####        " lr-row
s"  0$H $    H  $S  H   $  H0 $" lr-row
s" ###H######H######H######H###" lr-row
s"    $      H      H      $   " lr-row
s" ###H######H      H######H###" lr-row
s"  $ H   H  H ---- H  H 0 H $ " lr-row
s" #######H####    ####H#######" lr-row
s"        H$          $H ######" lr-row
s"       #H############H#      " lr-row
s"     H 0H  $  H   $  H  H    " lr-row
s" ####H########H#########H####" lr-row
s" ####H#       H         H    " lr-row
s"   $ H    $   H&   $    H $ 0" lr-row
s" @@@@@@@@@@@@@@@@@@@@@@@@@@@@" lr-row

( ---- The map ---- )
: lr-t@ ( cx cy -- ch )   ( outside: solid on the sides and below, nothing above )
  over 0 lr-cols within 0= if 2drop [char] @ exit then
  dup 0< if 2drop 32 exit then
  dup lr-rows >= if 2drop [char] @ exit then
  lr-cols * + lr-map + c@ ;
: lr-t! ( ch cx cy -- )   lr-cols * + lr-map + c! ;
: lr-wall? ( ch -- f )   dup [char] # = swap [char] @ = or ;
: lr-hold? ( ch -- f )   dup [char] H = swap [char] - = or ;   \ a ladder or a rope

0 value lr-c   0 value lr-b   0 value lr-mx   0 value lr-my
: lr-moves ( cx cy -- mask )   ( 1 left, 2 right, 4 up, 8 down: what a body in this cell can do )
  to lr-my to lr-mx
  lr-mx lr-my lr-t@ to lr-c   lr-mx lr-my 1+ lr-t@ to lr-b
  lr-c lr-hold? 0=  lr-b lr-wall? 0= and  lr-b [char] H <> and if 8 exit then   \ in the air: falls
  0
  lr-mx 1- lr-my lr-t@ lr-wall? 0= if 1 or then
  lr-mx 1+ lr-my lr-t@ lr-wall? 0= if 2 or then
  lr-c [char] H = if lr-mx lr-my 1- lr-t@ [char] H = if 4 or then then
  lr-b lr-wall? 0= if lr-c lr-hold? lr-b [char] H = or if 8 or then then ;

( ---- The way to the runner, for the guards: breadth first ---- )
0 value lr-bt   0 value lr-bs   0 value lr-qh   0 value lr-qt   0 value lr-bi   0 value lr-bf   0 value lr-bm
: lr-bpush ( idx d -- )
  lr-bf 16 <> if drop lr-bf then
  over lr-first + c@ if 2drop exit then
  over lr-first + c!
  lr-queue lr-qt cells + !  1 +to lr-qt ;
: lr-bfs ( gx gy tx ty -- dir )   ( the first step toward tx ty: 1 2 4 8, 16 if there, 0 if no way )
  lr-cols * + to lr-bt   lr-cols * + to lr-bs
  lr-first lr-cols lr-rows * erase
  16 lr-first lr-bs + c!   lr-bs lr-queue !   0 to lr-qh   1 to lr-qt
  begin lr-qh lr-qt < lr-first lr-bt + c@ 0= and while
    lr-queue lr-qh cells + @ to lr-bi   1 +to lr-qh
    lr-bi lr-first + c@ to lr-bf
    lr-bi lr-cols mod  lr-bi lr-cols /  lr-moves to lr-bm
    1 lr-bm and if lr-bi 1-         1 lr-bpush then
    2 lr-bm and if lr-bi 1+         2 lr-bpush then
    4 lr-bm and if lr-bi lr-cols -  4 lr-bpush then
    8 lr-bm and if lr-bi lr-cols +  8 lr-bpush then
  repeat
  lr-first lr-bt + c@ ;

\ ---- Bodies: entity 0 is the runner, 1 to 6 the guards ----
\ x y in pixels from the top left of the field; f falling; d wanted way;
\ s 0 free 1 stuck in a hole 2 climbing out 3 digging; t timer; c gold carried
\ (time before dropping it); v 1 looks right; a walk counter; m last move 1 side 2 up or down
: lr-cx ( i -- cx )   le.x @ 5 + lr-t / ;
: lr-cy ( i -- cy )   le.y @ 5 + lr-t / ;

0 value lr-gc   0 value lr-gy
: lr-trapped-at? ( cx cy -- f )   ( a guard stuck in that cell: others walk on its head )
  to lr-gy to lr-gc  0
  lr-ne 1 do
    i le.on @ if i le.s @ 1 = if i lr-cx lr-gc = if i lr-cy lr-gy = if drop -1 then then then then
  loop ;
: lr-support? ( cx cy -- f )
  2dup lr-t@ lr-hold? if 2drop -1 exit then
  1+ 2dup lr-t@ dup lr-wall? swap [char] H = or if 2drop -1 exit then
  lr-trapped-at? ;

0 value lr-i   0 value lr-dx   0 value lr-dy   0 value lr-x   0 value lr-y   0 value lr-rx   0 value lr-ry   0 value lr-off
: lr-go ( i dx dy -- )   ( one pixel of walking, climbing or falling )
  to lr-dy to lr-dx to lr-i
  lr-i le.x @ to lr-x   lr-i le.y @ to lr-y
  lr-x 5 + lr-t / to lr-rx   lr-y 5 + lr-t / to lr-ry
  lr-y lr-t mod 0= if  lr-rx lr-ry lr-support? 0= lr-i le.f !  then
  lr-i le.f @ if
    lr-rx lr-t * lr-i le.x !   lr-y 2 + lr-i le.y !   0 lr-i le.m !  exit
  then
  lr-dx 0<> if
    lr-y lr-t mod if exit then
    lr-x lr-rx lr-t * - to lr-off
    lr-off lr-dx * 0< 0= if
      lr-rx lr-dx + lr-ry lr-t@ lr-wall? if exit then
    then
    lr-x lr-dx + lr-i le.x !   lr-dx 0> 1 and lr-i le.v !   1 lr-i le.a +!   1 lr-i le.m !
    exit
  then
  lr-dy 0<> if
    lr-x lr-rx lr-t * - to lr-off
    lr-off if  lr-off 0< if 1 else -1 then lr-x + lr-i le.x !  exit then
    lr-y lr-t mod if  lr-y lr-dy + lr-i le.y !  2 lr-i le.m !  exit then
    lr-rx lr-ry lr-moves  lr-dy 0< if 4 else 8 then and if
      lr-y lr-dy + lr-i le.y !   2 lr-i le.m !   1 lr-i le.a +!
    then
  then ;

( ---- The game ---- )
0 value lr-state   0 value lr-wait   0 value lr-tick   0 value lr-dying
0 value lr-grace   0 value lr-score   0 value lr-best   0 value lr-lives   0 value lr-level   0 value lr-need   0 value lr-ng   0 value lr-ch

: lr-reveal ( -- )   ( all the gold is taken: the hidden ladders show )
  lr-cols lr-rows * 0 do  lr-map i + c@ [char] S = if [char] H lr-map i + c! then  loop
  1200 120 beep ;

0 value lr-sl
: lr-spawn ( slot cx cy -- )
  rot to lr-sl
  lr-t * lr-sl le.y !   lr-t * lr-sl le.x !
  1 lr-sl le.on !   0 lr-sl le.f !   0 lr-sl le.d !   0 lr-sl le.s !   0 lr-sl le.t !   0 lr-sl le.c !
  1 lr-sl le.v !   0 lr-sl le.a !   0 lr-sl le.m ! ;

: lr-load ( n -- )
  lr-nlev mod lr-lsize * lr-levels + lr-map lr-lsize cmove
  0 to lr-need   1 to lr-ng
  lr-ne 0 do 0 i le.on ! loop
  lr-maxh 0 do 0 i lh.on ! loop
  lr-rows 0 do lr-cols 0 do
    i j lr-t@ to lr-ch
    lr-ch [char] $ = if 1 +to lr-need then
    lr-ch [char] & = if 0 i j lr-spawn  32 i j lr-t! then
    lr-ch [char] 0 = if
      lr-ng lr-ne < if lr-ng i j lr-spawn  1 +to lr-ng then
      32 i j lr-t!
    then
  loop loop
  60 to lr-wait   100 to lr-grace   0 to lr-dying ;

: lr-new-game ( -- )
  0 to lr-score   5 to lr-lives   0 to lr-level   0 to lr-state
  lr-level lr-load ;

( ---- Keys ---- )
create lr-was 256 allot   lr-was 256 erase
: lr-just ( k -- f )   ( pressed now, not at the last look )
  dup pressed? swap 255 and lr-was + 2dup c@ 0= and >r c! r> ;

( ---- Gold, holes, guards ---- )
0 value lr-pi
: lr-pickup ( i -- )
  to lr-pi
  lr-pi lr-cx lr-pi lr-cy lr-t@ [char] $ <> if exit then
  lr-pi le.x @ lr-pi lr-cx lr-t * - abs 4 > if exit then
  lr-pi le.y @ lr-pi lr-cy lr-t * - abs 4 > if exit then
  lr-pi 0= if
    32 lr-pi lr-cx lr-pi lr-cy lr-t!
    250 +to lr-score   -1 +to lr-need   880 40 beep
    lr-need 0= if lr-reveal then
  else
    lr-pi le.c @ 0= if
      32 lr-pi lr-cx lr-pi lr-cy lr-t!
      150 100 random + lr-pi le.c !
    then
  then ;

: lr-drop-gold ( i -- )   ( a guard puts its gold down, here if the cell is free )
  to lr-pi
  lr-pi le.c @ 1 > if -1 lr-pi le.c +! exit then
  lr-pi le.c @ 0= if exit then
  lr-pi le.x @ lr-t mod  lr-pi le.y @ lr-t mod or if exit then
  lr-pi lr-cx lr-pi lr-cy lr-t@ 32 = if
    [char] $ lr-pi lr-cx lr-pi lr-cy lr-t!   0 lr-pi le.c !
  then ;

0 value lr-hx   0 value lr-hy
: lr-hole-at ( cx cy -- f )   ( is there a hole in this cell )
  to lr-hy to lr-hx  0
  lr-maxh 0 do
    i lh.on @ if i lh.x @ lr-hx = if i lh.y @ lr-hy = if drop -1 then then then
  loop ;

0 value lr-dd
: lr-free-for-dig? ( cx cy -- f )   ( no body there )
  to lr-hy to lr-hx  -1
  lr-ne 0 do
    i le.on @ if i lr-cx lr-hx = if i lr-cy lr-hy = if drop 0 then then then
  loop ;
: lr-dig ( d -- )   ( the brick down left or down right of the runner )
  to lr-dd
  0 le.f @ if exit then
  0 le.x @ lr-t mod  0 le.y @ lr-t mod or if exit then
  0 lr-cx 0 lr-cy lr-t@ [char] - = if exit then
  0 lr-cx lr-dd + 0 lr-cy 1+ lr-t@ [char] # <> if exit then
  0 lr-cx lr-dd + 0 lr-cy lr-t@ 32 <> if exit then
  0 lr-cx lr-dd + 0 lr-cy lr-free-for-dig? 0= if exit then
  lr-maxh 0 do
    i lh.on @ 0= if
      1 i lh.on !   300 i lh.t !
      0 lr-cx lr-dd + i lh.x !   0 lr-cy 1+ i lh.y !
      32 0 lr-cx lr-dd + 0 lr-cy 1+ lr-t!
      3 0 le.s !   12 0 le.t !   lr-dd 0> 1 and 0 le.v !
      220 40 beep
      leave
    then
  loop ;

: lr-die ( -- )   lr-state 0= if -1 to lr-dying then ;

: lr-kill ( i -- )   ( crushed in a hole: a new guard comes in from the top )
  to lr-pi
  0 lr-pi le.c !  0 lr-pi le.s !  0 lr-pi le.f !  0 lr-pi le.d !
  0 lr-pi le.y !
  20 0 do
    lr-cols 2 - random 1+  dup 0 lr-t@ 32 = if lr-t * lr-pi le.x !  leave then drop
  loop ;

: lr-refill ( h -- )
  dup lh.x @ to lr-hx   dup lh.y @ to lr-hy   0 swap lh.on !
  lr-ne 0 do
    i le.on @ if
      i lr-cx lr-hx = i lr-cy lr-hy = and if  i 0= if lr-die else i lr-kill then  then
    then
  loop
  [char] # lr-hx lr-hy lr-t! ;
: lr-holes-step ( -- )
  lr-maxh 0 do
    i lh.on @ if
      -1 i lh.t +!
      i lh.t @ 0= if i lr-refill then
    then
  loop ;

: lr-trap ( i -- )   ( a guard falls in a hole )
  to lr-pi
  1 lr-pi le.s !   140 lr-pi le.t !   0 lr-pi le.d !
  lr-pi lr-cx lr-t * lr-pi le.x !
  75 +to lr-score   300 80 beep
  lr-pi le.c @ if
    lr-pi lr-cx lr-pi lr-cy 1- lr-t@ 32 = if
      [char] $ lr-pi lr-cx lr-pi lr-cy 1- lr-t!   0 lr-pi le.c !
    then
  then ;

: lr-sign ( n -- -1|0|1 )   dup 0> swap 0< + ;
0 value lr-gi
: lr-climb-out ( -- )   ( at the top of the hole, a step aside toward the runner )
  0 le.x @ lr-gi le.x @ - lr-sign dup 0= if drop 1 then   ( dx )
  dup lr-gi lr-cx +  lr-gi lr-cy lr-t@ lr-wall? if negate then
  lr-gi lr-cx + lr-t * lr-gi le.x !
  0 lr-gi le.s !   0 lr-gi le.f !   0 lr-gi le.d ! ;

: lr-dirx ( d -- dx )   dup 2 and if drop 1 exit then  1 and negate ;
: lr-diry ( d -- dy )   dup 8 and if drop 1 exit then  4 and 0<> ;
: lr-decide ( -- )
  lr-gi lr-cx lr-gi lr-cy 0 lr-cx 0 lr-cy lr-bfs
  dup 16 = over 0= or if
    drop  0 le.x @ lr-gi le.x @ - lr-sign
    dup 0< if drop 1 else dup 0> if drop 2 then then   ( 1 left, 2 right, 0 stay )
    lr-gi lr-cx lr-gi lr-cy lr-moves over and 0= if drop 0 then
  then
  lr-gi le.d ! ;

: lr-guard-walk ( -- )
  lr-gi le.f @ 0= lr-tick lr-gi + 3 and 0= and if exit then   \ a guard goes 3 steps out of 4
  lr-gi le.x @ lr-t mod 0=  lr-gi le.y @ lr-t mod 0= and  lr-gi le.f @ 0= and if lr-decide then
  lr-gi lr-gi le.d @ lr-dirx lr-gi le.d @ lr-diry lr-go
  lr-gi lr-pickup   lr-gi lr-drop-gold
  lr-gi le.f @ 0=  lr-gi le.y @ lr-t mod 0= and if
    lr-gi lr-cx lr-gi lr-cy lr-hole-at if lr-gi lr-trap then
  then ;

: lr-guard-step ( i -- )
  to lr-gi
  lr-gi le.s @ 1 = if
    -1 lr-gi le.t +!   lr-gi le.t @ 0< if 2 lr-gi le.s ! then exit
  then
  lr-gi le.s @ 2 = if
    -1 lr-gi le.y +!   2 lr-gi le.m !
    lr-gi le.y @ lr-t mod 0= if lr-climb-out then exit
  then
  lr-guard-walk ;

( ---- The runner ---- )
: lr-runner-step ( -- )
  0 le.s @ 3 = if
    -1 0 le.t +!   0 le.t @ 0= if 0 0 le.s ! then exit
  then
  [char] z pressed? [char] q pressed? or if -1 lr-dig then
  [char] x pressed? [char] e pressed? or if 1 lr-dig then
  0 le.s @ 3 = if exit then
  0 key-left pressed? [char] a pressed? or if -1 + then
        key-right pressed? [char] d pressed? or if 1 + then to lr-dx
  0 key-up pressed? [char] w pressed? or if -1 + then
        key-down pressed? [char] s pressed? or if 1 + then to lr-dy
  lr-dy 0<> if
    0 le.y @ lr-t mod 0<>
    0 lr-cx 0 lr-cy lr-moves  lr-dy 0< if 4 else 8 then and  0<>  or
    lr-dx 0= or  if 0 0 lr-dy lr-go else 0 lr-dx 0 lr-go then
  else
    0 lr-dx 0 lr-go
  then
  0 lr-pickup ;

( ---- One step of the game ---- )
: lr-collide ( -- )   ( touch a free guard and the runner dies )
  lr-ne 1 do
    i le.on @ if i le.s @ 1 <> if
      i le.x @ 0 le.x @ - abs 7 <  i le.y @ 0 le.y @ - abs 7 < and if lr-die then
    then then
  loop ;
: lr-lose ( -- )
  150 400 beep   -1 +to lr-lives
  lr-lives 0> if lr-level lr-load
  else  2 to lr-state   lr-score lr-best max to lr-best  then ;
: lr-win ( -- )
  1500 +to lr-score   1 +to lr-level   lr-level lr-load
  1000 100 beep ;
: lr-step ( -- )
  1 +to lr-tick
  lr-wait 0> if -1 +to lr-wait exit then
  lr-runner-step
  lr-grace 0> if -1 +to lr-grace else  lr-ne 1 do i le.on @ if i lr-guard-step then loop  then
  lr-holes-step
  lr-collide
  lr-dying if 0 to lr-dying lr-lose exit then
  lr-need 0=  0 le.y @ 0= and  0 lr-cx 0 lr-cy lr-t@ [char] H = and if lr-win then ;

( ---- Drawing: a tiny 3x5 pixel font ---- )
create lr-digits
  $7b6f , $2c97 , $73e7 , $73cf , $5bc9 , $79cf , $79ef , $7249 , $7bef , $7bcf ,
create lr-font   ( A to Z )
  $2bed , $6bae , $3923 , $6b6e , $79a7 , $79a4 , $396b , $5bed , $7497 , $126a , $5bad , $4927 , $5fed ,
  $6b6d , $2b6a , $6ba4 , $2b7b , $6bad , $388e , $7492 , $5b6f , $5b6a , $5bfd , $5aad , $5a92 , $72a7 ,
: lr-bits { bits x y s }
  15 0 do
    bits 14 i - rshift 1 and if
      x i 3 mod s * +   y i 3 / s * +   s s box
    then
  loop ;
: lr-number { n digits x y s }   ( right aligned in digits places )
  digits 0 do
    n 10 mod cells lr-digits + @   x digits 1- i - s 4 * * +   y s lr-bits
    n 10 / to n
  loop ;
: lr-glyph ( c -- bits )
  32 or dup 97 < over 122 > or if drop 0 exit then
  97 - cells lr-font + @ ;
: lr-text { a n x y s }   n 0 do  a i + c@ lr-glyph   x i 4 s * * +   y s lr-bits  loop ;
: lr-banner { a n rgb }   ( a word in the middle, over a black bar )
  black color   lr-w 2 / 60 -  lr-h 2 / 8 -  120 16 box
  rgb color  a n  lr-w n 8 * 2 - - 2 /  lr-h 2 / 5 -  2 lr-text ;

( ---- Drawing: the field and the bodies ---- )
create lr-frames
  lr-run-l1 , lr-run-l2 , lr-run-r1 , lr-run-r2 , lr-hang-l , lr-hang-l2 , lr-hang-r , lr-hang-r2 ,
: lr-frame ( n -- spr )   cells lr-frames + @ ;
: lr-spr-of ( ch -- spr | 0 )
  dup [char] # = if drop lr-brick exit then
  dup [char] @ = if drop lr-solid exit then
  dup [char] H = if drop lr-ladder exit then
  dup [char] - = if drop lr-rope exit then
  dup [char] $ = if drop lr-gold exit then
  drop 0 ;
: lr-draw-map ( -- )
  lr-rows 0 do lr-cols 0 do
    i j lr-t@ lr-spr-of dup if i lr-t * lr-ox +  j lr-t * lr-oy +  spr-draw else drop then
  loop loop
  lr-maxh 0 do   ( the last moments of a hole: the bricks come back )
    i lh.on @ if i lh.t @ 40 < if i lh.t @ 4 / 1 and if
      lr-brickfill  i lh.x @ lr-t * lr-ox +  i lh.y @ lr-t * lr-oy +  spr-draw
    then then then
  loop ;

0 value lr-si   0 value lr-sc
: lr-ent-spr ( i -- spr )
  to lr-si
  lr-si le.f @ if lr-fall exit then
  lr-si lr-cx lr-si lr-cy lr-t@ to lr-sc
  lr-sc [char] H = lr-si le.m @ 2 = and  lr-si le.s @ 2 = or if
    lr-si le.y @ 4 / 1 and if lr-climb2 else lr-climb1 then exit
  then
  lr-sc [char] - = lr-si le.y @ lr-t mod 0= and if
    lr-si le.v @ 2 *  lr-si le.a @ 3 rshift 1 and + 4 + lr-frame exit
  then
  lr-si le.v @ 2 *  lr-si le.a @ 3 rshift 1 and + lr-frame ;
: lr-draw-ent ( i -- )
  dup 0= if $ffffff else dup le.s @ 1 = if $a03030 else $ff5050 then then  [char] B spr-pal
  dup lr-ent-spr  over le.x @ lr-ox +  rot le.y @ lr-oy +  spr-draw ;

: lr-hud-draw ( -- )
  white color  lr-score 6 20 8 2 lr-number
  $808080 color  s" MEN" 130 8 2 lr-text   lr-lives 1 160 8 2 lr-number
  $808080 color  s" LV" 190 8 2 lr-text   lr-level 1+ 2 208 8 2 lr-number
  $f0c020 color  s" GOLD" 240 8 2 lr-text   lr-need 2 276 8 2 lr-number
  $606060 color  lr-best 6 lr-w 60 - 22 1 lr-number ;

: lr-draw-all ( -- )
  black color cls
  lr-draw-map
  lr-ne 0 do i le.on @ if i lr-draw-ent then loop
  lr-hud-draw
  lr-state 0= lr-wait 0> and if s" READY" yellow lr-banner then
  lr-state 1 = if s" PAUSE" yellow lr-banner then
  lr-state 2 = if s" GAME OVER" $ff5555 lr-banner then
  flip ;

( ---- The loop ---- )
0 value lr-last
: lr-run-steps ( -- )
  lr-state 0<> if ticks to lr-last exit then
  ticks lr-last - 160 > if ticks 16 - to lr-last then
  begin ticks lr-last - 16 >= while  16 +to lr-last  lr-step  repeat ;
: lr-keys ( -- )
  [char] p lr-just if lr-state 0= if 1 to lr-state else lr-state 1 = if 0 to lr-state then then then
  lr-state 0= if [char] k lr-just if lr-die then then
  lr-state 2 = if
    [char] r lr-just key-space lr-just or key-enter lr-just or if lr-new-game then
  then ;
: lr-one-frame ( -- )
  events
  key-esc pressed? if quit! then
  lr-keys lr-run-steps lr-draw-all
  dt 16 < if 16 dt - delay then ;
: lr-run ( -- )   begin lr-one-frame quit? until close-screen ;

[defined] loderunner-no-run [if] [else]
3 to zoom
lr-w lr-h screen
s" Lode Runner" title
lr-new-game
ticks to lr-last
lr-run
script-bye
[then]
