\ Tests of the logic of gfx_raycaster.fs, loaded without its game loop:
\   lugforth examples/games/raycaster_tests.fs
\ (the output ends with PASS). Needs libSDL2, but opens no window.

variable tn   variable tfail
: t= ( actual expected -- )
  1 tn +!  2dup <> if
    ." FAIL test " tn @ . ." got " over . ." expected " dup . cr  1 tfail +!
  then 2drop ;
: near= ( actual expected tolerance -- )
  >r over - abs r> > if ." FAIL test (near) " tn @ . cr 1 tfail +! then 1 tn +! drop ;

: raycaster-no-run ;
include gfx_raycaster.fs

\ sines in 16.16: a quarter turn is 1024 units
0 sin@ 0 t=   1024 sin@ 65536 t=   2048 sin@ 0 t=   3072 sin@ -65536 t=
0 cos@ 65536 t=   1024 cos@ 0 t=   2048 cos@ -65536 t=   4096 sin@ 0 t=   -1024 sin@ -65536 t=
512 sin@ 46341 2 near=

\ the map: outside is a wall, the border too, the middle is free
0 0 wall@ 49 t=   -1 5 wall@ 49 t=   5 -1 wall@ 49 t=   mw 3 wall@ 49 t=   3 mh wall@ 49 t=
1 1 empty? -1 t=   5 3 wall@ 50 t=   16 3 wall@ 51 t=   8 10 wall@ 52 t=   11 1 empty? 0 t=

\ a ray along a row: the wall at x = 11 is hit on a side of x, 8.5 cells away
new-game   px 163840 t=   py 163840 t=   dirx 65536 t=   diry 0 t=
sw 2/ cast   hit 49 t=   side 0 t=   dist 557056 300 near=
\ the first and last columns are the two edges of the field of view: they hit different walls
0 cast dist 0> -1 t=   sw 1- cast dist 0> -1 t=
\ turned a quarter: looking down +y, the wall of row 9 (x = 2: a wall, only column 6 and 17 are open)
1024 to ang  aim   sw 2/ cast   hit 49 t=   side 1 t=   dist 425984 300 near=
\ no division by zero on a ray parallel to an axis
0 delta 0> -1 t=   65536 delta 65536 t=   -65536 delta 65536 t=   32768 delta 131072 t=

\ walking: stops at a wall, slides along it
new-game  one 3 * 2/ to px   one 3 * 2/ to py
-65536 0 move   px one 3 * 2/ t=
0 -65536 move   py one 3 * 2/ t=
3000 0 move   px one 3 * 2/ 3000 + t=
\ turning wraps around the circle
0 to ang   -10 turn-by   ang 4086 t=   20 turn-by   ang 10 t=
depth 0 t=

: report ( -- )   tfail @ 0= if ." PASS " tn @ . cr else ." FAILED " tfail @ . cr then ;
report
bye
