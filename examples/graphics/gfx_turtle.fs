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

\ Turtle graphics (Logo) with SDL2, after forthtoise.fs of Eric Forgeot
\ (https://gitlab.com/garvalf/forth-is-fun, MIT license), which itself builds on
\ gforth-turtle (Robert Coffey) and WAForth (Remko Troncon). The words and their
\ names are the same, in English and in French; the code is new: the strokes are
\ kept in a list and drawn again at each frame, so the window can be moved or
\ covered, and the colors and the pen size work.
\
\   forward backward left right        n          move or turn (degrees, clockwise)
\   penup pendown                                 move without or with drawing
\   setxy ( x y -- )  setheading ( deg -- )  home clearscreen
\   setpencolor ( $rrggbb -- )  setpensize ( n -- )  setscreencolor ( $rrggbb -- )
\   hideturtle showturtle   slow ( ms -- ) 0 = at once   watch  ( shows, Esc quits )
\ Short names: fw bk lt rt color heading.  French: avance recule gauche droite
\ aller oriente leve pose taille cache montre efface maison.
\ The origin is the center of the window, heading 0 points up.
\
\ Run the file to get the shell: the window shows the drawing and, below it, a prompt
\ where the commands are typed (no terminal needed, so it works on the web and on a
\ Linux console too; Esc clears the line, the wheel scrolls the log). A line is not drawn at once, it is added to the program;
\ then `go` draws it instantly and `play` draws it stroke by stroke.
\     logo> 100 forward 90 right 50 forward        add a line
\     logo> go                                     draw instantly
\     logo> speed 100  and  play                   draw slowly, 100 ms a stroke (Esc stops)
\ Also: list, undo (drops the last line), new, demo (adds an example), help, bye.
\ A line is any Forth: `4 0 do 100 forward 90 right loop`, or your own words.
\ From a program of yours, end the drawing with `watch` (shows it until Esc) or run
\ `logo` yourself after including this file (`turtle-no-run` defined: nothing starts).

sdl2

[undefined] fold-case [if]   ( the web build has no such word )
: fold-case ( c -- c ) dup 65 >= over 90 <= and if 32 + then ;
[then]

640 constant tw   480 constant th
8192 constant max-seg

( ---- State ---- )
fvariable tx   fvariable ty   fvariable theading   fvariable ang   ( position and heading, floats )
1 value pen-down?   1 value turtle-shown?   1 value pen-size
$ffffff value pen-rgb   $101828 value screen-rgb
0 value nseg   0 value slow-ms   variable from-x   variable from-y
create segs max-seg 6 * cells allot   ( x1 y1 x2 y2 rgb size )

: seg ( n -- a ) 6 * cells segs + ;
: fround ( f: x -- n )   ( nearest integer )
  fdup f0< if 0.5e f- else 0.5e f+ then f>s ;
: fdeg>rad ( f: deg -- rad ) 3.14159265e f* 180e f/ ;

: home ( -- ) 0e tx sf!  0e ty sf!  0e theading sf! ;
: clearscreen ( -- ) 0 to nseg  home ;

( ---- Drawing the list of strokes ---- )
: thick-line ( x1 y1 x2 y2 size -- )   ( a stroke wider than one pixel )
  { x1 y1 x2 y2 size }
  size 1 <= if x1 y1 x2 y2 line exit then
  size 2 / dup negate ?do
    size 2 / dup negate ?do
      x1 j + y1 i + x2 j + y2 i + line
    loop
  loop ;

: tpos ( -- x y )   ( the turtle in window pixels )
  tx sf@ fround tw 2 / +   ty sf@ fround th 2 / + ;

: corner ( r deg -- x y )   ( the point at distance r, heading+deg, in window pixels )
  { r deg }
  theading sf@ deg s>f f+ fdeg>rad ang sf!
  ang sf@ fsin r s>f f* fround
  ang sf@ fcos r s>f f* fround negate
  tpos rot + >r + r> ;

: draw-turtle ( -- )   ( a small triangle pointing along the heading )
  green color
  12 0 corner  8 140 corner  8 -140 corner
  { ax ay bx by cx cy }
  ax ay bx by line   bx by cx cy line   cx cy ax ay line ;

: draw-seg ( n -- )
  seg { s }
  s 4 cells + @ color
  s @ s cell+ @ s 2 cells + @ s 3 cells + @ s 5 cells + @ thick-line ;

( ---- The console below the drawing: text drawn with boxes, so it needs no font file and
  runs the same on Linux, Windows, macOS and the web ---- )
2 constant ts   tw 8 ts * / constant cols   ( scale of the 8x8 font, characters a line )
4 constant con-lines   8 ts * 2 + constant lh   con-lines 1+ lh * 10 + constant con-h

\ The font is font8x8 by Daniel Hepper (https://github.com/dhepper/font8x8), public domain,
\ the same table as ports/baremetal/font8x8.h.
create font8   ( the characters from space to ~, 8 rows each, bit 0 is the left pixel )
  $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c,
  $18 c, $3c c, $3c c, $18 c, $18 c, $00 c, $18 c, $00 c,
  $36 c, $36 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c,
  $36 c, $36 c, $7f c, $36 c, $7f c, $36 c, $36 c, $00 c,
  $0c c, $3e c, $03 c, $1e c, $30 c, $1f c, $0c c, $00 c,
  $00 c, $63 c, $33 c, $18 c, $0c c, $66 c, $63 c, $00 c,
  $1c c, $36 c, $1c c, $6e c, $3b c, $33 c, $6e c, $00 c,
  $06 c, $06 c, $03 c, $00 c, $00 c, $00 c, $00 c, $00 c,
  $18 c, $0c c, $06 c, $06 c, $06 c, $0c c, $18 c, $00 c,
  $06 c, $0c c, $18 c, $18 c, $18 c, $0c c, $06 c, $00 c,
  $00 c, $66 c, $3c c, $ff c, $3c c, $66 c, $00 c, $00 c,
  $00 c, $0c c, $0c c, $3f c, $0c c, $0c c, $00 c, $00 c,
  $00 c, $00 c, $00 c, $00 c, $00 c, $0c c, $0c c, $06 c,
  $00 c, $00 c, $00 c, $3f c, $00 c, $00 c, $00 c, $00 c,
  $00 c, $00 c, $00 c, $00 c, $00 c, $0c c, $0c c, $00 c,
  $60 c, $30 c, $18 c, $0c c, $06 c, $03 c, $01 c, $00 c,
  $3e c, $63 c, $73 c, $7b c, $6f c, $67 c, $3e c, $00 c,
  $0c c, $0e c, $0c c, $0c c, $0c c, $0c c, $3f c, $00 c,
  $1e c, $33 c, $30 c, $1c c, $06 c, $33 c, $3f c, $00 c,
  $1e c, $33 c, $30 c, $1c c, $30 c, $33 c, $1e c, $00 c,
  $38 c, $3c c, $36 c, $33 c, $7f c, $30 c, $78 c, $00 c,
  $3f c, $03 c, $1f c, $30 c, $30 c, $33 c, $1e c, $00 c,
  $1c c, $06 c, $03 c, $1f c, $33 c, $33 c, $1e c, $00 c,
  $3f c, $33 c, $30 c, $18 c, $0c c, $0c c, $0c c, $00 c,
  $1e c, $33 c, $33 c, $1e c, $33 c, $33 c, $1e c, $00 c,
  $1e c, $33 c, $33 c, $3e c, $30 c, $18 c, $0e c, $00 c,
  $00 c, $0c c, $0c c, $00 c, $00 c, $0c c, $0c c, $00 c,
  $00 c, $0c c, $0c c, $00 c, $00 c, $0c c, $0c c, $06 c,
  $18 c, $0c c, $06 c, $03 c, $06 c, $0c c, $18 c, $00 c,
  $00 c, $00 c, $3f c, $00 c, $00 c, $3f c, $00 c, $00 c,
  $06 c, $0c c, $18 c, $30 c, $18 c, $0c c, $06 c, $00 c,
  $1e c, $33 c, $30 c, $18 c, $0c c, $00 c, $0c c, $00 c,
  $3e c, $63 c, $7b c, $7b c, $7b c, $03 c, $1e c, $00 c,
  $0c c, $1e c, $33 c, $33 c, $3f c, $33 c, $33 c, $00 c,
  $3f c, $66 c, $66 c, $3e c, $66 c, $66 c, $3f c, $00 c,
  $3c c, $66 c, $03 c, $03 c, $03 c, $66 c, $3c c, $00 c,
  $1f c, $36 c, $66 c, $66 c, $66 c, $36 c, $1f c, $00 c,
  $7f c, $46 c, $16 c, $1e c, $16 c, $46 c, $7f c, $00 c,
  $7f c, $46 c, $16 c, $1e c, $16 c, $06 c, $0f c, $00 c,
  $3c c, $66 c, $03 c, $03 c, $73 c, $66 c, $7c c, $00 c,
  $33 c, $33 c, $33 c, $3f c, $33 c, $33 c, $33 c, $00 c,
  $1e c, $0c c, $0c c, $0c c, $0c c, $0c c, $1e c, $00 c,
  $78 c, $30 c, $30 c, $30 c, $33 c, $33 c, $1e c, $00 c,
  $67 c, $66 c, $36 c, $1e c, $36 c, $66 c, $67 c, $00 c,
  $0f c, $06 c, $06 c, $06 c, $46 c, $66 c, $7f c, $00 c,
  $63 c, $77 c, $7f c, $7f c, $6b c, $63 c, $63 c, $00 c,
  $63 c, $67 c, $6f c, $7b c, $73 c, $63 c, $63 c, $00 c,
  $1c c, $36 c, $63 c, $63 c, $63 c, $36 c, $1c c, $00 c,
  $3f c, $66 c, $66 c, $3e c, $06 c, $06 c, $0f c, $00 c,
  $1e c, $33 c, $33 c, $33 c, $3b c, $1e c, $38 c, $00 c,
  $3f c, $66 c, $66 c, $3e c, $36 c, $66 c, $67 c, $00 c,
  $1e c, $33 c, $07 c, $0e c, $38 c, $33 c, $1e c, $00 c,
  $3f c, $2d c, $0c c, $0c c, $0c c, $0c c, $1e c, $00 c,
  $33 c, $33 c, $33 c, $33 c, $33 c, $33 c, $3f c, $00 c,
  $33 c, $33 c, $33 c, $33 c, $33 c, $1e c, $0c c, $00 c,
  $63 c, $63 c, $63 c, $6b c, $7f c, $77 c, $63 c, $00 c,
  $63 c, $63 c, $36 c, $1c c, $1c c, $36 c, $63 c, $00 c,
  $33 c, $33 c, $33 c, $1e c, $0c c, $0c c, $1e c, $00 c,
  $7f c, $63 c, $31 c, $18 c, $4c c, $66 c, $7f c, $00 c,
  $1e c, $06 c, $06 c, $06 c, $06 c, $06 c, $1e c, $00 c,
  $03 c, $06 c, $0c c, $18 c, $30 c, $60 c, $40 c, $00 c,
  $1e c, $18 c, $18 c, $18 c, $18 c, $18 c, $1e c, $00 c,
  $08 c, $1c c, $36 c, $63 c, $00 c, $00 c, $00 c, $00 c,
  $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $ff c,
  $0c c, $0c c, $18 c, $00 c, $00 c, $00 c, $00 c, $00 c,
  $00 c, $00 c, $1e c, $30 c, $3e c, $33 c, $6e c, $00 c,
  $07 c, $06 c, $06 c, $3e c, $66 c, $66 c, $3b c, $00 c,
  $00 c, $00 c, $1e c, $33 c, $03 c, $33 c, $1e c, $00 c,
  $38 c, $30 c, $30 c, $3e c, $33 c, $33 c, $6e c, $00 c,
  $00 c, $00 c, $1e c, $33 c, $3f c, $03 c, $1e c, $00 c,
  $1c c, $36 c, $06 c, $0f c, $06 c, $06 c, $0f c, $00 c,
  $00 c, $00 c, $6e c, $33 c, $33 c, $3e c, $30 c, $1f c,
  $07 c, $06 c, $36 c, $6e c, $66 c, $66 c, $67 c, $00 c,
  $0c c, $00 c, $0e c, $0c c, $0c c, $0c c, $1e c, $00 c,
  $30 c, $00 c, $30 c, $30 c, $30 c, $33 c, $33 c, $1e c,
  $07 c, $06 c, $66 c, $36 c, $1e c, $36 c, $67 c, $00 c,
  $0e c, $0c c, $0c c, $0c c, $0c c, $0c c, $1e c, $00 c,
  $00 c, $00 c, $33 c, $7f c, $7f c, $6b c, $63 c, $00 c,
  $00 c, $00 c, $1f c, $33 c, $33 c, $33 c, $33 c, $00 c,
  $00 c, $00 c, $1e c, $33 c, $33 c, $33 c, $1e c, $00 c,
  $00 c, $00 c, $3b c, $66 c, $66 c, $3e c, $06 c, $0f c,
  $00 c, $00 c, $6e c, $33 c, $33 c, $3e c, $30 c, $78 c,
  $00 c, $00 c, $3b c, $6e c, $66 c, $06 c, $0f c, $00 c,
  $00 c, $00 c, $3e c, $03 c, $1e c, $30 c, $1f c, $00 c,
  $08 c, $0c c, $3e c, $0c c, $0c c, $2c c, $18 c, $00 c,
  $00 c, $00 c, $33 c, $33 c, $33 c, $33 c, $6e c, $00 c,
  $00 c, $00 c, $33 c, $33 c, $33 c, $1e c, $0c c, $00 c,
  $00 c, $00 c, $63 c, $6b c, $7f c, $7f c, $36 c, $00 c,
  $00 c, $00 c, $63 c, $36 c, $1c c, $36 c, $63 c, $00 c,
  $00 c, $00 c, $33 c, $33 c, $33 c, $3e c, $30 c, $1f c,
  $00 c, $00 c, $3f c, $19 c, $0c c, $26 c, $3f c, $00 c,
  $38 c, $0c c, $0c c, $07 c, $0c c, $0c c, $38 c, $00 c,
  $18 c, $18 c, $18 c, $00 c, $18 c, $18 c, $18 c, $00 c,
  $07 c, $0c c, $0c c, $38 c, $0c c, $0c c, $07 c, $00 c,
  $6e c, $3b c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c,

variable gx   variable gy   variable gbits   variable gstart   variable gy0   variable gp
: row-boxes ( bits y -- )   ( the runs of set bits of one row of a character )
  gy !  gbits !  -1 gstart !
  9 0 do
    i 8 < if gbits @ i rshift 1 and else 0 then
    if gstart @ 0< if i gstart ! then
    else gstart @ 0< 0= if gx @ gstart @ ts * +  gy @  i gstart @ - ts *  ts box  -1 gstart ! then then
  loop ;
: glyph ( c x y -- )
  gy0 !  gx !  32 - 0 max 94 min 8 * font8 + gp !
  8 0 do  gp @ i + c@  gy0 @ i ts * +  row-boxes  loop ;
: draw-text ( x y a n -- )
  { x y a n }
  n 0 ?do a i + c@  x i 8 ts * * +  y  glyph loop ;

100 constant log-max   80 constant log-w
create logbuf log-max log-w * allot   create loglen log-max cells allot
create curl log-w allot   0 value curn   0 value log-n   0 value log-off
create line 128 allot   0 value llen
create last-line 128 allot   0 value last-n
0 value console?

: log-commit ( -- )
  curl  log-n log-max mod log-w * logbuf +  curn move
  curn  log-n log-max mod cells loglen + !
  1 +to log-n  0 to curn ;
: log-char ( c -- )
  dup 10 = if drop log-commit exit then
  dup 32 < if drop exit then
  curn cols >= if log-commit then
  curl curn + c!  1 +to curn ;
: log-type ( a n -- )   ( the new `type` while the shell runs: the text goes to the console )
  0 ?do dup i + c@ log-char loop drop ;
: log-line@ ( k -- a n )   ( the line number k, 0 for the first one ever written )
  log-max mod dup log-w * logbuf +  swap cells loglen + @ ;
: log-back ( n -- )   ( scroll the console back by n lines, negative: forward )
  log-off + 0 max  log-n con-lines - 0 max min  to log-off ;

create pbuf 160 allot   variable shown
: prompt-y ( -- y )   th con-lines lh * + 6 + ;
: draw-prompt ( -- )
  cols 7 - llen min shown !
  s" logo> " pbuf swap move
  line llen + shown @ -  pbuf 6 + shown @ move
  0 prompt-y pbuf shown @ 6 + draw-text
  ticks 500 / 1 and if
    shown @ 6 + 8 ts * *  prompt-y 8 ts * ts - +  8 ts *  ts box
  then ;

: draw-log-row ( i -- )
  dup lh * th 6 + +  swap  log-n con-lines - + log-off -  { y k }
  k -1 > k log-n < and k log-n log-max - >= and if
    0 y k log-line@ draw-text
  then ;

: draw-console ( -- )
  $0c0c14 color  0 th tw con-h box
  $3a3a50 color  0 th tw 2 box
  $c8e0ff color
  con-lines 0 do i draw-log-row loop
  $ffffff color  draw-prompt ;

: render ( -- )
  screen-rgb color cls
  nseg 0 ?do i draw-seg loop
  turtle-shown? if draw-turtle then
  console? if draw-console then
  flip ;

: wait-frame ( -- )   ( one frame while slow motion is on, Esc leaves )
  events
  key-esc pressed? if key-esc unpress  -99 throw then
  quit? if -99 throw then
  render slow-ms delay ;

( ---- The moves ---- )
: record ( x1 y1 x2 y2 -- )
  nseg max-seg >= if 2drop 2drop exit then
  nseg seg { s }
  s 3 cells + !  s 2 cells + !  s cell+ !  s !
  pen-rgb s 4 cells + !  pen-size s 5 cells + !
  1 +to nseg ;

: forward ( n -- )
  { n }  tpos from-y ! from-x !
  theading sf@ fdeg>rad ang sf!
  tx sf@  ang sf@ fsin n s>f f* f+  tx sf!
  ty sf@  ang sf@ fcos n s>f f* f-  ty sf!
  pen-down? if from-x @ from-y @ tpos record then
  slow-ms if wait-frame then ;
: backward ( n -- ) negate forward ;
: right ( n -- ) s>f theading sf@ f+ theading sf! ;
: left ( n -- ) negate right ;
: setheading ( n -- ) s>f theading sf! ;
: setxy ( x y -- )
  { x y }  tpos from-y ! from-x !
  x s>f tx sf!  y s>f ty sf!
  pen-down? if from-x @ from-y @ tpos record then ;
: penup ( -- ) 0 to pen-down? ;
: pendown ( -- ) 1 to pen-down? ;
: hideturtle ( -- ) 0 to turtle-shown? ;
: showturtle ( -- ) 1 to turtle-shown? ;
: setpencolor ( $rrggbb -- ) to pen-rgb ;
: setpensize ( n -- ) 1 max to pen-size ;
: setscreencolor ( $rrggbb -- ) to screen-rgb ;
: slow ( ms -- ) to slow-ms ;

: fw forward ;   : bk backward ;   : lt left ;   : rt right ;
: heading setheading ;   : color! setpencolor ;
: hidepen hideturtle ;   : showpen showturtle ;

( French: the names of forthtoise )
: avance forward ;   : recule backward ;   : gauche left ;   : droite right ;
: aller setxy ;   : oriente setheading ;   : leve penup ;   : pose pendown ;
: taille setpensize ;   : cache hideturtle ;   : montre showturtle ;
: efface clearscreen ;   : maison home ;

( ---- Showing the result ---- )
: open-turtle ( -- )
  screen-open? 0= if tw  console? if th con-h + else th then  screen s" Turtle" title then ;
: watch ( -- )   ( draws until Esc or the window is closed )
  open-turtle
  begin events key-esc pressed? if quit! then render 16 delay quit? until
  close-screen ;

( ---- Drawings ---- )
: enneagram ( size -- )   ( the 9-pointed star of forthtoise )
  { n }  9 0 do n forward 65 left n forward 145 right loop ;

: spiral ( -- )
  200 0 do i 3 * 20 + 2 / forward 91 right loop ;

: flower ( -- )
  36 0 do
    i 7 * 16 lshift  255 i 7 * - 8 lshift or  200 or setpencolor
    80 enneagram  10 right
  loop ;

: say ( a n -- ) type cr ;

( ---- The shell: lines are kept as a program, then drawn ---- )
8192 constant prog-max
create prog prog-max allot   0 value plen
create src prog-max 32 + allot
40 value play-ms   0 value line-no
variable w-a   variable w-n   variable r-a   variable r-n   ( first word of a line, and the rest )

: same? ( a n b m -- f )   ( the same text, whatever the case )
  { a n b m }
  n m <> if 0 exit then
  n 0 ?do
    a i + c@ fold-case  b i + c@ fold-case <> if 0 unloop exit then
  loop -1 ;

: split-word ( a n -- )   ( first word to w-a w-n, what follows to r-a r-n )
  begin dup 0> if over c@ 33 < else 0 then while 1 /string repeat
  over w-a !  0 w-n !
  begin dup 0> if over c@ 32 > else 0 then while 1 /string 1 w-n +! repeat
  begin dup 0> if over c@ 33 < else 0 then while 1 /string repeat
  r-n !  r-a ! ;

: is? ( a n -- f ) w-a @ w-n @ same? ;
: number? ( a n -- x f )   ( the whole text is a number )
  0 0 2swap >number nip nip 0= ;

: prog-add ( a n -- )
  { a n }
  plen n + 1+ prog-max > if s" the program is full" say exit then
  a prog plen + n move  n +to plen  10 prog plen + c!  1 +to plen ;

: prog-undo ( -- )
  plen 0= if exit then
  -1 +to plen
  begin plen 0> if prog plen 1- + c@ 10 <> else 0 then while -1 +to plen repeat ;

: prog-list ( -- )
  plen 0= if s" (empty)" say exit then
  1 to line-no  line-no . ." "
  plen 0 do
    prog i + c@ dup 10 = if
      drop cr  1 +to line-no
      i 1+ plen < if line-no . ." " then
    else emit then
  loop ;

also internals
: safe-eval ( a n -- err )   ( evaluate, then the source back in place after a throw )
  'tib @ #tib @ >in @ { a n t m i }  depth { d }
  a n ['] evaluate catch   t 'tib !  m #tib !  i >in !
  begin depth d 1+ > while nip repeat ;   ( what a failed line left on the stack )
previous

: build-src ( -- a n )   ( ": logo-prog" + the program + ";" in src )
  s" : logo-prog " { h hn }
  h src hn move   prog src hn + plen move
  s"  ;" src hn + plen + swap move
  src  hn plen + 2 + ;

: run-prog ( ms -- )   ( draws the program from the start; 0 draws at once )
  slow-ms swap slow
  clearscreen
  build-src safe-eval ?dup if
    0 state !  s" does not compile: " type
    dup -1 = if drop s" an unknown word" type else . then
    s"  (undo drops the last line)" say
    slow render exit
  then
  s" logo-prog" safe-eval
  swap slow
  ?dup if
    dup -99 = if drop s" stopped" say else ." error " . s" (undo drops the last line)" say then
  then
  render ;

: demo-lines ( -- )
  s" $40d0ff setpencolor  spiral" prog-add
  s" penup 0 setheading 0 0 setxy pendown" prog-add
  s" flower" prog-add ;

: help-shell ( -- )
  s" a line is added to the program (any Forth: 100 forward 90 right)" say
  s" go           draw it at once" say
  s" play         draw it stroke by stroke (Esc in the window stops)" say
  s" speed n      n ms per stroke for play, now " type play-ms . cr
  s" list undo new demo help bye" say ;

: do-line ( -- )
  line llen split-word
  w-n @ 0= if exit then
  s" go" is? if 0 run-prog exit then
  s" play" is? if play-ms run-prog exit then
  s" speed" is? if r-a @ r-n @ number? if 1 max to play-ms else drop then
    ." speed " play-ms . cr exit then
  s" list" is? if prog-list exit then
  s" undo" is? if prog-undo 0 run-prog exit then
  s" new" is? if 0 to plen clearscreen render exit then
  s" demo" is? if demo-lines s" demo added: go or play" say exit then
  s" help" is? if help-shell exit then
  s" bye" is? if quit! exit then
  line llen prog-add ;

( ---- The line typed in the window ---- )
: recall ( -- )   last-line line last-n move  last-n to llen ;
: remember ( -- )   llen if line last-line llen move  llen to last-n then ;
: edit-key ( c -- done? )
  dup 13 = if drop -1 exit then
  dup 8 = if drop llen if -1 +to llen then 0 exit then
  dup 1 = if drop recall 0 exit then
  dup 32 >= over 127 < and llen 127 < and if line llen + c! 1 +to llen 0 exit then
  drop 0 ;

: read-line ( -- f )   ( a line typed in the window, drawn again meanwhile; 0 = quit )
  curn if log-commit then
  0 to llen  0 to log-off
  begin
    events
    quit? if 0 exit then
    key-esc pressed? if key-esc unpress  0 to llen then
    wheel ?dup if log-back then
    begin typed ?dup while
      edit-key if
        s" logo> " log-type  line llen log-type  10 log-char
        remember  -1 exit
      then
    repeat
    render 25 delay
  again ;

0 value old-type
: logo ( -- )
  -1 to console?  open-turtle  clearscreen
  ['] type >body @ to old-type  ['] log-type ['] type >body !
  s" Turtle: type commands here" say
  s" help lists them, Esc clears the line" say
  begin read-line while do-line repeat
  old-type ['] type >body !
  close-screen ;

[defined] turtle-no-run [if] [else]
logo
script-bye
[then]
