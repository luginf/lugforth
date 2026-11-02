\ Tests of the logic of gfx_tetris.fs, gfx_snake.fs, gfx_invaders.fs, gfx_minesweeper.fs, gfx_mastermind.fs, gfx_pacman.fs and gfx_digger.fs,
\ loaded without their game loop:  lugforth examples/games/games_tests.fs
\ (the output ends with PASS). Needs libSDL2, but opens no window.

variable tn   variable tfail
: t= ( actual expected -- )
  1 tn +!  2dup <> if
    ." FAIL test " tn @ . ." got " over . ." expected " dup . cr  1 tfail +!
  then 2drop ;

( ---- Tetris ---- )
: tetris-no-run ;
include gfx_tetris.fs
1 rand-seed !

: fill-row ( y n -- )   ( the n first cells of row y )
  0 ?do 1 i 2 pick grid-at c! loop drop ;
: row-count ( y -- n ) 0 swap  cols 0 do i over grid-at c@ if swap 1+ swap then loop drop ;

new-game   score 0 t=   state 0 t=   kind 1 8 within -1 t=   fits? -1 t=
-1 0 solid? -1 t=   cols 0 solid? -1 t=   0 rows solid? -1 t=   0 -1 solid? 0 t=   0 0 solid? 0 t=
\ an I lying on the floor fills the last row: one line, 40 points
new-game  19 6 fill-row   1 to kind  0 to turn  6 to px  18 to py   fits? -1 t=
lock   cleared 1 t=   score 40 t=   lines 1 t=   19 row-count 0 t=
\ the rows above come down
new-game  1 0 18 grid-at c!   19 cols fill-row   clear-rows   cleared 1 t=   0 19 grid-at c@ 1 t=   18 row-count 0 t=
\ four lines at once: 1200
new-game  16 9 fill-row 17 9 fill-row 18 9 fill-row 19 9 fill-row
1 to kind  1 to turn  7 to px  16 to py   fits? -1 t=   lock  cleared 4 t=  score 1200 t=
\ moves stop at the walls, turns are kicked off them
new-game  3 to kind  0 to turn  0 to px  5 to py   -1 0 0 try 0 t=   px 0 t=
1 0 0 try -1 t=   px 1 t=
new-game  1 to kind  1 to turn  -2 to px  5 to py   fits? -1 t=   rotate  turn 2 t=  fits? -1 t=
\ falling stops on the floor, then a new piece comes
new-game  hard-drop   19 row-count 0 > -1 t=   py 0 t=
\ no room for a new piece: the game is over
new-game  0 cols fill-row  1 cols fill-row  spawn  state 2 t=
level 0 t=   100 to lines  level 10 t=   fall-ms 100 t=
depth 0 t=

( ---- Snake ---- )
: snake-no-run ;
include gfx_snake.fs
1 rand-seed !

new-game   len 5 t=   0 at 11 cols * 8 + t=   state 0 t=
advance   0 at 11 cols * 9 + t=   len 5 t=
1 to next-dir  advance   0 at 12 cols * 9 + t=
\ never straight back
new-game  2 turn-to  next-dir 0 t=   3 turn-to  next-dir 3 t=
\ through the edges
new-game  cols 1- head cells body + !   0 to dir  ahead 0 t=
5 head cells body + !   3 to dir  ahead rows 1- cols * 5 + t=
\ a fruit: one more point, three more cells
new-game  ahead to fruit  advance  score 1 t=  grow 3 t=
advance advance advance  len 8 t=   grow 0 t=   fruit taken + c@ 0 t=
\ biting itself ends the game
new-game  advance  1 to next-dir advance  2 to next-dir advance  3 to next-dir advance
state 2 t=   best 0 t=
depth 0 t=

( ---- Space Invaders ---- )
: invaders-no-run ;
include gfx_invaders.fs
1 rand-seed !

: shield-count ( -- n )   0  4 ssize * 0 do shields i + c@ + loop ;
new-game  scan  alive-n 55 t=   lc 0 t=   rc 10 t=   lr 4 t=   state 0 t=   lives 3 t=
\ a shot between two cells misses, one on an alien kills it and scores by row
3 ax@ 5 +  4 ay@ 10 +  shoot-alien 0 t=   alive-n 55 t=
3 ax@ 5 +  4 ay@ 2 +   shoot-alien -1 t=  3 4 alive@ c@ 0 t=   alive-n 54 t=   score 10 t=
0 ax@ 3 +  0 ay@ 1 +   shoot-alien -1 t=  score 40 t=
0 ax@ 3 +  0 ay@ 1 +   shoot-alien 0 t=
\ the formation marches, drops at the edge and turns back
new-game  march  ax 28 t=   ay 40 t=   anim 1 t=
new-game  w 2 - 14 - cols 1- pit * - 3 - to ax   march   ay 48 t=   dir -1 t=   ax 45 t=
\ a lonely alien marches fast
new-game  nal 1 do 0 alive i + c! loop   scan  alive-n 1 t=   step-ms 35 t=
\ shield shape, bites
0 0 shape-on? 0 t=   11 5 shape-on? -1 t=   10 12 shape-on? 0 t=
new-shields  shield-count dup 0 > -1 t=
24 11 + sy 5 +  shield-hit?  -1 t=   shield-count swap <  -1 t=   0 11 5 shield-at c@ 0 t=
24 11 + sy 5 +  shield-hit?  0 t=
\ the cannon bullet goes up 4 pixels a frame
new-game  1 to bullet-on  3 to bx  100 to by  bullet-move  by 96 t=
1 to bullet-on  3 to bx  15 to by  bullet-move  bullet-on 0 t=
\ the cannon is hit once, then the game goes on or ends
new-game  player-hit  lives 2 t=  state 3 t=  player-hit  lives 2 t=
after-hit  state 0 t=
1 to lives  0 to state  player-hit  state 3 t=  lives 0 t=  after-hit  state 2 t=   best score t=
\ the wave cleared: the next one starts lower and faster
new-game  alive nal erase  0 to alive-n  next-wave  level 1 t=  alive-n 55 t=  ay 48 t=
depth 0 t=

( ---- Minesweeper ---- )
: minesweeper-no-run ;
include gfx_minesweeper.fs

: mine-count ( -- n ) 0  cols rows * 0 do i mine has? if 1+ then loop ;
\ the first cell opened never has a mine on it or around it
: safe-start ( seed -- f ) rand-seed !  new-game  4 4 open-cell
  state 0=  4 4 mines-near 0= and  open-count 9 >= and  mine-count mines = and ;
: safe-starts ( -- n ) 0  50 1 do i safe-start if 1+ then loop ;
safe-starts 49 t=
30 16 99 level  0 0 open-cell  mine-count 99 t=  state 0 t=   9 9 10 level
\ flags: counted, and a flagged cell does not open
7 rand-seed !  new-game  4 4 open-cell
: closed-safe ( -- i )   ( a closed cell without a mine )
  cols rows * 0 do i opened has? 0= i mine has? 0= and if i unloop exit then loop -1 ;
: closed-mine ( -- i ) cols rows * 0 do i mine has? if i unloop exit then loop -1 ;
closed-safe dup cols mod swap cols / 2dup toggle-flag  flags 1 t=
2dup open-cell  idx opened has? 0 t=
\ open around a number whose flags are all there
: flag-mines-near { x y }   2 -1 do 2 -1 do
    x i + y j + 2dup in? if 2dup idx dup mine has? swap flag has? 0= and if toggle-flag else 2drop then
    else 2drop then loop loop ;
: closed-near { x y }   ( -- n ) 0  2 -1 do 2 -1 do
    x i + y j + 2dup in? if idx dup opened has? 0= swap mine has? 0= and if 1+ then else 2drop then
    loop loop ;
: a-number ( -- x y )   ( an open number with a closed safe cell near )
  rows 0 do cols 0 do
    i j idx opened has?  i j mines-near 0> and  i j closed-near 0> and if i j unloop unloop exit then
  loop loop -1 -1 ;
7 rand-seed !  new-game  4 4 open-cell
a-number over 0< 0 t=  2dup flag-mines-near  2dup open-cell  closed-near 0 t=  state 0 t=
\ a mine opened: lost
closed-mine dup cols mod swap cols / open-cell  state 1 t=
\ every safe cell opened: won, and the mines get their flags
7 rand-seed !  new-game  4 4 open-cell
: open-all-safe ( -- ) cols rows * 0 do i mine has? 0= if i open-one then loop flood check-win ;
open-all-safe  state 2 t=  flags mines t=  open-count cols rows * mines - t=
depth 0 t=

( ---- Mastermind ---- )
: mastermind-no-run ;
include gfx_mastermind.fs
1 rand-seed !

: secret! ( a b c d -- ) secret 3 + c! secret 2 + c! secret 1+ c! secret c! ;
create try 4 allot
: try! ( a b c d -- a ) try 3 + c! try 2 + c! try 1+ c! try c! try ;
new-game   state 0 t=   row 0 t=
: secret-ok? ( -- f ) -1  holes 0 do secret i + c@ 1 7 within 0= if drop 0 then loop ;
secret-ok? -1 t=
\ the pegs: right places, then right colors elsewhere, each color counted once
1 2 3 4 secret!
1 2 3 4 try! score 0 t= 4 t=
4 3 2 1 try! score 4 t= 0 t=
1 3 5 6 try! score 1 t= 1 t=
5 5 5 5 try! score 0 t= 0 t=
1 1 1 1 try! score 0 t= 1 t=
1 1 2 2 secret!
2 2 1 1 try! score 4 t= 0 t=
1 2 1 2 try! score 2 t= 2 t=
1 1 1 3 try! score 0 t= 2 t=
\ filling a row with the keys' words, then checking it
new-game  1 1 2 2 secret!
3 put 3 put 3 put   full? 0 t=   slot 3 t=
check-row  row 0 t=   ( not full: nothing checked )
4 put   full? -1 t=   check-row   row 1 t=  0 exact@ 0 t=  0 near@ 0 t=  slot 0 t=
1 put 2 put  0 to slot  1 turn-color  row guess c@ 2 t=  -1 turn-color -1 turn-color  row guess c@ 0 t=
take-away  slot 0 t=
\ the right row: won
new-game  1 1 2 2 secret!  0 to won 0 to played
1 put 1 put 2 put 2 put  check-row  state 1 t=  won 1 t=  played 1 t=
\ ten wrong rows: lost
new-game  1 1 2 2 secret!
: wrong-row ( -- ) 3 put 3 put 3 put 3 put check-row ;
: ten-wrong ( -- ) 10 0 do wrong-row loop ;
ten-wrong  state 2 t=  played 11 t=  won 1 t=
depth 0 t=

: pacman-no-run ;
include gfx_pacman.fs
\ the maze
new-game
pm-dots 159 t=   count-dots 159 t=   pm-lives 3 t=   pm-state 0 t=
0 tile-x 9 t=   0 tile-y 15 t=
0 0 pm-solid? -1 t=   9 8 pm-solid? -1 t=   9 9 pm-solid? 0 t=   1 1 pm-solid? 0 t=
-1 9 pm-solid? 0 t=   19 9 pm-solid? 0 t=   -1 8 pm-solid? -1 t=
\ Pac-Man eats the dot under him and goes on, then stops at a wall
1 to pm-state   0 to pm-paused
7 15 0 place-tile  2 0 e-dir !  2 to pm-want  0 0 e-acc !
: pix ( n -- ) 0 do pac-pixel loop ;
10 pix   0 tile-x 6 t=   pm-score 10 t=   pm-dots 158 t=
\ a wall straight ahead: no move
1 1 0 place-tile  2 0 e-dir !  2 to pm-want
5 pix   0 e-x @ 15 t=
\ the tunnel wraps round
0 e-x @ drop  5 0 e-x !  95 0 e-y !  2 0 e-dir !  2 to pm-want
10 pix   0 e-x @ 185 t=
\ a big pellet scares the ghosts and is worth 50
new-game   1 to pm-state
1 15 0 place-tile  2 0 e-dir !  2 to pm-want  2 2 e-st !
eat-here   pm-score 50 t=   2 e-fr @ 1 t=   2 e-rev @ 1 t=   pm-fright 270 t=
\ eating the ghosts: 200 then 400
0 to pm-score   0 to pm-chain
2 eat-ghost   pm-score 200 t=   2 e-st @ 3 t=   2 e-fr @ 0 t=
3 eat-ghost   pm-score 600 t=
\ a ghost choosing at a crossing: red, in the corridor, Pac-Man on its left
new-game   1 to pm-state   1 to pm-mode
3 3 1 place-tile   1 1 e-dir !   2 1 e-st !
1 3 0 place-tile
1 choose-dir   1 e-dir @ 2 t=
\ it does not turn back, even if Pac-Man is behind it
7 3 1 place-tile   2 1 e-dir !   17 3 0 place-tile   1 1 e-fr !  0 1 e-fr !
1 choose-dir   1 e-dir @ 2 t=
\ contact: a ghost out kills, lives go down
new-game   1 to pm-state
9 15 0 place-tile   9 15 1 place-tile   2 1 e-st !
collide   pm-state 2 t=   pm-lives 2 t=
\ contact with a frightened ghost eats it
new-game   1 to pm-state
9 15 0 place-tile   9 15 1 place-tile   2 1 e-st !   1 1 e-fr !
collide   pm-state 1 t=   1 e-st @ 3 t=   pm-score 200 t=
\ the last dot ends the level
new-game   1 to pm-state   0 to pm-dots
play-step   pm-state 4 t=
next-level   pm-level 2 t=   pm-state 0 t=   pm-dots 159 t=
\ the ghosts leave the house one after the other
new-game   1 to pm-state
251 0 do play-step loop   2 e-st @ 0 <> -1 t=
depth 0 t=

( ---- Digger ---- )
: digger-no-run ;
include gfx_digger.fs
1 rand-seed !
: count-items ( v -- n )   0 swap  dg-rows 0 do dg-cols 0 do  i j item-at c@ over = if swap 1+ swap then  loop loop drop ;
: count-bags ( -- n )   0  dg-maxbag 0 do i bag.st @ 0<> 1 and + loop ;
: clean-level ( -- )   ( a game with no bags and no items, to build the cases by hand )
  new-game  dg-maxbag 0 do 0 i bag.st ! loop  dg-item dg-cols dg-rows * erase  99 to dg-left ;
: steps ( n -- )   0 do dg-step loop ;
\ a new level: the corridor on top, the start tunnel, emeralds and four bags
new-game
dg-state 0 t=   dg-lives 3 t=   dg-level 1 t=   dg-left 0> -1 t=
1 count-items dg-left t=   count-bags 4 t=
0 0 cell-clear? -1 t=   14 0 cell-clear? -1 t=   0 1 cell-clear? 0 t=
6 9 cell-clear? -1 t=   8 9 cell-clear? -1 t=   5 9 cell-clear? 0 t=
6 9 0 link? -1 t=   5 9 0 link? 0 t=   7 9 3 link? 0 t=   3 0 0 link? -1 t=
dg-gx 140 t=   dg-gy 162 t=
\ taking the earth away, and not far from the field
2 5 dirt? -1 t=   20 50 10 9 clear-rect   2 5 dirt? 0 t=   -50 -50 10 10 clear-rect   500 500 10 10 clear-rect   -1 -1 dirt? -1 t=
\ the digger tunnels up, eats the emerald in the way and digs the cell
clean-level   1 7 8 item-at c!   1 to dg-left
9 0 do 3 walk-to loop
dg-gy 144 t=   7 8 cell-clear? -1 t=   dg-left 0 t=   dg-score 25 t=   7 8 item-at c@ 0 t=
\ at the edge of the field it stops
clean-level   0 to dg-gx  0 to dg-gy  2 to dg-gd   2 walk-to   dg-gx 0 t=
\ eight emeralds in a row: 250 more
clean-level   0 to dg-left
8 0 do  1 i 1 + 0 item-at c!  1 +to dg-left  loop
0 to dg-gx  0 to dg-gy  0 to dg-gd
90 0 do 0 walk-to loop
dg-score 450 t=   dg-left 0 t=
\ a bag over a free cell wobbles, then falls one cell and stays
clean-level   0 to dg-gx  0 to dg-gy
1 7 bag.st !  7 dg-cw * 7 bag.x !  6 dg-ch * 7 bag.y !   0 7 bag.t !  0 7 bag.n !   \ slot 7 (clean-level emptied them)
7 dg-cw * 7 dg-ch * dg-cw dg-ch clear-rect
1 steps   7 bag.st @ 2 t=
40 steps  7 bag.st @ 3 t=
20 steps  7 bag.st @ 1 t=   7 bag.y @ 7 dg-ch * t=
\ two cells: it breaks into gold; a monster under it dies
7 8 cell-clear? 0 t=
7 dg-cw * 8 dg-ch * dg-cw dg-ch clear-rect
1 1 mon.t !  7 dg-cw * 1 mon.x !  8 dg-ch * 1 mon.y !   0 1 mon.a !  500 1 mon.m !
60 0 do bags-update collisions loop
7 bag.st @ 0 t=   7 9 item-at c@ 2 t=   1 mon.t @ 0 t=   dg-score 250 t=
\ a bag that falls on the digger kills it
clean-level
1 7 bag.st !  3 7 bag.st !  dg-gx 7 bag.x !  dg-gy 7 bag.y !
collisions   dg-state 3 t=   dg-lives 2 t=
\ pushing a bag along a tunnel
clean-level
8 dg-cw * 9 dg-ch * dg-cw dg-ch clear-rect   9 dg-cw * 9 dg-ch * dg-cw dg-ch clear-rect
1 7 bag.st !  8 dg-cw * 7 bag.x !  9 dg-ch * 7 bag.y !
0 to dg-gd   10 0 do 0 walk-to loop
dg-gx 160 t=   7 bag.x @ 180 t=   dg-push -1 t=
\ a bag cannot be pushed up, nor into earth
clean-level   1 7 bag.st !  7 dg-cw * 7 bag.x !  8 dg-ch * 7 bag.y !   0 to dg-gd
7 dg-cw * 8 dg-ch * dg-cw dg-ch clear-rect
dg-gx 140 t=   3 to dg-gd   can-enter? 0 t=
\ the distance to the digger along the tunnels; the nobbin and the hobbin choose
clean-level   140 18 20 144 clear-rect
bfs   7 9 dist-at c@ 0 t=   6 9 dist-at c@ 1 t=   7 0 dist-at c@ 9 t=   5 9 dist-at c@ 255 t=   14 0 dist-at c@ 16 t=
7 0 2 nob-dir 1 t=   7 5 3 nob-dir 3 t=
0 0 0 hob-dir 1 t=   14 9 0 hob-dir 2 t=
\ a bag in the way
1 7 bag.st !  7 dg-cw * 7 bag.x !  4 dg-ch * 7 bag.y !
bfs   7 3 dist-at c@ 255 t=   7 5 dist-at c@ 4 t=   7 0 dist-at c@ 255 t=
\ the fire ball hits the first monster on its way
clean-level   5 dg-cw * to dg-gx  0 to dg-gy  0 to dg-gd
1 3 mon.t !  8 dg-cw * 3 mon.x !  0 3 mon.y !
fire!   dg-fon 1 t=   dg-fire-t 300 t=
20 0 do fire-step loop
3 mon.t @ 0 t=   dg-score 250 t=   dg-fon 0 t=
fire!   dg-fon 0 t=
\ ... and stops at the earth
clean-level   5 dg-cw * to dg-gx  4 dg-ch * to dg-gy  0 to dg-gd   dg-gx dg-gy 20 18 clear-rect
fire!   10 0 do fire-step loop   dg-fon 0 t=
\ monsters meet the digger: death, or a meal when the cherry was eaten
clean-level   1 1 mon.t !  dg-gx 1 mon.x !  dg-gy 1 mon.y !
monsters-vs-digger   dg-state 3 t=   dg-lives 2 t=
clean-level   100 to dg-bonus   1 1 mon.t !  dg-gx 1 mon.x !  dg-gy 1 mon.y !
monsters-vs-digger   dg-state 0 t=   1 mon.t @ 0 t=   dg-score 200 t=
1 2 mon.t !  dg-gx 2 mon.x !  dg-gy 2 mon.y !
monsters-vs-digger   dg-score 600 t=
\ a monster comes out after two seconds; no more than the level allows
clean-level   0 to dg-spawned
119 0 do spawn loop   mon-count 0 t=
spawn   mon-count 1 t=   dg-spawned 1 t=
\ the cherry, the free life at 20000
clean-level   19990 to dg-score   20 score+   dg-lives 4 t=   dg-next-life 40000 t=
\ the end of a life, of the game, then a new one
clean-level   1 to dg-lives   kill-digger   dg-lives 0 t=
100 steps   dg-state 2 t=   dg-best dg-score t=
new-game   dg-state 0 t=   dg-lives 3 t=   dg-score 0 t=
\ the level is done when the emeralds are all eaten, and the next one begins
clean-level   0 to dg-left
1 steps   dg-state 4 t=
120 steps   dg-state 0 t=   dg-level 2 t=   dg-left 0> -1 t=   4 dg-level 2/ + 8 min count-bags t=
\ a bag right above the digger: the fire goes down; the bag is kept 40 steps after he left
clean-level   1 0 bag.st !  7 dg-cw * 0 bag.x !  5 dg-ch * 0 bag.y !
7 dg-cw * to dg-gx   6 dg-ch * to dg-gy   3 to dg-gd   under-bag? -1 t=
0 to dg-fire-t   0 to dg-fon   fire!   dg-fd 1 t=
2 to dg-gd   under-bag? 0 t=
0 bag.st @ 1 t=
: sed-nth-t ( i -- spr )   cells spr-all + @ ;
\ the sprites (gfx_spr.fs): cut into rectangles, and written as source again
3 3 sprite: spr-t1
  s" aa." spr-row
  s" a.." spr-row
  s" ..b" spr-row
;sprite
spr-t1 spr-n 3 t=   0 0 spr-t1 spr-pix@ 97 t=   2 2 spr-t1 spr-pix@ 98 t=
spr-t1 spr-rects @ spr-rw 2 t=   spr-t1 spr-rects @ spr-rh 1 t=
spr-t1 spr-rects cell+ @ spr-rw 1 t=   spr-t1 spr-rects 2 cells + @ spr-rc 98 t=
spr-t1 spr-source evaluate
spr-count 1- sed-nth-t   dup spr-n 3 t=   spr-grid c@ 97 t=
bag spr-n 0> -1 t=   emerald spr-name s" emerald" compare 0 t=
depth 0 t=

: report ( -- )   tfail @ 0= if ." PASS " tn @ . cr else ." FAILED " tfail @ . cr then ;
report
bye
