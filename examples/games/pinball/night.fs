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

\ Night Strike (mode 2 of the pinball menu): a night bombing run, after a
\ coin-op pinball of the early 80s. The table keeps the proportions and the
\ layout of the original (measured on screen captures, 140 x 190 pixels):
\   top        five NIGHT rollover lanes, five bumpers, a building with a
\              captive hole, a dive bomb chute with an inner spinner on its
\              right, a bomb release lane on its left
\   sides      two V kickers (upper), two slingshots, a bank of four DROP
\              targets on the left, an outer spinner on the right
\   below      two flippers, a post under them, and the shooter lane
\ Rules (see the table in docs or the notes of the original instructions):
\   A B C D and F L Y        1000 and a bonus step; ABCD raises the bonus
\                            multiplier (1X 3X 5X), FLY = 5000
\   NIGHT lanes              1500 and a bonus step; all five = 10000, lights
\                            the night arrows and the bomb release lane
\   DROP targets             2000 each, all four = 10000; R O P down and D
\                            still up lights the special (D = free game and
\                            30000)
\   bumpers 90 and 50, outer spinner 100 (500 lit), inner spinner 200 and a
\   city light (1000 each) and an ABCD letter, bomb release lane 5000
\   captive hole: 1st the lane lights (rotate them with the flippers, all
\   done = 5000), 2nd the drop arrow, 3rd the outer spinner at 500, then the
\   arrow turns. Night, four cities and ABCD light the 25000 dive bomb arrow.
\   FLY while night opens the chute hold gate: balls entering the chute are
\   held and a new ball is shot; ABCD, a drain or four held balls let them go.
\ A million points give a star, the free game score gives a credit, a tilt
\ costs the ball and its bonus. Keys:
\   z or left shift or left      left flipper
\   m or right shift or right    right flipper
\   down (hold, let go)          plunger
\   space                        nudge the table; a and l nudge from the left, from the right
\   q   s   (or enter)           insert a coin, start or add a player (1 to 4)
\   b                            the machine plays (b again: you)
\   esc                          back to the menu
\ Only libSDL2 is needed. Same fixed point physics as raid.fs: 4096 units to
\ a pixel, 6 steps a frame, walls are segments, the ball a circle.

sdl2
[undefined] pb-common [if] include common.fs [then]

320 constant nm-w   240 constant nm-h
20 constant oy       ( the table is the 320x200 of the original, drawn 20 lines lower )

( ---- Settings and state ---- )

4 value gravity      ( the original has a "forward incline" of 8 )
100 value bounce     ( walls give back bounce/256 of the speed )
5 value balls-per-game
650000 value free-score
0 value mode         ( 0 game over, 1 a ball in play, 2 bonus count, 3 between balls )
0 value credits   0 value nplayers   0 value cur   0 value freed
0 value high   150000 to high
0 value timer   0 value frames   0 value leave
0 value tilt   0 value tilted
0 value plunge
create pscore 4 cells allot   create pball 4 cells allot
create pnight 4 cells allot   create pdone 4 cells allot

: score ( -- n ) cur cells pscore + @ ;
: ball# ( -- n ) cur cells pball + @ ;
: night? ( -- f ) cur cells pnight + @ 0<> ;

( ---- The balls: up to 5, a slot holds x y vx vy state ---- )

( state: 0 none, 1 in play, 2 held in the chute, 3 in the captive hole )
0 value bx   0 value by   0 value vx   0 value vy
3 px 2 / 4096 + constant radius      ( 2.5 pixels )
9000 constant vmax                   ( units per step, less than the radius )
5 constant #bl
create bls #bl 5 * cells allot
: bl ( i -- a ) 5 * cells bls + ;
: bl-st ( i -- a ) bl 4 cells + ;
: load-ball ( i -- ) bl dup @ to bx cell+ dup @ to by cell+ dup @ to vx cell+ @ to vy ;
: save-ball ( i -- ) bl >r bx r@ !  by r@ cell+ !  vx r@ 2 cells + !  vy r> 3 cells + ! ;
: count-state ( st -- n )
  0 swap #bl 0 do i bl-st @ over = if swap 1+ swap then loop drop ;
: free-slot ( -- i|-1 )
  -1 #bl 0 do i bl-st @ 0= if drop i leave then loop ;

( ---- Segments: walls, slingshots, drop targets ---- )

12 cells constant seg-size
create segs 80 seg-size * allot
0 value #segs
: seg ( i -- s ) seg-size * segs + ;
: s-ax ( s -- a ) ;           : s-ay ( s -- a ) cell+ ;
: s-dx ( s -- a ) 2 cells + ; : s-dy ( s -- a ) 3 cells + ;
: s-len ( s -- a ) 4 cells + ; : s-kind ( s -- a ) 5 cells + ;
: s-id ( s -- a ) 6 cells + ;  : s-up ( s -- a ) 7 cells + ;
: s-x1 ( s -- a ) 8 cells + ;  : s-y1 ( s -- a ) 9 cells + ;
: s-x2 ( s -- a ) 10 cells + ; : s-y2 ( s -- a ) 11 cells + ;

( kinds: 0 wall, 1 slingshot, 2 drop target, 3 one way gate, 4 V kicker )
0 value new-kind   0 value new-id   0 value first-target
: add-seg ( x1 y1 x2 y2 -- )   ( pixels of the original capture )
  { x1 y1 x2 y2 }  #segs seg { s }
  y1 oy + to y1  y2 oy + to y2
  x1 s s-x1 !  y1 s s-y1 !  x2 s s-x2 !  y2 s s-y2 !
  x1 px s s-ax !  y1 px s s-ay !
  x2 x1 - px { ex }  y2 y1 - px { ey }
  ex 4 arshift dup *  ey 4 arshift dup *  + isqrt 16 * { l }
  l s s-len !  ex 1024 * l / s s-dx !  ey 1024 * l / s s-dy !
  new-kind s s-kind !  new-id s s-id !  -1 s s-up !
  1 +to #segs ;
: wall ( x1 y1 x2 y2 -- ) 0 to new-kind add-seg ;
: sling ( x1 y1 x2 y2 -- ) 1 to new-kind add-seg 0 to new-kind ;
: vkick ( x1 y1 x2 y2 -- ) 4 to new-kind add-seg 0 to new-kind ;
: target ( x1 y1 x2 y2 id -- ) to new-id 2 to new-kind add-seg 0 to new-kind ;
: gate ( x1 y1 x2 y2 -- ) 3 to new-kind add-seg 0 to new-kind ;
0 value last-x   0 value last-y
: start-at ( x y -- ) to last-y to last-x ;
: wall-to ( x y -- ) 2dup last-x last-y 2swap wall start-at ;
: mx ( x -- x' ) 283 swap - ;       ( the table is symmetrical round x = 141.5 )
: mwall ( x1 y1 x2 y2 -- )   ( a wall and its mirror image )
  { x1 y1 x2 y2 }  x1 y1 x2 y2 wall  x1 mx y1 x2 mx y2 wall ;

: build-table ( -- )
  0 to #segs
  ( outside: from the foot of the left wall, round the top, down the right wall )
  81 196 start-at  81 30 wall-to  82 24 wall-to  86 18 wall-to  92 13 wall-to
  100 10 wall-to  190 10 wall-to  198 12 wall-to  204 16 wall-to  209 23 wall-to
  211 32 wall-to  211 40 wall-to  211 196 wall-to  202 196 wall-to
  ( the shooter lane and the one way gate at its top )
  202 52 wall-to
  211 40 202 52 gate
  ( the V kickers under the top corners )
  81 76 96 108 vkick   81 110 96 108 wall
  202 76 187 108 vkick   202 110 187 108 wall
  ( the lanes between the slingshots and the outer wall, and the slopes to the flippers )
  92 150 92 164 mwall   92 164 115 173 mwall
  ( the slingshots )
  104 128 104 156 mwall   104 156 115 163 mwall   115 163 104 128 sling
  168 163 179 128 sling
  ( the building, its roof and the chute on its right )
  100 30 100 72 wall   121 30 121 72 wall   100 30 110 23 wall   110 23 121 30 wall
  100 72 121 72 wall   130 32 130 74 wall
  ( the posts between the NIGHT lanes )
  6 0 do 142 9 i * +  28  142 9 i * +  44 wall loop
  ( a bank of four drop targets on the left wall, D R O P from the top )
  #segs to first-target
  4 0 do 84 122 6 i * +  84 128 6 i * +  i target loop ;

: targets-up ( -- ) #segs 0 do i seg s-kind @ 2 = if -1 i seg s-up ! then loop ;
: targets-down ( -- n ) 0 #segs 0 do i seg dup s-kind @ 2 = swap s-up @ 0= and if 1+ then loop ;
: target-down? ( id -- f ) first-target + seg s-up @ 0= ;

( ---- Bumpers: x y radius kind, 0 large 90, 1 small 50, 2 the post under the flippers ---- )

create bumpers
  167 , 66 , 6 , 0 ,    133 , 100 , 6 , 0 ,
  144 , 57 , 4 , 1 ,    166 , 100 , 4 , 1 ,    188 , 57 , 4 , 1 ,
  141 , 194 , 3 , 2 ,
6 constant #bumpers
create flashes #bumpers cells allot
: bumper@ ( i n -- v ) swap 4 * + cells bumpers + @ ;
: bumper-x ( i -- x ) 0 bumper@ ;
: bumper-y ( i -- y ) 1 bumper@ oy + ;
: bumper-r ( i -- r ) 2 bumper@ ;
: bumper-kind ( i -- k ) 3 bumper@ ;

( ---- Sensors: rectangles the ball rolls over; ids below ---- )

( 0-4 NIGHT lanes, 5-7 F L Y, 8-11 A B C D, 12 captive hole, 13 chute entry,
  14 inner spinner, 15 outer spinner, 16 bomb release lane )
create sens 20 4 * cells allot
create scool 20 cells allot   scool 20 cells erase
0 value #sens
: sensor ( x1 y1 x2 y2 -- )
  { x1 y1 x2 y2 }  #sens 4 * cells sens + { a }
  x1 a !  y1 oy + a cell+ !  x2 a 2 cells + !  y2 oy + a 3 cells + !
  1 +to #sens ;
: build-sensors ( -- )
  0 to #sens
  5 0 do 142 9 i * + 1+  30  142 9 i * + 9 +  38 sensor loop
  95 91 101 97 sensor   105 96 111 102 sensor   116 99 122 105 sensor
  190 86 196 92 sensor   192 96 198 102 sensor   121 12 127 18 sensor   127 20 133 26 sensor
  107 80 114 87 sensor
  122 32 129 36 sensor   122 60 129 66 sensor   193 118 200 126 sensor
  84 46 98 50 sensor ;
: in-sens? ( i -- f )
  4 * cells sens + { a }
  bx u>p a @ a 2 cells + @ within   by u>p a cell+ @ a 3 cells + @ within  and ;

( ---- Flippers ---- )

8 cells constant fl-size
create flippers 2 fl-size * allot
: fl ( i -- f ) fl-size * flippers + ;
: f-px ( f -- a ) ;           : f-py ( f -- a ) cell+ ;
: f-th ( f -- a ) 2 cells + ; : f-rest ( f -- a ) 3 cells + ;
: f-up ( f -- a ) 4 cells + ; : f-om ( f -- a ) 5 cells + ;
: f-len ( f -- a ) 6 cells + ; : f-side ( f -- a ) 7 cells + ;
: make-flipper ( x y rest up side i -- )
  fl { f }  f f-side !  f f-up !  dup f f-rest !  f f-th !
  oy + px f f-py !  px f f-px !  21 px f f-len !  0 f f-om ! ;
: build-flippers ( -- )
  115 175 550 -450 0 0 make-flipper       ( angles in milliradians, y down )
  167 175 2592 3592 1 1 make-flipper ;
70 constant flip-up-speed   40 constant flip-down-speed   ( mrad per step )

( ---- Keys ---- )

0 value bot?   0 value bot-left   0 value bot-right   0 value bot-hold   0 value bot-side
0 value debug   0 value fast

225 constant key-lshift   229 constant key-rshift
create prev-keys 16 allot   prev-keys 16 erase
: edge ( f n -- f )   ( true only when the key has just gone down )
  prev-keys + dup c@ >r over swap c! r> 0= and ;
: left-key? ( -- f ) bot? if bot-left exit then [char] z pressed? key-left pressed? or key-lshift pressed? or ;
: right-key? ( -- f ) bot? if bot-right exit then [char] m pressed? key-right pressed? or key-rshift pressed? or [char] / pressed? or ;
: plunger-key? ( -- f ) key-down pressed? ;

( ---- Sound ---- )

: snd ( freq ms -- ) drum-hit ;
: fanfare ( -- ) 523 100 snd 659 140 snd 784 200 snd ;

( ---- Collisions: the same circle and segment tests as raid.fs ---- )

0 value cx  0 value cy  0 value rs  0 value svx  0 value svy
0 value rest  0 value kick  0 value nx  0 value ny
: contact ( -- hit? )
  bx cx - { dx }  by cy - { dy }
  dx abs rs >= dy abs rs >= or if 0 exit then
  dx 4 arshift { dx' }  dy 4 arshift { dy' }
  dx' dx' * dy' dy' * + { d2 }
  d2 rs 4 arshift dup * >= d2 0= or if 0 exit then
  d2 isqrt { d }
  dx' 1024 * d / to nx   dy' 1024 * d / to ny
  rs d 16 * - { pen }
  nx pen * 1024 / +to bx   ny pen * 1024 / +to by
  vx svx - { rvx }   vy svy - { rvy }
  rvx nx * rvy ny * + 1024 / { vn }
  vn 0< if
    nx vn * 1024 / 256 rest + * 256 / negate +to rvx
    ny vn * 1024 / 256 rest + * 256 / negate +to rvy
    rvx svx + nx kick * 1024 / + to vx
    rvy svy + ny kick * 1024 / + to vy
  then
  vn -300 < ;

: closest ( x y dx dy len -- )
  { ax ay sdx sdy len }
  bx ax - 4 arshift sdx *  by ay - 4 arshift sdy *  + 64 / 0 max len min { t }
  ax sdx t * 1024 / + to cx   ay sdy t * 1024 / + to cy ;

( ---- Lights and rules ---- )

0 value lanes-lit   0 value lites     0 value fly-bits   0 value abcd-bits
0 value cities      0 value mult-i    0 value bonus
0 value special-lit 0 value arrow     0 value stage      0 value gate-on
0 value dive-lit    0 value spin-lit  0 value abcd-done
0 value spin-i      0 value spin-o    0 value target-timer
0 value hole-ball   0 value hole-timer
0 value cur-ball   0 value found
create mults 1 , 3 , 5 ,
: mult ( -- n ) mult-i cells mults + @ ;

: add ( n -- )
  tilted if drop exit then
  cur cells pscore + +!
  freed 0= score free-score >= and if -1 to freed 1 +to credits fanfare then ;
: step-bonus ( -- ) bonus 20 < if 1 +to bonus then ;

: put-ball ( x y vx vy i -- )   ( pixels of the original capture; the ball is in play )
  dup 1 swap bl-st !  bl >r  r@ 3 cells + !  r@ 2 cells + !
  oy + px r@ cell+ !  px r> ! ;
: serve ( -- )   ( a ball on the plunger )
  free-slot dup 0< if drop exit then  >r 206 190 0 0 r> put-ball ;
: release-held ( -- )
  #bl 0 do
    i bl-st @ 2 = if  125 74  i 2 mod if 300 else -300 then  700  i put-ball  then
  loop ;

: check-dive ( -- )
  night? cities 15 = and abcd-done and dive-lit 0= and if -1 to dive-lit fanfare then ;
: night-done ( -- )
  10000 add  1 cur cells pnight + !  fanfare  check-dive ;
: abcd-complete ( -- )
  0 to abcd-bits  -1 to abcd-done  mult-i 2 < if 1 +to mult-i then
  fanfare  release-held  check-dive ;
: letter ( i -- )   ( 0 to 3: A B C D )
  { i }  1 i lshift { m }
  abcd-bits m and 0= if
    abcd-bits m or to abcd-bits  1000 add step-bonus  660 40 snd
    abcd-bits 15 = if abcd-complete then
  then ;
: fly-event ( i -- )
  { i }  1 i lshift { m }
  fly-bits m and 0= if
    fly-bits m or to fly-bits  1000 add step-bonus  770 40 snd
    fly-bits 7 = if
      0 to fly-bits  5000 add  fanfare
      night? if -1 to gate-on then
    then
  then ;
: lane-event ( i -- )
  { i }  1 i lshift { m }
  lites m and if
    lites m xor to lites
    lites 0= if 5000 add fanfare else 500 add then
  then
  lanes-lit m and 0= if
    lanes-lit m or to lanes-lit  1500 add step-bonus  880 40 snd
    lanes-lit 31 = if 0 to lanes-lit night-done then
  then ;
: city-spot ( -- )
  night? 0= if exit then
  4 0 do 1 i lshift cities and 0= if cities 1 i lshift or to cities 1000 add leave then loop
  4 0 do 1 i lshift abcd-bits and 0= if i letter leave then loop
  check-dive ;
: spin-start ( inner? -- )
  if 6 to spin-i city-spot else 6 to spin-o then ;
: brl-event ( -- )
  night? if 5000 add fanfare else 100 add then ;
: hold-ball ( -- )
  125 px to bx  48 oy + px to by  0 to vx  0 to vy  2 cur-ball bl-st !
  serve  2 count-state 4 >= if release-held then ;
: chute-enter ( -- )
  dive-lit if 25000 add fanfare 0 to dive-lit 0 to cities 0 to abcd-done then
  gate-on if hold-ball then ;
: hole-enter ( -- )
  hole-ball 0< 0= if exit then
  cur-ball to hole-ball  45 to hole-timer  3 hole-ball bl-st !
  111 px to bx  83 oy + px to by  0 to vx  0 to vy
  500 add  1 +to stage  330 80 snd
  stage 1 = if 11 to lites then
  stage 2 = if 0 to arrow then
  stage 3 = if -1 to spin-lit then
  stage 3 > if arrow 0< 0= if arrow 1+ 3 and to arrow then then ;
: hole-tick ( -- )
  hole-ball 0< if exit then
  -1 +to hole-timer
  hole-timer 0= if
    111 90  frames 1 and if 500 else -500 then  900  hole-ball put-ball
    -1 to hole-ball
  then ;
: spin-tick ( -- )
  frames 3 and if exit then
  spin-i if -1 +to spin-i 200 add 600 20 snd then
  spin-o if -1 +to spin-o spin-lit if 500 else 100 then add 500 20 snd then ;

: sensor-fired ( id -- )
  dup 5 < if lane-event exit then
  dup 8 < if 5 - fly-event exit then
  dup 12 < if 8 - letter exit then
  12 - case
    0 of hole-enter endof
    1 of chute-enter endof
    2 of -1 spin-start endof
    3 of 0 spin-start endof
    4 of brl-event endof
  endcase ;
: sens-scan ( -- )
  #sens 0 ?do
    i cells scool + @ if -1 i cells scool + +!
    else i in-sens? if 40 i cells scool + ! i sensor-fired then then
  loop ;

: special-award ( -- ) 1 +to credits 30000 add fanfare 0 to special-lit ;
: target-hit ( s -- )
  { s }  0 s s-up !  s s-id @ { id }
  2000 add  740 60 snd
  arrow id = if 5000 add -1 to arrow then
  id 0= special-lit and if special-award then
  1 target-down? 2 target-down? and 3 target-down? and 0 target-down? 0= and
  if -1 to special-lit then
  targets-down 4 = if 10000 add fanfare 90 to target-timer 0 to special-lit then ;

( ---- Hits ---- )

: seg-hit ( s -- )
  { s }
  s s-kind @ 2 = s s-up @ 0= and if exit then
  s s-kind @ 3 = if
    by s s-ay @ - 4 arshift s s-dx @ *  bx s s-ax @ - 4 arshift s s-dy @ *  -
    0> 0= if exit then
  then
  s s-ax @ s s-ay @ s s-dx @ s s-dy @ s s-len @ closest
  0 to svx  0 to svy  radius to rs
  s s-kind @ case
    0 of bounce to rest 0 to kick endof
    1 of 120 to rest 700 to kick endof
    2 of 60 to rest 0 to kick endof
    3 of bounce to rest 0 to kick endof
    4 of 120 to rest 500 to kick endof
  endcase
  contact if
    s s-kind @ 1 = if 10 add 520 30 snd then
    s s-kind @ 4 = if 50 add 560 30 snd then
    s s-kind @ 2 = if s target-hit then
  then ;

: bumper-hit ( i -- )
  { i }
  i bumper-x px to cx   i bumper-y px to cy
  radius i bumper-r px + to rs  0 to svx  0 to svy
  i bumper-kind 2 = if 60 to rest 0 to kick else 150 to rest 1000 to kick then
  contact if
    i bumper-kind 2 <> if
      i bumper-kind if 50 else 90 then add  step-bonus
      8 i cells flashes + !  330 50 snd
    then
    bx cx - abs 1024 < if frames 1 and if 300 else -300 then +to vx then
  then ;

: flipper-hit ( i -- )
  fl { f }
  f f-th @ cos-sin { c s }
  f f-px @ f f-py @ c s f f-len @ closest
  cy f f-py @ - negate f f-om @ * 1000 / to svx
  cx f f-px @ - f f-om @ * 1000 / to svy
  radius 2 px + to rs  50 to rest  0 to kick
  contact drop ;

: move-flipper ( i -- )
  fl { f }
  f f-side @ if right-key? else left-key? then tilted 0= and
  if f f-up @ flip-up-speed else f f-rest @ flip-down-speed then { goal speed }
  goal f f-th @ -  speed min  speed negate max { om }
  om f f-om !  om f f-th +! ;

: clamp-speed ( -- )
  vx 4 arshift dup *  vy 4 arshift dup *  + isqrt 16 * { v }
  v vmax > if vx vmax * v / to vx  vy vmax * v / to vy then ;

: step-ball ( -- )
  gravity +to vy  vx +to bx  vy +to by  clamp-speed
  #segs 0 ?do i seg seg-hit loop
  #bumpers 0 do i bumper-hit loop
  0 flipper-hit  1 flipper-hit
  sens-scan ;

: step ( -- )   ( one of the 6 steps of a frame )
  0 move-flipper  1 move-flipper
  #bl 0 do
    i bl-st @ 1 = if
      i to cur-ball  i load-ball  step-ball  i save-ball
    then
  loop ;

( ---- Game flow ---- )

: clear-balls ( -- ) #bl 0 do 0 i bl-st ! loop ;
: reset-ball-state ( -- )   ( what a new ball starts without )
  0 to lanes-lit  0 to lites  0 to fly-bits  0 to abcd-bits  0 to cities
  0 to mult-i  0 to bonus  0 to special-lit  -1 to arrow  0 to stage
  0 to gate-on  0 to dive-lit  0 to spin-lit  0 to abcd-done
  0 to spin-i  0 to spin-o  0 to target-timer  -1 to hole-ball
  0 to tilt  0 to tilted  0 to plunge
  targets-up ;
: new-ball ( -- ) clear-balls reset-ball-state serve 1 to mode ;
: new-game ( players -- )
  to nplayers  0 to cur  0 to freed
  4 0 do 0 i cells pscore !  1 i cells pball + !  0 i cells pnight + ! loop
  new-ball ;

: lane-ball ( -- i|-1 )   ( a ball waiting in the shooter lane )
  -1 #bl 0 do
    i bl-st @ 1 = if i bl @ u>p 203 > i bl cell+ @ u>p oy + 160 > and if drop i leave then then
  loop ;

: kick-balls ( dvx dvy -- )
  #bl 0 do
    i bl-st @ 1 = if 2dup i bl 3 cells + +!  i bl 2 cells + +! then
  loop 2drop ;

: launch ( -- )
  lane-ball dup 0< if drop else plunge 20 * 1800 + negate swap bl 3 cells + ! 150 80 snd then
  0 to plunge ;
: plunger ( -- )
  plunger-key? if plunge 2 + 100 min to plunge
  else plunge if launch then then ;

: bump ( dvx -- )   ( one shove of the table: the balls jump, the tilt gauge fills )
  tilted mode 1 <> or if drop exit then
  -600 kick-balls  45 +to tilt  90 25 snd ;
: nudge ( -- )
  [char] a pressed? 1 edge if 500 bump then
  [char] l pressed? 4 edge if -500 bump then
  key-space pressed? 8 edge if frames 1 and if 350 else -350 then bump then
  tilt 100 > tilted 0= and if -1 to tilted 110 400 snd then
  tilt 0> if -1 +to tilt then ;

: rotate-lites ( right? -- )
  lites 0= if drop exit then
  if lites 1 and 4 lshift lites 2/ or
  else lites 2* dup 32 and if 31 and 1 or then then
  to lites ;

0 value ball-frames
: end-ball ( -- )
  debug if ." ball" ball# . ." frames" frames ball-frames - . ." score" score . ." bonus" bonus . cr then
  frames to ball-frames
  tilted if 0 to bonus then
  2 to mode  0 to timer ;
: check-drain ( -- )
  #bl 0 do
    i bl-st @ 1 = if
      i bl cell+ @ u>p oy 200 + > if
        0 i bl-st !  2 count-state if release-held then
      then
    then
  loop
  mode 1 = if
    1 count-state 0= 3 count-state 0= and if end-ball then
  then ;

: next-player ( -- f )   ( true if somebody has a ball left )
  1 cur cells pball + +!  0 to found
  nplayers 0 do
    found 0= if
      cur 1+ nplayers mod to cur
      ball# balls-per-game <= if -1 to found then
    then
  loop found ;
: game-over ( -- )
  0 to mode  debug if 1 to leave then
  0 nplayers 0 do i cells pscore + @ max loop
  dup high > if 3 +to credits fanfare then  high max to high ;

: bonus-count ( -- )
  1 +to timer
  bonus 0> timer 6 mod 0= and if
    -1 +to bonus  mult 1000 * add  600 20 snd
  then
  bonus 0= timer 40 > and if
    next-player if new-ball else game-over then
  then ;

: start-keys ( -- )
  [char] q pressed? 5 edge if credits 9 < if 1 +to credits 800 60 snd then then
  [char] s pressed? 6 edge key-enter pressed? 7 edge or if
    credits 0= key-enter pressed? and if 1 to credits then
    credits 0> if
      mode 0 = if -1 +to credits 1 new-game
      else mode 1 = if
        cur 0= ball# 1 = and plunge 0= and lane-ball 0< 0= and nplayers 4 < and
        if -1 +to credits 1 +to nplayers then
      then then
    then
  then ;

: bot-play ( -- )   ( the machine: shoots, flips a ball that comes down near a flipper )
  mode 0 = if 1 to credits [char] s pressed? drop 1 new-game exit then
  mode 1 <> if exit then
  lane-ball 0< 0= if
    plunge 60 < if 2 +to plunge else 60 to plunge launch then
  then
  bot-hold if
    -1 +to bot-hold
    bot-hold 0= if 0 to bot-left 0 to bot-right then
  else
    0 to bot-left  0 to bot-right
    #bl 0 do
      i bl-st @ 1 = if
        i bl cell+ @ u>p oy 150 + > i bl 3 cells + @ -300 > and
        i bl @ u>p 202 < and if
          i bl @ u>p 141 < if -1 to bot-left else -1 to bot-right then
          frames 7 mod 0= if 0 to bot-hold else 5 frames 5 mod + to bot-hold then
        then
      then
    loop
  then ;

: play ( -- )
  left-key? 2 edge if 90 25 snd 0 rotate-lites then
  right-key? 3 edge if 90 25 snd -1 rotate-lites then
  bot? if bot-play else plunger then nudge
  6 0 do step loop
  check-drain
  debug 2 > frames 5 mod 0= and if #bl 0 do i bl-st @ . i bl @ u>p . i bl cell+ @ u>p oy - . ." | " loop mode . hole-ball . gate-on . cr then
  target-timer if -1 +to target-timer target-timer 0= if targets-up then then ;

: update ( -- )
  1 +to frames  debug frames 9000 > and if 1 to leave then
  #bumpers 0 do i cells flashes + dup @ 1- 0 max swap ! loop
  spin-tick hole-tick start-keys
  [char] b pressed? 9 edge if bot? 0= to bot? then
  bot? mode 1 <> and if bot-play then
  mode 1 = if play exit then
  mode 2 = if bonus-count then ;

( ---- Drawing: magenta, cyan and white on black, like the original screen ---- )

$ff55ff constant c-mag   $55ffff constant c-cyan   $ffffff constant c-white
$ffff55 constant c-lit   $602860 constant c-dim    $303030 constant c-grey

: thick-line ( x1 y1 x2 y2 -- ) { x1 y1 x2 y2 }
  x1 y1 x2 y2 line  x1 1+ y1 x2 1+ y2 line ;
: draw-seg ( s -- )
  { s }
  s s-kind @ { k }
  k 2 = s s-up @ 0= and if
    c-dim color  s s-x1 @ 2 + s s-y1 @ s s-x2 @ 2 + s s-y2 @ line exit
  then
  k case
    0 of c-mag color endof
    1 of c-white color endof
    2 of c-cyan color endof
    3 of c-dim color endof
    4 of c-cyan color endof
  endcase
  s s-x1 @ s s-y1 @ s s-x2 @ s s-y2 @
  k 1 = k 4 = or  s segs - seg-size / 14 < k 0= and  or if thick-line else line then ;

: lamp ( lit? x y -- )   ( a 3 by 3 light )
  rot if c-lit else c-dim then color  3 3 box ;
: tag ( lit? c x y -- )   ( a letter that lights )
  { c x y }  if c-lit else c-dim then color  c x y 1 draw-char ;
: tag-text ( lit? a n x y -- )
  { a n x y }  if c-lit else c-dim then color  a n x y 1 say ;

: draw-bumper ( i -- )
  { i }  i bumper-x { x }  i bumper-y { y }  i bumper-r { r }
  i cells flashes + @ if c-white color x y r disc then
  c-mag color  x y r circle  i bumper-kind 0= if x y r 2 - circle then  x y 1 disc ;

: vhatch ( x1 x2 t1 t2 b1 b2 -- )   ( vertical lines every 2 pixels between two edges )
  { x1 x2 t1 t2 b1 b2 }  x2 x1 - { w }
  x2 1+ x1 do
    i  i x1 - t2 t1 - * w / t1 + oy +
    i  i x1 - b2 b1 - * w / b1 + oy +  line
  2 +loop ;

: draw-hatches ( -- )
  c-white color
  105 114 131 160 157 162 vhatch   169 178 160 131 162 157 vhatch
  c-cyan color
  82 95 78 106 110 108 vhatch   188 201 106 78 108 110 vhatch ;

: draw-building ( -- )
  ( a magenta block with black windows, a captive target and the hangar below )
  c-mag color  100 30 oy + 22 43 box
  7 0 do  110 i 10 * 7 / - 23 oy + i +  110 i 10 * 7 / + 23 oy + i +  line  loop
  black color  102 36 oy + 6 5 box  112 36 oy + 6 5 box  102 62 oy + 6 5 box  112 62 oy + 6 5 box
  4 0 do
    cities 1 i lshift and if
      c-lit color  102 i 1 and 10 * + 36 oy + i 2 / 26 * +  6 5 box
    then
  loop
  black color  110 52 oy + 9 disc
  c-mag color  110 52 oy + 7 circle  110 52 oy + 3 circle
  110 46 oy + 110 58 oy + line  104 52 oy + 116 52 oy + line
  98 124 74 74 92 92 vhatch
  black color  98 82 oy + 124 82 oy + line
  black color  110 83 oy + 5 disc   c-white color  110 83 oy + 4 circle  110 83 oy + 1 disc ;

: draw-lights ( -- )
  ( the NIGHT lanes )
  5 0 do
    lanes-lit 1 i lshift and 0<>  146 9 i * + 30 oy +  lamp
    lites 1 i lshift and 0<>  146 9 i * + 35 oy +  lamp
  loop
  night? s" NIGHT" 145 20 oy + tag-text
  night? s" 10000" 150 12 oy + tag-text
  dive-lit 0<> s" 25000" 84 12 oy + tag-text
  lites 0<> s" 5000" 86 112 oy + tag-text
  ( F L Y and A B C D )
  fly-bits 1 and 0<> [char] F 93 oy 89 + tag
  fly-bits 2 and 0<> [char] L 103 oy 94 + tag
  fly-bits 4 and 0<> [char] Y 114 oy 97 + tag
  abcd-bits 1 and 0<> [char] A 186 oy 84 + tag
  abcd-bits 2 and 0<> [char] B 188 oy 94 + tag
  abcd-bits 4 and 0<> [char] C 116 oy 13 + tag
  abcd-bits 8 and 0<> [char] D 134 oy 22 + tag
  special-lit 0<> s" SP" 86 oy 152 + tag-text ;

: draw-drop-tags ( -- )
  4 0 do
    i target-down? 0=  s" DROP" drop i + c@  88  oy 122 + i 6 * + 1+  tag
    arrow i = if c-lit color 94 oy 124 + i 6 * + 1+ 3 box then
  loop ;

: draw-spinners ( -- )
  c-white color
  spin-o 0<> frames 2 and 0<> and if 193 oy 122 + 200 oy 122 + line
  else 196 oy 118 + 196 oy 126 + line then
  spin-i 0<> frames 2 and 0<> and if 122 oy 63 + 129 oy 63 + line
  else 125 oy 60 + 125 oy 66 + line then ;

: draw-flipper ( i -- )
  fl { f }
  f f-th @ cos-sin { c s }
  f f-px @ u>p { x }  f f-py @ u>p { y }
  f f-len @ u>p { l }
  c-mag color
  l 1+ 0 do  x c i * 1024 / +  y s i * 1024 / +  4 i l 3 / > + i l 2* 3 / > + disc  loop ;

: draw-balls ( -- )
  c-white color
  #bl 0 do
    i bl-st @ if i bl dup @ u>p swap cell+ @ u>p 2 disc then
  loop ;

: draw-plunger ( -- )
  c-white color  203 oy 192 + plunge 12 / +  7 2 box
  c-mag color
  203 oy 196 + 210 oy 196 + line
  5 0 do 204 oy 194 + i 2 * +  209 oy 195 + i 2 * +  line loop ;

: draw-frame ( -- )   ( the cyan edge on the left, the white bar on the right )
  c-cyan color  79 30 oy + 79 196 oy + line
  c-white color  212 40 oy + 212 196 oy + line ;

: draw-table ( -- )
  draw-frame
  #segs 0 ?do i seg draw-seg loop
  draw-hatches
  #bumpers 0 do i draw-bumper loop
  draw-building draw-lights draw-drop-tags draw-spinners
  0 draw-flipper  1 draw-flipper  draw-plunger  draw-balls ;

( ---- The panels ---- )

: vbar ( x y h w -- )   ( vertical lines every 2 pixels )
  { x y h w }  w 0 do  x i + y x i + y h + line  2 +loop ;
: stripe-char ( c x y s -- )   ( a letter made of vertical bars, like the title of the original )
  { c x y s }  c $7f and cells glyphs + @ { bits }
  15 0 do
    bits 14 i - rshift 1 and if  x i 3 mod s * +  y i 3 / s * +  s 1- s vbar  then
  loop ;
: stripe-say ( a n x y s -- )
  { a n x y s }
  n 0 ?do a i + c@ x i 4 * s * + y s stripe-char loop ;
: dotted-box ( x y w h -- )
  { x y w h }
  w 0 do  x i + y dot  x i + y h + dot  2 +loop
  h 0 do  x y i + dot  x w + y i + dot  2 +loop ;

: draw-score6 ( n x y -- )
  { n x y }
  6 0 do  n 10 mod  x 5 i - 12 * +  y seg7  n 10 / to n  loop ;

: draw-star ( x y -- )   ( one for each million )
  c-lit color  2dup 1- dot  2dup 1+ dot  2dup swap 1- swap dot  2dup swap 1+ swap dot  dot ;

: draw-player ( p -- )
  { p }  p 30 * 26 + { y }  p cells pscore + @ { n }
  c-mag color  p 1+ 40 y 2 - 1 say-number
  p nplayers < 0= if exit then
  n 1000000 / 3 min 0 ?do  8 i 7 * +  y 3 + draw-star  loop
  mode 0<> p cur = and frames 16 and 0<> and if exit then
  c-white color  n 1000000 mod 3 y 6 + draw-score6 ;

: draw-bomber ( x y -- )   ( seen from above, x is the middle )
  { x y }  c-mag color
  x 2 - y 4 + 4 34 box
  x 26 - y 12 + 52 7 box
  x 10 - y 30 + 20 4 box
  4 0 do  x 22 - i 12 * + y 6 + i 1 and 3 * + 4 12 box  loop
  black color  x 1- y 8 + 2 3 box ;

: draw-left ( -- )
  c-mag color
  4 0 do  2 i 30 * 29 + 72 22 dotted-box  loop
  4 0 do i draw-player loop
  38 146 draw-bomber
  c-cyan color  s" CREDITS" 4 190 1 say   s" BALL" 52 190 1 say
  2 200 36 20 dotted-box  44 200 34 20 dotted-box
  c-white color  credits 20 204 2 say-number  ball# 70 204 2 say-number ;

: draw-right ( -- )
  c-mag color  214 0 214 240 line
  s" NIGHT" 220 20 5 stripe-say   s" STRIKE" 222 46 3 stripe-say
  c-cyan color  s" HIGH" 224 78 1 say   c-white color  high 312 78 1 say-number
  20 0 do  i bonus < if c-lit else c-dim then color  226 180 i 4 * - 14 2 box  loop
  c-cyan color  s" BONUS" 224 186 1 say
  mult-i 2 = c-lit c-dim rot if drop else nip then color  s" 5X" 252 120 2 say
  mult-i 1 >= if c-lit else c-dim then color  s" 3X" 252 140 2 say
  c-lit color  s" 1X" 252 160 2 say
  gate-on 0<> s" HOLD" 252 100 1 + tag-text
  mode 0 = if
    frames 32 and if c-white color s" GAME OVER" 224 84 2 say then
  else tilted if frames 16 and if c-lit color s" TILT" 252 110 2 say then then then
  c-white color
  222 204 312 204 line  222 234 312 234 line  222 204 222 234 line  312 204 312 234 line
  s" 25C" 228 208 2 say
  credits 0> if s" PRESS S" else s" PRESS Q" then 250 222 1 say ;

: draw-all ( -- )
  black color cls
  draw-table draw-left draw-right
  flip ;

: frame ( -- )
  events
  key-esc pressed? if 1 to leave then
  update draw-all
  dt 16 < fast 0= and if 16 dt - delay then ;

: night ( -- )   ( one session at the machine, until Esc or the window is closed )
  0 to leave  0 to mode  0 to nplayers  0 to cur  0 to credits  0 to frames
  0 to tilted  0 to plunge  0 to spin-i  0 to spin-o  -1 to hole-ball
  0 to cities  0 to lanes-lit  0 to lites  0 to fly-bits  0 to abcd-bits
  0 to mult-i  0 to bonus  0 to dive-lit  0 to gate-on  0 to special-lit  -1 to arrow
  4 0 do 0 i cells pscore + !  0 i cells pnight + !  1 i cells pball + !  loop
  clear-balls
  build-table build-sensors build-flippers
  nm-w nm-h open-screen
  s" Night Strike" title
  0 cursor
  begin frame leave quit? or until ;
