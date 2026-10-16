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
\
\ Devices: System, Console (to the console and the UART), Screen, Controller
\ and Mouse, through the sdl2 words of the board (ports/rpi/sdl2.fs). Not done:
\ Audio, Datetime and the File devices (they read and write nothing).
\ Keys: Ctrl A, Alt B, Shift Select, Home Start, the arrows, the text typed;
\ F4 loads the ROM again, F12 quits.
\ The screen is 512x320 at zoom 2 (1024x640): `1 to varvara-zoom` for 512x320.

needs uxn.fs
needs screen.fs
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

: open-surface ( w h -- )
  pixels if pixels free drop 0 to pixels then
  to scr-h to scr-w
  scr-w to pitch
  scr-w scr-h * allocate throw to pixels
  pixels pitch scr-h * erase
  scr-w 8 rshift $22 dev!  scr-w $23 dev!  scr-h 8 rshift $24 dev!  scr-h $25 dev!
  varvara-zoom to zoom   scr-w scr-h screen
  set-colors ;

: rpi-resize ( -- )
  $22 dev2@ $24 dev2@ 2dup scr-h = swap scr-w = and if 2drop exit then
  open-surface ;
' rpi-resize is resize

: present ( -- )
  pixels scr-w scr-h pal16 fb-blit8  flip  0 to dirty ;

( ---- Controller and Mouse: polled once a frame ---- )

create button-keys 224 c, 226 c, 225 c, 227 c, 210 c, 209 c, 208 c, 207 c,   ( bits 0 to 7 )
0 value held
0 value mheld
0 value mx   0 value my

: button-update ( i -- )
  { i }  i button-keys + c@ pressed? 0<> { now }  1 i lshift { m }
  now  m held and 0<>  <> if  m now buttons  m held xor to held  then ;
: mouse-button-update ( n -- )
  { n }  256 n - pressed? 0<> { now }  1 n 1- lshift { m }
  now  m mheld and 0<>  <> if  n now mouse-button  mouse  m mheld xor to mheld  then ;

: poll-input ( -- )
  events
  8 0 do i button-update loop
  begin input-text dup 0< 0= while send-key repeat drop
  mouse-x mouse-y my = swap mx = and 0= if
    mouse-x to mx  mouse-y to my  mx my mouse-xy mouse
  then
  4 1 do i mouse-button-update loop
  wheel ?dup if 0 swap negate scroll then
  229 pressed? if -1 to reboot then
  228 pressed? if -1 to quit-gui then ;

( ---- The main loop ---- )

: setup-devices ( -- )
  ['] system-dei ['] varvara-system-deo 0 device!
  ['] plain-dei ['] screen-deo 2 device! ;

: boot ( -- )
  rom-path rom-len load-rom  setup-devices
  init-w init-h open-surface
  0 to held  0 to mheld  -1 to mx  -1 to my
  $100 eval  present ;

: frame ( -- )
  poll-input  $20 dev2@ eval  flush-out
  dirty if present then ;

: run-rom ( -- )
  boot  0 to quit-gui  0 to reboot  ticks { next }
  begin quit-gui halted? or 0= while
    reboot if 0 to reboot boot then
    frame
    17 +to next  next ticks - dup 0> if delay else drop ticks to next then
  repeat
  flush-out close-screen
  pixels if pixels free drop 0 to pixels then ;

previous previous definitions

also uxn
: varvara ( a n -- )   ( run the ROM named a n )
  rom! run-rom ;
previous
