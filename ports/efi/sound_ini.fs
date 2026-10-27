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

( sound-off, sound-on, mouse-save: the choices are kept in lugforth.ini at the root of the stick )
\ Lines "sound = off" (or on) and "mouse = 55" (the mouse speed, see mouse-speed). Read at boot, before autoexec.fs.
forth definitions
also rpi
: snd-enable! ( f -- )   snd-enable ;
: ini-mouse! ( n -- )   mouse-speed! ;
: ini-mouse@ ( -- n )   mouse-speed@ ;
previous
internals definitions
create ini-buf 256 allot
create nl-buf 10 c,
create ini-nbuf 16 allot
variable ini-snd   1 ini-snd !
: ini-num>s ( n -- a u )   \ the digits of n (not negative)
  ini-nbuf 16 +
  begin 1 - >r 10 /mod swap [char] 0 + r@ c! r> over 0= until nip
  dup ini-nbuf 16 + swap - ;
: ini-num ( a n -- u )   \ the digits found in a line
  0 -rot over + swap ?do i c@ dup [char] 0 >= over [char] 9 <= and if [char] 0 - swap 10 * + else drop then loop ;
: ini-save ( -- )
  s" lugforth.ini" w/o create-file if drop exit then
  >r
  ini-snd @ if s" sound = on" else s" sound = off" then r@ write-file drop
  nl-buf 1 r@ write-file drop
  s" mouse = " r@ write-file drop
  ini-mouse@ ini-num>s r@ write-file drop
  nl-buf 1 r@ write-file drop
  r> close-file drop ;
: ini-line ( a n -- )
  begin dup if 2dup + 1- c@ 33 < else 0 then while 1- repeat
  dup 3 < if 2drop exit then
  over c@ $DF and [char] S = if + 1- c@ $DF and [char] F = if 0 else 1 then ini-snd ! exit then
  over c@ $DF and [char] M = if ini-num ?dup if ini-mouse! then exit then
  2drop ;
: ini-eol ( a n -- k )
  0 { a n k }
  begin k n < if a k + c@ 10 <> else 0 then while 1 +to k repeat k ;
: ini-step ( a n -- a' n' )
  2dup ini-eol { a n k }
  a k ini-line
  k n < if a k 1+ + n k 1+ - else a n + 0 then ;
: ini-load ( -- )
  s" lugforth.ini" r/o open-file if drop exit then
  >r
  ini-buf 255 r@ read-file if drop else
    ini-buf swap begin dup 0> while ini-step repeat 2drop
    ini-snd @ 0= if 0 snd-enable! then
  then
  r> close-file drop ;
variable ini-old
boot-prompt @ ini-old !
: ini-boot  ini-load ini-old @ ?dup if execute then ;
' ini-boot boot-prompt !
forth definitions
also internals
: sound-off ( -- )   0 snd-enable!  0 ini-snd !  ini-save ;
: sound-on ( -- )    1 snd-enable!  1 ini-snd !  ini-save ;
: sound ( -- )       sound-on ;
: mouse-save ( -- )   ini-save ;      \ keeps the current mouse speed (and the sound choice) in lugforth.ini
previous
