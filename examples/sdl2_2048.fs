#! /usr/bin/env lugforth

\ 2048 in an SDL2 window. The game itself (grid, moves, merging, score) is in 2048.fs,
\ ported from the 2048 project of Marc Petremann (eForth Windows, GNU General Public
\ License); this file only draws it and reads the input. The digits are drawn from
\ a 3x5 bitmap, so no font file is needed.
\   arrows or w a s d   move the tiles
\   mouse               drag (or swipe on a touch screen) in a direction
\   r                   new game
\   esc                 quits (or close the window)
\ Needs libSDL2.

include 2048.fs

sdl2

472 value w   640 value h
24 value margin   12 value gap   100 value cell     \ the board: 4 cells of 100, 3 gaps, 2 margins
150 value by                                          \ top of the board

\ ---- Digits: 3x5 bitmaps, 15 bits each, the top left pixel is the highest bit ----
create glyphs  $7B6F , $2C97 , $73E7 , $73CF , $5BC9 , $79CF , $79EF , $7249 , $7BEF , $7BCF ,

0 value gs   0 value gx   0 value gy   0 value gbits
: glyph-pixel ( r c -- )
  over 3 * over + 14 swap - gbits swap rshift 1 and 0= if 2drop exit then
  gs * gx +  swap gs * gy +  gs dup box ;
: draw-glyph ( digit x y s -- )
  to gs  to gy  to gx  cells glyphs + @ to gbits
  5 0 ?do 3 0 ?do j i glyph-pixel loop loop ;

\ the number of digits of n, and the number n centered on x y, the digits being 5 * s high
: digits# ( n -- k )   1 swap begin 10 / dup while swap 1+ swap repeat drop ;
0 value ds   0 value dn   0 value dstr   0 value dx0   0 value dy0
: draw-number ( n x y s -- )
  to ds  to dy0  to dx0
  <# #s #> to dn  to dstr
  dx0  dn 4 * 1- ds * 2/  -  to dx0
  dy0  ds 5 * 2/  -  to dy0
  dn 0 ?do  dstr i + c@ [char] 0 -  dx0 i 4 * ds * +  dy0  ds  draw-glyph  loop ;

\ ---- Colors of the tiles, by exponent ----
create tile-rgb  $cdc1b4 , $eee4da , $ede0c8 , $f2b179 , $f59563 , $f67c5f , $f65e3b ,
                 $edcf72 , $edcc61 , $edc850 , $edc53f , $edc22e , $3c3a32 ,
: tile-rgb@ ( n -- rgb )   12 min cells tile-rgb + @ ;

: cell-x ( col -- x )   cell gap + * margin + ;
: cell-y ( row -- y )   cell gap + * by + ;

: draw-tile ( col row -- )
  2dup xyVal@ { col row e }
  col cell-x { x }  row cell-y { y }
  e tile-rgb@ color  x y cell cell box
  e 0= if exit then
  e 2 <= if $776e65 else $f9f6f2 then color
  1 e lshift { n }
  n  x cell 2/ +  y cell 2/ +  80 n digits# 4 * 1- /  7 min 1 max  draw-number ;

: draw-board ( -- )
  $bbada0 color  margin gap - by gap -  cell gap + 4 * gap +  dup  box
  GRID_WIDTH 0 ?do GRID_WIDTH 0 ?do  i j draw-tile  loop loop ;

: game-over? ( -- f )   can-move? 0= ;

: draw-all ( -- )
  $faf8ef color cls
  $edc22e color  margin 24 140 60 box
  $f9f6f2 color  2048 margin 70 +  54  5 draw-number
  $bbada0 color  190 24 120 60 box   330 24 120 60 box
  $f9f6f2 color  score 250 54 5 draw-number   best 390 54 5 draw-number
  draw-board
  game-over? if
    $eee4da color  128 alpha
    margin gap - by gap -  cell gap + 4 * gap +  dup box
    255 alpha
  then
  flip ;

\ ---- Input ----
0 value held   0 value mouse-was   0 value mx0   0 value my0
: key-dir ( -- d|-1 )
  key-left pressed? [char] a pressed? or if 0 exit then
  key-right pressed? [char] d pressed? or if 1 exit then
  key-up pressed? [char] w pressed? or if 2 exit then
  key-down pressed? [char] s pressed? or if 3 exit then
  -1 ;
\ a drag of more than 30 pixels, seen when the button is released: its direction
0 value mdown   0 value dxv   0 value dyv
: swipe-dir ( -- d|-1 )
  -1 { r }
  LEFT-BUTTON pressed? { down }
  down mdown 0= and if mouse-x to mx0  mouse-y to my0 then
  down 0= mdown and if
    mouse-x mx0 - to dxv  mouse-y my0 - to dyv
    dxv abs dyv abs max 30 > if
      dxv abs dyv abs > if dxv 0< if 0 else 1 then else dyv 0< if 2 else 3 then then to r
    then
  then
  down to mdown  r ;

\ ---- The game ----
create tbuf 80 allot   0 value tlen
: t+ ( a n -- )   dup >r tbuf tlen + swap cmove  r> +to tlen ;
: show-score ( -- )
  0 to tlen  s" 2048  score " t+  score <# #s #> t+
  game-over? if s"   -  game over, r for a new game" t+ then
  tbuf tlen title ;

: play ( d -- )   move-tiles if add-random then  score best max to best  show-score ;

: step ( -- )
  events
  key-esc pressed? if quit! then
  [char] r pressed? if score best max to best  new-game show-score then
  key-dir dup held <> swap to held  held 0< 0= and if held play then
  swipe-dir dup 0< 0= if play else drop then
  draw-all
  dt 16 < if 16 dt - delay then ;

: run ( -- ) begin step quit? until close-screen ;

ms-ticks 1 or rand-seed !
new-game
w h screen
show-score
run
script-bye
