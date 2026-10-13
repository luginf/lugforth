\ Tests of the logic of 2048.fs:  lugforth examples/2048_tests.fs   (the output ends with PASS)

include 2048.fs

variable tn   variable tfail
: t= ( actual expected -- )
  1 tn +!  2dup <> if
    ." FAIL test " tn @ . ." got " over . ." expected " dup . cr  1 tfail +!
  then 2drop ;

\ a row of the grid as four exponents, from the left
: row! ( a b c d y -- )   >r  3 r@ xyVal!  2 r@ xyVal!  1 r@ xyVal!  0 r> xyVal! ;
: row@ ( y -- a b c d )   >r  0 r@ xyVal@  1 r@ xyVal@  2 r@ xyVal@  3 r> xyVal@ ;

grid-init  0 to score
\ 2 2 . 4  left -> 4 4 . .   (exponents 1 1 0 2 -> 2 2 0 0), score 4
1 1 0 2 0 row!   0 move-tiles -1 t=   0 row@ 0 t= 0 t= 2 t= 2 t=   score 4 t=
\ nothing moves: false
grid-init  1 2 3 0 0 row!  0 move-tiles 0 t=
\ the same row to the right
grid-init  1 1 0 2 0 row!   1 move-tiles -1 t=   0 row@ 2 t= 2 t= 0 t= 0 t=
\ 2 2 2 2 left -> 4 4 . .  (a tile merges once)
grid-init  0 to score  1 1 1 1 1 row!   0 move-tiles drop   1 row@ 0 t= 0 t= 2 t= 2 t=   score 8 t=
\ 2 . 2 4 left -> 4 4 . .
grid-init  1 0 1 2 2 row!   0 move-tiles drop   2 row@ 0 t= 0 t= 2 t= 2 t=
\ columns: up and down
grid-init  1 0 0 0 0 row!  1 0 0 0 1 row!  0 0 0 0 2 row!   2 move-tiles -1 t=
0 0 xyVal@ 2 t=   0 1 xyVal@ 0 t=
3 move-tiles -1 t=   0 3 xyVal@ 2 t=   0 0 xyVal@ 0 t=
\ a new tile, and the end of the game
grid-init  empty-count 16 t=   add-random  empty-count 15 t=
1 2 1 2 0 row!  2 1 2 1 1 row!  1 2 1 2 2 row!  2 1 2 1 3 row!   can-move? 0 t=
1 2 1 2 0 row!  2 1 2 1 1 row!  1 2 1 2 2 row!  2 1 2 2 3 row!   can-move? -1 t=
add-random  empty-count 0 t=
grid-init  11 0 0 11 0 row!  2048? -1 t=   grid-init 2048? 0 t=
depth 0 t=
: report ( -- )   tfail @ 0= if ." PASS " tn @ . cr else ." FAILED " tfail @ . cr then ;
report
bye
