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

\ Night Raid, a pinball in the style of Night Mission Pinball (subLOGIC, 1982):
\ a table drawn with lines on a black screen, the score on a panel at the
\ right, missions to fly, and the physics can be tuned. Only libSDL2 is needed.
\   z  or left shift or left     left flipper
\   m  or right shift or right   right flipper
\   space or down (hold, let go) plunger
\   n                            nudge the table (too much and it is TILT)
\   enter or 1                   start a game
\   g e b (before a game)        gravity, bounce, balls per game
\   esc                          quit
\ Before a game the machine plays alone. Hit the four drop targets to fly a
\ mission (6 ranks, from cadet to ace); light the three top lanes to raise the
\ bonus multiplier (the flippers move the lit lanes).
\
\ The physics works in fixed point, 4096 units to a pixel, 6 steps a frame:
\ the ball is a circle, the walls segments, the flippers turning segments
\ whose speed is given to the ball, the bumpers and slingshots kick it.

sdl2

320 constant view-w   240 constant view-h
: px ( pixels -- units ) 12 lshift ;
: u>p ( units -- pixels ) 12 arshift ;

: isqrt ( n -- r )   ( integer square root, Newton )
  dup 2 < if exit then
  dup >r begin dup r@ over / + 2/ 2dup > while nip repeat drop r> drop ;

: cos-sin ( mrad -- cos sin )   ( both times 1024 )
  s>f 1000e f/ fdup fcos 1024e f* f>s fsin 1024e f* f>s ;

( ---- Settings and game state ---- )

10 value gravity     ( units added to the speed each step )
90 value bounce      ( walls give back bounce/256 of the speed )
3 value balls
1 value setting-g   1 value setting-e   0 value setting-b

0 value state        ( 1 play, 2 ball lost, 3 game over )
0 value demo?
0 value score   0 value high   0 value ball#
0 value mission   1 value mult   0 value bonus
0 value tilt   0 value tilted
0 value timer        ( frames left in states 2 and 3 )
0 value plunge       ( 0 to 100 )
0 value frames

( ---- The ball ---- )

0 value bx   0 value by   0 value vx   0 value vy
3 px 2048 + constant radius      ( 3.5 pixels )
12000 constant vmax              ( units per step, less than the radius )

( ---- Segments: walls, slingshots, drop targets ---- )

12 cells constant seg-size
create segs 64 seg-size * allot
0 value #segs
: seg ( i -- s ) seg-size * segs + ;
: s-ax ( s -- a ) ;           : s-ay ( s -- a ) cell+ ;
: s-dx ( s -- a ) 2 cells + ; : s-dy ( s -- a ) 3 cells + ;
: s-len ( s -- a ) 4 cells + ; : s-kind ( s -- a ) 5 cells + ;
: s-id ( s -- a ) 6 cells + ;  : s-up ( s -- a ) 7 cells + ;
: s-x1 ( s -- a ) 8 cells + ;  : s-y1 ( s -- a ) 9 cells + ;
: s-x2 ( s -- a ) 10 cells + ; : s-y2 ( s -- a ) 11 cells + ;

0 value new-kind   0 value new-id   ( 0 wall, 1 slingshot, 2 drop target )
: add-seg ( x1 y1 x2 y2 -- )   ( pixels )
  { x1 y1 x2 y2 }  #segs seg { s }
  x1 s s-x1 !  y1 s s-y1 !  x2 s s-x2 !  y2 s s-y2 !
  x1 px s s-ax !  y1 px s s-ay !
  x2 x1 - px { ex }  y2 y1 - px { ey }
  ex 4 arshift dup *  ey 4 arshift dup *  + isqrt 16 * { l }   ( 32-bit cells on the web )
  l s s-len !  ex 1024 * l / s s-dx !  ey 1024 * l / s s-dy !
  new-kind s s-kind !  new-id s s-id !  -1 s s-up !
  1 +to #segs ;
: wall ( x1 y1 x2 y2 -- ) 0 to new-kind add-seg ;
: sling ( x1 y1 x2 y2 -- ) 1 to new-kind add-seg 0 to new-kind ;
: target ( x1 y1 x2 y2 id -- ) to new-id 2 to new-kind add-seg 0 to new-kind ;
0 value last-x   0 value last-y
: start-at ( x y -- ) to last-y to last-x ;
: wall-to ( x y -- ) 2dup last-x last-y 2swap wall start-at ;

: build-table ( -- )
  0 to #segs
  ( outside, from the left wall round the top to the right wall, then
    the floor and the inner wall of the shooter lane )
  12 180 start-at  12 50 wall-to  18 30 wall-to  30 16 wall-to  48 7 wall-to
  70 3 wall-to  140 3 wall-to  165 7 wall-to  185 15 wall-to  199 27 wall-to
  207 42 wall-to  208 60 wall-to  208 236 wall-to  196 236 wall-to  196 70 wall-to
  ( slopes down to the flippers )
  12 180 64 209 wall   196 180 144 209 wall
  ( slingshots: two walls and a kicking side )
  30 150 30 176 wall   30 176 54 192 wall   54 192 30 150 sling
  178 150 178 176 wall  178 176 154 192 wall  154 192 178 150 sling
  ( posts between the top lanes )
  70 16 70 30 wall  95 16 95 30 wall  120 16 120 30 wall  145 16 145 30 wall
  ( a bank of four drop targets on the left, close to the wall, with a
    deflector above so that the ball cannot rest on the bank )
  12 80 15 87 wall
  4 0 do 15 88 i 10 * +  15 96 i 10 * +  i target loop ;

: targets-up ( -- ) #segs 0 do i seg s-kind @ 2 = if -1 i seg s-up ! then loop ;
: targets-down ( -- n ) 0 #segs 0 do i seg dup s-kind @ 2 = swap s-up @ 0= and if 1+ then loop ;

( ---- Bumpers, lanes, flippers ---- )

create bumpers 78 , 72 , 130 , 72 , 104 , 100 ,
9 constant bumper-r
create flashes 3 cells allot
: bumper-x ( i -- x ) 2* cells bumpers + @ ;
: bumper-y ( i -- y ) 2* 1+ cells bumpers + @ ;

create lanes 3 cells allot     ( lit or not )
: lane ( i -- a ) cells lanes + ;
0 value target-timer           ( frames before the targets come back up )

8 cells constant fl-size
create flippers 2 fl-size * allot
: fl ( i -- f ) fl-size * flippers + ;
: f-px ( f -- a ) ;           : f-py ( f -- a ) cell+ ;
: f-th ( f -- a ) 2 cells + ; : f-rest ( f -- a ) 3 cells + ;
: f-up ( f -- a ) 4 cells + ; : f-om ( f -- a ) 5 cells + ;
: f-len ( f -- a ) 6 cells + ; : f-side ( f -- a ) 7 cells + ;
: make-flipper ( x y rest up side i -- )
  fl { f }  f f-side !  f f-up !  dup f f-rest !  f f-th !
  px f f-py !  px f f-px !  28 px f f-len !  0 f f-om ! ;
: build-flippers ( -- )
  68 212 524 -524 0 0 make-flipper       ( angles in milliradians, y down )
  140 212 2618 3665 1 1 make-flipper ;
60 constant flip-up-speed   30 constant flip-down-speed   ( mrad per step )

( ---- Keys ---- )

225 constant key-lshift   229 constant key-rshift
create prev-keys 8 allot   prev-keys 8 erase
: edge ( f n -- f )   ( true only when the key has just gone down )
  prev-keys + dup c@ >r over swap c! r> 0= and ;
0 value demo-left   0 value demo-right
0 value demo-hold   0 value demo-wait   0 value demo-side
: left-key? ( -- f )
  demo? if demo-left exit then
  [char] z pressed? key-left pressed? or key-lshift pressed? or ;
: right-key? ( -- f )
  demo? if demo-right exit then
  [char] m pressed? key-right pressed? or key-rshift pressed? or [char] / pressed? or ;
: plunger-key? ( -- f ) demo? if 0 exit then key-down pressed? key-space pressed? or ;

( ---- Sound and score ---- )

: snd ( freq ms -- ) drum-hit ;
: add ( n -- ) tilted if drop exit then +to score ;
: fanfare ( -- ) 523 120 snd 659 160 snd 784 220 snd ;

( ---- Collisions ---- )

0 value cx  0 value cy  0 value rs  0 value svx  0 value svy
0 value rest  0 value kick  0 value nx  0 value ny
: contact ( -- hit? )
  ( ball against the point cx cy, at distance rs; the surface moves at
    svx svy, gives back rest/256 of the speed and kicks by kick )
  ( distances are squared at 1/256 pixel, to stay within 32-bit cells on the web )
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

: closest ( x y dx dy len -- )   ( closest point to the ball on a segment )
  { ax ay sdx sdy len }
  bx ax - 4 arshift sdx *  by ay - 4 arshift sdy *  + 64 / 0 max len min { t }
  ax sdx t * 1024 / + to cx   ay sdy t * 1024 / + to cy ;

: target-hit ( s -- )
  0 swap s-up !  500 add  1 +to bonus  740 60 snd
  targets-down 4 = if
    mission 6 < if 1 +to mission then
    mission 5000 * add  fanfare  60 to target-timer
  then ;
: seg-hit ( s -- )
  { s }
  s s-kind @ 2 = s s-up @ 0= and if exit then
  s s-ax @ s s-ay @ s s-dx @ s s-dy @ s s-len @ closest
  0 to svx  0 to svy  radius to rs
  s s-kind @ case
    0 of bounce to rest 0 to kick endof
    1 of 120 to rest 900 to kick endof
    2 of 60 to rest 0 to kick endof
  endcase
  contact if
    s s-kind @ 1 = if 10 add 520 30 snd then
    s s-kind @ 2 = if s target-hit then
  then ;

: bumper-hit ( i -- )
  { i }
  i bumper-x px to cx   i bumper-y px to cy
  radius bumper-r px + to rs  0 to svx  0 to svy  150 to rest  1300 to kick
  contact if 100 add 1 +to bonus 8 i cells flashes + ! 330 50 snd then ;

: flipper-hit ( i -- )
  fl { f }
  f f-th @ cos-sin { c s }
  f f-px @ f f-py @ c s f f-len @ closest
  cy f f-py @ - negate f f-om @ * 1000 / to svx
  cx f f-px @ - f f-om @ * 1000 / to svy
  radius 3 px + to rs  50 to rest  0 to kick
  contact drop ;

: move-flipper ( i -- )
  fl { f }
  f f-side @ if right-key? else left-key? then tilted 0= and
  if f f-up @ flip-up-speed else f f-rest @ flip-down-speed then { goal speed }
  goal f f-th @ -  speed min  speed negate max { om }
  om f f-om !  om f f-th +! ;

: lane-sensors ( -- )
  by u>p dup 18 >= swap 30 <= and 0= if exit then
  3 0 do
    bx u>p 70 i 25 * + 95 i 25 * + within if
      i lane @ 0= if -1 i lane ! 50 add 880 40 snd then
    then
  loop
  0 lane @ 1 lane @ and 2 lane @ and if
    lanes 3 cells erase  mult 5 < if 1 +to mult then  1000 add  fanfare
  then ;

: clamp-speed ( -- )
  vx 4 arshift dup *  vy 4 arshift dup *  + isqrt 16 * { v }
  v vmax > if vx vmax * v / to vx  vy vmax * v / to vy then ;

: step ( -- )   ( one of the 6 steps of a frame )
  0 move-flipper  1 move-flipper
  gravity +to vy  vx +to bx  vy +to by  clamp-speed
  #segs 0 ?do i seg seg-hit loop
  3 0 do i bumper-hit loop
  0 flipper-hit  1 flipper-hit
  lane-sensors ;

( ---- Game flow ---- )

: in-lane? ( -- f ) bx u>p 196 > by u>p 215 > and ;
: new-ball ( -- )
  202 px to bx  230 px to by  0 to vx  0 to vy
  0 to tilt  0 to tilted  0 to bonus  0 to plunge  1 to state ;
: reset-table ( -- ) targets-up  lanes 3 cells erase  0 to target-timer ;
: new-game ( demo? -- )
  to demo?  0 to score  1 to ball#  0 to mission  1 to mult
  reset-table new-ball ;

: rotate-lanes ( right? -- )
  0 lane @ 1 lane @ 2 lane @ { a b c }
  if c a b else b c a then 2 lane ! 1 lane ! 0 lane ! ;

: launch ( -- )
  in-lane? if plunge 40 * 1500 + negate to vy  150 80 snd then  0 to plunge ;
: plunger ( -- )
  plunger-key? if plunge 3 + 100 min to plunge
  else plunge if launch then then ;

: nudge ( -- )
  [char] n pressed? 1 edge demo? 0= and tilted 0= and if
    frames 1 and if 500 else -500 then +to vx  -700 +to vy  45 +to tilt
    tilt 100 > if -1 to tilted 120 400 snd then
  then
  tilt 0> if -1 +to tilt then ;

: demo-play ( -- )   ( the machine plays: a short flip when the ball comes down near a flipper )
  in-lane? if 100 to plunge launch then
  demo-hold if
    -1 +to demo-hold  demo-hold 0= if 20 to demo-wait then
  else demo-wait if -1 +to demo-wait else
    by u>p 190 > by u>p 216 < and vy 0> and if
      bx u>p 104 < to demo-side  8 to demo-hold
    then
  then then
  demo-hold 0<> demo-side and to demo-left
  demo-hold 0<> demo-side 0= and to demo-right ;

: lose-ball ( -- )
  bonus 1000 * mult * add  110 400 snd  2 to state  90 to timer ;
: next-ball ( -- )
  demo? if 1 new-game exit then
  ball# balls < if 1 +to ball# new-ball exit then
  score high max to high  3 to state  180 to timer ;

: play ( -- )
  demo? if demo-play then
  left-key? 2 edge if 90 25 snd 0 rotate-lanes then
  right-key? 3 edge if 90 25 snd -1 rotate-lanes then
  plunger nudge
  6 0 do step loop
  target-timer if -1 +to target-timer target-timer 0= if targets-up then then
  by u>p 248 > if lose-ball then ;

create gravities 7 , 10 , 14 ,
create bounces 60 , 90 , 130 ,
create ball-counts 3 , 5 ,
: settings ( -- )
  [char] g pressed? 4 edge if setting-g 1+ 3 mod to setting-g then
  [char] e pressed? 5 edge if setting-e 1+ 3 mod to setting-e then
  [char] b pressed? 6 edge if setting-b 1+ 2 mod to setting-b then
  setting-g cells gravities + @ to gravity
  setting-e cells bounces + @ to bounce
  setting-b cells ball-counts + @ to balls ;

: start? ( -- f ) key-enter pressed? [char] 1 pressed? or 7 edge ;

: update ( -- )
  1 +to frames
  3 0 do i cells flashes + dup @ 1- 0 max swap ! loop
  demo? if settings  start? if 0 new-game exit then then
  state 1 = if play exit then
  state 2 = if -1 +to timer timer 0= if next-ball then exit then
  state 3 = if -1 +to timer timer 0= if 1 new-game then then ;

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
  n <# #s #> { a l }  a l x l 4 * s * - y s say ;

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

( ---- Drawing the table ---- )

$ffffff constant c-white   $40e040 constant c-green   $c050ff constant c-violet
$ff7020 constant c-orange  $30a0ff constant c-blue    $303030 constant c-dim

: thick-line ( x1 y1 x2 y2 -- ) { x1 y1 x2 y2 }
  x1 y1 x2 y2 line  x1 1+ y1 x2 1+ y2 line ;
: draw-seg ( s -- )
  { s }
  s s-kind @ case
    0 of c-white color s s-x1 @ s s-y1 @ s s-x2 @ s s-y2 @ line endof
    1 of c-orange color s s-x1 @ s s-y1 @ s s-x2 @ s s-y2 @ thick-line endof
    2 of s s-up @ if c-blue else c-dim then color
         s s-x1 @ s s-y1 @ s s-x2 @ s s-y2 @ thick-line endof
  endcase ;

: draw-bumper ( i -- )
  { i }  i bumper-x { x }  i bumper-y { y }
  i cells flashes + @ if c-white color x y bumper-r disc then
  c-violet color  x y bumper-r circle  x y 5 circle  x y 2 disc ;

: draw-flipper ( i -- )   ( discs from the pivot to the tip, thinner at the tip )
  fl { f }
  f f-th @ cos-sin { c s }
  f f-px @ u>p { x }  f f-py @ u>p { y }
  f f-len @ u>p { l }
  c-orange color
  l 1+ 0 do  x c i * 1024 / +  y s i * 1024 / +  3 i l 2/ > +  disc  loop ;

: draw-lanes ( -- )
  3 0 do
    i lane @ if c-green color 82 i 25 * + 40 3 disc
    else c-dim color 82 i 25 * + 40 3 circle then
  loop ;

: draw-stars ( -- )   ( the night over the target )
  c-dim color
  60 120 dot  150 130 dot  120 150 dot  84 160 dot  170 110 dot  48 60 dot  160 44 dot ;

: draw-plunger ( -- )
  c-white color  198 237 plunge 30 / + 8 2 box ;

: draw-ball ( -- ) c-white color bx u>p by u>p 3 disc ;

( ---- The panel ---- )

: rank ( n -- a n ) 1- 7 *  s" CADET  PILOT  CAPTAINMAJOR  COLONELACE    " drop +  7 ;

: draw-plane ( x y lit? -- )
  if c-green else c-dim then color
  { x y }  x y x 12 + y line  x 4 + y 3 - x 7 + y 3 + line  x y 2 - x y 2 + line ;

: draw-panel ( -- )
  c-violet color  216 0 216 240 line
  c-white color  s" NIGHT RAID" 228 6 2 say
  c-blue color   s" SCORE" 222 26 1 say
  c-orange color score 226 34 draw-score
  c-blue color   s" HIGH" 222 54 1 say
  c-white color  high 314 54 1 say-number
  c-blue color   s" BALL" 222 64 1 say
  c-white color  ball# 314 64 1 say-number
  c-blue color   s" BONUS" 222 74 1 say
  c-white color  bonus 1000 * 286 74 1 say-number  s" x" 290 74 1 say  mult 314 74 1 say-number
  c-blue color   s" MISSION" 222 90 1 say
  6 0 do  224 i 16 * +  104  i mission < draw-plane  loop
  mission if c-green color mission rank 222 112 2 say then
  tilted if frames 16 and if c-orange color s" TILT" 238 136 3 say then then
  state 2 = if c-white color s" BALL LOST" 222 136 2 say then
  state 3 = if c-orange color s" GAME OVER" 222 136 2 say then
  demo? state 3 <> and if
    frames 32 and if c-green color s" PRESS ENTER" 226 160 2 say then
    c-blue color   s" G GRAVITY" 222 190 1 say
    s" E BOUNCE" 222 200 1 say  s" B BALLS" 222 210 1 say
    c-white color
    setting-g 7 * s" LOW    MEDIUM HIGH   " drop + 7 286 190 1 say
    setting-e 7 * s" SOFT   MEDIUM HARD   " drop + 7 286 200 1 say
    balls 286 210 1 say-number
    s" Z M FLIP  SPACE SHOOT" 222 226 1 say
  then ;

: draw-all ( -- )
  black color cls
  draw-stars
  #segs 0 ?do i seg draw-seg loop
  3 0 do i draw-bumper loop
  draw-lanes  0 draw-flipper  1 draw-flipper  draw-plunger  draw-ball
  draw-panel
  flip ;

: frame ( -- )
  events
  key-esc pressed? if quit! then
  update draw-all
  dt 16 < if 16 dt - delay then ;

: run ( -- ) begin frame quit? until close-screen ;

build-table build-flippers
3 to zoom
view-w view-h screen
s" Night Raid" title
0 cursor
1 new-game
run
script-bye
