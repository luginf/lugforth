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

\ A small desktop for the bare-metal boots (the PC booted from UEFI, the Raspberry Pi): icons and a file manager,
\ drawn with the short sdl2 words (examples/utils/desktop.fs is the one for a window of a desktop, it needs the
\ real libSDL2). Type  include /utils/desktop_bm.fs  then  desk   (a mouse is needed, the keyboard works too).
\   icons          double click (or Enter on the selected one, arrows): the file manager, a game folder, the
\                  editors writhdeck and ed (they take the screen, then the desktop comes back), a program,
\                  the settings
\   windows        the file manager and the settings are windows: drag the title bar, click to raise, x closes; the
\                  taskbar lists the open ones. Esc closes the window on top.
\   file manager   click a name, double click to open it: a folder is entered, a .fs is run, a .z3 .z5 .z8 story
\                  is played by examples/games/zmachine.fs, any other file opens in writhdeck. Wheel and arrows
\                  scroll.
\   settings       mouse speed (- and +, Save keeps it in lugforth.ini, read at boot) and the background color.
\   Quit (or Esc with no window open) gives the Forth prompt, `desk` brings the desktop back.
\ The icons are the table in dk-defaults: kind 0 file manager (starting folder), 1 command run with the text screen
\ (no pause), 4 the same with a "press a key" after it, 2 the Forth prompt, 5 the settings window.

sdl2 sdl2-ttf
also rpi          \ chdir and getcwd (the pc vocabulary is an alias of rpi)

640 constant dk-w   480 constant dk-h
20 constant bar-h
12 constant row-h
48 constant icon-s

( ---- The icons: records of 96 bytes, kind, label, command: counted strings ---- )
12 constant icon-max
create dk-ic icon-max 96 * allot
0 value dk-n
: dk-rec ( i -- a ) 96 * dk-ic + ;
: dk-kind ( i -- n ) dk-rec @ ;
: dk-label ( i -- a n ) dk-rec 8 + count ;
: dk-cmd ( i -- a n ) dk-rec 24 + count ;
: dk-icon ( kind la ln ca cn -- )
  { kind la ln ca cn }  0 { r }
  dk-n icon-max >= if exit then
  dk-n dk-rec to r
  kind r !
  ln 15 min dup r 8 + c!   la r 9 + rot cmove
  cn 71 min dup r 24 + c!  ca r 25 + rot cmove
  1 +to dk-n ;
: dk-defaults ( -- )
  0 to dk-n
  0 s" Files" s" /" dk-icon
  0 s" Games" s" /games" dk-icon
  1 s" Writhdeck" s" writhdeck" dk-icon
  1 s" Editor" s" ed" dk-icon
  4 s" Mouse" s" include /graphics/sdl2_mouse_test.fs" dk-icon
  4 s" Cave" s" include /games/zmachine.fs zplay /games/zmachine/cave.z5" dk-icon
  2 s" Forth" s" " dk-icon
  5 s" Settings" s" " dk-icon ;

: dk-ix ( i -- x ) 4 mod 110 * 40 + ;
: dk-iy ( i -- y ) 4 / 100 * 40 + ;

create dk-bgs $2a5070 , $305830 , $603048 , $404040 , $102030 ,
0 value dk-bg-i
: dk-bg ( -- rgb ) dk-bg-i cells dk-bgs + @ ;
: dk-kind-color ( kind -- rgb )
  dup 0 = if drop $d0a030 exit then
  dup 1 = if drop $40a0d0 exit then
  dup 4 = if drop $50b050 exit then
  dup 2 = if drop $d05050 exit then
  drop $a070d0 ;

( ---- Small helpers: mouse, keys, text ---- )
0 value was-down   0 value last-click   0 value last-hit
0 value dk-quit
0 value dk-sel                            \ the selected icon, or the selected row (file manager)
0 value saved-ms
create dk-prev 256 allot
: dk-tap? ( k -- f )
  255 and { k }  k pressed? 1 and { now }
  k dk-prev + c@ 0=  now and   now k dk-prev + c! ;
: dk-in? ( x y w h -- f )
  { x y w h }
  mouse-x x >=  mouse-x x w + <  and  mouse-y y >=  mouse-y y h + <  and and ;
: dk-say ( x y a n rgb -- )   color text ;
: dk-button ( x y w h a n -- )
  { x y w h a n }
  x y w h dk-in? if $7080a0 else $506080 then color  x y w h box
  white color  a n text-size drop { tw }
  x w tw - 2/ + y 2 + a n text ;

( ---- Running things with the text screen ---- )
: dk-open ( -- )   2 to zoom  dk-w dk-h screen  s" x" 8 load-font font ;
: dk-pause ( -- )   cr ." Press a key to return to the desktop" key drop ;
: dk-after ( pause? ior -- )   ?dup if cr ." Error " . drop dk-pause exit then  if dk-pause then ;
: dk-run ( a n pause? -- )
  >r close-screen   ['] evaluate catch r> swap dk-after   dk-open ;
create dk-path 300 allot
: dk-abs ( a n -- a' n' )   \ the name with the current folder in front: include is relative to the including file
  { a n }  0 { c }
  getcwd dup to c  dk-path swap cmove
  c 1 > if [char] / dk-path c + c!  1 +to c then
  a dk-path c + n cmove   dk-path c n + ;
: dk-runfile ( a n -- )   dk-abs close-screen   ['] included catch 1 swap dk-after   dk-open ;

create dk-buf 200 allot   0 value dk-bn
: dk-put ( a n -- )   dk-buf dk-bn + swap dup >r cmove  r> +to dk-bn ;
: dk-eval ( pause? -- )   dk-buf dk-bn rot dk-run   0 to dk-bn ;

create dk-low 80 allot
: dk-lname ( a n -- a' n' )
  80 min dup >r  0 ?do
    dup i + c@ dup [char] A >= over [char] Z <= and if 32 + then  dk-low i + c!
  loop drop  dk-low r> ;
: dk-ends? ( a n ea en -- f )
  { ea en }  dk-lname  dup en < if 2drop 0 exit then
  + en -  en ea en compare 0= ;

( ---- Windows: 0 the file manager, 1 the settings; records of 5 cells: open, x, y, w, h ---- )
5 cells constant win-size
create wins 2 win-size * allot
: win ( i -- a ) win-size * wins + ;
: wo ( i -- a ) win ;
: wx ( i -- a ) win 8 + ;
: wy ( i -- a ) win 16 + ;
: ww ( i -- a ) win 24 + ;
: wh ( i -- a ) win 32 + ;
14 constant title-h
0 value ztop   -1 value drag-w   0 value drag-dx   0 value drag-dy
: win-init ( -- )
  wins 2 win-size * erase
  150 0 wx !  30 0 wy !  360 0 ww !  300 0 wh !
  200 1 wx !  60 1 wy !  300 1 ww !  150 1 wh !
  0 to ztop  -1 to drag-w ;
: win-open ( i -- )   1 over wo !  to ztop ;
: win-close ( i -- )   0 swap wo ! ;
: win-title ( i -- a n )   0= if s" Files" else s" Settings" then ;
: win-hit? ( i -- f )
  { i }  i wo @ 0= if 0 exit then
  i wx @ i wy @ i ww @ i wh @ dk-in? ;
: win-pick ( -- i|-1 )
  ztop win-hit? if ztop exit then
  1 ztop - win-hit? if 1 ztop - exit then
  -1 ;
: active ( -- i|-1 )   \ the window that gets the keys
  ztop wo @ if ztop exit then
  1 ztop - wo @ if 1 ztop - exit then
  -1 ;

( ---- The file manager: names of the current folder, folders first ---- )
200 constant fm-max
create fm-names fm-max 40 * allot
0 value fm-n   0 value fm-top
: fm-name ( i -- a n ) 40 * fm-names + count ;
: fm-add ( a n -- )
  fm-n fm-max >= if 2drop exit then
  38 min dup fm-n 40 * fm-names + c!  fm-names fm-n 40 * + 1+ swap cmove  1 +to fm-n ;
: fm-pass ( dirs? -- )
  { want }  0 { dh }
  s" ." open-dir if drop exit then
  to dh
  begin dh read-dir dup while
    over c@ [char] . = if 2drop else
      2dup + 1- c@ [char] / =  want = if fm-add else 2drop then
    then
  repeat 2drop
  dh close-dir drop ;
: fm-scan ( -- )
  0 to fm-n  0 to fm-top  0 to dk-sel
  s" ../" fm-add  -1 fm-pass  0 fm-pass ;
: fm-rows ( -- n ) 0 wh @ title-h - 18 - row-h / ;
: fm-clamp ( -- )
  dk-sel 0 max fm-n 1- min to dk-sel
  dk-sel fm-top < if dk-sel to fm-top then
  dk-sel fm-top fm-rows - 1+ >= if dk-sel fm-rows - 1+ to fm-top then
  fm-top 0 max to fm-top ;
: fm-dir? ( a n -- f ) 1- + c@ [char] / = ;
: fm-open ( i -- )
  fm-name { a n }
  a n fm-dir? if a n 1- chdir drop fm-scan exit then
  a n s" .fs" dk-ends? if a n dk-runfile exit then
  a n s" .z3" dk-ends? a n s" .z5" dk-ends? or a n s" .z8" dk-ends? or if
    s" include /games/zmachine.fs zplay " dk-put  a n dk-abs dk-put  -1 dk-eval exit then
  s" writhdeck " dk-put  a n dk-put  0 dk-eval ;
: dk-files ( a n -- )   chdir drop  fm-scan  0 win-open ;

( ---- Settings ---- )
create dk-numbuf 16 allot
: dk-num ( n -- a u )   \ the digits of n, not negative
  dk-numbuf 16 +
  begin 1 - >r 10 /mod swap [char] 0 + r@ c! r> over 0= until nip
  dup dk-numbuf 16 + swap - ;
: set-y1 ( -- y ) 1 wy @ title-h + 24 + ;
: set-y2 ( -- y ) set-y1 28 + ;
: speed! ( n -- )   1 max 1000 min mouse-speed! ;
: set-click ( -- )
  1 wx @ { x }
  x 150 + set-y1 20 14 dk-in? if mouse-speed@ 5 - speed! exit then
  x 176 + set-y1 20 14 dk-in? if mouse-speed@ 5 + speed! exit then
  x 210 + set-y1 70 14 dk-in? if mouse-save ticks to saved-ms exit then
  x 150 + set-y2 60 14 dk-in? if dk-bg-i 1+ 5 mod to dk-bg-i then ;
: set-draw ( -- )
  1 wx @ { x }
  white color  x 12 + set-y1 3 + s" Mouse speed" text
  $ffd060 color  x 112 + set-y1 3 + mouse-speed@ dk-num text
  x 150 + set-y1 20 14 s" -" dk-button
  x 176 + set-y1 20 14 s" +" dk-button
  x 210 + set-y1 70 14 s" Save" dk-button
  white color  x 12 + set-y2 3 + s" Background" text
  x 150 + set-y2 60 14 s" Next" dk-button
  $c0d0e0 color  x 12 + set-y2 30 + s" Move the mouse: the arrow follows" text
  ticks saved-ms - 2000 < saved-ms 0<> and if
    $60e060 color  x 12 + set-y2 44 + s" Saved in lugforth.ini" text then ;

( ---- Actions of the icons ---- )
: dk-go ( i -- )
  dup dk-kind
  dup 0 = if drop dk-cmd dk-files exit then
  dup 1 = if drop dk-cmd 0 dk-run exit then
  dup 4 = if drop dk-cmd -1 dk-run exit then
  dup 2 = if 2drop -1 to dk-quit exit then
  5 = if drop 1 win-open exit then
  drop ;

( ---- Drawing ---- )
: dk-pwd ( -- a n ) getcwd ;
: dk-icon-draw ( i -- )
  { i }  i dk-ix  i dk-iy { x y }
  i dk-sel = if $ffffff color x 3 - y 3 - icon-s 6 + icon-s 6 + box then
  i dk-kind dk-kind-color color  x y icon-s icon-s box
  $000000 color  x 16 + y 16 +  i dk-label drop 1 text
  i dk-label text-size drop { tw }
  white color  x icon-s tw - 2/ +  y icon-s + 4 +  i dk-label text ;
: dk-paint-icons ( -- )   dk-n 0 ?do i dk-icon-draw loop ;
: dk-row-draw ( i -- )
  { i }  0 wx @  0 wy @ title-h + 16 + i fm-top - row-h * + { x y }
  i dk-sel = if $305080 color x y 0 ww @ row-h box then
  i fm-name 2dup fm-dir? if $ffd060 else white then color  x 4 + y 2 + 2swap text ;
: files-draw ( -- )
  $c0d0e0 color  0 wx @ 4 +  0 wy @ title-h + 3 +  dk-pwd text
  fm-n 0 ?do
    i fm-top >= i fm-top fm-rows + < and if i dk-row-draw then
  loop ;
: win-draw ( i -- )
  { i }  i wo @ 0= if exit then
  i wx @ i wy @ { x y }  i ww @ i wh @ { w h }
  $000000 color  x 1 - y 1 - w 2 + h 2 + box
  i ztop = if $3060a0 else $505860 then color  x y w title-h box
  white color  x 4 + y 3 +  i win-title text
  $a04040 color  x w + title-h - y title-h title-h box
  white color  x w + title-h - 4 + y 3 +  s" x" text
  $14202c color  x y title-h + w h title-h - box
  i 0 = if files-draw else set-draw then ;
: dk-bar ( -- )
  $202830 color  0 dk-h bar-h - dk-w bar-h box
  dk-w 64 -  dk-h bar-h - 2 +  60 bar-h 4 -  s" Quit" dk-button
  0 wo @ if 4 dk-h bar-h - 2 +  70 bar-h 4 -  s" Files" dk-button then
  1 wo @ if 78 dk-h bar-h - 2 +  70 bar-h 4 -  s" Settings" dk-button then ;
: dk-paint ( -- )
  dk-bg color cls
  dk-paint-icons
  1 ztop - win-draw  ztop win-draw
  dk-bar ;

( ---- Input ---- )
: dk-double? ( hit -- f )   \ the second click on the same thing within 450 ms
  dup last-hit = ticks last-click - 450 < and  swap to last-hit  ticks to last-click ;
: dk-click-desktop ( -- )
  dk-n 0 ?do
    i dk-ix i dk-iy icon-s dup 16 + dk-in? if
      i to dk-sel  i dk-double? if i dk-go then
      unloop exit
    then
  loop ;
: fm-pick-row ( -- )
  mouse-y 0 wy @ title-h + 16 + -  dup 0< if drop exit then
  row-h /  fm-top +  { i }
  i fm-n < if  i to dk-sel  i 100 + dk-double? if i fm-open then  then ;
: win-click ( i -- )
  { i }  i to ztop
  mouse-y i wy @ - title-h < if
    mouse-x i wx @ i ww @ + title-h - >= if i win-close exit then
    i to drag-w  mouse-x i wx @ - to drag-dx  mouse-y i wy @ - to drag-dy exit
  then
  i 0 = if fm-pick-row else set-click then ;
: bar-click ( -- f )   \ a click on the taskbar: true when it was
  mouse-y dk-h bar-h - < if 0 exit then
  dk-w 64 - dk-h bar-h - 2 + 60 bar-h 4 - dk-in? if -1 to dk-quit exit then
  0 wo @ if 4 dk-h bar-h - 2 + 70 bar-h 4 - dk-in? if 0 to ztop exit then then
  1 wo @ if 78 dk-h bar-h - 2 + 70 bar-h 4 - dk-in? if 1 to ztop exit then then
  -1 ;
: dk-click ( -- )
  bar-click if exit then
  win-pick dup 0< if drop dk-click-desktop else win-click then ;
: dk-drag ( -- )
  mouse-x drag-dx - 0 max dk-w 40 - min  drag-w wx !
  mouse-y drag-dy - 0 max dk-h bar-h - title-h - min  drag-w wy ! ;
: dk-esc ( -- )
  active dup 0< if drop -1 to dk-quit else win-close then ;
: dk-keys-files ( -- )
  key-down dk-tap? if 1 +to dk-sel then
  key-up dk-tap? if -1 +to dk-sel then
  0 win-hit? if wheel ?dup if negate 3 * +to dk-sel then then
  key-enter dk-tap? if dk-sel fm-open then
  fm-clamp ;
: dk-keys-icons ( -- )
  key-right dk-tap? if dk-sel 1+ dk-n 1- min to dk-sel then
  key-left dk-tap? if dk-sel 1- 0 max to dk-sel then
  key-down dk-tap? if dk-sel 4 + dk-n 1- min to dk-sel then
  key-up dk-tap? if dk-sel 4 - 0 max to dk-sel then
  key-enter dk-tap? if dk-sel dk-go then ;
: dk-keys ( -- )
  key-esc dk-tap? if dk-esc then
  active
  dup 0 = if drop dk-keys-files exit then
  dup 1 = if
    drop
    key-right dk-tap? if mouse-speed@ 5 + speed! then
    key-left dk-tap? if mouse-speed@ 5 - speed! then
    exit then
  drop dk-keys-icons ;
: dk-input ( -- )
  events  LEFT-BUTTON pressed? { down }
  dk-keys
  down was-down 0= and if dk-click then
  down 0= if -1 to drag-w then
  down drag-w 0< 0= and if dk-drag then
  down to was-down ;

: dk-step ( -- )   dk-input dk-paint flip 16 delay ;

: desk ( -- )
  dk-defaults  win-init  0 to dk-sel  0 to dk-quit  0 to was-down
  fm-scan
  dk-open
  begin dk-step dk-quit until
  close-screen
  cr ." Type desk to come back to the desktop" cr ;

desk
