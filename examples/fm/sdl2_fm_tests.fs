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

( Tests of the FM synthesizer and of its MIDI byte parser )
\
\ Not part of the default build. Run with:
\   SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy lugforth examples/fm/sdl2_fm_tests.fs

needs ../../common/testing.fs

include engine_posix.fs
sdl2 sdl2-fm
sdl2 definitions

( ---- The parser: a recorder replaces the synth ---- )
create log 64 cells allot   0 value logn
: rec3 ( st d1 d2 -- ) { st a b }  st log logn 3 * cells + !  a log logn 3 * 1+ cells + !
  b log logn 3 * 2 + cells + !  1 +to logn ;
: log@ ( msg field -- n ) swap 3 * + cells log + @ ;
: start-log ( -- ) 0 to logn  midi-reset  ['] rec3 to midi-hook ;

: test-parser-plain
  start-log   $90 midi-feed $3c midi-feed $64 midi-feed
  logn 1 =assert   0 0 log@ $90 =assert   0 1 log@ $3c =assert   0 2 log@ $64 =assert ;
: test-parser-running-status
  start-log   $90 midi-feed  $3c midi-feed $64 midi-feed  $40 midi-feed $7f midi-feed
  logn 2 =assert   1 0 log@ $90 =assert   1 1 log@ $40 =assert   1 2 log@ $7f =assert ;
: test-parser-one-data-byte
  start-log   $c5 midi-feed  $07 midi-feed  $d1 midi-feed $20 midi-feed
  logn 2 =assert   0 0 log@ $c5 =assert   0 1 log@ 7 =assert   1 0 log@ $d1 =assert ;
: test-parser-realtime-inside
  start-log   $90 midi-feed $3c midi-feed $f8 midi-feed $fe midi-feed $64 midi-feed
  logn 1 =assert   0 2 log@ $64 =assert ;
: test-parser-sysex
  start-log   $90 midi-feed  $f0 midi-feed $01 midi-feed $02 midi-feed $f7 midi-feed
  $3c midi-feed $64 midi-feed
  logn 0 =assert   ( sysex ended the running status )
  $90 midi-feed $3c midi-feed $64 midi-feed   logn 1 =assert ;
: test-parser-lone-data
  start-log   $12 midi-feed $34 midi-feed   logn 0 =assert ;
: test-parser-interrupted
  start-log   $90 midi-feed $3c midi-feed   $80 midi-feed $3c midi-feed $40 midi-feed
  logn 1 =assert   0 0 log@ $80 =assert ;

( ---- The default hook: what fm-midi asks of the synth ---- )
: test-midi-controls
  start-log  ['] fm-midi to midi-hook
  $b0 midi-feed 1 midi-feed 127 midi-feed   fm-index 190 =assert
  $b0 midi-feed 7 midi-feed 0 midi-feed   fm-volume 0 =assert
  $b0 midi-feed 7 midi-feed 127 midi-feed   fm-volume 100 =assert
  70 to fm-volume   30 to fm-index ;

( ---- The synth: render straight into mix ---- )
0 value peak
: peak! ( n -- ) mix swap 0 ?do mix i cells + @ abs peak max to peak loop drop ;
: render-n ( n -- ) mix over cells erase  0 to peak  dup fm-render  peak! ;
: crossings ( n -- n )   ( upward zero crossings of mix )
  0 { c }  0 { prev }  0 { cur }
  0 ?do mix i cells + @ to cur   prev 0 < cur 0 >= and if 1 +to c then   cur to prev loop
  c ;
: plain-patch   0 to fm-index  100 to fm-ratio  0 to fm-attack  5000 to fm-decay
  100 to fm-sustain  100 to fm-volume  100 to fm-release  fm-reset ;

: test-silent-before-note   plain-patch  512 render-n  peak 0 =assert   fm-active 0 =assert ;

: test-pure-tone
  plain-patch   audio-open   69 127 fm-on
  fm-active 1 =assert
  4096 render-n
  ( 440 Hz for 4096 samples: 40.8 periods )
  4096 crossings dup 40 >= swap 41 <= and assert
  peak 8000 > assert   peak 12000 < assert
  69 fm-off   fm-reset ;

: test-modulation-adds-sidebands
  plain-patch   audio-open   69 127 fm-on   4096 render-n   4096 crossings { pure }
  fm-reset
  30 to fm-index  200 to fm-ratio  100 to fm-msus   69 127 fm-on   4096 render-n
  4096 crossings pure <> assert    ( the shape is no longer a sine )
  fm-reset   plain-patch ;

: test-release-frees-the-voice
  plain-patch   audio-open   60 100 fm-on   67 100 fm-on   fm-active 2 =assert
  60 fm-off   2048 render-n   fm-active 2 =assert   ( still fading, 100 ms )
  60 fm-off   67 fm-off   4096 render-n   4096 render-n
  fm-active 0 =assert   fm-reset ;

: test-same-note-retriggers
  plain-patch   audio-open   60 100 fm-on   60 100 fm-on   fm-active 1 =assert  fm-reset ;

: test-voice-stealing
  plain-patch   audio-open
  12 0 do  40 i + 100 fm-on  loop   fm-active #fm =assert   fm-reset ;

: test-note-off-velocity-zero
  plain-patch   audio-open   start-log  ['] fm-midi to midi-hook
  $90 midi-feed 60 midi-feed 100 midi-feed   fm-active 1 =assert
  60 midi-feed 0 midi-feed   ( running status, velocity 0 )
  4 0 do 4096 render-n loop
  fm-active 0 =assert   fm-reset ;

: test-drums-channel-ignored
  plain-patch   audio-open   start-log  ['] fm-midi to midi-hook
  $99 midi-feed 36 midi-feed 100 midi-feed   fm-active 0 =assert ;

: test-pitch-bend-up
  plain-patch   audio-open   69 127 fm-on
  8191 fm-bend   4096 render-n   4096 crossings   ( two semitones up: 494 Hz, 45.9 periods )
  dup 45 >= swap 47 <= and assert
  fm-reset ;

: test-bend-down-is-lower   -8192 fm-bend  fv-bendmul 65536 < assert  0 fm-bend  fv-bendmul 65536 =assert ;


( ---- midi-event: an ALSA event written by hand, 28 bytes, offsets from alsa/seq_event.h ---- )
create fake-ev 28 allot
: fake ( type channel a b -- )   ( a b are note and velocity, or param and value )
  fake-ev 28 erase
  { type ch a b }
  type fake-ev c!   ch fake-ev 16 + c!
  type 10 = type 13 = or if
    a fake-ev 20 + l!   b fake-ev 24 + l!
  else
    a fake-ev 17 + c!   b fake-ev 18 + c!
  then
  fake-ev evp ! ;
: test-event-note-on
  start-log   6 3 60 100 fake  midi-event
  logn 1 =assert   0 0 log@ $93 =assert   0 1 log@ 60 =assert   0 2 log@ 100 =assert ;
: test-event-note-off
  start-log   7 0 61 64 fake  midi-event
  logn 1 =assert   0 0 log@ $80 =assert   0 1 log@ 61 =assert   0 2 log@ 64 =assert ;
: test-event-controller
  start-log   10 2 7 99 fake  midi-event
  logn 1 =assert   0 0 log@ $b2 =assert   0 1 log@ 7 =assert   0 2 log@ 99 =assert ;
: test-event-bend-low
  start-log   13 0 0 -8192 fake  midi-event
  logn 1 =assert   0 0 log@ $e0 =assert   0 1 log@ 0 =assert   0 2 log@ 0 =assert ;
: test-event-bend-high
  start-log   13 1 0 8191 fake  midi-event
  logn 1 =assert   0 0 log@ $e1 =assert   0 1 log@ 127 =assert   0 2 log@ 127 =assert ;
: test-event-bend-centre
  start-log   13 0 0 0 fake  midi-event
  logn 1 =assert   0 1 log@ 0 =assert   0 2 log@ 64 =assert ;

forth definitions
sdl2
run-tests
bye
