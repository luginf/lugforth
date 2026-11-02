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

\ The editor ed, for a terminal on the serial line (minicom, picocom, screen: ANSI, 24 rows
\ by 80 columns unless  rows cols terminal-size  says otherwise). The big ed of the other
\ ports (editors/editor.fs) needs about 140 KB of heap while it loads, an ESP32-C3 has about
\ 70 KB: this is ports/esp32/miniedit.fs, a small one, sent once to /spiffs with
\     tools/esp32_put.py /dev/ttyACM0 ports/esp32/miniedit.fs
\ then  ed /spiffs/notes.txt . The first use includes the file, which redefines ed, then
\ runs it. Written without a board at hand: tested in the simulator for the editing.

0 value term-rows   0 value term-cols
: terminal-size ( rows cols -- )   to term-cols to term-rows ;
: form ( -- rows cols )   term-rows ?dup 0= if 24 then   term-cols ?dup 0= if 80 then ;

create lz-buf 300 allot
: lz-run ( na nn wa wn -- )   \ evaluates "word name"
  { na nn wa wn }
  wa lz-buf wn cmove  32 lz-buf wn + c!  na lz-buf wn + 1+ nn cmove
  lz-buf wn 1+ nn + evaluate ;

: load-lib ( a n -- )   \ includes a file of /spiffs and says what went wrong
  unused { a n free0 }
  a n ['] included catch ?dup if
    cr ." cannot load " a n type ."  (error " . ." , heap left " unused . ." of " free0 . ." )" cr
    -1 throw
  then ;

: ed ( "file" -- )   \ edit a file, with its whole path: /spiffs/notes.txt
  bl parse 200 min { na nn }
  s" /spiffs/miniedit.fs" load-lib
  na nn s" ed" lz-run ;
