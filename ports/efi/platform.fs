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

( Add a yielding task so pause yields )
internals definitions
: yield-step   raw-yield yield ;
' yield-step 100 100 task yield-task
yield-task start-task
forth definitions

( Basic I/O: the screen console and COM1, see main.c. Files: files.h, see ../baremetal/files.fs. )
also internals definitions
: setup-saving-base ;  ( no saved image )
variable last-key  -1 last-key !
forth definitions internals
: default-type ( a n -- ) raw-type ;
: default-key? ( -- f )
   last-key @ 0< if 1000 getchar_timeout_us last-key ! then
   last-key @ 0< 0=
;
: default-key ( -- ch )
   begin default-key? 0= while pause repeat
   last-key @ -1 last-key !
;
previous

' default-type is type
' default-key is key
' default-key? is key?
' raw-terminate is terminate
-1 echo !

( The machine: the vocabulary pc, also named rpi, so that the sdl2 words of )
( ports/baremetal/sdl2.fs and the programs written for the Pi find their words here )
vocabulary pc   pc definitions
transfer pc-builtins
forth definitions
: rpi ( -- ) pc ;

( Keyboard layout of the console: US by default, or French AZERTY with AltGr )
also rpi
: azerty ( -- )   1 keyboard! ;
: qwerty ( -- )   0 keyboard! ;
previous

( Size of the console on the screen: 1 is 640x480, 2 is 1280x960, 3 is 1920x1440 )
also rpi
: console-zoom ( n -- )   con-zoom! ;
previous

( Speed of a relative mouse: percent of a screen pixel per count of the device, 40 by default )
also rpi
: mouse-speed ( n -- )   mouse-speed! ;
previous

( The current directory of the disk: cd, pwd )
also rpi
: cd ( "dir" -- )   bl parse chdir if ." no such directory" cr then ;
: pwd ( -- )   getcwd type cr ;
: md ( "dir" -- )   bl parse mkdir if ." cannot make the directory" cr then ;
: rd ( "dir" -- )   bl parse rmdir if ." cannot remove the directory (not empty?)" cr then ;
previous

( The editors ed and writhdeck, from the disk: /lib/editor.fs and /lib/writhdeck.fs )
\ The first use includes the file, which redefines the word, then runs it with
\ the file name typed. The console is the terminal: 80 columns by 30 rows.
: form ( -- rows cols ) 30 80 ;
create lz-buf 300 allot
: lz-run ( na nn wa wn -- )   \ evaluates "word name"
  { na nn wa wn }
  wa lz-buf wn cmove  32 lz-buf wn + c!  na lz-buf wn + 1+ nn cmove
  lz-buf wn 1+ nn + evaluate ;
: ed ( "file" -- )   \ edit a file
  bl parse 200 min { na nn }
  s" /lib/editor.fs" included
  na nn s" ed" lz-run ;
: writhdeck ( "file" -- )   \ edit a text for writing; no name opens the file browser
  bl parse 200 min { na nn }
  s" /lib/writhdeck.fs" included
  na nn s" writhdeck" lz-run ;

( paste: a whole file over the serial line, evaluated as include would )
\ Over the serial line (QEMU): send `paste`, the text, then Ctrl-D (4). The
\ text is kept and evaluated at once, so strings and comments may span
\ lines (r~ ... ~ tunes, for example), which line by line sending breaks.
\ tools/efi_qemu_test.py sends files this way.
262144 value paste-max
: paste ( -- )
  paste-max allocate throw 0 { buf n }
  begin key dup 4 <> while
    n paste-max < if buf n + c! n 1+ to n else drop then
  repeat drop
  buf n ['] evaluate catch  buf free drop  throw ;

( Setup entry )
internals also rpi
: ok   ." Lugforth (" board type ." )" raw-ok ;
previous forth
