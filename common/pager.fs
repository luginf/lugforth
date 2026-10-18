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

( Pager: the output of a line typed at the prompt stops at each screenful, )
( like less. Space: next page, Enter: next line, q or Esc: stop. Only on a )
( terminal of the POSIX port and on the screen console of the boards; not )
( while a file is included. pager-off and pager-on switch it. )
( The lines are counted from the last key read, so a program that reads )
( keys is never stopped. Stopping is a throw of -28, which quit does not )
( report. )

only forth also internals definitions
DEFINED? posix [IF] also posix [THEN]

-1 value pg-enabled
0 value pg-limit       ( lines to write before stopping, 0: off )
0 value pg-lines
0 value pg-old-type   0 value pg-old-key   0 value pg-old-key?   0 value pg-old-prompt
variable pg-k   variable pg-f

: pg-raw ( a n -- )   pg-old-type execute ;
: pg-emit ( c -- )   >r rp@ 1 pg-raw rdrop ;

DEFINED? posix [IF]
: pg-height ( -- rows )
  le-tty? 0= if 0 exit then
  ['] form catch if 0 exit then drop ;
[ELSE]
also rpi
: pg-height ( -- rows )   con-rows ;
previous
[THEN]

: pg-key ( -- c )   0 to pg-lines pg-old-key execute ;
: pg-key? ( -- f )   0 to pg-lines pg-old-key? execute ;
: pg-prompt ( -- )   \ the screen height, asked once per prompt
  pg-enabled if pg-height dup 7 > if 2 - else drop 0 then else 0 then to pg-limit
  0 to pg-lines   pg-old-prompt execute ;

: pg-active? ( -- f )   pg-limit 0<> 'tib @ input-buffer = and ;
: pg-esc[ ( -- )   27 pg-emit [char] [ pg-emit ;
: pg-wait ( -- c )   begin pg-old-key execute dup 32 = over 13 = or over 10 = or
  over [char] q = or over [char] Q = or over 27 = or over 3 = or 0= while drop repeat ;
: pg-pause ( -- )
  pg-esc[ [char] 7 pg-emit [char] m pg-emit
  s" -- more --" pg-raw
  pg-esc[ [char] m pg-emit
  pg-wait
  13 pg-emit pg-esc[ [char] K pg-emit
  dup 32 = if drop 0 to pg-lines exit then
  dup 13 = over 10 = or if drop pg-limit 1- to pg-lines exit then
  27 = if begin pg-old-key? execute while pg-old-key execute drop repeat -28 throw then
  -28 throw ;

: pg-scan ( a n -- k f )   \ k characters up to and including the first newline, f: found
  0 begin 2dup > while
    >r over r@ + c@ nl = if 2drop r> 1+ -1 exit then r> 1+
  repeat drop nip 0 ;
: pg-type ( a n -- )
  pg-active? 0= if pg-raw exit then
  begin dup while
    2dup pg-scan pg-f ! pg-k !
    over pg-k @ pg-raw
    pg-k @ /string
    pg-f @ if 1 +to pg-lines pg-lines pg-limit < 0= if pg-pause then then
  repeat 2drop ;

: pg-install
  ['] type >body @ to pg-old-type
  ['] key >body @ to pg-old-key
  ['] key? >body @ to pg-old-key?
  ['] prompt >body @ to pg-old-prompt
  ['] pg-type is type   ['] pg-key is key   ['] pg-key? is key?   ['] pg-prompt is prompt ;
pg-install

forth definitions internals
: pager-off   0 to pg-enabled   0 to pg-limit ;
: pager-on    -1 to pg-enabled ;

only forth definitions
