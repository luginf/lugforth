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

\ A small text editor for a terminal on the serial line (minicom, picocom, screen),
\ made to fit in the 70 KB of heap an ESP32-C3 has left: about 6 KB compiled.
\ Send it once to /spiffs (tools/esp32_put.py), then:  ed /spiffs/melody.fs
\   arrows, Home, End, PageUp, PageDown, Delete, Backspace, Enter, Tab (2 spaces)
\   Ctrl+S or Ctrl+O  save          Ctrl+Q or Ctrl+X  quit (twice if not saved)
\   Ctrl+A, Ctrl+E  start, end of the line      Ctrl+K  delete to the end of the line
\ The whole file is in memory (its size plus 8 KB). 24 x 80 unless  rows cols terminal-size.
\ Written without a board at hand: the editing is tested in the simulator, the files not.

0 value me-buf   0 value me-len   0 value me-cap   0 value me-cur
0 value me-top   0 value me-lx    0 value me-dirty 0 value me-warn
0 value me-quit  0 value me-last  0 value me-rows  0 value me-cols
create me-name 200 allot   0 value me-namen
create me-msg 80 allot   0 value me-msgn

: me@ ( i -- c ) me-buf + c@ ;
: csi ( -- ) 27 emit 91 emit ;
: me-num ( n -- ) s>d <# #s #> type ;
: me-goto ( row col -- )   \ 0 based
  csi swap 1+ me-num 59 emit 1+ me-num 72 emit ;
: me-say ( a n -- )   79 min dup to me-msgn me-msg swap cmove ;

( ---- Lines ---- )
: line-start ( i -- i' )   begin dup 0> if dup 1- me@ 10 <> else 0 then while 1- repeat ;
: line-end ( i -- i' )   begin dup me-len < if dup me@ 10 <> else 0 then while 1+ repeat ;
: next-line ( i -- i' )   line-end dup me-len < if 1+ then ;
: prev-line ( i -- i' )   dup 0> if 1- line-start then ;
: lines-between ( a b -- n )   0 -rot swap ?do i me@ 10 = if 1+ then loop ;
: me-col ( -- n )   me-cur dup line-start - ;

( ---- Editing ---- )
: me-insert ( c -- )
  me-len me-cap >= if drop 7 emit exit then
  me-buf me-cur + dup 1+  me-len me-cur -  cmove>
  me-buf me-cur + c!   1 +to me-len   1 +to me-cur   -1 to me-dirty ;
: me-delete ( -- )
  me-cur me-len < if
    me-buf me-cur + 1+  me-buf me-cur +  me-len me-cur - 1-  cmove
    -1 +to me-len   -1 to me-dirty
  then ;
: me-back ( -- )   me-cur 0> if -1 +to me-cur me-delete then ;
: me-left ( -- )   me-cur 0> if -1 +to me-cur then ;
: me-right ( -- )   me-cur me-len < if 1 +to me-cur then ;
: me-home ( -- )   me-cur line-start to me-cur ;
: me-end ( -- )   me-cur line-end to me-cur ;
: put-col ( start c -- )   over line-end 2 pick - min + to me-cur ;
: me-up ( -- )   me-cur line-start dup 0> if me-col swap prev-line swap put-col else drop then ;
: me-down ( -- )   me-cur line-end dup me-len < if me-col swap 1+ swap put-col else drop then ;
: me-kill ( -- )   me-cur me@ 10 = if me-delete else me-cur line-end me-cur - 0 ?do me-delete loop then ;
: times ( n xt -- )   swap 0 ?do dup execute loop drop ;

( ---- The screen ---- )
: text-rows ( -- n ) me-rows 1- ;
: me-scroll ( -- )
  me-cur me-top < if me-cur line-start to me-top then
  begin me-top me-cur line-start lines-between text-rows >= while me-top next-line to me-top repeat
  me-col me-lx < if me-col to me-lx then
  me-col me-lx me-cols + >= if me-col me-cols - 1+ to me-lx then ;
: show-char ( c -- )   dup 32 < if drop 32 else dup 127 = if drop 46 then then emit ;
0 value dr-i   0 value dr-e
: draw-line ( -- )   \ the row of the line at dr-i; dr-i moves to the next line
  dr-i line-end to dr-e
  dr-i me-lx +  dr-i me-lx + me-cols +  dr-e min  2dup < if swap ?do i me@ show-char loop else 2drop then
  csi 75 emit
  dr-e me-len < if dr-e 1+ else me-len 1+ then  to dr-i ;
: draw-status ( -- )
  me-rows 1- 0 me-goto  csi 55 emit 109 emit
  me-name me-namen 40 min type   me-dirty if 42 emit then
  ."   line " me-cur line-start 0 swap lines-between 1+ me-num   ."   col " me-col 1+ me-num
  me-warn if ."   NOT SAVED: Ctrl+Q again" else me-msgn if 2 spaces me-msg me-msgn type then then
  csi 75 emit  csi 48 emit 109 emit ;
: me-draw ( -- )
  me-scroll   0 0 me-goto   me-top to dr-i
  text-rows 0 ?do
    dr-i me-len <= if draw-line else csi 75 emit then
    13 emit 10 emit
  loop
  draw-status
  me-top me-cur line-start lines-between  me-col me-lx -  me-goto ;

( ---- Keys ---- )
1001 constant k-up   1002 constant k-down   1003 constant k-right   1004 constant k-left
1005 constant k-home   1006 constant k-end   1007 constant k-del   1008 constant k-pgup   1009 constant k-pgdn
0 value kn
: me-wait ( -- f )   30 0 do key? if -1 unloop exit then 2 ms loop 0 ;
: tilde-code ( n -- code )
  dup 1 = over 7 = or if drop k-home exit then
  dup 4 = over 8 = or if drop k-end exit then
  dup 3 = if drop k-del exit then
  dup 5 = if drop k-pgup exit then
  6 = if k-pgdn exit then
  27 ;
: letter-code ( c -- code )
  dup 65 = if drop k-up exit then      dup 66 = if drop k-down exit then
  dup 67 = if drop k-right exit then   dup 68 = if drop k-left exit then
  dup 72 = if drop k-home exit then    70 = if k-end exit then
  27 ;
: read-num ( n -- n' )   \ the rest of ESC [ n ... ~
  to kn
  begin
    me-wait if
      key dup 126 = if drop 0
      else dup 48 >= over 58 < and if 48 - kn 10 * + to kn else drop then -1 then
    else 0 then
  0= until  kn ;
: me-key ( -- code )
  key dup 27 <> if exit then drop
  me-wait 0= if 27 exit then
  key dup 91 <> over 79 <> and if drop 27 exit then drop
  me-wait 0= if 27 exit then
  key dup 48 >= over 58 < and 0= if letter-code exit then
  48 - read-num tilde-code ;

( ---- Files ---- )
: me-load ( -- )
  me-name me-namen r/o open-file if
    drop 8192 to me-cap  me-cap allocate throw to me-buf  0 to me-len
    s" new file" me-say exit
  then
  >r  r@ file-size throw drop  dup 8192 + to me-cap
  me-cap allocate throw to me-buf
  me-buf swap r@ read-file throw to me-len
  r> close-file drop ;
: me-save ( -- )
  me-name me-namen delete-file drop
  me-name me-namen w/o bin create-file if drop s" cannot save" me-say exit then
  >r  me-buf me-len r@ write-file  r> close-file drop
  if s" write error" me-say exit then
  0 to me-dirty   s" saved" me-say ;

( ---- The loop ---- )
: me-quit-key ( -- )
  me-dirty 0= me-warn or if -1 to me-quit else -1 to me-warn then ;
: me-handle ( k -- )
  dup 17 = over 24 = or if drop me-quit-key exit then
  0 to me-warn   0 to me-msgn
  dup 19 = over 15 = or if drop me-save exit then
  dup 13 = if drop 10 me-insert exit then
  dup 10 = if drop me-last 13 <> if 10 me-insert then exit then
  dup 127 = over 8 = or if drop me-back exit then
  dup 9 = if drop 32 me-insert 32 me-insert exit then
  dup 1 = if drop me-home exit then
  dup 5 = if drop me-end exit then
  dup 11 = if drop me-kill exit then
  dup k-up = if drop me-up exit then
  dup k-down = if drop me-down exit then
  dup k-left = if drop me-left exit then
  dup k-right = if drop me-right exit then
  dup k-home = if drop me-home exit then
  dup k-end = if drop me-end exit then
  dup k-del = if drop me-delete exit then
  dup k-pgup = if drop text-rows 1- 0 ?do me-up loop exit then
  dup k-pgdn = if drop text-rows 1- 0 ?do me-down loop exit then
  dup 32 >= over 1000 < and if me-insert exit then
  drop ;
: me-loop ( -- )
  begin
    key? 0= if me-draw then
    me-key dup me-handle to me-last
    me-quit
  until ;
: me-start ( -- )   0 to me-cur 0 to me-top 0 to me-lx 0 to me-dirty 0 to me-warn 0 to me-quit 0 to me-last ;
: me-run ( -- )   me-start  27 emit ." [2J"  me-loop  27 emit ." [2J" 27 emit ." [H" ;

: ed ( "file" -- )   \ edit a file; the whole path, as /spiffs/notes.txt
  bl parse dup 0= if 2drop ." usage: ed /spiffs/name" cr exit then
  200 min dup to me-namen  me-name swap cmove
  form to me-cols to me-rows
  0 to me-msgn  me-load
  ['] me-run catch
  me-buf free drop
  ?dup if ." editor error " . cr then ;
