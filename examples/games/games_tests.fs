\ Tests of the logic of sdl2_tetris.fs, sdl2_snake.fs, sdl2_minesweeper.fs and sdl2_mastermind.fs,
\ loaded without their game loop:  lugforth examples/games/games_tests.fs
\ (the output ends with PASS). Needs libSDL2, but opens no window.

variable tn   variable tfail
: t= ( actual expected -- )
  1 tn +!  2dup <> if
    ." FAIL test " tn @ . ." got " over . ." expected " dup . cr  1 tfail +!
  then 2drop ;

( ---- Tetris ---- )
: tetris-no-run ;
include sdl2_tetris.fs
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
include sdl2_snake.fs
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

( ---- Minesweeper ---- )
: minesweeper-no-run ;
include sdl2_minesweeper.fs

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
include sdl2_mastermind.fs
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

: report ( -- )   tfail @ 0= if ." PASS " tn @ . cr else ." FAILED " tfail @ . cr then ;
report
bye
