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
\ so a game written with the short words (examples/games/gfx_breakout.fs) runs
\ unchanged. Drawing is in C (FB- words in ports/baremetal/screen.h). Drawing,
\ text (a built-in 8x8 font), images (built into the kernel), timing, keys
\ and mouse, beeps and ABC tunes. See ports/rpi/README.md.

vocabulary sdl2   also rpi also sdl2 definitions

( ---- Screen ---- )
1 value zoom   0 value width   0 value height   0 value screen-open?
0 value quitting   0 value gfx-mode   ( -1: the drawing words work under the console )
: screen ( w h -- )   \ a zoom too big for the display is lowered until it fits
  to height to width
  begin  width height zoom fb-open  while  zoom 1 > 0= if -21 throw then  -1 +to zoom  repeat
  -1 to screen-open?   0 to quitting   0 to gfx-mode ;
: close-screen ( -- )   screen-open? if fb-close then  0 to screen-open?  1 to zoom  0 to gfx-mode  1 mouse-cursor ;
: title ( a n -- ) 2drop ;
: fullscreen ( f -- ) drop ;
: cursor ( f -- ) mouse-cursor ;   ( the built-in arrow: 0 for a program that draws its own )

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
\ At the prompt, no SCREEN needed: after `graphic` a drawing word shows at once under the
\ text of the console (a layer of 640x480; the console keeps its own size). `cls` clears
\ it, `page` clears the text.
: gfx-on ( -- ) gfx-open  640 to width  480 to height  -1 to gfx-mode ;
: graphic-size ( w h -- )   ( the drawing area, up to 1920x1440; the console screen is 640x480 times console-zoom )
  gfx-on  2dup gfx-size  to height to width ;
: box ( x y w h -- ) fb-box ;
: gfx-cls ( -- )   ( at the prompt: back to black, whatever the pen )
  pen-r pen-g pen-b pen-a { r g b a }
  0 0 0 255 rgba  0 0 width height fb-box  r g b a rgba ;
: cls ( -- )
  gfx-mode if gfx-cls exit then
  pen-a >r 255 alpha  0 0 width height fb-box  r> alpha ;
: dot ( x y -- ) 1 1 fb-box ;
: plot ( x y -- ) dot ;   \ alias of dot, the usual Forth name
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

( ---- Colors by number: the sixteen of an 8 bit micro ---- )
create ink-table
  $000000 , $ff0000 , $00c000 , $ffff00 , $0000ff , $ff00ff , $00ffff , $ffffff ,
  $808080 , $ff8000 , $ff80c0 , $804000 , $800080 , $80ff80 , $8080ff , $c0c0c0 ,
: ink ( n -- )   ( 0 black 1 red 2 green 3 yellow 4 blue 5 magenta 6 cyan 7 white, 8 to 15 others )
  15 and cells ink-table + @ color ;

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

: typed ( -- c )   ( the next typed character, or 0; no shift or accents here, the serial line and the keyboard give characters already )
  key? if key else 0 then ;
: unpress ( k -- )   255 and cells key-times + -1000000 swap ! ;

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
: setting ( n "name" -- )   ( a setting: `3000 beep-volume` sets it, `to beep-volume` too; name@ reads it )
  create , does> ! ;
0 setting wave   3000 setting beep-volume   0 setting pitch-drop
: wave@ ( -- n ) [ ' wave >body ] literal @ ;
: beep-volume@ ( -- n ) [ ' beep-volume >body ] literal @ ;
: pitch-drop@ ( -- n ) [ ' pitch-drop >body ] literal @ ;
2205 value latency   ( no effect here: the output keeps its own queue )
44100 constant sample-rate
: beep ( freq ms -- ) wave@ beep-volume@ snd-beep ;
\ ADSR envelope of the beeps (and of play): attack, decay in ms, sustain in percent, release in ms
\ (the release is the end of the note, not added to it). Off by default: no envelope.
0 value env-on   0 value env-a   0 value env-d   100 value env-s   0 value env-r
: adsr ( a d s r -- ) 0 max to env-r  0 max 100 min to env-s  0 max to env-d  0 max to env-a  -1 to env-on
  env-a env-d env-s env-r -1 snd-adsr ;
: adsr@ ( -- a d s r ) env-a env-d env-s env-r ;
: adsr-off ( -- ) 0 to env-on  0 0 100 0 0 snd-adsr ;

( ---- Waves by name, and play: notes typed fast ---- )
: square 0 wave ;   : triangle 1 wave ;   : saw 2 wave ;   : noise 3 wave ;   : sine 4 wave ;
\ s" t140 o4 l8 c d e c  e4 g4 r4 >c2" play     queues the notes, like beep. Letters c d e f g a b,
\ # or + sharp, - flat, then a length (4 is a quarter, 8 an eighth, a dot lengthens by half);
\ r rest; o4 octave, < and > one down or up; l8 default length; t120 tempo (quarters a minute);
\ w1 the wave; p a rest too; n49 a note by number (n0 a rest, n49 is the middle C); c4&c8 ties two notes of
\ the same pitch; v8 the volume 0 to 15. Spaces are free. The settings stay from one play to the next.
create semis 262 , 277 , 294 , 311 , 330 , 349 , 370 , 392 , 415 , 440 , 466 , 494 ,   ( octave 4 )
create letters 9 c, 11 c, 0 c, 2 c, 4 c, 5 c, 7 c,                                      ( a b c d e f g )
4 value p-oct   4 value p-len   120 value p-tempo   0 value p-a   0 value p-n
defer p-out   ' beep is p-out
: p-peek ( -- c ) p-n 0> if p-a c@ else 0 then ;
: p-next ( -- ) 1 +to p-a  -1 +to p-n ;
: p-num ( -- n f )   ( the digits at the cursor, and whether there were any )
  0 0 { n any }
  begin p-peek dup [char] 0 >= swap [char] 9 <= and while
    n 10 * p-peek [char] 0 - + to n  -1 to any  p-next
  repeat n any ;
: p-acc ( -- n )   ( # + sharps and - flats after a letter )
  0 begin
    p-peek dup [char] # = over [char] + = or if drop 1+ p-next 0
    else [char] - = if 1- p-next 0 else -1 then then
  until ;
: p-dur ( -- ms )   ( a length and its dots )
  p-num 0= if drop p-len then
  240000 p-tempo / swap /
  begin p-peek [char] . = while 3 2 */ p-next repeat ;
: p-freq ( semitones from c4 -- hz )
  120 + 12 /mod 10 - swap cells semis + @ swap
  dup 0< if negate rshift else lshift then ;
0 value p-pf   0 value p-pm   0 value p-tie   ( the note waiting, and a & before the next one )
: p-flush ( -- ) p-pm if p-pf p-pm p-out 0 to p-pm then ;
: p-emit ( freq ms -- )   ( a note tied with & to one of the same pitch is made longer )
  { f m }
  p-tie p-pm 0<> and  f p-pf = and if m +to p-pm 0 to p-tie exit then
  0 to p-tie  p-flush  m to p-pm  f to p-pf ;
: p-note ( c -- )
  [char] a - letters + c@  p-acc +  p-oct 4 - 12 * +  p-freq  p-dur p-emit ;
: p-rest ( -- ) 0 p-dur p-emit ;
: p-number ( -- )   ( n<k>: the note k semitones above the C of octave 0, n0 is a rest )
  p-num 0= if drop exit then
  dup 0= if drop p-rest exit then
  49 - p-freq p-dur p-emit ;
: p-step ( -- )
  p-peek p-next 32 or
  dup [char] a >= over [char] g <= and if p-note exit then
  dup [char] r = over [char] p = or if drop p-rest exit then
  dup [char] n = if drop p-number exit then
  dup [char] & = if drop -1 to p-tie exit then
  dup [char] v = if drop p-flush p-num if 0 max 15 min 400 * beep-volume else drop then exit then
  dup [char] o = if drop p-num if 0 max 8 min to p-oct else drop then exit then
  dup [char] l = if drop p-num if 1 max to p-len else drop then exit then
  dup [char] t = if drop p-num if 20 max to p-tempo else drop then exit then
  dup [char] w = if drop p-flush p-num if wave else drop then exit then
  dup [char] < = if drop p-oct 0> if -1 +to p-oct then exit then
  dup [char] > = if drop p-oct 8 < if 1 +to p-oct then exit then
  drop ;
( ---- Several voices: s" o4 c d e, o3 c2 g2" play ---- )
\ The voices are separated by commas (4 at most, 512 notes each) and start together. Each one
\ begins with the octave, length, tempo, wave and volume the play started with.
4 constant p-voices   512 constant p-max
create p-evs p-voices p-max * 4 * cells allot      ( by note: frequency, ms, wave, volume )
create p-cnt p-voices cells allot                  ( notes of each voice )
0 value p-v   0 value p-nv   0 value p-k
: p-slot ( v k -- a ) swap p-max * + 4 * cells p-evs + ;
: p-rec ( freq ms -- )
  p-v dup cells p-cnt + @ { f m v k }
  k p-max < if
    f  m  wave@  beep-volume@  v k p-slot
    >r r@ 3 cells + ! r@ 2 cells + ! r@ cell+ ! r> !
    1 v cells p-cnt + +!
  then ;
: p-comma ( a n -- i )   ( the index of the first comma, or n )
  { a n } n 0 ?do a i + c@ [char] , = if i unloop exit then loop n ;
: p-run ( a n -- ) to p-n to p-a  begin p-n 0> while p-step repeat p-flush ;
: p-parse ( a n -- )   ( fills p-evs and p-cnt, one voice for each part between commas )
  p-oct p-len p-tempo wave@ beep-volume@ { a n o l t w vol }
  p-cnt p-voices cells erase   0 to p-v
  ['] p-rec is p-out
  begin n 0> p-v p-voices < and while
    o to p-oct  l to p-len  t to p-tempo  w wave  vol beep-volume
    a n p-comma to p-k
    a p-k p-run
    p-k 1+ n min to p-k   p-k +to a   p-k negate +to n
    1 +to p-v
  repeat
  p-v to p-nv   ['] beep is p-out ;
: p-event ( e v -- ) { e v } e @ e cell+ @ e 2 cells + @ e 3 cells + @ v snd-vbeep ;
: p-mix ( -- )   ( queues each voice on its own channel, started together )
  snd-sync
  p-nv 0 ?do
    i cells p-cnt + @ 0 ?do j i p-slot j p-event loop
  loop
  snd-sync ;
: play ( a n -- )
  2dup p-comma over = if p-run exit then
  p-parse p-mix ;
: drum-hit ( freq ms -- ) wave@ beep-volume@ pitch-drop@ snd-hit ;
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
: sdl2 ( -- )   \ once: the search order is short, and every example says sdl2 sdl2-ttf...
  context @ [ ' sdl2 >body ] literal = if exit then   also sdl2 ;
: graphic ( -- )   \ the drawing words at the prompt: 0 0 100 100 line
  sdl2  s" gfx-on" evaluate ;
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
  tune-ms  wave@ 1 = wave@ 0 = or if 1 else wave@ then  tune-level  snd-tune-run ;
: tune-play ( -- ) 0 tune-send ;
: tune-loop ( -- ) -1 tune-send ;
: abc-play ( a n -- ) abc tune-play ;
: abc-loop ( a n -- ) abc tune-loop ;
previous previous
~ evaluate
  previous current ! ;
previous
