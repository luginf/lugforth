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

( sound-off, sound-on: the choice is kept in lugforth.ini at the root of the stick )
\ One line, "sound = off" or "sound = on". Read at boot, before autoexec.fs.
forth definitions
also rpi
: snd-enable! ( f -- )   snd-enable ;
previous
internals definitions
create ini-line 80 allot
create nl-buf 10 c,
: ini-write ( a n -- )
  s" lugforth.ini" w/o create-file if drop 2drop exit then
  >r r@ write-file drop  nl-buf 1 r@ write-file drop  r> close-file drop ;
: ini-trim ( n -- n' )   begin dup if ini-line over 1- + c@ 32 < else 0 then while 1- repeat ;
: ini-sound-load ( -- )
  s" lugforth.ini" r/o open-file if drop exit then
  >r
  ini-line 79 r@ read-file if drop else
    ini-trim dup 3 > if  ini-line + 1- c@ $DF and [char] F = if 0 snd-enable! then
    else drop then
  then
  r> close-file drop ;
variable ini-old
boot-prompt @ ini-old !
: ini-boot  ini-sound-load ini-old @ ?dup if execute then ;
' ini-boot boot-prompt !
forth definitions
also internals
: sound-off ( -- )   0 snd-enable!  s" sound = off" ini-write ;
: sound-on ( -- )    1 snd-enable!  s" sound = on" ini-write ;
: sound ( -- )       sound-on ;
previous
