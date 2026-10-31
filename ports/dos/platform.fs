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

( Setup entry )
internals also dos
: ok   ." Lugforth (" board type ." )" raw-ok ;
previous forth
