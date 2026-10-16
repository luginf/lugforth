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

( The sdl2 short words on bare metal: the HDMI framebuffer, keys from the UART )
\ Same names and stack effects as ports/posix/sdl2.fs and ports/web/sdl2.fs,
\ so a game written with the short words (examples/sdl2_breakout.fs) runs
\ unchanged. Drawing is in C (FB- words in ports/rpi/screen.h). Drawing,
\ text (a built-in 8x8 font), images (built into the kernel), timing, keys
\ and mouse, beeps and ABC tunes. See ports/rpi/README.md.

vocabulary sdl2   also rpi also sdl2 definitions

( ---- Screen ---- )
1 value zoom   0 value width   0 value height   0 value screen-open?
0 value quitting
: screen ( w h -- )
  2dup to height to width  zoom fb-open if -21 throw then
  -1 to screen-open?   0 to quitting ;
: close-screen ( -- )   screen-open? if fb-close then  0 to screen-open? ;
: title ( a n -- ) 2drop ;
: fullscreen ( f -- ) drop ;
: cursor ( f -- ) drop ;

( ---- Colors ---- )
0 value pen-r   0 value pen-g   0 value pen-b   255 value pen-a
: rgba ( r g b a -- )
  255 and to pen-a  255 and to pen-b  255 and to pen-g  255 and to pen-r
  pen-r 16 lshift pen-g 8 lshift or pen-b or  pen-a fb-color ;
: rgb ( r g b -- ) 255 rgba ;
: color ( $rrggbb -- )
  dup 16 rshift swap dup 8 rshift swap rgb ;
: alpha ( a -- ) >r pen-r pen-g pen-b r> rgba ;
: pen ( -- n ) pen-r pen-g 8 lshift or pen-b 16 lshift or pen-a 24 lshift or ;

$000000 constant black    $ffffff constant white
$ff0000 constant red      $00c000 constant green
$0000ff constant blue     $ffff00 constant yellow
$00ffff constant cyan     $ff00ff constant magenta
$ff8000 constant orange   $800080 constant purple
$ff80c0 constant pink     $804000 constant brown
$808080 constant gray

( ---- Drawing ---- )
: box ( x y w h -- ) fb-box ;
: cls ( -- ) pen-a >r 255 alpha  0 0 width height fb-box  r> alpha ;
: dot ( x y -- ) 1 1 fb-box ;
: hline ( x y w -- ) 1 fb-box ;
: line ( x1 y1 x2 y2 -- ) fb-line ;
: frame ( x y w h -- )
  { x y w h }
  w 0> h 0> and if
    x y w 1 box   x y h + 1- w 1 box
    x y 1 h box   x w + 1- y 1 h box
  then ;
: disc ( cx cy r -- )
  dup { cx cy r x }
  r 1+ 0 do
    begin x x * i i * + r r * > while x 1- to x repeat
    cx x - cy i + x 2* 1+ 1 box
    i if cx x - cy i - x 2* 1+ 1 box then
  loop ;
: circle ( cx cy r -- )
  dup { cx cy r x }
  r 1+ 0 do
    begin x x * i i * + r r * > while x 1- to x repeat
    cx x + cy i + dot  cx x - cy i + dot  cx x + cy i - dot  cx x - cy i - dot
    cx i + cy x + dot  cx i - cy x + dot  cx i + cy x - dot  cx i - cy x - dot
  loop ;
: pixel@ ( x y -- $rrggbb ) fb-pixel ;

( ---- Timing ---- )
: ticks ( -- ms ) ms-ticks ;
: delay ( ms -- ) ms ;
0 value dt   0 value last-flip
: flip ( -- ) fb-flip  ticks dup last-flip - to dt  to last-flip ;

( ---- Keys and mouse: a real keyboard, and the serial line ---- )
\ A keyboard and mouse (virtio-input on QEMU virt, USB on the Pi: input.h)
\ give real key down and up and the pointer. The serial line works too, but
\ a terminal sends a byte when a key goes down, and again for each
\ auto-repeat, never when it goes up. So a key typed there counts as held
\ for key-hold ms after its last byte. Arrows come as ESC [ A..D. Raise key-hold (or shorten the
\ terminal's auto-repeat delay, xset r rate 200 40) if a held key stutters.
255 constant LEFT-BUTTON   254 constant MIDDLE-BUTTON   253 constant RIGHT-BUTTON
208 constant key-left    207 constant key-right
210 constant key-up      209 constant key-down
32 constant key-space    13 constant key-enter    27 constant key-esc
9 constant key-tab       8 constant key-backspace

120 value key-hold
0 value last-key
create key-times 256 cells allot
: forget-keys   256 0 do -1000000 key-times i cells + ! loop ;
forget-keys
: key-hit ( k -- ) 255 and dup to last-key  ticks swap cells key-times + ! ;
: key-within ( ms -- k|-1 )   ( a byte, or -1 if none comes in time )
  ticks + { end }
  begin uart-key dup 0< 0= if exit then drop ticks end - 0< 0= until -1 ;
: arrow ( c -- )
  dup [char] A = if drop key-up key-hit exit then
  dup [char] B = if drop key-down key-hit exit then
  dup [char] C = if drop key-right key-hit exit then
  dup [char] D = if drop key-left key-hit exit then
  drop ;
: escape ( -- c|-1 )   ( after an ESC: an arrow, or the Esc key and what follows )
  10 key-within dup [char] [ = if drop 10 key-within arrow -1 exit then
  key-esc key-hit ;
: one-key ( c -- )
  dup 27 = if
    drop escape dup 0< if drop exit then
    dup 27 = if drop key-esc key-hit exit then
  then
  dup 10 = if drop key-enter key-hit exit then
  dup 127 = if drop key-backspace key-hit exit then
  dup [char] A >= over [char] Z <= and if 32 + then
  key-hit ;
: events ( -- )
  input-events   input-last-key ?dup if to last-key then
  begin uart-key dup 0< 0= while one-key repeat drop ;
: pressed? ( k -- f )   ( held on a keyboard, or recently on the serial line )
  dup [char] A >= over [char] Z <= and if 32 + then 255 and
  dup input-down? if drop -1 exit then
  cells key-times + @ ticks swap - key-hold < ;
: scan? ( k -- f ) drop 0 ;
: quit? ( -- f ) quitting ;
: quit! ( -- ) -1 to quitting ;
: mouse-x ( -- n ) input-mouse-x ;
: mouse-y ( -- n ) input-mouse-y ;
: wheel ( -- n ) input-wheel ;

( ---- Text: one built-in 8x8 font, scaled ---- )
\ As sdl2-ttf on Linux, but no TrueType: load-font ignores the file name and
\ gives the 8x8 font scaled to about pt pixels (8 or less: 1, 16: 2, 24: 3).
\ Without load-font, text uses scale 1. UTF-8, Latin-1 accents included.
1 value current-font
: load-font ( a n pt -- font ) nip nip 4 + 8 / 1 max ;
: font ( font -- ) to current-font ;
: free-font ( font -- ) drop ;
: text ( x y a n -- ) current-font fb-text ;
: text-size ( a n -- w h ) text-length 8 * current-font *  8 current-font * ;

( ---- Images: PNG files built into the kernel ---- )
\ No PNG decoder here: load-image finds the image by its name, directories
\ ignored, among the ASSETS of ports/rpi/Makefile (converted at build time).
\ Throws -38 (non-existent file) for any other name.
: load-image ( a n -- img ) image-load dup 0= if -38 throw then ;
: free-image ( img -- ) image-free ;
: image-size ( img -- w h ) dup image-w swap image-h ;
: draw-part ( img sx sy sw sh x y w h -- ) image-draw ;
: draw-size ( img x y w h -- )
  { img x y w h }   img 0 0 img image-size x y w h image-draw ;
: draw ( img x y -- ) { img x y }   img x y img image-size draw-size ;
: image-alpha ( img a -- ) image-alpha! ;
: image-tint ( img $rrggbb -- ) image-tint! ;

( ---- Sound: beeps and hits, as on Linux ---- )
\ Mixed in C (ports/rpi/sound.h) and played by virtio-sound on QEMU virt, the
\ headphone jack on a Pi 3. Silent without one (audio-open? is false).
0 value wave   3000 value beep-volume   0 value pitch-drop
2205 value latency   ( no effect here: the output keeps its own queue )
44100 constant sample-rate
: beep ( freq ms -- ) wave beep-volume snd-beep ;
: drum-hit ( freq ms -- ) wave beep-volume pitch-drop snd-hit ;
: silence ( -- ) snd-silence ;
: beeping? ( -- f ) snd-busy ;
: audio-open ( -- ) ;
: audio-open? ( -- f ) snd-ok ;

( ---- Music: no files and no MIDI here; tunes in ABC are, see sdl2-abc ---- )
2500 value tune-level   ( amplitude of the notes of a tune, 0 to 32767 )
: music-volume ( 0..128 -- ) 2500 * 128 / to tune-level ;
: soundfont? ( a n -- f ) 2drop 0 ;
: soundfont ( a n -- ) 2drop ;
: tune-stop ( -- ) snd-tune-stop ;
: tune-playing? ( -- f ) snd-tune-playing ;

previous previous forth definitions
: sdl2 ( -- ) also sdl2 ;
: sdl2-ttf ( -- ) sdl2 ;   ( the text words are part of sdl2 here )
: sdl2-image ( -- ) sdl2 ;   ( the image words too )
: sdl2-mixer ( -- ) sdl2 ;   ( music volume and soundfont words: no files here )

\ sdl2-abc: tunes in ABC notation (common/abc.fs reads them), played by the
\ mixer's oscillators as on the web: the triangle wave, or the one in wave.
also internals
variable abc-loaded
: sdl2-abc ( -- )
  sdl2  abc-loaded @ if exit then  -1 abc-loaded !
  current @  also sdl2 definitions
  abc-parser evaluate
  r~
also abc-int also rpi
: ticks>ms ( ticks -- ms ) 125 * tune-bpm / ;
: tune-send ( loop -- )
  snd-tune-clear
  n-notes 0 ?do  i nk@  i nt@ ticks>ms  i nd@ ticks>ms  snd-tune-add  loop
  tune-ms  wave 1 = wave 0 = or if 1 else wave then  tune-level  snd-tune-run ;
: tune-play ( -- ) 0 tune-send ;
: tune-loop ( -- ) -1 tune-send ;
: abc-play ( a n -- ) abc tune-play ;
: abc-loop ( a n -- ) abc tune-loop ;
previous previous
~ evaluate
  previous current ! ;
previous
