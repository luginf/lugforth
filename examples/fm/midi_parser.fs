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

( MIDI byte parser of the FM synth, shared by the POSIX and Web builds )
\
\ Not loaded by itself: fm-midi-parser gives the source as a string, and the loaders
\ sdl2-fm of engine_posix.fs and engine_web.fs evaluate it after
\ defining fm-on fm-off fm-all-off fm-bend and the values fm-index fm-volume.
\ midi-feed takes the bytes of a MIDI stream one by one (running status, note on with
\ velocity 0 as note off, system real time bytes, SysEx and system common messages
\ are handled) and calls midi-hook ( status d1 d2 -- ) for each complete message;
\ by default that is fm-midi, which plays note on and off, pitch bend, CC 1 (index),
\ CC 7 (volume), CC 120 and 123 (all notes off). MIDI channel 10 (drums) is ignored.
\ NOTE: the source below is a string, so it must not contain a vertical bar.

internals definitions
: fm-midi-parser r|
0 value midi-status   0 value midi-have   0 value midi-d1   0 value midi-sysex

: fm-midi { st a b }   ( one message, as the status byte and two data bytes )
  st $f0 and { cmd }   st $0f and { ch }
  cmd $90 = b 0= and if 128 to cmd then
  cmd $80 = cmd $90 = or  ch 9 = and if exit then
  cmd $90 = if a b fm-on exit then
  cmd $80 = if a fm-off exit then
  cmd $e0 = if a b 7 lshift or 8192 - fm-bend exit then
  cmd $b0 <> if exit then
  a 1 = if b 3 * 2/ to fm-index exit then
  a 7 = if b 100 * 127 / to fm-volume exit then
  a 120 = a 123 = or if fm-all-off then
;
' fm-midi value midi-hook   ( xt, status d1 d2 -- : run for each message )

: midi-one? ( status -- f )   ( a message with one data byte: program change, pressure )
  $f0 and dup $c0 = swap $d0 = or ;
: midi-send ( d1 d2 -- ) midi-status -rot midi-hook execute ;
: midi-head ( byte -- )   ( a status byte or a system message )
  dup $f0 < if to midi-status 0 to midi-have 0 to midi-sysex exit then
  0 to midi-status   0 to midi-have
  $f0 = if 1 else 0 then to midi-sysex ;
: midi-feed ( byte -- )   ( the next byte of a MIDI stream )
  dup $f8 >= if drop exit then
  dup $80 >= if midi-head exit then
  midi-sysex midi-status 0= or if drop exit then
  midi-status midi-one? if 0 midi-send exit then
  midi-have 0= if to midi-d1 1 to midi-have exit then
  midi-d1 swap midi-send   0 to midi-have ;
: midi-reset ( -- ) 0 to midi-status 0 to midi-have 0 to midi-sysex ;
|  ;
forth definitions
