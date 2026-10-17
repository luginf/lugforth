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

( Run a Uxn ROM on the bare-metal board of ports/rpi, like varvara.fs on Linux )
\
\   include varvara/varvara_rpi.fs
\   s" varvara/boot.rom" varvara
\   s" notes.txt" s" w.rom" varvara-open       ( a file given to the ROM )
\
\ Devices: System, Console (to the console and the UART; the UART's keys are its
\ input; varvara-arg gives it arguments), Screen, Controller and Mouse (the
\ board's keyboard and pointer), Audio (the board's PCM queue: HDMI-less jack
\ on a Pi 3, virtio-sound on QEMU virt), Datetime and the two File devices (the
\ FAT of the SD card, its root is the working directory).
\ Keys: Ctrl A, Alt B, Shift Select, Home Start, the arrows, the text typed;
\ F1 zoom, F2 stacks (on the UART), F3 screenshot (varvara.bmp), F4 loads the
\ ROM again, F12 quits.
\ The screen is 512x320 at zoom 2 (1024x640): `1 to varvara-zoom` for 512x320.
\ The clock starts from the day of the build (or the RTC of QEMU virt): set it
\ with `unix-time!` (rpi vocabulary), and `60 tz!` for UTC+1.

needs uxn.fs
needs screen.fs
needs audio.fs
needs file.fs
sdl2 also rpi also uxn definitions

2 value varvara-zoom
512 value init-w   ( screen size at the start of a ROM )
320 value init-h
0 value quit-gui
0 value reboot
create rom-path 512 allot
0 value rom-len
: rom! ( a n -- ) 511 min dup to rom-len rom-path swap cmove ;

( ---- Screen: the pixels are a byte each, the board draws them ---- )

create pal16 16 4 * allot   ( $00rrggbb, 32 bits each )
: rpi-set-colors ( -- )
  16 0 do
    i 2 rshift ?dup 0= if i 3 and then system-color
    dup 8 rshift nibble>byte 16 lshift
    over 4 rshift nibble>byte 8 lshift or
    swap nibble>byte or
    pal16 i 4 * + l!
  loop  -1 to dirty ;
' rpi-set-colors is set-colors

: show ( -- )   ( the screen of the board for the pixels, at the zoom chosen )
  varvara-zoom to zoom   scr-w scr-h screen  -1 to dirty ;

: open-surface ( w h -- )
  pixels if pixels free drop 0 to pixels then
  to scr-h to scr-w
  scr-w to pitch
  scr-w scr-h * allocate throw to pixels
  pixels pitch scr-h * erase
  scr-w 8 rshift $22 dev!  scr-w $23 dev!  scr-h 8 rshift $24 dev!  scr-h $25 dev!
  show  set-colors ;

: rpi-resize ( -- )
  $22 dev2@ $24 dev2@ 2dup scr-h = swap scr-w = and if 2drop exit then
  open-surface ;
' rpi-resize is resize

: present ( -- )
  pixels scr-w scr-h pal16 fb-blit8  flip  0 to dirty ;

: zoom-step ( -- )
  varvara-zoom 3 < if varvara-zoom 1+ else 1 then to varvara-zoom  show ;

( ---- Screenshot: an 8-bit BMP with the 16 colors, like SDL's ---- )

create bmp-head 54 allot
create bmp-zeros 4 allot
: le32! ( v a -- ) over $ffff and over w! swap 16 rshift swap 2 + w! ;
: write-bmp ( fh -- )
  { fh }
  scr-w 3 + -4 and { stride }
  bmp-head 54 erase  66 bmp-head c!  77 bmp-head 1+ c!
  118 stride scr-h * + bmp-head 2 + le32!  118 bmp-head 10 + le32!  40 bmp-head 14 + le32!
  scr-w bmp-head 18 + le32!  scr-h bmp-head 22 + le32!
  1 bmp-head 26 + w!  8 bmp-head 28 + w!
  stride scr-h * bmp-head 34 + le32!  16 bmp-head 46 + le32!
  bmp-head 54 fh write-file throw
  pal16 64 fh write-file throw
  0 scr-h 1- do
    pixels i pitch * + scr-w fh write-file throw
    stride scr-w - ?dup if bmp-zeros swap fh write-file throw then
  -1 +loop ;
: shot ( -- )
  s" varvara.bmp" w/o create-file throw dup >r write-bmp r> close-file throw ;
: screenshot ( -- ) ['] shot catch if ." varvara.bmp: not written" cr then ;

( ---- Audio: the PCM queue of the board ---- )

: rpi-audio-queued ( -- n ) snd-queued ;
: rpi-audio-queue ( n -- ) aout swap snd-queue ;
' rpi-audio-queued is audio-queued
' rpi-audio-queue is audio-queue
: audio-start-up ( -- ) snd-ok if -1 else 0 then to audio-dev ;
also internals
: pump ( -- ) raw-yield ;       ( feeds the sound output )
previous

( ---- Datetime: the clock of the board, UTC unless told ---- )

0 value tz-minutes
0 value tm-s   0 value tm-days   0 value ty   0 value tmo   0 value td
create cumdays 0 , 31 , 59 , 90 , 120 , 151 , 181 , 212 , 243 , 273 , 304 , 334 ,
: civil ( days -- )   ( sets ty tmo td )
  719468 + { z }
  z 146097 / { era }
  z era 146097 * - { doe }
  doe doe 1460 / - doe 36524 / + doe 146096 / - 365 / { yoe }
  yoe era 400 * + { y }
  doe 365 yoe * yoe 4 / + yoe 100 / - - { doy }
  5 doy * 2 + 153 / { mp }
  doy 153 mp * 2 + 5 / - 1+ to td
  mp 10 < if mp 3 + else mp 9 - then dup to tmo
  2 <= if y 1+ else y then to ty ;
: leap? ( -- f ) ty 4 mod 0= ty 100 mod 0<> and ty 400 mod 0= or ;
: yday ( -- n ) cumdays tmo 1- cells + @ td + 1- tmo 2 > leap? and if 1+ then ;
: tm-update ( -- )
  unix-time tz-minutes 60 * +
  dup 86400 / dup to tm-days civil
  86400 mod to tm-s ;
: rpi-tm@ ( n -- v )   ( the fields of struct tm )
  tm-update
  case
    0 of tm-s 60 mod endof
    1 of tm-s 60 / 60 mod endof
    2 of tm-s 3600 / endof
    3 of td endof
    4 of tmo 1- endof
    5 of ty 1900 - endof
    6 of tm-days 4 + 7 mod endof
    7 of yday endof
    0 swap
  endcase ;
' rpi-tm@ is tm@

( ---- Controller, Mouse, Console input: polled once a frame ---- )

create button-keys 224 c, 226 c, 225 c, 227 c, 210 c, 209 c, 208 c, 207 c,   ( bits 0 to 7 )
0 value held
0 value mheld
0 value fprev
0 value mx   0 value my

: button-update ( i -- )
  { i }  i button-keys + c@ input-down? 0<> { now }  1 i lshift { m }
  now  m held and 0<>  <> if  m now buttons  m held xor to held  then ;
: mouse-button-update ( n -- )
  { n }  256 n - input-down? 0<> { now }  1 n 1- lshift { m }
  now  m mheld and 0<>  <> if  n now mouse-button  mouse  m mheld xor to mheld  then ;
: edge? ( k bit -- f )   ( the key went down since the last frame )
  { k bit }  k input-down? 0<> { now }  fprev bit and 0<> { was }
  now if fprev bit or else fprev bit invert and then to fprev
  now was 0= and ;

: poll-input ( -- )
  input-events
  8 0 do i button-update loop
  begin input-text dup 0< 0= while send-key repeat drop
  mouse-x mouse-y my = swap mx = and 0= if
    mouse-x to mx  mouse-y to my  mx my mouse-xy mouse
  then
  4 1 do i mouse-button-update loop
  wheel ?dup if 0 swap negate scroll then
  begin uart-key dup 0< 0= while 1 console-input repeat drop
  229 1 edge? if -1 to reboot then
  228 2 edge? if -1 to quit-gui then
  230 4 edge? if zoom-step then
  231 8 edge? if flush-out debug-stacks then
  232 16 edge? if screenshot then ;

( ---- The main loop ---- )

: setup-devices ( -- )
  ['] system-dei ['] varvara-system-deo 0 device!
  ['] plain-dei ['] screen-deo 2 device!
  4 0 do ['] audio-dei ['] audio-deo i 3 + device! loop
  ['] datetime-dei ['] plain-deo $c device!
  setup-files ;

: boot ( -- )
  rom-path rom-len load-rom  setup-devices
  achan 4 16 cells * erase  audio-start-up
  init-w init-h open-surface
  0 to held  0 to mheld  0 to fprev  -1 to mx  -1 to my
  n-args 0> if 1 $17 dev! then
  $100 eval  run-args  present ;

: frame ( -- )
  poll-input  $20 dev2@ eval  audio-pump  flush-out
  dirty if present then ;

: run-rom ( -- )
  boot  0 to quit-gui  0 to reboot  ticks { next }
  begin quit-gui halted? or 0= while
    reboot if 0 to reboot boot then
    frame
    17 +to next  next ticks - dup 0> if delay else drop pump ticks to next then
  repeat
  flush-out close-screen  0 to audio-dev
  pixels if pixels free drop 0 to pixels then ;

previous previous definitions

also uxn
: varvara ( a n -- )   ( run the ROM named a n )
  rom! run-rom ;
: varvara-arg ( a n -- )   ( one more argument for the ROM, sent to its Console )
  arg-add ;
: varvara-args-clear ( -- ) args-clear ;
: varvara-open ( file-a file-n rom-a rom-n -- )   ( the ROM, with a file as its argument )
  2>r args-clear arg-add 2r> rom! run-rom ;
previous

also rpi
: tz! ( minutes -- )   ( the offset of the local time from UTC, for varvara's Datetime )
  [ also uxn ] to tz-minutes [ previous ] ;
previous
