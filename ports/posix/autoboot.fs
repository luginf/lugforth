\ Copyright 2021 Bradley D. Nelson
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
: gui-open ( -- )   gui-load s" sdl2 desktop console" eval-copy ;
: gui-restore ( -- )   only forth also internals 1 'argc ! previous definitions ;
: gui-start ( -- )
   ['] gui-open catch if
      only forth definitions
      s" --gui: cannot open a window (SDL2 or a display is missing)" type cr
      gui-tty? 0= if 1 terminate then exit
   then
   s" Use the menu (button at the top right), or type: words" type cr
   argc 3 >= if 2 argv ['] included catch drop then
   gui-restore ;
: autoexec
   ( Open passed file if any. )
   argc 2 >= if
      1 argv s" --gui" compare 0= if gui-start exit then
      1 argv ['] included catch if 1 terminate then exit
   then
   ( Open remembered file if any. )
   ['] revive catch drop
;
' autoexec ( leave on dstack for fini.fs )
forth definitions
