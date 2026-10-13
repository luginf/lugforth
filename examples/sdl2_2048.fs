#! /usr/bin/env lugforth

\ 2048 in an SDL2 window. The game itself (grid, moves, merging, score) is in 2048.fs,
\ ported from the 2048 project of Marc Petremann (eForth Windows, GNU General Public
\ License); this file only draws it and reads the input. The digits are drawn from
\ a 3x5 bitmap, so no font file is needed.
\   arrows or w a s d   move the tiles
\   mouse               drag (or swipe on a touch screen) in a direction
\   r                   new game
\   esc                 quits (or close the window)
\ The tiles slide, merged ones and new ones pop; `--no-anim` after the file name turns that off:
\   lugforth examples/sdl2_2048.fs --no-anim
\ Needs libSDL2.

include 2048.fs

sdl2

484 value w   640 value h
24 value margin   12 value gap   100 value cell     \ the board: 4 cells of 100, 3 gaps, 2 margins
150 value by                                          \ top of the board

\ ---- Digits: 3x5 bitmaps, 15 bits each, the top left pixel is the highest bit ----
create glyphs  $7B6F , $2C97 , $73E7 , $73CF , $5BC9 , $79CF , $79EF , $7249 , $7BEF , $7BCF ,  $7927 , $7BF5 , $79E7 , $6BAE , $7492 ,   \ then C R E B T

0 value gs   0 value gx   0 value gy   0 value gbits
: glyph-pixel ( r c -- )
  over 3 * over + 14 swap - gbits swap rshift 1 and 0= if 2drop exit then
  gs * gx +  swap gs * gy +  gs dup box ;
: draw-glyph ( digit x y s -- )
  to gs  to gy  to gx  cells glyphs + @ to gbits
  5 0 ?do 3 0 ?do j i glyph-pixel loop loop ;

: letter>glyph ( c -- i )   \ S and O are the 5 and the 0
  dup [char] S = if drop 5 exit then   dup [char] O = if drop 0 exit then
  dup [char] C = if drop 10 exit then  dup [char] R = if drop 11 exit then
  dup [char] E = if drop 12 exit then  dup [char] B = if drop 13 exit then
  drop 14 ;
\ a word of the letters SCORE BT, centered on x, its top at y
0 value ls   0 value ly   0 value lx   0 value la   0 value ln
: draw-label ( a n x y s -- )
  to ls  to ly  to lx  to ln  to la
  lx  ln 4 * 1- ls * 2/  -  to lx
  ln 0 ?do  la i + c@ letter>glyph  lx i 4 * ls * +  ly  ls  draw-glyph  loop ;

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

: tile-digit-scale ( n side -- s )   80 swap cell */  swap digits# 4 * 1- /  7 min 1 max ;

\ the tile of exponent e with its top left corner at x y and its side s (an empty cell too)
: draw-tile-at ( e x y s -- )
  { e x y s }
  e tile-rgb@ color  x y s s box
  e 0= if exit then
  e 2 <= if $776e65 else $f9f6f2 then color
  1 e lshift { n }
  n  x s 2/ +  y s 2/ +  n s tile-digit-scale  draw-number ;

\ ---- Animation: the tiles slide, then merged and new tiles pop ----
true value anim-on
0 value anim-phase   0 value anim-t0
220 constant SLIDE-MS   200 constant POP-MS
\ the tiles before the move: from x y, exponent, to x y (in cells)
create mv 16 5 * cells allot   0 value nmv
create popf 16 allot       \ per cell: 0 nothing, 1 merged here, 2 new tile
create snap 16 allot       \ the grid after the move, before the new tile
0 value pr   0 value pn   -1 value ppend   0 value pidx   0 value px   0 value py   0 value pe

: mv-add ( x y e di -- )
  nmv 5 * cells mv + { x y e di a }
  pr di line-xy { x1 y1 }
  x a !  y a cell+ !  e a 2 cells + !  x1 a 3 cells + !  y1 a 4 cells + !
  1 +to nmv ;
: plan-line ( r -- )
  to pr  0 to pn  -1 to ppend
  GRID_WIDTH 0 ?do
    pr i line-xy to py to px  px py xyVal@ to pe
    pe if
      pe ppend = if
        px py pe pidx mv-add
        1  pr pidx line-xy GRID_WIDTH * + popf + c!
        -1 to ppend  1 +to pn
      else
        ppend 0< 0= if 1 +to pn then
        pn to pidx  pe to ppend  px py pe pidx mv-add
      then
    then
  loop ;
\ before the move d: where each tile goes
: plan-moves ( d -- )
  to dir  0 to nmv  popf 16 erase
  GRID_WIDTH 0 ?do i plan-line loop ;

: note-born ( -- )   \ after add-random: the cell that was empty and is not any more
  16 0 ?do  snap i + c@ 0=  2048grid i + c@ 0<> and if 2 popf i + c! then loop ;

: elapsed ( -- ms )   ticks anim-t0 - ;
: anim-start ( phase -- )   to anim-phase  ticks to anim-t0 ;

: draw-empty-cells ( -- )
  GRID_WIDTH 0 ?do GRID_WIDTH 0 ?do  0 i cell-x j cell-y cell draw-tile-at  loop loop ;

: draw-slide-one ( i -- )
  5 * cells mv + { a }  elapsed SLIDE-MS min dup SLIDE-MS 2* swap - * SLIDE-MS / { t }     \ ease out
  a 2 cells + @
  a @ cell-x  a 3 cells + @ cell-x  over -  t * SLIDE-MS /  +
  a cell+ @ cell-y  a 4 cells + @ cell-y  over -  t * SLIDE-MS /  +
  cell draw-tile-at ;
: draw-sliding ( -- )   nmv 0 ?do i draw-slide-one loop ;

\ the size of a tile that pops: a merged tile swells, a new one grows
: pop-side ( kind -- s )
  elapsed POP-MS min { t }
  2 = if  cell 3 /  cell 3 / 2 * t * POP-MS /  +  exit then
  t POP-MS 2/ > if POP-MS t - else t then  cell 25 * 2 * POP-MS */ 100 / cell +  ;

: draw-pop-one ( col row -- )
  2dup xyVal@ { col row e }
  e 0= if exit then
  row GRID_WIDTH * col + popf + c@ { kind }
  kind if kind pop-side else cell then { s }
  e  col cell-x cell s - 2/ +  row cell-y cell s - 2/ +  s  draw-tile-at ;
: draw-popping ( -- )
  draw-empty-cells
  GRID_WIDTH 0 ?do GRID_WIDTH 0 ?do  i j draw-pop-one  loop loop ;

: draw-board ( -- )
  $bbada0 color  margin gap - by gap -  cell gap + 4 * gap +  dup  box
  anim-phase 1 = if draw-empty-cells draw-sliding exit then
  anim-phase 2 = if draw-popping exit then
  draw-empty-cells
  GRID_WIDTH 0 ?do GRID_WIDTH 0 ?do
    i j xyVal@ ?dup if i cell-x j cell-y cell draw-tile-at then
  loop loop ;

: game-over? ( -- f )   can-move? 0= ;

: draw-all ( -- )
  $faf8ef color cls
  $edc22e color  margin 24 140 60 box
  $f9f6f2 color  2048 margin 70 +  54  5 draw-number
  $bbada0 color  190 24 120 60 box   330 24 120 60 box
  $f9f6f2 color  s" SCORE" 250 30 2 draw-label   s" BEST" 390 30 2 draw-label
  score 250 62 4 draw-number   best 390 62 4 draw-number
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

: play ( d -- )
  anim-phase if drop exit then
  anim-on if dup plan-moves then
  move-tiles if
    2048grid snap 16 cmove
    add-random
    anim-on if note-born 1 anim-start then
  then
  score best max to best  show-score ;

\ the animation goes on: slide, pop, then the plain board
: animate ( -- )
  anim-phase 1 = if elapsed SLIDE-MS >= if 2 anim-start then exit then
  anim-phase 2 = if elapsed POP-MS >= if 0 to anim-phase then then ;

: step ( -- )
  events
  key-esc pressed? if quit! then
  [char] r pressed? if 0 to anim-phase  score best max to best  new-game show-score then
  key-dir dup held <> swap to held  held 0< 0= and if held play then
  swipe-dir dup 0< 0= if play else drop then
  animate  draw-all
  dt 16 < if 16 dt - delay then ;

: run ( -- ) begin step quit? until close-screen ;

\ the option --no-anim after the name of the file
: option? ( a n -- f )   { oa on }  0  argc 2 ?do  oa on i argv compare 0= or  loop ;
\ (an `if` outside a definition does not work here)
: read-options ( -- )   s" --no-anim" option? if false to anim-on then ;
read-options
ms-ticks 1 or rand-seed !
new-game
w h screen
show-score
run
script-bye
