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

( Control structures outside a definition: 3 0 do i . loop, 1 if ." yes" then )
\ The standard leaves them undefined in interpret state. Here, when if begin do ?do
\ for or case is read outside a definition, the code up to the end of the structure
\ is compiled as an unnamed word, run at once, and forgotten (unless it allotted
\ memory, then the memory stays). The numbers typed before are its arguments, as in a definition.
\ Done with a recognizer that is asked first: if, do... are not redefined.
internals definitions
also recognizers

variable ctl-xt       \ the unnamed word being compiled, 0 when none
variable ctl-depth    \ structures opened and not yet closed
variable ctl-latest   \ the last dictionary entry before the unnamed word
variable ctl-heap     \ the real heap pointer, while the unnamed word is built in ctl-buf
8192 constant ctl-size
create ctl-buf ctl-size allot   \ the unnamed word is compiled here, so the dictionary is untouched
create ctl-table 32 cells allot   variable ctl-n   \ pairs: xt, delta

: ctl-add ( delta xt -- ) ctl-n @ 2* cells ctl-table + dup >r ! r> cell+ ! 1 ctl-n +! ;
: ctl-delta ( xt -- delta -1 | 0 )   \ 2 starts a temporary word, 1 opens, -1 closes
  ctl-n @ 0 ?do
    dup i 2* cells ctl-table + @ = if
      drop i 2* cells ctl-table + cell+ @ -1 unloop exit
    then
  loop drop 0 ;

: ctl: ( delta "name" -- ) ' ctl-add ;
2 ctl: if    2 ctl: begin   2 ctl: do   2 ctl: ?do   2 ctl: for   2 ctl: case
1 ctl: aft   1 ctl: ahead
-1 ctl: then   -1 ctl: until   -1 ctl: again   -1 ctl: repeat
-1 ctl: loop   -1 ctl: +loop   -1 ctl: next   -1 ctl: endcase

: ctl-live? ( -- f )   \ a temporary word is the one being compiled
  ctl-xt @ dup if current @ @ = else 0= 0= then ;

: ctl-restore ( -- )   \ back to the real dictionary
  ctl-heap @ 'heap !   ctl-latest @ current @ ! ;
: ctl-abort ( -- )   \ an error left an unnamed word half built
  ctl-xt @ if ctl-restore 0 ctl-xt ! then ;

: ctl-finish ( -- )
  ['] ; execute
  ctl-restore
  ctl-xt @ >r   0 ctl-xt !        \ cleared first: a throw in the code leaves nothing stale
  r> execute ;

: ctl-compile ( xt -- )   \ in the unnamed word: the structure word runs, and the depth follows
  dup ctl-delta drop dup 2 = if drop 1 then
  >r execute r> ctl-depth +!
  'heap @ ctl-buf - ctl-size 256 - > if ctl-abort -8 throw then
  ctl-depth @ 0= if ctl-finish then ;

: ctl-open ( xt -- )   \ if, do... met outside a definition
  current @ @ ctl-latest !   'heap @ ctl-heap !   ctl-buf 'heap !
  :noname ctl-xt !   0 ctl-depth !
  ctl-compile ;

' ctl-open ' ctl-compile ' ,  RECTYPE: RECTYPE-CTL

: rec-ctl ( a n -- xt rectype | rectype-none )
  state @ 0= if ctl-abort then   \ a structure dropped by an error
  state @ if ctl-live? 0= if 2drop RECTYPE-NONE exit then then
  find dup 0= if drop RECTYPE-NONE exit then
  dup ctl-delta 0= if drop RECTYPE-NONE exit then
  state @ 0= if 2 <> if drop RECTYPE-NONE exit then else drop then
  RECTYPE-CTL ;

\ Asked before the others (the stack of recognizers is read from its bottom)
: ctl-install ( -- )
  RECSTACK @ 1+ RECSTACK !
  RECSTACK cell+ dup cell+ RECSTACK @ 1- cells move
  ['] rec-ctl RECSTACK cell+ ! ;
ctl-install

previous
forth definitions
