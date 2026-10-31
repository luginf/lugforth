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

( Basic I/O: the text screen and the keyboard, or stdin and stdout when they )
( are files; see main.c. Files: files.h, see ../baremetal/files.fs. )
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

( The machine: the vocabulary dos, also named rpi, for the Forth shared with )
( the boards: common/pager.fs asks rpi for con-rows )
vocabulary dos   dos definitions
transfer dos-builtins
forth definitions
: rpi ( -- ) dos ;

( The current directory: cd, pwd, md, rd )
also dos
: cd ( "dir" -- )   bl parse chdir if ." no such directory" cr then ;
: pwd ( -- )   getcwd type cr ;
: md ( "dir" -- )   bl parse mkdir if ." cannot make the directory" cr then ;
: rd ( "dir" -- )   bl parse rmdir if ." cannot remove the directory (not empty?)" cr then ;
previous

( The keyboard of the sdl2 screens, read by interrupt: US by default, or French )
( AZERTY with AltGr. In text mode the BIOS gives the characters, laid out by KEYB. )
also dos
: azerty ( -- )   1 keyboard! ;
: qwerty ( -- )   0 keyboard! ;
previous

( The size of the text screen, for the editors and the pager )
also dos
: form ( -- rows cols )   con-rows ?dup 0= if 25 then con-cols ;
previous

( The command line: argc, argv, as on Linux )
internals
: argc ( -- n ) 'argc @ ;
: argv ( n -- a n ) cells 'argv @ + @ z>s ;
forth

( Where lugforth.exe is, for its LIB folder: the directory of argv 0 )
create home-buf 260 allot   0 value home-n
: home-find ( -- )   \ home-n: the length of the folder in argv 0, up to its last / or \
  0 argv 200 min { a n }
  n begin dup 0> while  1- dup a + c@ dup [char] / = swap [char] \ = or if 1+ to home-n 0 then  repeat drop
  a home-buf home-n cmove ;
: home ( -- a n )   home-n 0= if home-find then  home-buf home-n ;   \ the folder of the program (empty if unknown)
create lib-buf 300 allot
: lib-file ( a n -- a' n' )   \ LIB/name beside the program, or in the current folder
  { a n }  home lib-buf swap cmove  s" lib/" lib-buf home-n + swap cmove
  a lib-buf home-n + 4 + n cmove  lib-buf home-n 4 + n + ;

( The editors ed and writhdeck, from LIB: editor.fs, writhdeck.fs, eval_copy.fs )
\ The first use includes the file, which redefines the word, then runs it with
\ the file name typed. The names are cut to 8.3 by DOS (writhdec.fs, eval_cop.fs):
\ tools/dos_stage.py writes them so.
create lz-buf 300 allot
: lz-run ( na nn wa wn -- )   \ evaluates "word name"
  { na nn wa wn }
  wa lz-buf wn cmove  32 lz-buf wn + c!  na lz-buf wn + 1+ nn cmove
  lz-buf wn 1+ nn + evaluate ;
: ed ( "file" -- )   \ edit a file
  bl parse 200 min { na nn }
  s" editor.fs" lib-file included
  na nn s" ed" lz-run ;
: writhdeck ( "file" -- )   \ edit a text for writing; no name opens the file browser
  bl parse 200 min { na nn }
  s" writhdeck.fs" lib-file included
  na nn s" writhdeck" lz-run ;

( Setup entry )
internals also dos
: ok   ." Lugforth (" board type ." )" raw-ok ;
previous forth
