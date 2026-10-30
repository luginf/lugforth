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

\ A small desktop, 800x600, in an SDL2 window: `lugforth examples/utils/desktop.fs`, or `desk` at the prompt.
\   icons          double click: the file manager (a window of the desktop), the editors
\                  writhdeck and ed (a text terminal in a window), or a Forth program
\                  (the desktop closes, the program runs, the desktop comes back)
\   icons          drag them to place them; Settings: color of the background, size of the desktop
\                  (places, color and size are kept in $HOME/.config/lugforth/desktop.ini, Forth lines)
\   icon format    one line per icon: `kind s" label" s" command" x y icon@`, kinds: 0 file manager,
\                  1 command run in a terminal window (ed, writhdeck, repl), 2 Forth program (path from the
\                  project folder, or absolute when it starts with /), 3 settings, 4 SDL Forth console
\                  (`desk` brings the desktop back). The file can be edited by hand.
\   windows        drag the title bar, click to raise, x closes (a terminal gets Ctrl+Q)
\   file manager   a double click on a .fs runs it, any other file opens in writhdeck
\   taskbar        the open windows, and Quit
\ It can be started from any folder of the project (examples/utils too): the folder of the project is looked for
\ from where it starts, the programs of the icons are found from there.
\ One text terminal at a time: it is the editor, running in a loop that draws the desktop
\ whenever it waits for a key. Needs libSDL2, libSDL2_ttf, DejaVu fonts (see term-font-file).

: filer-no-run ;
include filer.fs
also sdl2 also fm definitions

: ui-font-file ( -- a n ) s" /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf" ;
: term-font-file ( -- a n ) s" /usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf" ;

z" SDL_RenderSetViewport" 2 sdl SDL_RenderSetViewport ( a a -- n )
z" SDL_StartTextInput" 0 sdl SDL_StartTextInput ( -- void )
z" SDL_StopTextInput" 0 sdl SDL_StopTextInput ( -- void )
z" SDL_GetModState" 0 sdl SDL_GetModState ( -- n )
$0303 constant SDL_TEXTINPUT

800 value dk-w   600 value dk-h     ( the size of the desktop, set in the settings )
$2a5070 value dk-bg                    ( the color of the background )
0 value resize?   0 value console?                        ( the screen has to be opened again )
28 constant bar-h        ( the taskbar )
20 constant title-h
0 value ui-font   0 value term-font
create dk-home 300 allot
-1 value dk-dirty   ( something changed: draw again )
0 value last-phase
0 value dk-nested        ( a terminal program is running, it asks for keys in a loop of ours )

( ---- The terminal: a grid of characters with colors, and the ANSI codes the editors use ---- )
120 constant t-max-c   50 constant t-max-r
create t-chars t-max-c t-max-r * 4 * allot     ( code points )
create t-attrs t-max-c t-max-r * 4 * allot     ( as in the console: 9 bits of foreground, 9 of background, flags )
0 value t-cols   0 value t-rows   0 value t-cw   0 value t-ch
0 value t-x   0 value t-y   0 value t-sx   0 value t-sy
0 value t-sgr   0 value t-curvis
0 value t-esc   0 value t-p1   0 value t-p2   0 value t-acc   0 value t-np   0 value t-priv
0 value t-ucp   0 value t-uneed
create t-prm 8 cells allot
$d2d2d2 value t-fg   $101018 value t-bg
: t-i ( x y -- i ) t-max-c * + ;
: t-set ( c x y -- )   t-i dup >r 4 * t-chars + l!  t-sgr r> 4 * t-attrs + l! ;
: t-blank ( x y n -- )   \ n cells from x y emptied
  0 { x y n i }
  begin i n < while
    32 x i + y t-i 4 * t-chars + l!   0 x i + y t-i 4 * t-attrs + l!
    1 +to i
  repeat ;
: t-clear ( -- ) 0 0 t-max-c t-max-r * t-blank  0 to t-x 0 to t-y ;
: t-scroll ( -- )
  t-chars t-max-c 4 * + t-chars t-max-c t-max-r 1- * 4 * cmove
  t-attrs t-max-c 4 * + t-attrs t-max-c t-max-r 1- * 4 * cmove
  0 t-max-r 1- t-max-c t-blank ;
: t-nl ( -- )
  0 to t-x  1 +to t-y
  t-y t-rows >= if
    t-scroll t-rows 1- to t-y
    0 t-rows t-max-r t-rows - t-max-c * t-blank then ;
: t-put ( cp -- )
  t-x t-cols >= if t-nl then
  t-x t-y t-set  1 +to t-x ;

( colors as in xterm: 0 to 15 the ANSI ones, 16 to 231 a cube, 232 to 255 grays )
: ansi16 ( i -- $rrggbb )
  { i }
  i 7 = if $e5e5e5 exit then   i 8 = if $7f7f7f exit then
  i 12 = if $5c5cff exit then   i 4 = if $0000ee exit then
  i 8 < if 205 else 255 then { v }
  i 1 and if v 16 lshift else 0 then
  i 2 and if v 8 lshift or then
  i 4 and if v or then ;
: cube-comp ( c -- v ) dup 0= if exit then 40 * 55 + ;
: xterm>rgb ( i -- $rrggbb )
  dup 16 < if ansi16 exit then
  dup 232 >= if 232 - 10 * 8 + dup 16 lshift over 8 lshift or or exit then
  16 - { n }
  n 36 / cube-comp 16 lshift  n 6 / 6 mod cube-comp 8 lshift or  n 6 mod cube-comp or ;

: t-fg! ( n -- )   t-sgr $1ff invert and  swap 1+ or to t-sgr ;       ( n is 0 to 255, or -1 for the default )
: t-bg! ( n -- )   t-sgr $3fe00 invert and  swap 1+ 9 lshift or to t-sgr ;
: t-flag ( mask f -- )   if t-sgr or else invert t-sgr and then to t-sgr ;
: t-sgr1 ( v -- )
  dup 0 = if drop 0 to t-sgr exit then
  dup 1 = if drop $80000 -1 t-flag exit then
  dup 4 = if drop $200000 -1 t-flag exit then
  dup 7 = if drop $40000 -1 t-flag exit then
  dup 22 = if drop $80000 0 t-flag exit then
  dup 24 = if drop $200000 0 t-flag exit then
  dup 27 = if drop $40000 0 t-flag exit then
  dup 39 = if drop -1 t-fg! exit then
  dup 49 = if drop -1 t-bg! exit then
  dup 30 >= over 37 <= and if 30 - t-fg! exit then
  dup 40 >= over 47 <= and if 40 - t-bg! exit then
  dup 90 >= over 97 <= and if 82 - t-fg! exit then
  dup 100 >= over 107 <= and if 92 - t-bg! exit then
  drop ;
: t-sgr-apply ( -- )
  0 0 { si sv }
  t-np 0= if 0 to t-sgr exit then
  begin si t-np < while
    t-prm si cells + @ to sv
    sv 38 = sv 48 = or if
      t-prm si 1+ cells + @ 5 = if
        t-prm si 2 + cells + @ 255 and  sv 38 = if t-fg! else t-bg! then
        2 +to si
      else t-prm si 1+ cells + @ 2 = if 4 +to si then then
    else sv t-sgr1 then
    1 +to si
  repeat ;

: t-csi ( c -- )
  dup [char] H = over [char] f = or if
    drop  t-p2 1- 0 max t-cols 1- min to t-x   t-p1 1- 0 max t-rows 1- min to t-y exit then
  dup [char] J = if
    drop t-p1 2 = if t-clear exit then
    t-p1 0 = if t-x t-y t-cols t-x - t-blank  0 t-y 1+ t-max-c t-max-r t-y 1+ - * t-blank then exit then
  dup [char] K = if
    drop t-p1 0 = if t-x t-y t-cols t-x - 0 max t-blank exit then
    t-p1 1 = if 0 t-y t-x 1+ t-blank exit then
    0 t-y t-cols t-blank exit then
  dup [char] A = if drop t-y t-p1 1 max - 0 max to t-y exit then
  dup [char] B = if drop t-p1 1 max t-y + t-rows 1- min to t-y exit then
  dup [char] C = if drop t-p1 1 max t-x + t-cols 1- min to t-x exit then
  dup [char] D = if drop t-x t-p1 1 max - 0 max to t-x exit then
  dup [char] G = if drop t-p1 1- 0 max t-cols 1- min to t-x exit then
  dup [char] s = if drop t-x to t-sx t-y to t-sy exit then
  dup [char] u = if drop t-sx to t-x t-sy to t-y exit then
  dup [char] m = if drop t-sgr-apply exit then
  dup [char] l = if drop t-priv t-p1 25 = and if 0 to t-curvis then exit then
  dup [char] h = if drop t-priv t-p1 25 = and if -1 to t-curvis then exit then
  drop ;
: t-csi-char ( c -- )
  dup [char] 0 >= over [char] 9 <= and if
    [char] 0 - t-acc 10 * + to t-acc exit then
  dup [char] ; = if drop t-np 8 < if t-acc t-prm t-np cells + ! 1 +to t-np then 0 to t-acc exit then
  dup [char] ? = if drop -1 to t-priv exit then
  dup 64 >= over 126 <= and if
    0 to t-esc
    t-np 8 < if t-acc t-prm t-np cells + ! 1 +to t-np then
    t-prm @ to t-p1   t-prm cell+ @ to t-p2
    t-csi exit then
  drop ;
: t-putc ( c -- )
  t-esc 2 = if t-csi-char exit then
  t-esc 3 = if 7 = if 0 to t-esc then exit then
  t-esc 1 = if
    dup [char] [ = if drop 2 to t-esc  0 to t-acc  0 to t-np  0 to t-priv  t-prm 8 cells erase exit then
    [char] ] = if 3 to t-esc else 0 to t-esc then exit then
  t-uneed 0> if   \ the rest of a UTF-8 character
    $3f and  t-ucp 6 lshift or to t-ucp  -1 +to t-uneed
    t-uneed 0= if t-ucp t-put then exit then
  dup 27 = if drop 1 to t-esc exit then
  dup 10 = if drop t-nl exit then
  dup 13 = if drop 0 to t-x exit then
  dup 8 = if drop t-x 1- 0 max to t-x exit then
  dup 9 = if drop begin 32 t-put t-x 7 and 0= until exit then
  dup 32 < if drop exit then
  dup $c0 >= if
    dup $e0 >= if
      dup $f0 >= if $07 and 3 else $0f and 2 then
    else $1f and 1 then
    to t-uneed to t-ucp exit then
  dup $80 >= if drop exit then
  t-put ;
: t-type ( a n -- ) -1 to dk-dirty  0 ?do dup i + c@ t-putc loop drop ;

( keys for the program: a ring of bytes )
256 constant tq-size
create tq tq-size allot
0 value tq-h   0 value tq-t
: tq-push ( c -- ) tq-t tq-h - tq-size < if tq tq-t tq-size 1- and + c! 1 +to tq-t else drop then ;
: tq-pop ( -- c ) tq tq-h tq-size 1- and + c@ 1 +to tq-h ;
: tq-any? ( -- f ) tq-h tq-t <> ;
0 value repl-active   ( the terminal runs the Forth line editor: closing it types bye )
: bye-keys ( -- ) 21 tq-push [char] b tq-push [char] y tq-push [char] e tq-push 13 tq-push ;
: tq-csi ( c -- ) 27 tq-push [char] [ tq-push tq-push ;
: tq-tilde ( n -- ) 27 tq-push [char] [ tq-push [char] 0 + tq-push [char] ~ tq-push ;

: t-form ( -- rows cols ) t-rows t-cols ;

\ drawing: runs of cells with the same colors are one text
create run-buf 600 allot
0 value run-n
: cp>utf8 ( cp -- )   \ appended to run-buf
  dup $80 < if run-buf run-n + c!  1 +to run-n exit then
  dup $800 < if
    dup 6 rshift $c0 or run-buf run-n + c!   $3f and $80 or run-buf run-n 1+ + c!  2 +to run-n exit then
  dup $10000 < if
    dup 12 rshift $e0 or run-buf run-n + c!
    dup 6 rshift $3f and $80 or run-buf run-n 1+ + c!
    $3f and $80 or run-buf run-n 2 + + c!  3 +to run-n exit then
  drop 63 run-buf run-n + c! 1 +to run-n ;
: t-color ( idx9 default -- rgb ) over 0= if nip exit then drop 1- xterm>rgb ;
: t-run-draw ( x0 x1 y -- )   \ cells x0 up to x1 (not included) of the row y
  { x0 x1 y }
  0 0 0 { at fgc bgc }
  x0 y t-i 4 * t-attrs + ul@ to at
  at $1ff and t-fg t-color to fgc
  at 9 rshift $1ff and t-bg t-color to bgc
  at $40000 and if fgc bgc to fgc to bgc then
  bgc t-bg <> if bgc color x0 t-cw * y t-ch * x1 x0 - t-cw * t-ch box then
  0 to run-n
  x1 x0 ?do i y t-i 4 * t-chars + ul@ cp>utf8 loop
  fgc color
  x0 t-cw * y t-ch * run-buf run-n text
  at $200000 and if x0 t-cw * y 1+ t-ch * 1- x1 t-cw * y 1+ t-ch * 1- line then ;
: t-draw ( w h -- )
  { w h }
  0 0 0 { rr cc ee }
  t-bg color 0 0 w h box
  term-font font
  begin rr t-rows < while
    0 to cc
    begin cc t-cols < while
      cc to ee
      begin ee t-cols < if ee rr t-i 4 * t-attrs + ul@ cc rr t-i 4 * t-attrs + ul@ = else 0 then while 1 +to ee repeat
      cc ee rr t-run-draw
      ee to cc
    repeat
    1 +to rr
  repeat
  t-curvis if
    ticks 500 / 1 and 0= if t-fg color t-x t-cw * t-y t-ch * 2 t-ch box then then ;

( ---- Windows: a few, in a stack ---- )
8 constant max-win
16 cells constant win-size
create wins max-win win-size * allot
create zord max-win allot   0 value nz
: win ( i -- a ) win-size * wins + ;
: w-on ( i -- a ) win ;
: w-kind ( i -- a ) win 1 cells + ;     ( 1 file manager, 2 terminal )
: w-x ( i -- a ) win 2 cells + ;
: w-y ( i -- a ) win 3 cells + ;
: w-w ( i -- a ) win 4 cells + ;
: w-h ( i -- a ) win 5 cells + ;
: w-title ( i -- a ) win 6 cells + ;     ( 80 bytes, zero-ended )
: z-remove ( id -- )
  0 { id j }
  nz 0 ?do
    zord i + c@ id <> if zord i + c@ zord j + c! 1 +to j then
  loop
  j to nz ;
: z-add ( id -- ) zord nz + c!  1 +to nz ;
: raise ( id -- ) dup z-remove z-add ;
: top-win ( -- id ) nz 0= if -1 exit then zord nz 1- + c@ ;
: win-new ( kind x y w h a n -- id )
  { kind x y w h na nn }
  -1 { id }
  max-win 0 ?do i w-on @ 0= if i to id leave then loop
  id 0< if -1 exit then
  id win win-size erase
  -1 id w-on !  kind id w-kind !  x id w-x !  y id w-y !  w id w-w !  h id w-h !
  na id w-title nn 78 min cmove
  id z-add  id ;
: win-close ( id -- ) dup z-remove  0 swap w-on ! ;
: in-win? ( x y id -- f )
  { px py id }
  px id w-x @ >=  px id w-x @ id w-w @ + < and
  py id w-y @ >=  py id w-y @ id w-h @ + < and and ;
: win-at ( x y -- id )
  { px py }
  -1 nz { id k }
  begin k 0> id 0< and while
    -1 +to k
    px py zord k + c@ in-win? if zord k + c@ to id then
  repeat
  id ;
: cont-x ( id -- n ) w-x @ 1+ ;
: cont-y ( id -- n ) w-y @ title-h + 1+ ;
: cont-w ( id -- n ) w-w @ 2 - ;
: cont-h ( id -- n ) w-h @ title-h - 2 - ;

: viewport ( x y w h -- ) rect! renderer rect SDL_RenderSetViewport drop ;
: no-viewport ( -- ) renderer 0 SDL_RenderSetViewport drop ;

( the program's terminal, further down: the loop that draws everything while it waits for a key )
defer dk-frame
defer set-draw ( w h -- )   ( the settings window, further down )

: draw-win ( id -- )
  { id }
  $50505c color id w-x @ id w-y @ id w-w @ id w-h @ box
  id top-win = if $305890 else $606068 then color
  id w-x @ 1+ id w-y @ 1+ id w-w @ 2 - title-h 1- box
  ui-font font  white color
  id w-x @ 8 + id w-y @ 3 + id w-title z>s text
  $c04040 color id w-x @ id w-w @ + 22 - id w-y @ 3 + 18 16 box
  white color id w-x @ id w-w @ + 17 - id w-y @ 3 + s" x" text
  id cont-x id cont-y id cont-w id cont-h viewport
  id w-kind @ 1 = if
    id cont-w to fl-w  id cont-h to fl-h  fl-draw then
  id w-kind @ 2 = if
    id cont-w id cont-h t-draw then
  id w-kind @ 3 = if
    id cont-w id cont-h set-draw then
  no-viewport
  ui-font font ;

( ---- Icons: kind 0 the file manager, 1 a command in a terminal window, 2 a Forth program, 3 the settings, 4 the SDL console ---- )
12 constant max-icon
104 constant icon-size    ( byte 0 kind, 1 to 20 label, 21 to 94 command, 96 and 98 the place )
create icons max-icon icon-size * allot   0 value nicon
: icon ( kind la ln ca cn -- )   \ label and command, the command of a program is a path from the folder where desk was started
  { kind la ln ca cn }
  icons nicon icon-size * + { p }
  nicon max-icon >= if exit then
  p icon-size erase  kind p c!
  la p 1+ ln 19 min cmove
  ca p 21 + cn 72 min cmove
  nicon 6 / 90 * 20 + p 96 + w!   nicon 6 mod 88 * 16 + p 98 + w!
  1 +to nicon ;
: icon-at ( i -- a ) icon-size * icons + ;
: icon-label ( i -- a n ) icon-at 1+ z>s ;
: icon-cmd ( i -- a n ) icon-at 21 + z>s ;
: icon-kind ( i -- n ) icon-at c@ ;
: icon-x ( i -- n ) icon-at 96 + uw@ ;
: icon-y ( i -- n ) icon-at 98 + uw@ ;
: icon-xy! ( x y i -- )   \ kept inside the desktop
  { x y i }
  x 0 max dk-w 74 - min  i icon-at 96 + w!
  y 0 max dk-h bar-h - 80 - min  i icon-at 98 + w! ;
: icon@ ( kind la ln ca cn x y -- )   \ one icon of the settings file, with its place
  { kind la ln ca cn x y }
  nicon max-icon >= if exit then
  kind la ln ca cn icon  x y nicon 1- icon-xy! ;
: icon-place ( la ln x y -- )   \ from the settings file: the icon with this label goes there
  { la ln x y }
  nicon 0 ?do i icon-label la ln compare 0= if x y i icon-xy! then loop ;
: icon-hit ( x y -- i )
  { px py }
  -1 { r }
  nicon 0 ?do
    px i icon-x >=  px i icon-x 70 + < and
    py i icon-y >=  py i icon-y 76 + < and and if i to r then
  loop r ;
-1 value icon-sel   0 value icon-time
create kind-colors $e0c050 , $60c0a0 , $d07060 , $a090d0 , $50b0e0 ,   ( by kind )

: draw-icon ( i -- )
  { i }
  i icon-sel = if $4070b0 color i icon-x 4 - i icon-y 4 - 78 80 box then
  kind-colors i icon-kind cells + @ color
  i icon-x i icon-y 64 48 box
  $202030 color i icon-x 2 + i icon-y 2 + 60 44 frame
  $202030 color i icon-x 22 + i icon-y 12 + i icon-label 1 min text
  white color i icon-x i icon-y 52 + i icon-label text ;

( ---- The desktop ---- )
create dk-msg 80 allot   0 value dk-msg-n   0 value dk-msg-t
: dk-say ( a n -- ) 79 min dup to dk-msg-n dk-msg swap cmove  ticks to dk-msg-t ;
: nth-win ( k -- id )   \ the k-th open window in order of creation, -1 when there is none
  -1 { k r }
  max-win 0 ?do
    i w-on @ if k 0= if r 0< if i to r then then  -1 +to k then
  loop r ;

: draw-bar ( -- )
  0 0 { k id }
  $20202c color 0 dk-h bar-h - dk-w bar-h box
  begin k nth-win dup to id 0< 0= while
    id top-win = if $305890 else $404050 then color
    4 k 132 * + dk-h bar-h - 3 + 128 bar-h 6 - box
    white color 10 k 132 * + dk-h bar-h - 6 + id w-title z>s 18 min text
    1 +to k
  repeat
  dk-msg-n if ticks dk-msg-t - 4000 < if
    gray color 4 k 132 * + 8 + dk-h bar-h - 6 + dk-msg dk-msg-n text then then
  $803030 color dk-w 64 - dk-h bar-h - 3 + 60 bar-h 6 - box
  white color dk-w 52 - dk-h bar-h - 6 + s" Quit" text ;

: draw-all ( -- )
  dk-bg color 0 0 dk-w dk-h box
  ui-font font
  nicon 0 ?do i draw-icon loop
  nz 0 ?do zord i + c@ draw-win loop
  draw-bar ;

( ---- Settings, kept in a file of Forth lines that desk loads when it starts ---- )
also posix z" getenv" 1 sysfunc dk-getenv
z" mkdir" 2 sysfunc dk-mkdir previous
create ini-path 300 allot   0 value ip-n   0 value cfg-d1   0 value cfg-d2
: ip+ ( a n -- ) { a n } a ini-path ip-n + n cmove  n +to ip-n  0 ini-path ip-n + c! ;
: ini-name ( -- )   \ $HOME/.config/lugforth/desktop.ini, or desktop.ini here without HOME
  0 to ip-n
  z" HOME" dk-getenv ?dup if
    z>s 200 min ip+  s" /.config" ip+ ip-n to cfg-d1  s" /lugforth" ip+ ip-n to cfg-d2  s" /desktop.ini" ip+
  else 0 to cfg-d1  s" desktop.ini" ip+ then ;
: ini-dirs ( -- )   \ the two folders, made when missing
  cfg-d1 0= if exit then
  0 ini-path cfg-d1 + c!  ini-path 493 dk-mkdir drop  47 ini-path cfg-d1 + c!
  0 ini-path cfg-d2 + c!  ini-path 493 dk-mkdir drop  47 ini-path cfg-d2 + c! ;
create ini-buf 3000 allot   0 value ini-n
create q-str 115 c, 34 c, 32 c,    ( s" and a space )
create q-end 34 c, 32 c,
create nl-str 10 c,
: ib+ ( a n -- ) { a n } a ini-buf ini-n + n cmove  n +to ini-n ;
: ib-num ( n -- ) base @ >r decimal  0 <# #s #> ib+  s"  " ib+  r> base ! ;
: dk-bg! ( n -- ) to dk-bg ;
: dk-size! ( w h -- ) 480 max to dk-h  640 max to dk-w ;
: ini-save ( -- )
  0 to ini-n
  dk-bg ib-num s" dk-bg!" ib+ nl-str 1 ib+
  dk-w ib-num dk-h ib-num s" dk-size!" ib+ nl-str 1 ib+
  nicon 0 ?do
    i icon-kind ib-num
    q-str 3 ib+  i icon-label ib+  q-end 2 ib+
    q-str 3 ib+  i icon-cmd ib+  q-end 2 ib+
    i icon-x ib-num  i icon-y ib-num  s" icon@" ib+ nl-str 1 ib+
  loop
  ini-dirs
  ini-path z>s w/o create-file if drop exit then
  { fid }
  ini-buf ini-n fid write-file drop
  fid close-file drop ;
: ini-load ( -- )
  ini-path z>s r/o open-file if drop exit then
  close-file drop
  ini-path z>s ['] included catch drop ;

( the folder of the project, found from where desk was started: here, one level up, two or three )
create start-dir 300 allot   create try-path 300 allot   0 value tp-n
: tp+ ( a n -- ) { a n } a try-path tp-n + n cmove  n +to tp-n  0 try-path tp-n + c! ;
: root? ( a n -- f )
  0 to tp-n  tp+ s" examples/utils/desktop.fs" tp+
  try-path z>s r/o open-file if drop 0 else close-file drop -1 then ;
: find-home ( -- )
  start-dir 290 fl-getcwd drop
  s" ./" root? if s" ./" else
  s" ../" root? if s" ../" else
  s" ../../" root? if s" ../../" else s" ../../../" then then then
  0 to tp-n tp+  try-path fl-chdir drop
  dk-home 290 fl-getcwd drop
  start-dir fl-chdir drop ;

( ---- The settings window: colors of the background, size of the desktop ---- )
create bg-list $2a5070 , $1e1e2e , $2e5e3e , $5a3a5a , $5c5c5c , $0b3d4d , $703030 , $8a6a2a ,
8 constant nbg
create size-list 640 , 480 , 800 , 600 , 1024 , 768 , 1280 , 800 ,
4 constant nsize
: size-label ( i -- a n )
  dup 0= if drop s" 640x480" exit then
  dup 1 = if drop s" 800x600" exit then
  2 = if s" 1024x768" else s" 1280x800" then ;
: size-w ( i -- n ) 2 * cells size-list + @ ;
: size-h ( i -- n ) 2 * 1+ cells size-list + @ ;
: apply-size ( w h -- )
  { w h }
  dk-nested if s" Close the editor first" dk-say exit then
  w dk-w = h dk-h = and if exit then
  w h dk-size!
  nicon 0 ?do i icon-x i icon-y i icon-xy! loop
  -1 to resize?  ini-save  quit! ;
: set-draw-body ( w h -- )
  { w h }
  $303040 color 0 0 w h box
  white color 10 8 s" Background" text
  nbg 0 ?do
    bg-list i cells + @ color  10 i 38 * +  30 32 32 box
    bg-list i cells + @ dk-bg = if white color 8 i 38 * +  28 36 36 frame then
  loop
  white color 10 78 s" Desktop size" text
  nsize 0 ?do
    i size-w dk-w = i size-h dk-h = and if $305890 else $505060 then color
    10 i 2 mod 155 * +  104 i 2 / 34 * +  150 28 box
    white color 18 i 2 mod 155 * +  107 i 2 / 34 * + i size-label text
  loop
  gray color 10 h 24 - s" Saved for the next time" text ;
' set-draw-body is set-draw
: set-click ( px py -- )
  { px py }
  0 { k }
  px 10 < if exit then
  py 30 >= py 62 < and if
    px 10 - 38 / to k
    k nbg < px 10 - 38 mod 32 < and if bg-list k cells + @ dk-bg! ini-save then exit then
  py 104 >= if
    py 104 - 34 / 2 * px 10 - 155 / + to k
    px 10 - 155 mod 150 < py 104 - 34 mod 28 < and k nsize < and if
      k size-w k size-h apply-size then then ;
: open-settings ( -- )
  max-win 0 ?do i w-on @ i w-kind @ 3 = and if i raise exit then loop
  3 100 80 330 230 s" Settings" win-new drop ;

( ---- Mouse and keys ---- )
-1 value drag-id   0 value drag-dx   0 value drag-dy
-1 value drag-icon   0 value ic-x0   0 value ic-y0   0 value ic-moved   ( an icon being moved )
0 value ctrl-cmd
-1 value want-launch   ( an icon asked for: started from the main loop )
: file-win ( -- id )   -1 { r }  max-win 0 ?do i w-on @ i w-kind @ 1 = and if i to r then loop r ;
: close-win ( id -- )
  dup w-kind @ 2 = if drop repl-active if bye-keys else 17 tq-push then exit then
  dup w-kind @ 1 = if 0 to fl-quit then
  win-close ;
: open-files ( -- )
  file-win dup 0< 0= if raise exit then drop
  1 40 30 dk-w 60 - 560 min  dk-h bar-h - 60 - 440 min  s" Files" win-new dup 0< if drop exit then drop
  fl-init ;
: bar-click ( x -- )
  { px }
  px dk-w 64 - >= if quit! exit then
  px 4 - 132 / nth-win dup 0< 0= if
    px 4 - 132 mod 128 < if raise exit then then drop ;
: on-down ( x y -- )
  { px py }
  -1 { id }
  py dk-h bar-h - >= if px bar-click exit then
  px py win-at to id
  id 0< if
    px py icon-hit dup to icon-sel
    0< if exit then
    icon-sel to drag-icon  px to ic-x0  py to ic-y0  0 to ic-moved
    px icon-sel icon-x - to drag-dx  py icon-sel icon-y - to drag-dy
    icon-time 0<> ticks icon-time - 600 < and if icon-sel to want-launch 0 to icon-time exit then
    ticks to icon-time exit then
  id raise
  py id w-y @ title-h 1+ + < if
    px id w-x @ id w-w @ + 24 - > if id close-win exit then
    id to drag-id  px id w-x @ - to drag-dx  py id w-y @ - to drag-dy exit then
  id w-kind @ 1 = if px id cont-x - py id cont-y - fl-click then
  id w-kind @ 3 = if px id cont-x - py id cont-y - set-click then ;
: on-up ( -- )
  -1 to drag-id
  drag-icon 0< 0= ic-moved and if ini-save then
  -1 to drag-icon ;
: on-move ( x y -- )
  { px py }
  px to mouse-x  py to mouse-y
  drag-icon 0< 0= if
    px ic-x0 - abs 3 > py ic-y0 - abs 3 > or if -1 to ic-moved  0 to icon-time then
    ic-moved if px drag-dx -  py drag-dy -  drag-icon icon-xy! then
    exit then
  drag-id 0< if exit then
  px drag-dx - 0 max dk-w drag-id w-w @ - min drag-id w-x !
  py drag-dy - 0 max dk-h bar-h - title-h - min drag-id w-y ! ;
: on-wheel ( n -- )
  { n }
  top-win dup 0< if drop exit then
  dup w-kind @ 1 = if drop n fl-wheel exit then drop ;

: term-key ( sym md -- )
  { sym md }
  md $c0 and if
    sym [char] a >= sym [char] z <= and if sym 96 - tq-push -1 to ctrl-cmd exit then then
  sym $0d = sym $40000058 = or if 13 tq-push exit then
  sym 8 = sym 9 = or sym 27 = or if sym tq-push exit then
  sym $7f = if 3 tq-tilde exit then
  sym $40000052 = if [char] A tq-csi exit then
  sym $40000051 = if [char] B tq-csi exit then
  sym $4000004f = if [char] C tq-csi exit then
  sym $40000050 = if [char] D tq-csi exit then
  sym $4000004a = if [char] H tq-csi exit then
  sym $4000004d = if [char] F tq-csi exit then
  sym $4000004b = if 5 tq-tilde exit then
  sym $4000004e = if 6 tq-tilde exit then ;
: on-key ( -- )
  ev 20 + sl@ ev 24 + sl@ { sym md }
  top-win { id }
  id 0< if exit then
  id w-kind @ 2 = if sym md term-key exit then
  id w-kind @ 1 = if sym ev 16 + sl@ key-index fl-key then ;
: on-text ( -- )
  top-win { id }
  ctrl-cmd if exit then
  id 0< if exit then
  id w-kind @ 2 = if ev 12 + begin dup c@ dup while tq-push 1+ repeat 2drop then ;

0 value pt
: pump ( -- )
  0 to wheel
  begin ev SDL_PollEvent $ffffffff and while
    -1 to dk-dirty
    ev-type to pt
    pt SDL_TEXTINPUT = if on-text else
    pt SDL_KEYDOWN = if on-key else
    pt SDL_MOUSEBUTTONDOWN = if ev 16 + c@ 1 = if ev 20 + sl@ ev 24 + sl@ on-down then else
    pt SDL_MOUSEBUTTONUP = if on-up else
    pt SDL_MOUSEMOTION = if ev 20 + sl@ ev 24 + sl@ on-move else
    pt SDL_MOUSEWHEEL = if ev 20 + sl@ on-wheel else
    handle-event
    then then then then then then
  repeat
  0 to ctrl-cmd ;

( ---- Launching ---- )
create prog-cmd 300 allot   0 value prog?    ( a program to run once the desktop is closed )
create open-name 300 allot  0 value open-pending

also termios
: set-io ( type key key? form -- )
  to form-hook  ['] key? >body !  ['] key >body !  ['] type >body ! ;
: form-hook@ ( -- xt ) form-hook ;
previous
0 value saved-type   0 value saved-key   0 value saved-key?   0 value saved-form
: t-key? ( -- f ) tq-any? if -1 exit then dk-frame tq-any? ;
0 value q-flip
0 value q-flip
: t-key ( -- c )
  begin tq-any? 0= quit? 0= and while dk-frame repeat
  quit? tq-any? 0= and if
    repl-active if bye-keys else q-flip 1 xor to q-flip  q-flip if 17 else 113 then exit then then   ( the window is closed: Ctrl+Q for an editor, q for a browser )
  tq-pop ;

: run-in-terminal ( a n ta tn -- )
  { na nn ta tn }
  0 { id }
  dk-nested if s" One terminal at a time" dk-say exit then
  2 20 20 dk-w 40 - 660 min  dk-h bar-h - 40 - 480 min  ta tn win-new to id
  id 0< if exit then
  term-font font  s" M" text-size to t-ch to t-cw  ui-font font
  id cont-w t-cw / t-max-c min to t-cols
  id cont-h t-ch / t-max-r min to t-rows
  0 to t-sgr  -1 to t-curvis  0 to t-esc  0 to t-uneed  0 to tq-h  0 to tq-t
  t-clear
  ['] type >body @ to saved-type   ['] key >body @ to saved-key
  ['] key? >body @ to saved-key?   form-hook@ to saved-form
  ['] t-type ['] t-key ['] t-key? ['] t-form set-io
  -1 to dk-nested
  na nn ['] evaluate catch
  saved-type saved-key saved-key? saved-form set-io
  ?dup if ." terminal ended with exception " . cr then
  0 to dk-nested
  id win-close ;

( ---- A Forth terminal: the line editor of the console, with history, arrows and Tab, and evaluate ---- )
256 constant repl-max
create repl-buf repl-max allot
0 value repl-n   0 value repl-done
: crlf ( -- ) 13 emit 10 emit ;
: edit-line ( a n -- n ) [ also internals ] edit-accept [ previous ] ;
: repl-on ( -- ) [ also internals ] -1 le-force !  -1 echo ! [ previous ] ;
: repl-eval ( -- )
  previous
  repl-buf repl-n ['] evaluate catch
  also fm
  ?dup if ." error " . else ."  ok" then crlf ;
: repl ( -- )
  0 to repl-done  -1 to repl-active  repl-on
  s" Forth terminal: bye or the x closes it. Up and Down: history, Tab: completion." type crlf
  begin
    repl-buf repl-max edit-line to repl-n
    repl-buf repl-n s" bye" compare 0= if -1 to repl-done then
    repl-done 0= if repl-n if repl-eval then then
    repl-done
  until
  0 to repl-active ;

create line-buf 400 allot
: ends-fs? ( a n -- f ) dup 3 < if 2drop 0 exit then + 3 - 3 s" .fs" compare 0= ;
: set-program ( a n -- )   \ a program to run when the desktop is closed
  dk-nested if 2drop s" Close the editor first" dk-say exit then
  prog-cmd swap cmove  -1 to prog?  quit! ;
: queue-open ( a n -- )   \ the file manager opened a file: handled from the main loop
  299 min dup { n } open-name swap cmove  0 open-name n + c!  -1 to open-pending ;
: do-open ( -- )
  0 to open-pending
  open-name z>s { na nn }
  cwd z>s { ca cn }
  na nn ends-fs? if
    ca line-buf cn cmove  [char] / line-buf cn + c!  na line-buf cn 1+ + nn cmove
    0 line-buf cn 1+ nn + + c!
    dk-nested if s" Close the editor first" dk-say exit then
    line-buf cn 1+ nn + prog-cmd swap cmove  -1 to prog?  quit! exit then
  s" writhdeck " line-buf swap cmove  na line-buf 10 + nn cmove
  line-buf nn 10 + na nn run-in-terminal ;
: do-launch ( i -- )
  { i }
  i icon-cmd { ca cn }
  dk-home z>s { ha hn }
  cn 0> if ca c@ [char] / = else 0 then { abs? }
  i icon-kind 0 = if open-files exit then
  i icon-kind 3 = if open-settings exit then
  i icon-kind 1 = if ca cn i icon-label run-in-terminal exit then
  dk-nested if s" Close the editor first" dk-say exit then
  i icon-kind 4 = if -1 to console?  quit! exit then
  abs? if
    ca line-buf cn cmove  0 line-buf cn + c!
  else
    ha line-buf hn cmove  [char] / line-buf hn + c!
    ca line-buf hn 1+ + cn cmove  0 line-buf hn 1+ cn + + c!
  then
  line-buf prog-cmd 300 cmove  -1 to prog?  quit! ;
: do-pending ( -- )
  want-launch 0< 0= if want-launch -1 to want-launch do-launch exit then
  open-pending if do-open then ;

0 value last-soft
: phase ( -- n )   \ changes twice a second (the cursor), and when the message of the taskbar expires
  ticks 500 / 1 and  dk-msg-n if ticks dk-msg-t - 4000 < 2 and or then ;
: soft-tick ( -- f )   \ KMSDRM (soft-cursor): the screen is shown again 5 times a second even if nothing changed
  soft-cursor 0= if 0 exit then
  ticks 200 / dup last-soft <> swap to last-soft ;
: frame-body ( -- )
  no-viewport
  pump
  file-win dup 0< 0= if drop fl-quit if file-win win-close 0 to fl-quit -1 to dk-dirty then else drop then
  do-pending
  phase last-phase <> if phase to last-phase -1 to dk-dirty then
  dk-dirty soft-tick or if
    0 to dk-dirty
    draw-all
    flip
  then
  20 delay ;
' frame-body is dk-frame

( ---- Start ---- )
0 value ui-loaded
: fonts ( -- )
  ui-font ?dup if free-font then
  term-font ?dup if free-font then
  ui-font-file 13 load-font to ui-font
  term-font-file 15 load-font to term-font ;
: default-icons ( -- )
  nicon if exit then
  0 s" Files" s" " icon
  1 s" Forth" s" repl" icon
  4 s" Console" s" " icon
  1 s" Writhdeck" s" writhdeck" icon
  1 s" Ed" s" ed" icon
  3 s" Settings" s" " icon
  2 s" Snake" s" examples/games/gfx_snake.fs" icon
  2 s" 2048" s" examples/games/gfx_2048.fs" icon
  2 s" Tetris" s" examples/games/gfx_tetris.fs" icon
  2 s" Minesweeper" s" examples/games/gfx_minesweeper.fs" icon
  2 s" Demo" s" examples/graphics/gfx_demo.fs" icon ;

0 value saved-argc
: argc@ ( -- n ) [ also internals ] 'argc @ [ previous ] ;
: argc! ( n -- ) [ also internals ] 'argc ! [ previous ] ;
: run-program ( -- )   \ the program, with the arguments of a script put aside: it may end with script-bye
  argc@ to saved-argc
  1 argc!
  previous
  prog-cmd z>s ['] included catch ?dup if ." program error " . cr then
  also fm
  saved-argc argc! ;

: desk-once ( -- )
  1 to zoom
  dk-w dk-h screen
  s" Lugforth desktop" title
  fonts
  ui-font font
  SDL_StartTextInput drop
  -1 to want-launch  0 to open-pending  -1 to drag-id  -1 to drag-icon  0 to dk-nested  -1 to dk-dirty
  begin dk-frame quit? until
  close-screen ;

: open-console ( -- )   \ the console window of lugforth --gui, with the prompt of lugforth: `desk` comes back
  [ also internals ] gui-open gui-restore [ previous ] ;
: desk-main ( -- console? )
  find-home  ini-name  0 to nicon
  ini-load  nicon 0= if default-icons ini-load then
  ['] queue-open is fl-open
  begin
    0 to prog?  0 to resize?  0 to console?
    desk-once
    console? if 0 else prog? if run-program -1 else resize? then then
  while repeat
  console? ;

previous also forth definitions previous
also fm ' desk-main ' open-console previous constant console-xt constant desk-xt
: desk ( -- ) also fm desk-xt execute previous if console-xt execute then ;

[defined] desk-no-run [if] [else]
also fm desk-xt execute previous
[if] console-xt execute [else] script-bye [then]
[then]
