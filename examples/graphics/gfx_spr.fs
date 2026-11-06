\ gfx_spr.fs: sprites as grids of characters, drawn with boxes.
\
\ A sprite is a grid of up to 63x63 characters, one character per pixel:
\ '.' is transparent, any other character is a color of the palette
\ (spr-pal). The grid is cut into a short list of rectangles when the
\ sprite is closed, so drawing it costs a few `box`, not one per pixel.
\ The palette is read at each draw: change a color with spr-pal and the
\ next spr-draw uses it (a flash, a day and night palette...).
\
\ The files written by gfx_sprited.fs are made of these words (in a
\ file the character is given by its code):
\
\   $b89a58 98 spr-pal                  \ the color of 'b'
\   16 16 sprite: coin
\     s" ....bbbb........" spr-row   ...
\   ;sprite
\   coin 10 20 spr-draw                 \ at x y
\
\ It loads gfx (POSIX and web) and uses `color` and `box`. In a game, put
\ `include ../graphics/gfx_spr.fs` first (the web build finds it too).

gfx   \ so that color and box are the real ones (on the web, before this the same names are the console graphics)

128 cells constant spr-pal-size
create spr-pt spr-pal-size allot   spr-pt spr-pal-size erase
: spr-pal ( rgb ch -- )   swap $ffffff and $1000000 or swap  127 and cells spr-pt + ! ;
: spr-color@ ( ch -- rgb )   127 and cells spr-pt + @ $ffffff and ;
: spr-defined? ( ch -- f )   127 and cells spr-pt + @ 0<> ;

\ A sprite is a block of cells: 0 w, 1 h, 2 number of rectangles, 3 to 6
\ the name (counted, 31 characters at most), then the grid (w*h bytes,
\ rounded up to whole cells), then one cell for each possible rectangle
\ (w*h at most): x | y<<6 | w<<12 | h<<18 | char<<24.
: spr-w ( spr -- w )   @ ;
: spr-h ( spr -- h )   cell+ @ ;
: spr-n ( spr -- n )   2 cells + @ ;
: spr-name ( spr -- a n )   3 cells + count ;
: spr-grid ( spr -- a )   7 cells + ;
: spr-cells ( spr -- n )   dup spr-w swap spr-h * 1 cells + 1- 1 cells / ;   \ cells of the grid
: spr-rects ( spr -- a )   dup spr-grid swap spr-cells cells + ;

: spr-pix@ ( x y spr -- ch )   dup >r spr-w * + r> spr-grid + c@ ;
: spr-pix! ( ch x y spr -- )   dup >r spr-w * + r> spr-grid + c! ;

: spr-rect ( x y w h ch -- cell )   24 lshift  swap 18 lshift or  swap 12 lshift or  swap 6 lshift or or ;
: spr-rx ( cell -- x )   63 and ;
: spr-ry ( cell -- y )   6 rshift 63 and ;
: spr-rw ( cell -- w )   12 rshift 63 and ;
: spr-rh ( cell -- h )   18 rshift 63 and ;
: spr-rc ( cell -- ch )   24 rshift ;

\ ---- Cutting the grid: greedy rectangles of one character ----
create spr-used 4096 allot   \ a flag per pixel: already inside a rectangle
0 value sp-bs   0 value sp-bx   0 value sp-by   0 value sp-bw   0 value sp-bh   0 value sp-bc
: spr-ok? ( x y -- f )   sp-bs spr-h <  swap sp-bs spr-w <  and ;
: spr-used@ ( x y -- a )   sp-bs spr-w * + spr-used + ;
: spr-free? ( x y -- f )   ( inside, of the color sp-bc and not used )
  2dup spr-ok? 0= if 2drop 0 exit then
  2dup sp-bs spr-pix@ sp-bc = >r  spr-used@ c@ 0= r> and ;
: spr-row-free? ( x y w -- f )
  -1 swap 0 do   \ x y f
    2 pick i + 2 pick spr-free? and
  loop nip nip ;
: spr-mark ( -- )   sp-bh 0 do sp-bw 0 do  1  sp-bx i + sp-by j + spr-used@ c!  loop loop ;

: spr-build ( spr -- )
  to sp-bs   0 sp-bs 2 cells + !
  spr-used sp-bs spr-w sp-bs spr-h * erase
  sp-bs spr-h 0 do sp-bs spr-w 0 do
    i j sp-bs spr-pix@ to sp-bc
    sp-bc [char] . <> if
      i j spr-free? if
        i to sp-bx   j to sp-by   1 to sp-bw   1 to sp-bh
        begin sp-bx sp-bw + sp-by spr-free? while 1 +to sp-bw repeat
        begin sp-bx sp-by sp-bh + sp-bw spr-row-free? while 1 +to sp-bh repeat
        spr-mark
        sp-bx sp-by sp-bw sp-bh sp-bc spr-rect  sp-bs spr-rects sp-bs spr-n cells + !
        1 sp-bs 2 cells + +!
      then
    then
  loop loop ;

\ ---- Defining and drawing ----
0 value spr-cur   0 value spr-row#   0 value sp-sa   0 value sp-sn
64 constant spr-max
create spr-all spr-max cells allot   0 value spr-count   \ every sprite defined, in order
: sprite: ( w h "name" -- )
  >in @ >r  create  r> >in !
  here to spr-cur   0 to spr-row#
  spr-count spr-max < if spr-cur spr-all spr-count cells + !  1 +to spr-count then
  2dup swap , ,  0 ,
  parse-name 31 min to sp-sn to sp-sa
  sp-sn c,  sp-sa here sp-sn cmove  sp-sn allot  31 sp-sn - allot
  2dup * dup 1 cells + 1- 1 cells / cells  here over [char] . fill  allot
  nip nip cells allot ;
: spr-row ( a n -- )   ( the next row of the grid )
  spr-cur spr-w min
  spr-row# spr-cur spr-w *  spr-cur spr-grid +  swap cmove
  1 +to spr-row# ;
: ;sprite ( -- )   spr-cur spr-build ;

0 value sp-spr   0 value sp-x   0 value sp-y   0 value sp-s
: spr-box ( r -- )   ( one rectangle: x | y<<6 | w<<12 | h<<18 | char<<24 )
  dup spr-rc spr-color@ color
  dup spr-rx sp-s * sp-x +
  over spr-ry sp-s * sp-y +
  2 pick spr-rw sp-s *
  3 pick spr-rh sp-s *
  box drop ;
: spr-drawn ( spr x y s -- )   ( at x y, every pixel s times bigger )
  to sp-s  to sp-y  to sp-x  to sp-spr
  sp-spr spr-n 0 do  sp-spr spr-rects i cells + @ spr-box  loop ;
: spr-draw ( spr x y -- )   1 spr-drawn  0 drop ;   \ not a tail call: see PITFALLS

\ ---- Source text, for the editor ----
4096 constant spr-bufsize
create spr-buf spr-bufsize allot
0 value spr-len
: spr-reset ( -- )   0 to spr-len ;
: spr+ ( a n -- )   spr-len over + spr-bufsize 1- > if 2drop exit then
  spr-buf spr-len + swap dup +to spr-len cmove ;
: spr-c+ ( c -- )   spr-len spr-bufsize 1- < if spr-buf spr-len + c!  1 +to spr-len else drop then ;
: spr-nl ( -- )   10 spr-c+ ;
: spr-num ( n -- )   s>d <# #s #> spr+ ;
: spr-hex ( u -- )   base @ >r hex  0 <# # # # # # # [char] $ hold #> spr+  r> base ! ;

: spr-source ( spr -- a n )   ( the text that makes this sprite again )
  { spr }  spr-reset
  spr spr-w spr-num  32 spr-c+  spr spr-h spr-num  s"  sprite: " spr+  spr spr-name spr+  spr-nl
  spr spr-h 0 do
    s"   s" spr+  34 spr-c+  32 spr-c+
    spr spr-grid i spr spr-w * +  spr spr-w spr+
    34 spr-c+  s"  spr-row" spr+  spr-nl
  loop
  s" ;sprite" spr+  spr-nl
  spr-buf spr-len ;

: spr-pal-source ( -- a n )   ( one line per color of the palette )
  spr-reset
  127 33 do
    i [char] . <> if
      i spr-defined? if
        i spr-color@ spr-hex  32 spr-c+  i spr-num  s"  spr-pal   \ " spr+  i spr-c+  spr-nl
      then
    then
  loop
  spr-buf spr-len ;
