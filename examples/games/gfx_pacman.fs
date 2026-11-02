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

\ A Pac-Man game with SDL2, written from the rules of the arcade game as they
\ are widely described (the targets of the four ghosts, scatter and chase
\ turns, frightened ghosts, the tunnel); the maze is our own. Only libSDL2 is
\ needed (no fonts, images or sound files); the letters are a tiny 3x5 pixel
\ font and the sounds are beeps. One file, so that it runs the same on POSIX,
\ DOS (8.3 name GFX_PACM.FS) and the web (terminal.html?run=gfx_pacman.fs).
\   arrows or wasd   turn (the turn is kept until the maze allows it)
\   p                pause         esc   quit
\   space or enter   after the end: again
\ Eat the dots (10) and the four big pellets (50: the ghosts turn blue for a
\ while and are worth 200, 400, 800, 1600 in a row). Ghosts: red chases you,
\ pink aims ahead of you, cyan works with the red one, orange chases you
\ unless it is near. A free life at 10000.
\ All the positions are in pixels; a character takes one pixel a step, every
\ 16 ms whatever the speed of the screen, and decides where to go in the middle
\ of a tile.

sdl2

( ---- The maze: a template of tiles, and the working copy ---- )
19 constant pm-cols   21 constant pm-rows   10 constant pm-t   20 constant pm-top
9 constant pm-trow    \ the row of the tunnel: open on both sides
pm-cols pm-t * constant pm-w   pm-rows pm-t * pm-top + constant pm-h

create pm-tpl pm-cols pm-rows * allot
create pm-map pm-cols pm-rows * allot
0 value pm-nrow
: row, ( a n -- )   drop pm-tpl pm-nrow pm-cols * + pm-cols cmove   1 +to pm-nrow ;

\ # wall   . dot   o big pellet   - door of the house   P start of Pac-Man
s" ###################" row,
s" #........#........#" row,
s" #o##.###.#.###.##o#" row,
s" #.................#" row,
s" #.##.#.#####.#.##.#" row,
s" #....#...#...#....#" row,
s" ####.#########.####" row,
s"    #...........#   " row,
s" ####.####-####.####" row,
s"     .###   ###.    " row,
s" ####.#########.####" row,
s"    #.#########.#   " row,
s" ####.#########.####" row,
s" #........#........#" row,
s" #.##.###.#.###.##.#" row,
s" #o..#....P....#..o#" row,
s" ##.#.#.#####.#.#.##" row,
s" #....#...#...#....#" row,
s" #.######.#.######.#" row,
s" #.................#" row,
s" ###################" row,

: pm-at ( tx ty -- a ) pm-cols * + pm-map + ;

: pm-tile { tx ty -- c }   ( the character of a tile; outside is wall, but the tunnel row is open )
  ty 0< ty pm-rows >= or if 35 exit then
  tx 0< tx pm-cols >= or if ty pm-trow = if 32 else 35 then exit then
  tx ty pm-at c@ ;
: pm-solid? ( tx ty -- f )   pm-tile dup 35 = swap 45 = or ;   \ wall or door

( ---- The walls, as a few boxes: lines along the tiles next to a free one ---- )
400 constant pm-maxseg
create pm-seg pm-maxseg 4 * cells allot   0 value pm-nseg
: pm-seg+ ( x y w h -- )
  pm-nseg pm-maxseg >= if 2drop 2drop exit then
  pm-nseg 4 * cells pm-seg +  >r   r@ 3 cells + !  r@ 2 cells + !  r@ cell+ !  r> !
  1 +to pm-nseg ;

: pm-wall? { tx ty -- f }   ( a wall for the outline; outside is not )
  tx 0< ty 0< or tx pm-cols >= or ty pm-rows >= or if 0 exit then
  tx ty pm-at c@ 35 = ;

0 value pm-rs
: pm-hruns { dy off }   ( horizontal lines, a run of tiles at a time )
  pm-rows 0 do
    -1 to pm-rs
    pm-cols 1+ 0 do
      i pm-cols < if i j pm-wall? i j dy + pm-wall? 0= and else 0 then
      if pm-rs 0< if i to pm-rs then
      else pm-rs 0< 0= if pm-rs pm-t *  j pm-t * off +  i pm-rs - pm-t *  2  pm-seg+  -1 to pm-rs then then
    loop
  loop ;
: pm-vruns { dx off }   ( vertical lines )
  pm-cols 0 do
    -1 to pm-rs
    pm-rows 1+ 0 do
      i pm-rows < if j i pm-wall? j dx + i pm-wall? 0= and else 0 then
      if pm-rs 0< if i to pm-rs then
      else pm-rs 0< 0= if j pm-t * off +  pm-rs pm-t *  2  i pm-rs - pm-t *  pm-seg+  -1 to pm-rs then then
    loop
  loop ;
: pm-build-walls ( -- )
  0 to pm-nseg
  -1 0 pm-hruns   1 8 pm-hruns   -1 0 pm-vruns   1 8 pm-vruns ;

( ---- The characters: 0 is Pac-Man, 1 to 4 the ghosts ---- )
5 constant pm-n
: pm-array ( "name" -- )   create pm-n cells allot  does> swap cells + ;
pm-array e-x      pm-array e-y      pm-array e-dir   \ pixels, direction 0 right 1 down 2 left 3 up
pm-array e-acc    pm-array e-st     pm-array e-fr
pm-array e-rev    pm-array e-rel
\ a ghost: e-st 0 in the house, 1 leaving it, 2 out, 3 eyes going back, 4 eyes entering
\ e-fr frightened, e-rev must turn round at the next tile, e-rel when it leaves (in steps)

create pm-dx 1 , 0 , -1 , 0 ,
create pm-dy 0 , 1 , 0 , -1 ,
: dxy ( d -- dx dy ) dup cells pm-dx + @ swap cells pm-dy + @ ;
: opposite ( d -- d' ) 2 + 3 and ;

0 value pm-score   0 value pm-best   3 value pm-lives   1 value pm-level
0 value pm-state    \ 0 ready, 1 playing, 2 dying, 3 game over, 4 level clear
0 value pm-timer    \ steps left in the state 0, 2, 4
0 value pm-clock    \ steps of play since the start of the life
0 value pm-want     \ the direction asked for with the keys
0 value pm-dots     0 value pm-fright   0 value pm-chain
0 value pm-mode     \ 0 scatter, 1 chase
0 value pm-sched    \ how many switches of mode were made
0 value pm-paused   0 value pm-frame    0 value pm-extra

create pm-switch 440 , 1690 , 2130 , 3380 , 3690 , 4940 , 5250 , 999999 ,

: tile-x ( i -- tx ) e-x @ pm-t / ;
: tile-y ( i -- ty ) e-y @ pm-t / ;
: centred? ( i -- f ) dup e-x @ pm-t mod 5 =  swap e-y @ pm-t mod 5 = and ;

: place-tile ( tx ty i -- ) >r  pm-t * 5 +  swap pm-t * 5 + swap  r@ e-y !  r@ e-x !  r> drop ;

( ---- Starting a level or a life ---- )
: reset-characters ( -- )
  9 15 0 place-tile   2 0 e-dir !   2 to pm-want   0 0 e-acc !
  9 7 1 place-tile    2 1 e-dir !   2 1 e-st !
  9 9 2 place-tile    0 2 e-dir !   0 2 e-st !   0 2 e-rel !
  8 9 3 place-tile    3 3 e-dir !   0 3 e-st !   250 3 e-rel !
  10 9 4 place-tile   3 4 e-dir !   0 4 e-st !   500 4 e-rel !
  5 1 do 0 i e-acc !  0 i e-fr !  0 i e-rev !  loop
  0 to pm-clock   0 to pm-sched   0 to pm-mode   0 to pm-fright   0 to pm-chain ;

: count-dots ( -- n )
  0  pm-cols pm-rows * 0 do  pm-map i + c@ dup 46 = swap 111 = or if 1+ then  loop ;

: new-maze ( -- )
  pm-tpl pm-map pm-cols pm-rows * cmove
  9 15 pm-at 32 swap c!      \ the P of the template is an empty tile
  count-dots to pm-dots
  pm-build-walls ;

: ready ( -- )   reset-characters   0 to pm-state   120 to pm-timer ;

: new-game ( -- )
  0 to pm-score  3 to pm-lives  1 to pm-level  0 to pm-extra
  new-maze ready ;

: next-level ( -- )   1 +to pm-level  new-maze ready ;

( ---- Speeds: sixteenths of a pixel a step ---- )
: ghost-speed ( i -- s )
  dup e-st @ 3 >= if drop 32 exit then
  dup e-st @ 2 < if drop 12 exit then
  e-fr @ if 8 exit then
  pm-level 4 / 2 min  14 + ;
: speed ( i -- s ) dup 0= if drop 16 else ghost-speed then ;

( ---- Moving ---- )
: ent-tile ( i -- tx ty ) dup tile-x swap tile-y ;

: move1 ( i -- )   ( one pixel in its direction, round the edges )
  >r  r@ e-dir @ dxy
  r@ e-y @ + r@ e-y !
  r@ e-x @ + pm-w + pm-w mod r@ e-x !
  r> drop ;

: free-ahead? ( i d -- f )   ( the tile next to i in the direction d can be entered )
  >r ent-tile r> dxy rot + >r + r> pm-solid? 0= ;

: sq ( n -- n*n ) dup * ;

( ---- Pac-Man ---- )
: beep-dot ( -- )   pm-dots 1 and if 440 25 else 523 25 then beep ;

: eat-here ( -- )   ( the tile of Pac-Man )
  0 ent-tile pm-at  dup c@
  dup 46 = if drop 32 swap c!  10 +to pm-score  -1 +to pm-dots  beep-dot exit then
  111 = if
    32 swap c!  50 +to pm-score  -1 +to pm-dots  0 to pm-chain
    300 pm-level 30 * - 100 max to pm-fright
    5 1 do i e-st @ 2 = if 1 i e-fr !  1 i e-rev ! then loop
    196 120 beep exit
  then drop ;

: pac-pixel ( -- )
  0 centred? if
    eat-here
    0 pm-want free-ahead? if pm-want 0 e-dir ! then
    0 0 e-dir @ free-ahead? 0= if exit then
  else
    pm-want 0 e-dir @ opposite = if pm-want 0 e-dir ! then
  then
  0 move1 ;

( ---- Ghosts: where they aim at ---- )
create corner-x 17 , 1 , 17 , 1 ,
create corner-y -2 , -2 , 22 , 22 ,
create ghost-colors $ff0000 , $ffb8ff , $00ffff , $ffb852 ,

0 value tg-x   0 value tg-y   0 value ah-n

: pac-ahead ( n -- tx ty )   ( the tile n tiles in front of Pac-Man )
  to ah-n  0 ent-tile  0 e-dir @ dxy
  ah-n * >r  ah-n * >r  swap r> + swap r> + ;

: ghost-corner ( i -- )   ( the corner of the ghost i, outside the maze )
  1- dup cells corner-x + @ to tg-x  cells corner-y + @ to tg-y ;

: chase-target ( i -- )
  dup 1 = if drop 0 ent-tile to tg-y to tg-x exit then              \ red: Pac-Man himself
  dup 2 = if drop 4 pac-ahead to tg-y to tg-x exit then             \ pink: 4 tiles ahead
  dup 3 = if drop 2 pac-ahead 2* 1 tile-y - to tg-y  2* 1 tile-x - to tg-x exit then   \ cyan: through the red one
  dup tile-x 0 tile-x - sq  over tile-y 0 tile-y - sq +  64 < if ghost-corner exit then   \ orange: near, it runs away
  drop 0 ent-tile to tg-y to tg-x ;

: ghost-target ( i -- )
  dup e-st @ 3 = if drop 9 to tg-x 7 to tg-y exit then              \ eyes: the door
  pm-mode 0= if ghost-corner exit then
  chase-target ;

( ---- Ghosts: the choice at a crossing ---- )
0 value gc-i   0 value gc-best   0 value gc-dist   0 value gc-d

: try-dir ( d -- )   ( the nearest to the target wins, a ghost never turns back )
  >r
  gc-i r@ free-ahead? 0= if rdrop exit then
  r@ gc-i e-dir @ opposite = if rdrop exit then
  gc-i ent-tile r@ dxy rot + >r + r>
  tg-y - sq swap tg-x - sq +
  dup gc-dist < if to gc-dist r@ to gc-best else drop then
  rdrop ;

: random-dir ( -- )   ( frightened: any way but back )
  -1 to gc-best   4 random to gc-d
  4 0 do
    gc-best 0< if
      gc-d i + 3 and
      dup gc-i swap free-ahead?  over gc-i e-dir @ opposite <> and
      if to gc-best else drop then
    then
  loop ;

: choose-dir ( i -- )
  to gc-i
  gc-i e-rev @ if gc-i e-dir @ opposite gc-i e-dir !  0 gc-i e-rev !  exit then
  -1 to gc-best
  gc-i e-fr @ if random-dir
  else
    gc-i ghost-target  99999 to gc-dist
    3 try-dir 2 try-dir 1 try-dir 0 try-dir      \ up, left, down, right win a tie
  then
  gc-best 0< if gc-i e-dir @ opposite to gc-best then
  gc-best gc-i e-dir ! ;

( ---- Ghosts: in the house, out, eyes ---- )
: leave-step ( i -- )   ( to the door, then up to the corridor )
  >r
  r@ e-x @ 95 < if 0 r@ e-dir ! r> move1 exit then
  r@ e-x @ 95 > if 2 r@ e-dir ! r> move1 exit then
  r@ e-y @ 75 > if 3 r@ e-dir ! r> move1 exit then
  2 r@ e-st !  2 r@ e-dir !  rdrop ;

: enter-step ( i -- )   ( the eyes go down in the house and the ghost is whole again )
  >r
  r@ e-y @ 95 < if 1 r@ e-dir ! r> move1 exit then
  1 r@ e-st !  0 r> e-fr ! ;

: ghost-pixel ( i -- )
  >r
  r@ e-st @
  dup 0 = if drop rdrop exit then
  dup 1 = if drop r> leave-step exit then
  dup 4 = if drop r> enter-step exit then
  drop
  r@ centred? if
    r@ e-st @ 3 = r@ tile-x 9 = and r@ tile-y 7 = and if 4 r> e-st ! exit then
    r@ choose-dir
  then
  r> move1 ;

: advance ( i -- )   ( the pixels it takes this step )
  dup speed over e-acc @ +  over e-acc !
  begin dup e-acc @ 16 >= while
    dup e-acc @ 16 - over e-acc !
    dup 0= if pac-pixel else dup ghost-pixel then
  repeat drop ;

( ---- Contacts ---- )
: near? ( i -- f )
  dup e-x @ 0 e-x @ - abs 6 <  swap e-y @ 0 e-y @ - abs 6 < and ;

: eat-ghost ( i -- )
  3 over e-st !  0 over e-fr !  drop
  200 pm-chain 3 min lshift +to pm-score   1 +to pm-chain
  523 60 beep  784 60 beep ;

: die ( -- )
  2 to pm-state   90 to pm-timer   -1 +to pm-lives
  pm-lives 0= if pm-score pm-best max to pm-best then
  392 100 beep  330 100 beep  262 200 beep ;

: collide ( -- )
  5 1 do
    pm-state 1 = if
      i e-st @ 2 = i near? and if
        i e-fr @ if i eat-ghost else die then
      then
    then
  loop ;

( ---- The clock of a life ---- )
: release-ghosts ( -- )
  5 2 do i e-st @ 0 = if pm-clock i e-rel @ >= if 1 i e-st ! then then loop ;

: update-mode ( -- )   ( scatter and chase take turns; the ghosts turn round at each change )
  pm-sched cells pm-switch + @  pm-clock = if
    1 pm-mode - to pm-mode   1 +to pm-sched
    5 1 do i e-st @ 2 = if 1 i e-rev ! then loop
  then ;

: update-fright ( -- )
  pm-fright 0> if
    -1 +to pm-fright
    pm-fright 0= if 5 1 do 0 i e-fr ! loop then
  then ;

: play-step ( -- )
  1 +to pm-clock
  update-mode update-fright release-ghosts
  5 0 do i advance loop
  collide
  pm-state 1 = pm-dots 0= and if 4 to pm-state 100 to pm-timer 523 100 beep 659 100 beep 784 200 beep then
  pm-score 10000 >= pm-extra 0= and if 1 to pm-extra 1 +to pm-lives 880 200 beep then ;

: step ( -- )   ( one step of the game, 16 ms )
  1 +to pm-frame
  pm-paused if exit then
  pm-state 0 = if -1 +to pm-timer pm-timer 0= if 1 to pm-state then exit then
  pm-state 1 = if play-step exit then
  pm-state 2 = if
    -1 +to pm-timer
    pm-timer 0= if pm-lives 0= if 3 to pm-state else ready then then
    exit
  then
  pm-state 4 = if -1 +to pm-timer pm-timer 0= if next-level then then ;

( ---- Keys ---- )
create was 256 allot   was 256 erase
: just ( k -- f )   ( pressed now, not at the last look )
  dup pressed? swap 255 and was + 2dup c@ 0= and >r c! r> ;

: read-keys ( -- )
  key-right pressed? [char] d pressed? or if 0 to pm-want then
  key-down pressed? [char] s pressed? or if 1 to pm-want then
  key-left pressed? [char] a pressed? or if 2 to pm-want then
  key-up pressed? [char] w pressed? or if 3 to pm-want then
  [char] p just if pm-state 2 < if 1 pm-paused - to pm-paused then then
  pm-state 3 = if
    key-space just key-enter just or [char] r just or if new-game then
  then ;

( ---- Drawing: a tiny 3x5 pixel font ---- )
create pm-digits
  $7b6f , $2c97 , $73e7 , $73cf , $5bc9 , $79cf , $79ef , $7249 , $7bef , $7bcf ,
create pm-font   ( A to Z )
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
    n 10 mod cells pm-digits + @   x digits 1- i - s 4 * * +   y s draw-bits
    n 10 / to n
  loop ;

: glyph ( c -- bits )
  dup 33 = if drop $2482 exit then
  32 or dup 97 < over 122 > or if drop 0 exit then
  97 - cells pm-font + @ ;

: draw-text { a n x y s }
  n 0 do  a i + c@ glyph   x i 4 s * * +   y s draw-bits  loop ;

: banner { a n rgb }   ( a word in the middle, over the black of the house )
  black color   45 pm-top 100 + 2 +  100 16 box
  rgb color  a n  pm-w n 8 * 2 - - 2 /  pm-top 105 +  2 draw-text ;

( ---- Drawing: the maze and the characters ---- )
: seg@ ( i -- x y w h )
  4 * cells pm-seg + >r  r@ @  r@ cell+ @ pm-top +  r@ 2 cells + @  r> 3 cells + @ ;

: draw-walls ( -- )
  pm-state 4 = if pm-timer 8 / 1 and if white else $2121de then else $2121de then color
  pm-nseg 0 do  i seg@ box  loop
  $ffb8de color   9 pm-t * 8 pm-t * pm-top + 4 +  pm-t 2 box ;    \ the door of the house

: draw-dots ( -- )
  $ffb8ae color
  pm-rows 0 do
    pm-cols 0 do
      i j pm-at c@
      dup 46 = if drop  i pm-t * 4 +  j pm-t * pm-top + 4 +  2 2 box
      else 111 = if
        pm-frame 20 / 1 and 0= if  i pm-t * 5 +  j pm-t * pm-top + 5 +  3 disc  then
      then then
    loop
  loop ;

: draw-pac ( -- )
  yellow color
  pm-state 2 = if
    pm-timer 4 * 90 /  dup if 0 e-x @ 0 e-y @ pm-top +  rot disc else drop then exit
  then
  0 e-x @  0 e-y @ pm-top +  4 disc
  pm-frame 3 rshift 3 and  { open }       \ the mouth: 0 to 3 pixels either side
  0 e-x @  0 e-y @ pm-top +  { x y }
  black color
  0 e-dir @
  dup 0 = if drop  x   y open -   5 open 2* 1+ box exit then
  dup 2 = if drop  x 4 -  y open -  5 open 2* 1+ box exit then
  dup 1 = if drop  x open -  y   open 2* 1+ 5 box exit then
  drop  x open -  y 4 -  open 2* 1+ 5 box ;

: ghost-color ( i -- rgb )
  dup e-fr @ if
    drop  pm-fright 120 < pm-frame 8 / 1 and and if $ffffff else $2121ff then exit
  then
  1- cells ghost-colors + @ ;

0 value gx   0 value gy
: draw-ghost { g }
  g e-x @ to gx   g e-y @ pm-top + to gy
  g e-st @ 3 < if
    g ghost-color color
    gx gy 1- 4 disc   gx 4 -  gy 1-  9 5 box
    black color   gx 3 -  gy 4 +  2 1 box   gx 1 +  gy 4 +  2 1 box     \ the notches of the feet
  then
  g e-fr @ if
    white color   gx 3 -  gy 2 +  7 1 box
  else
    white color   gx 3 -  gy 3 -  3 3 box   gx 1 +  gy 3 -  3 3 box
    $2121ff color
    g e-dir @ dxy  gy 2 - +  swap  gx 2 - +  swap          \ the pupils look ahead
    2dup 1 1 box  swap 4 + swap 1 1 box
  then ;

: draw-lives ( -- )
  yellow color
  pm-lives 1- 0 max 0 ?do  pm-w 8 - i 10 * -  10 3 disc  loop ;

: draw-all ( -- )
  black color cls
  draw-walls draw-dots
  1 draw-ghost 2 draw-ghost 3 draw-ghost 4 draw-ghost
  draw-pac
  white color  pm-score 6 4 4 2 draw-number
  $808080 color  pm-best 6 pm-w 2 / 12 - 4 2 draw-number
  draw-lives
  pm-state 0 = if s" READY!" yellow banner then
  pm-state 3 = if s" GAME OVER" red banner then
  pm-paused if s" PAUSED" white banner then
  flip ;

( ---- The loop ---- )
0 value pm-last
: run-steps ( -- )
  pm-paused if ticks to pm-last exit then
  ticks pm-last - 160 > if ticks 16 - to pm-last then      \ a slow screen: the game slows down, it does not jump
  begin ticks pm-last - 16 >= while  16 +to pm-last  step  repeat ;

: one-frame ( -- )
  events
  key-esc pressed? if quit! then
  read-keys run-steps draw-all
  dt 16 < if 16 dt - delay then ;

: run ( -- ) begin one-frame quit? until close-screen ;

[defined] pacman-no-run [if] [else]
ms-ticks 1 or rand-seed !
3 to zoom
pm-w pm-h screen
s" Pac-Man" title
0 cursor
new-game
ticks to pm-last
run
script-bye
[then]
