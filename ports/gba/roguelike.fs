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

\ A GBA port of examples/games/sdl2_roguelike.fs. The game logic (room/corridor
\ generation, turn-based combat, monster AI, potions/stairs, line of sight)
\ is untouched Forth, copied straight from there. Everything that touched
\ SDL2 -- drawing, input, sound -- is rewritten against ports/gba/platform.fs's
\ mode-3 framebuffer and the d-pad/buttons. No music yet (see the ABC tunes
\ still sitting in the original file): a from-scratch GBA sound backend is
\ its own separate piece of work, deferred on purpose.
\
\ Differences from the SDL2 version, all for GBA hardware/software reasons,
\ see ports/gba/README.md's "sdl2_roguelike.fs port" section for the long version:
\   - The map is still 40x24 tiles, but the GBA screen only fits 30x18 map
\     tiles (plus a 2-tile status bar) at native 8px, so the view scrolls,
\     following the player, instead of showing the whole map at once.
\   - Remembered-but-not-visible tiles are dimmed with a real per-channel
\     tint (`tint` in platform.fs), same idea as the original's `image-tint`.
\   - No alpha blending in a 16-bit framebuffer with no blit hardware set up
\     here yet, so the translucent "hurt" screen flash is dropped; the
\     player tile itself still flashes red (draw-player), which was always
\     the primary feedback.
\   - Mouse click-to-walk has no GBA equivalent and is dropped. Keys move to
\     the d-pad; B waits a turn; START restarts after dying (there is no Esc
\     to quit -- a cartridge has nowhere to quit to, see platform.fs).
\   - This is still the mode-3 software blit from the tile demo, not the
\     GBA's real tile/sprite hardware, so full-screen redraws are not fast;
\     fine for a turn-based game that only redraws on input.

also gba

40 constant map-w   24 constant map-h   8 constant tile   map-w map-h * constant area
create map area allot      ( 0 wall, 1 floor, 2 stairs, 3 potion )
create seen area allot     ( explored )
create vis area allot      ( in view now )
create room-cx 12 cells allot   create room-cy 12 cells allot
create mons-data 24 3 * cells allot   ( x y hp of each monster )

0 value rooms   0 value mons
0 value px   0 value py   0 value hp   10 constant max-hp
0 value level   0 value dead   0 value flash   0 value rng
0 value repeat-at

( ---- Random numbers and map access, unchanged from the SDL2 version ---- )
: rnd ( n -- 0..n-1 )
  rng 1103515245 * 12345 + $7fffffff and dup to rng   16 rshift swap mod ;

: in-map? ( x y -- f )
  dup 0 >= swap map-h < and  swap dup 0 >= swap map-w < and  and ;
: tile@ ( x y -- n ) 2dup in-map? if map-w * + map + c@ else 2drop 0 then ;
: tile! ( n x y -- ) map-w * + map + c! ;
: carve ( x y -- ) 1 -rot tile! ;
: visible? ( x y -- f ) map-w * + vis + c@ 0<> ;
: dir ( n -- -1 | 0 | 1 ) dup 0< if drop -1 exit then 0 > if 1 else 0 then ;

( ---- Monsters ---- )
: mon ( i -- a ) 3 cells * mons-data + ;
: mx ( i -- x ) mon @ ;         : my ( i -- y ) mon cell+ @ ;
: mhp ( i -- hp ) mon 2 cells + @ ;
: mx! ( x i -- ) mon ! ;        : my! ( y i -- ) mon cell+ ! ;
: mhp! ( hp i -- ) mon 2 cells + ! ;

: mon-at ( x y -- i | -1 )   ( the living monster on a tile )
  { x y }  -1 { found }
  mons 0 ?do
    i mx x =  i my y = and  i mhp 0 > and if i to found then
  loop
  found ;

( ---- Building a level ---- )
: dig-room { x y w h }
  h 0 do w 0 do  x i + y j + carve  loop loop ;
: dig-h ( x1 x2 y -- ) { x1 x2 y }
  x1 x2 max 1+   x1 x2 min ?do i y carve loop ;
: dig-v ( y1 y2 x -- ) { y1 y2 x }
  y1 y2 max 1+   y1 y2 min ?do x i carve loop ;

: add-room
  6 rnd 4 + { w }   4 rnd 3 + { h }
  map-w w - 2 - rnd 1 + { x }   map-h h - 2 - rnd 1 + { y }
  x y w h dig-room
  x w 2 / + { cx }   y h 2 / + { cy }
  rooms 0 > if
    rooms 1 - cells room-cx + @   cx   rooms 1 - cells room-cy + @   dig-h
    rooms 1 - cells room-cy + @   cy   cx   dig-v
  then
  cx rooms cells room-cx + !   cy rooms cells room-cy + !
  1 +to rooms ;

: free? { x y }   ( a plain floor tile with nobody on it )
  x y tile@ 1 =   x y mon-at 0<  and   x px = y py = and 0=  and ;
: random-floor ( -- x y )
  begin  map-w rnd map-h rnd 2dup free? 0=  while  2drop  repeat ;

: add-monster
  random-floor  mons my!  mons mx!
  2 rnd 2 + level 3 / +  mons mhp!
  1 +to mons ;

: far-room ( -- i )   ( the room whose center is farthest from the first one )
  0 { best }   0 { far }
  rooms 1 ?do
    i cells room-cx + @ room-cx @ - abs   i cells room-cy + @ room-cy @ - abs +
    dup far > if to far i to best else drop then
  loop
  best ;

: new-level
  map area erase   seen area erase   vis area erase
  0 to rooms   0 to mons
  6 rnd 6 + 0 do add-room loop
  far-room dup cells room-cx + @  swap cells room-cy + @   2 -rot tile!
  room-cx @ to px   room-cy @ to py
  level 3 + 12 min 0 do add-monster loop
  2 rnd 2 + 0 do random-floor 3 -rot tile! loop ;

( ---- Line of sight ---- )
: clear-line? { x0 y0 x1 y1 }   ( no wall between two tiles )
  x1 x0 - { dx }   y1 y0 - { dy }
  dx abs dy abs max { n }
  n 2 < if -1 exit then
  -1 { ok }
  n 1 ?do
    x0 dx i * n / +   y0 dy i * n / +   tile@ 0= if 0 to ok then
  loop
  ok ;

: see-cell { x y }
  x y in-map? 0= if exit then
  x px - dup *   y py - dup * +   64 > if exit then
  px py x y clear-line? if
    1 x y map-w * + vis + c!   1 x y map-w * + seen + c!
  then ;

: (update-fov) ( -- )
  vis area erase
  17 0 do 17 0 do  px i + 8 -  py j + 8 -  see-cell  loop loop ;

( ---- Turns ---- )
: hurt ( n -- )   ( no `beep`/music here: see the file header )
  negate +to hp   6 to flash
  hp 1 < if 1 to dead then ;

: mon-step { i nx ny }   ( moves a monster when the way is free )
  nx ny tile@ 0= if 0 exit then
  nx px = ny py = and if 0 exit then
  nx ny mon-at 0< 0= if 0 exit then
  nx i mx!   ny i my!   -1 ;

: monster-act { i }
  i mhp 1 < if exit then
  i mx { x }   i my { y }
  x y visible? 0= if exit then
  px x - { dx }   py y - { dy }
  dx abs dy abs + 1 = if  2 rnd 1 + level 4 / +  hurt exit then
  dx abs dy abs >= if
    i  x dx dir +  y  mon-step 0= if i x  y dy dir +  mon-step drop then
  else
    i  x  y dy dir +  mon-step 0= if i  x dx dir +  y  mon-step drop then
  then ;

: monsters-act   mons 0 ?do dead 0= if i monster-act then loop ;

: start-level
  new-level (update-fov) ;

: new-game
  1 to level   max-hp to hp   0 to dead   0 to flash
  ms-ticks to rng   start-level ;

: next-level
  1 +to level   hp 3 + max-hp min to hp   start-level ;

: attack ( i -- )
  { i }  3 rnd 1 + { d }
  i mhp d - i mhp! ;

: try-step { dx dy }   ( -- acted? )
  px dx + { nx }   py dy + { ny }
  nx ny mon-at dup 0< 0= if attack -1 exit then drop
  nx ny tile@ 0= if 0 exit then
  nx to px   ny to py
  px py tile@ 3 = if
    1 px py tile!   hp 5 + max-hp min to hp
  then
  px py tile@ 2 = if next-level 0 exit then
  -1 ;

-1 value dirty   ( redraw needed? starts true so the first frame draws )

: act ( dx dy -- )   ( one turn: the player, then the monsters )
  try-step if (update-fov) monsters-act -1 to dirty then ;

( ---- Keys: d-pad + A/B, no mouse, no letter keys ---- )
0 value cur-keys   0 value prev-keys

: poll-keys
  cur-keys to prev-keys
  REG_KEYINPUT h@ invert $3ff and to cur-keys ;
: key-down? ( mask -- f )   cur-keys and 0<> ;
: key-edge? ( mask -- f )   dup key-down? swap prev-keys and 0= and ;

: key-step? ( mask -- f )   ( pressed now, or held long enough to repeat )
  dup key-edge? if drop ms-ticks 250 + to repeat-at -1 exit then
  dup key-down? 0= if drop 0 exit then
  drop ms-ticks repeat-at - 0< if 0 exit then
  ms-ticks 90 + to repeat-at -1 ;

: handle-keys
  dead if
    KEY_START key-edge? if new-game -1 to dirty then
    exit
  then
  KEY_LEFT key-step?   if -1  0 act then
  KEY_RIGHT key-step?  if  1  0 act then
  KEY_UP key-step?     if  0 -1 act then
  KEY_DOWN key-step?   if  0  1 act then
  KEY_B key-step?      if  0  0 act then   ( wait a turn )
;

( ---- Drawing: mode 3, camera follows the player, view-w x view-map-h
  tiles of map plus a 2-tile status bar -- see the file header. ---- )
30 constant view-w   18 constant view-map-h
0 value cam-x   0 value cam-y

: update-camera
  px view-w 2/ -  0 max  map-w view-w - min  to cam-x
  py view-map-h 2/ -  0 max  map-h view-map-h - min  to cam-y ;

$ffffff color15 constant WHITE15
$707090 color15 constant DIM15      ( remembered-but-not-visible tint )
$ff6060 color15 constant HURT15
$0c0e18 color15 constant BAR-BG15
$402020 color15 constant EMPTY-HEART15
$000000 color15 constant BLACK15
$400000 color15 constant DEAD-BG15

: view-tint ( visible? -- tint )   if WHITE15 else DIM15 then ;

: draw-cell { x y }
  x y map-w * + { k }
  k seen + c@ 0= if exit then
  x cam-x -   y cam-y -   { vx vy }
  vx 0 < vx view-w >= or   vy 0 < vy view-map-h >= or   or if exit then
  vx tile-size *   vy tile-size *   { sx sy }
  k vis + c@ view-tint { tnt }
  x y tile@ { t }
  t 0= if 0 sx sy tnt draw-tile-tinted exit then
  1 sx sy tnt draw-tile-tinted
  t 2 = if 2 sx sy tnt draw-tile-tinted then
  t 3 = if 3 sx sy tnt draw-tile-tinted then ;

: draw-map
  view-map-h 0 do
    view-w 0 do
      cam-x i +   cam-y j +   draw-cell
    loop
  loop ;

\ A separate word, not inlined into the loop below: a `{ local }` declared
\ directly inside a do-loop body pushes onto the return stack (see
\ common/locals.fs's `(local)`, `postpone >r`) every time the loop comes
\ back around, but is only unwound once, at this word's own `;` -- not per
\ iteration -- so it silently corrupts the do-loop's own index/limit on the
\ same stack. A plain word call each iteration is its own scope and cleans
\ up correctly at its own `;`; only found this by bisecting a bus error
\ with checkpoints (see SKILLS.md).
: draw-monster { i }
  i mhp 0 > 0= if exit then
  i mx i my visible? 0= if exit then
  i mx cam-x -   i my cam-y -   { vx vy }
  vx 0 >= vx view-w < and   vy 0 >= vy view-map-h < and   and if
    5 vx tile-size * vy tile-size * WHITE15 draw-tile-tinted
  then ;

: draw-monsters   mons 0 ?do i draw-monster loop ;

: draw-player
  px cam-x -  tile-size *   py cam-y -  tile-size *   { sx sy }
  flash 0 > if HURT15 else WHITE15 then { tnt }
  4 sx sy tnt draw-tile-tinted ;

create digit-bits
  $7b6f , $2c97 , $73e7 , $73cf , $5bc9 , $79cf , $79ef , $7249 , $7bef , $7bcf ,
: draw-digit { d x y s c }
  d cells digit-bits + @ { bits }
  15 0 do
    bits 14 i - rshift 1 and if
      x i 3 mod s * +   y i 3 / s * +   s s c gba-box
    then
  loop ;
: draw-number { n x y s c }   ( one or two digits )
  n 10 / dup if  x y s c draw-digit  else drop then
  n 10 mod  x 4 s * +  y s c draw-digit ;

: draw-bar
  0  view-map-h tile-size *   gba-width  16  BAR-BG15  gba-box
  max-hp 0 do
    ( no `{ }` local here -- see draw-monster's comment above: this is a
      do-loop body, and the tint is just left on the stack instead )
    7   4 i 9 * +   view-map-h tile-size * 4 +
    i hp < if WHITE15 else EMPTY-HEART15 then
    draw-tile-tinted
  loop
  2  200  view-map-h tile-size * 4 +  WHITE15 draw-tile-tinted
  level  212  view-map-h tile-size * 4 +  2  WHITE15 draw-number ;

: draw-end
  0 0 gba-width gba-height DEAD-BG15 gba-box
  6  116  60  WHITE15 draw-tile-tinted
  level  108  76  2  WHITE15 draw-number ;

: draw-all
  update-camera
  BLACK15 gba-cls
  draw-map draw-monsters draw-player draw-bar
  dead if draw-end then ;

\ draw-all is a full clear-then-redraw of the whole viewport; calling it
\ every loop iteration regardless of input, on a single-buffered mode-3
\ framebuffer, showed up as constant flicker even standing still -- the
\ user caught this on real mgba. Only redraw when something visibly
\ changed: a turn happened (act, above) or the hurt-flash is still
\ counting down.
: step
  poll-keys
  handle-keys
  flash 0 > if -1 to dirty  -1 +to flash then
  dirty if draw-all  0 to dirty then
  pause ;

: run   begin step again ;

gba-mode3
previous

." arrows: move and hit   B: wait   START: play again after dying" cr
new-game

' run
