\ Copyright 2021 Bradley D. Nelson
\ Modifications Copyright 2026 Luginfo10
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

( Include first argument if any )
internals definitions
( lugforth --gui [file.fs]: the console window with a menu; a terminal if it cannot open )
DEFINED? gui-load [IF]
: gui-open ( -- )   gui-load s" sdl2 desktop console" eval-copy ;
: gui-restore ( -- )   only forth also internals 1 'argc ! previous definitions ;
: gui-start ( -- )
   ['] gui-open catch if
      only forth definitions
      s" --gui: cannot open a window (SDL2 or a display is missing)" type cr
      gui-tty? 0= if 1 terminate then exit
   then
   argc 3 >= if 2 argv ['] included catch drop then
   gui-restore ;
[ELSE]
: gui-start ( -- )   s" --gui: this build has no window (lugforth-mini)" type cr 1 terminate ;
[THEN]
: e-add { buf n i -- buf n' }   \ the argument i and a blank, after the n bytes already there
   i argv 4000 n - 0 max min { a l }
   a buf n + l cmove  32 buf n l + + c!  buf n l + 1+ ;
: e-is-e? ( i -- f )   dup argc < if argv s" -e" compare 0= else drop 0 then ;
: e-line ( from -- a n next )   \ the arguments from there up to the next -e, one line of Forth
   { i }  4096 allocate throw 0
   begin i argc < if i e-is-e? 0= else 0 then while  i e-add  1 +to i  repeat
   i ;
: e-run ( -- )   \ lugforth -e words... [-e words...]: each -e starts a line, run in order
   1 begin dup argc < while
      1+ e-line >r  ['] evaluate catch if 1 terminate then  r>
   repeat drop ;
: autoexec
   ( Open passed file if any. )
   argc 2 >= if
      1 argv s" -e" compare 0= if
         argc 3 < if s" -e needs Forth words: lugforth -e writhdeck notes.txt" type cr 1 terminate then
         e-run exit
      then
      1 argv s" --gui" compare 0= if gui-start exit then
      1 argv ['] included catch if 1 terminate then exit
   then
   ( Open remembered file if any. )
   ['] revive catch drop
;
' autoexec ( leave on dstack for fini.fs )
forth definitions
