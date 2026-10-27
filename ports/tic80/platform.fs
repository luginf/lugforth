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

( I/O: TYPE writes to the console of TIC-80. A cartridge reads the keyboard )
( and the gamepad with the API words: KEY waits for nothing and gives -1. )
internals definitions
: setup-saving-base ;
forth definitions internals
' host-type is type
:noname ( -- ch ) -1 ; is key
:noname ( -- f ) 0 ; is key?
:noname ( n -- ) drop ; is terminate

( The commands of TIC-80, see ports/tic80/main.c: the host writes one in 'host, )
( then resumes the loop, which yields back when it is done. )
internals definitions
: host@ ( n -- x ) cells 'host + @ ;
: host! ( x n -- ) cells 'host + ! ;
: host-run ( -- n )
   0 host@ 1 = if 1 host@ 2 host@ ['] evaluate catch exit then
   0 host@ 2 = if 1 host@ catch exit then
   0 host@ 3 = if 2 host@ 1 host@ catch exit then
   0 ;
: host-loop ( -- )
   -1 6 host!
   begin
      yield
      sp0 sp! fp0 fp!
      host-run dup 4 host!
      if 0 state ! then
      depth 5 host!
   again ;
forth definitions
