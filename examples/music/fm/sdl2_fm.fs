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

\ A two operator FM synthesizer with SDL2, MIDI in and a keyboard on screen.
\ The same file runs on Linux (SDL2, ALSA MIDI) and in the browser (canvas, WebAudio,
\ Web MIDI): see sdl2_fm.html.
\
\ One modulator oscillator bends the phase of one carrier. Nine sliders shape the
\ sound (drag them, or use the mouse wheel), six buttons load a sound.
\   RATIO    modulator frequency / carrier frequency (whole numbers sound like notes,
\            others like bells)             INDEX    how much the modulator bends
\   ATTACK DECAY SUSTAIN RELEASE            envelope of the loudness
\   MDECAY   how fast the modulation fades  MSUS     modulation kept while held
\   VOLUME
\ Play with the mouse on the keyboard of the screen, with the computer keyboard, or with
\ a MIDI keyboard (pitch bend, CC 1 = index, CC 7 = volume, channel 10 ignored).
\ The computer keys are read by their place, so it is the same on AZERTY and QWERTY: the
\ lower letter row (Z X C V B N M , . / on a QWERTY) is a C major scale with the black
\ keys on the row above (S D G H J L ;), and Q W E R T Y U I O P with the digits 2 3 5 6
\ 7 9 0 is the octave above.
\   up / down      octave          left / right    previous / next sound
\   space          all notes off   esc             quit
\ On Linux the MIDI keyboard must be readable by ALSA (your user in the audio group): the
\ window says NONE when there is no input. In a browser the sound starts after the first
\ click or key press, and the browser asks before it opens MIDI.

\ On Linux the synthesizer is included here; the web page has already loaded it.
[undefined] sdl2-fm [if] include engine_posix.fs [then]

sdl2
sdl2-fm

320 constant field-w   212 constant field-h
52 constant sl-x   170 constant sl-w   28 constant sl-y   12 constant sl-pitch
7 constant kb-x   140 constant kb-y   18 constant wk-w   54 constant wk-h
11 constant bk-w   33 constant bk-h   29 constant kb-n

( ---- Text: a 3 by 5 font for the characters 32 to 95 ---- )
create font
  $0000 , $0000 , $0000 , $5f7d , $0000 , $52a5 , $0000 , $0000 ,
  $0000 , $0000 , $0000 , $05d0 , $0000 , $01c0 , $0002 , $12a4 ,
  $7b6f , $2c97 , $62a7 , $628e , $5bc9 , $798e , $39ef , $7292 ,
  $7bef , $7bce , $0410 , $0000 , $1511 , $0000 , $4454 , $6282 ,
  $0000 , $2bed , $6bae , $3923 , $6b6e , $79a7 , $79a4 , $396b ,
  $5bed , $7497 , $126a , $5bad , $4927 , $5fed , $6b6d , $2b6a ,
  $6ba4 , $2b73 , $6bad , $388e , $7492 , $5b6f , $5b6a , $5bfd ,
  $5aad , $5a92 , $72a7 , $0000 , $0000 , $0000 , $0000 , $0000 ,

: glyph ( ch x y -- )
  { ch x y }
  ch 96 > if ch 32 - to ch then
  ch 32 -  dup 0< over 63 > or if drop 0 then
  cells font + @ { bits }
  5 0 do
    3 0 do
      bits  14 j 3 * i + -  rshift 1 and if x i + y j + dot then
    loop
  loop ;
: text ( a n x y -- )
  { a u x y }
  u 0 ?do a i + c@ x i 4 * + y glyph loop ;
: num ( n x y -- ) rot <# #s #> 2swap text ;
: fixed ( n digits x y -- )   ( n with a decimal point, 1 or 2 digits after it )
  { n d x y }
  d 1 = if n <# # 46 hold #s #> else n <# # # 46 hold #s #> then
  x y text ;

( ---- The patch: nine values, six sounds ---- )
9 constant #params
create params #params cells allot
create pmin  25 , 0 , 0 , 10 , 0 , 10 , 10 , 0 , 0 ,
create pmax  1600 , 200 , 500 , 3000 , 100 , 3000 , 3000 , 100 , 100 ,
create pstep 25 , 1 , 1 , 10 , 1 , 10 , 10 , 1 , 1 ,
: pv ( i -- a ) cells params + ;
: pmin@ ( i -- n ) cells pmin + @ ;
: pmax@ ( i -- n ) cells pmax + @ ;
: pstep@ ( i -- n ) cells pstep + @ ;
: pname ( i -- a n )
  dup 0 = if drop s" RATIO" exit then
  dup 1 = if drop s" INDEX" exit then
  dup 2 = if drop s" ATTACK" exit then
  dup 3 = if drop s" DECAY" exit then
  dup 4 = if drop s" SUSTAIN" exit then
  dup 5 = if drop s" RELEASE" exit then
  dup 6 = if drop s" MDECAY" exit then
  dup 7 = if drop s" MSUS" exit then
  drop s" VOLUME" ;
: apply-params ( -- )
  0 pv @ to fm-ratio     1 pv @ to fm-index    2 pv @ to fm-attack
  3 pv @ to fm-decay     4 pv @ to fm-sustain  5 pv @ to fm-release
  6 pv @ to fm-mdecay    7 pv @ to fm-msus     8 pv @ to fm-volume ;

6 constant #presets
create presets
\ ratio index attack decay sustain release mdecay msus volume
  100 , 25 , 3 , 900 , 15 , 300 , 250 , 10 , 70 ,
  350 , 60 , 2 , 2500 , 0 , 1500 , 1800 , 5 , 70 ,
  100 , 35 , 2 , 250 , 40 , 120 , 120 , 15 , 80 ,
  100 , 50 , 60 , 300 , 70 , 150 , 200 , 60 , 60 ,
  200 , 15 , 5 , 10 , 100 , 60 , 10 , 100 , 60 ,
  200 , 45 , 1 , 500 , 0 , 200 , 100 , 0 , 75 ,
: preset-name ( i -- a n )
  dup 0 = if drop s" E.PIANO" exit then
  dup 1 = if drop s" BELL" exit then
  dup 2 = if drop s" BASS" exit then
  dup 3 = if drop s" BRASS" exit then
  dup 4 = if drop s" ORGAN" exit then
  drop s" PLUCK" ;
0 value cur-preset
: load-preset ( i -- )
  dup to cur-preset  #params * cells presets +  params #params cells move
  apply-params ;
: bump-param { p n }
  p pv @  n p pstep@ * +  p pmax@ min  p pmin@ max  p pv !  apply-params ;
: set-param { p x }
  x sl-x - 0 max sl-w min  p pmax@ p pmin@ - *  sl-w /  { off }
  off p pstep@ / p pstep@ *  p pmin@ +  p pv !  apply-params ;

( ---- Notes: what sounds, so that the keys light up ---- )
create lit 128 allot   lit 128 erase
0 value base-note   0 value last-note
: lit! ( f note -- ) dup 0 128 within if lit + c! else 2drop then ;
: play-on ( note vel -- )
  over 0 128 within 0= if 2drop exit then
  1 2 pick lit!   over to last-note   fm-on ;
: play-off ( note -- )
  dup 0 128 within 0= if drop exit then
  0 over lit!   fm-off ;
: ui-midi { st a b }   ( what a MIDI keyboard plays lights the keys too )
  st $f0 and { cmd }
  cmd $90 = b 0<> and if 1 a lit!   a to last-note then
  cmd $80 =  cmd $90 = b 0= and  or if 0 a lit! then
  cmd $b0 = a 123 = and if lit 128 erase then
  st a b fm-midi
  cmd $b0 = a 1 = and if fm-index 1 pv ! then
  cmd $b0 = a 7 = and if fm-volume 8 pv ! then ;

( ---- The keyboard of the screen ---- )
: white? ( semitone -- f )
  12 mod dup 1 = over 3 = or over 6 = or over 8 = or swap 10 = or 0= ;
create wcount kb-n 1+ cells allot
: init-keys ( -- )
  0 wcount !
  kb-n 0 ?do  wcount i cells + @  i white? if 1+ then  wcount i 1+ cells + !  loop ;
: wkey-x ( semitone -- x ) cells wcount + @ wk-w * kb-x + ;
: bkey-x ( semitone -- x ) 1- cells wcount + @ wk-w * kb-x + wk-w + bk-w 2/ - ;
: note-at ( x y -- semitone or -1 )
  { mx my }
  my kb-y <  my kb-y wk-h + >= or if -1 exit then
  my kb-y bk-h + < if
    kb-n 0 ?do
      i white? 0= if
        mx i bkey-x -  dup 0 >= swap bk-w < and if i unloop exit then
      then
    loop
  then
  mx kb-x - 0< if -1 exit then
  mx kb-x - wk-w /  { w }
  kb-n 0 ?do  i white? if  i wkey-x kb-x - wk-w / w = if i unloop exit then  then  loop
  -1 ;

$1c2038 constant c-panel   $2a3050 constant c-track   $4a90e0 constant c-fill
$c0c8e0 constant c-text    $ffb040 constant c-lit     $f08020 constant c-blit
: lit? ( semitone -- f ) base-note + dup 0 128 within if lit + c@ 0<> else drop 0 then ;

: c-label { s }   ( C and its octave number, on the C keys )
  $707a90 color
  67 s wkey-x 5 +  kb-y wk-h + 9 -  glyph
  s base-note + 12 / 1-  0 max  s wkey-x 9 +  kb-y wk-h + 9 -  num ;
: draw-white ( semitone -- )
  dup lit? if c-lit else $e8e8f0 then color
  dup wkey-x 1 +  kb-y  wk-w 2 -  wk-h  box
  dup 12 mod 0= if c-label else drop then ;
: draw-black ( semitone -- )
  dup lit? if c-blit else $202028 then color
  bkey-x kb-y bk-w bk-h box ;
: draw-keys ( -- )
  $0c1020 color  kb-x 2 - kb-y 2 -  wk-w 17 * 4 +  wk-h 4 +  box
  kb-n 0 ?do  i white? if i draw-white then  loop
  kb-n 0 ?do  i white? 0= if i draw-black then  loop ;

( ---- Sliders, sounds, information ---- )
: draw-slider { p }
  p sl-pitch * sl-y + { y }
  c-text color   p pname 6 y text
  c-track color  sl-x y 1 +  sl-w 5  box
  p pv @ p pmin@ - sl-w * p pmax@ p pmin@ - /  { w }
  c-fill color   sl-x y 1 +  w 5  box
  white color    sl-x w + 1 -  y  3 7  box
  c-text color
  p 0 = if p pv @ 2 sl-x sl-w + 8 + y fixed exit then
  p 1 = if p pv @ 1 sl-x sl-w + 8 + y fixed exit then
  p pv @ sl-x sl-w + 8 + y num ;
: draw-sliders ( -- ) #params 0 ?do i draw-slider loop ;
: draw-preset { i }
  i cur-preset = if c-fill else c-track then color
  i 51 * 8 +  4  48 13  box
  c-text color
  i preset-name { a u }
  a u  i 51 * 8 + 48 u 4 * - 2/ +  8  text ;
: draw-presets ( -- ) #presets 0 ?do i draw-preset loop ;
: draw-info ( -- )
  c-text color
  s" MIDI" 262 28 text   midi-in? if s" IN" else s" NONE" then 262 36 text
  s" OCTAVE" 262 52 text   base-note 12 / 1- 262 60 num
  s" VOICES" 262 76 text   fm-active 262 84 num
  s" NOTE" 262 100 text   last-note 262 108 num ;
: draw-help ( -- )
  $707a90 color
  s" Z-/ Q-P: PLAY  UP DOWN: OCTAVE  LEFT RIGHT: SOUND  SPACE: OFF  ESC: QUIT"
  7 field-h 13 - text
  s" MIDI: PITCH BEND, CC 1 INDEX, CC 7 VOLUME. YOU CAN CLICK THE KEYS TOO"
  7 field-h 7 - text ;
: draw-all ( -- )
  $14182c color cls
  draw-presets draw-sliders draw-info draw-help draw-keys flip ;

( ---- Input ---- )
create kprev 256 allot   kprev 256 erase
: edge? ( k -- f ) dup pressed? swap kprev + c@ 0= and ;
: remember-key ( k -- ) dup pressed? 1 and swap kprev + c! ;
: remember-keys ( -- )
  key-left remember-key key-right remember-key key-up remember-key
  key-down remember-key key-space remember-key ;

17 constant lower-keys   34 constant #pk
\ SDL scancodes of Z S X D C V G B H N J M , L . ; /  then Q 2 W 3 E R 5 T 6 Y 7 U I 9 O 0 P
create pk-scan
  29 , 22 , 27 , 7 , 6 , 25 , 10 , 5 , 11 , 17 , 13 , 16 , 54 , 15 , 55 , 51 , 56 ,
  20 , 31 , 26 , 32 , 8 , 21 , 34 , 23 , 35 , 28 , 36 , 24 , 12 , 38 , 18 , 39 , 19 ,
create pk-held #pk allot   pk-held #pk erase
create pk-note #pk cells allot
: pk-semi ( k -- semitone ) dup lower-keys < if exit then 5 - ;
: piano-key { k }
  k cells pk-scan + @ scan? { down }
  k pk-held + c@ { was }
  down 0<> 1 and was = if exit then
  down 0<> 1 and  k pk-held + c!
  down if
    base-note k pk-semi +  dup k cells pk-note + !  96 play-on
  else
    k cells pk-note + @ play-off
  then ;
: handle-piano ( -- ) #pk 0 ?do i piano-key loop ;
: handle-keys ( -- )
  key-esc pressed? if quit! then
  key-up edge? if base-note 12 + 84 min to base-note then
  key-down edge? if base-note 12 - 24 max to base-note then
  key-right edge? if cur-preset 1+ #presets mod load-preset then
  key-left edge? if cur-preset 1- #presets + #presets mod load-preset then
  key-space edge? if fm-all-off lit 128 erase then
  handle-piano
  remember-keys ;

-1 value drag-row   -1 value mouse-note   0 value piano-drag   0 value was-btn
: slider-at ( -- row or -1 )
  mouse-y sl-y - dup 0< if drop -1 exit then
  sl-pitch / dup #params >= if drop -1 exit then
  mouse-x 46 <  mouse-x sl-x sl-w + 8 + > or if drop -1 then ;
: preset-at ( -- i or -1 )
  mouse-y 4 <  mouse-y 17 >= or if -1 exit then
  mouse-x 8 - dup 0< if drop -1 exit then
  51 /mod swap 48 >= if drop -1 exit then
  dup #presets >= if drop -1 then ;
: mouse-piano { down fresh }
  fresh if mouse-x mouse-y note-at 0< 0= to piano-drag then
  down 0= if 0 to piano-drag then
  piano-drag if
    mouse-x mouse-y note-at dup 0< 0= if base-note + then
  else -1 then
  { target }
  target mouse-note = if exit then
  mouse-note 0< 0= if mouse-note play-off then
  target to mouse-note
  target 0< 0= if target  40 mouse-y kb-y - 87 * wk-h / +  1 max 127 min  play-on then ;
: handle-mouse ( -- )
  LEFT-BUTTON pressed? { down }
  down was-btn 0= and { fresh }
  fresh if
    slider-at to drag-row
    preset-at dup 0< 0= if load-preset else drop then
  then
  down 0= if -1 to drag-row then
  drag-row 0< 0= if drag-row mouse-x set-param then
  wheel 0<> if slider-at dup 0< 0= if wheel 3 * bump-param else drop then then
  down fresh mouse-piano
  down 1 and to was-btn ;

0 value frames
: frame-step ( -- )
  events  midi-poll  handle-keys handle-mouse  draw-all
  1 +to frames   frames 120 mod 0= if midi-connect-all drop then
  dt 8 < if 8 dt - delay then ;
: run ( -- ) begin frame-step quit? until fm-all-off close-screen ;
: setup ( -- )
  init-keys   48 to base-note   0 load-preset
  ['] ui-midi to midi-hook
  midi-open drop   midi-connect-all drop
  1500 to latency ;

." keys Z-/ and Q-P: notes   up/down: octave   left/right: sound   space: all off   esc: quit" cr
3 to zoom
field-w field-h screen
s" FM synth" title
setup
run
script-bye
