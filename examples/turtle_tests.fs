\ Tests of sdl2_turtle.fs, loaded without its demo:  lugforth examples/turtle_tests.fs
\ (the output ends with PASS). Needs libSDL2 and SDL_VIDEODRIVER=dummy for no window on screen.

variable tn   variable tfail
: t= ( actual expected -- )
  1 tn +!  2dup <> if
    ." FAIL test " tn @ . ." got " over . ." expected " dup . cr  1 tfail +!
  then 2drop ;

: turtle-no-run ;
include sdl2_turtle.fs

: square ( n -- ) 4 0 do dup forward 90 right loop drop ;
: where ( -- x y ) tpos ;

open-turtle
clearscreen  where th 2 / t=  tw 2 / t=
100 square  where th 2 / t=  tw 2 / t=   nseg 4 t=
clearscreen  100 forward  where 140 t=  tw 2 / t=
90 right 50 forward  where 140 t=  370 t=
50 backward  where 140 t=  320 t=
\ pen up: no stroke
clearscreen penup 80 forward pendown  nseg 0 t=  where 160 t=  320 t=
\ setxy draws from the old position to the new one
clearscreen 30 40 setxy  nseg 1 t=  where 280 t=  350 t=
home 0 setheading
\ the stroke is drawn with its color and size
clearscreen $ff0000 setpencolor 3 setpensize hideturtle 60 forward render
320 210 pixel@ $ff0000 t=   319 210 pixel@ $ff0000 t=   330 210 pixel@ $101828 t=
\ rounding
3.7e fround 4 t=   -3.7e fround -4 t=   0.2e fround 0 t=   -0.2e fround 0 t=
\ French names
clearscreen 20 avance 90 droite 20 avance  nseg 2 t=

\ the shell: lines are kept, then drawn
: l ( a n -- ) line swap dup to llen move do-line ;
clearscreen  0 to plen
s" 100 forward" l   s" 4 0 do 30 forward 90 right loop" l   plen 0 > -1 t=   nseg 0 t=
s" go" l  nseg 5 t=
s" 1 2 bogus" l  s" go" l  nseg 0 t=  depth 0 t=
s" undo" l  nseg 5 t=
s" new" l  plen 0 t=  nseg 0 t=
s" demo" l  s" go" l  nseg 100 > -1 t=
s" speed 7" l  play-ms 7 t=
s" GO" l  nseg 100 > -1 t=    \ the case does not matter
s" a" s" A" same? -1 t=   s" ab" s" a" same? 0 t=
depth 0 t=
close-screen

: report ( -- ) tfail @ 0= if ." PASS " tn @ . cr else ." FAILED " tfail @ . cr then ;
report
bye
