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

( Tests of sdl2.fs )
\
\ Needs libSDL2, so this is not part of the default build. Run headless with:
\   SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy lugforth ports/posix/sdl2_tests.fs

needs ../../common/testing.fs

sdl2 definitions

( Inject a fake event, as if the user had done it )
create pushed 64 allot
: push-event ( a b type -- ) pushed 64 erase  pushed l!  pushed 16 + l!  pushed 20 + l!
                             pushed SDL_PushEvent ?sdl ;
: push-key ( sym scancode type -- ) push-event ;
: push-mouse ( x y type button -- ) pushed 64 erase  pushed 16 + c!  pushed l!
                                    pushed 24 + l!  pushed 20 + l!
                                    pushed SDL_PushEvent ?sdl ;

: test-screen
  64 48 screen
  width 64 =assert   height 48 =assert
  screen-open? assert
  close-screen
  screen-open? 0= assert
;

: test-reopen
  32 32 screen   16 16 screen
  width 16 =assert
  close-screen
;

: test-box
  64 48 screen
  black color cls
  red color 10 10 20 20 box
  15 15 pixel@ $ff0000 =assert
  5 5 pixel@ 0 =assert
  40 40 pixel@ 0 =assert
  close-screen
;

: test-rgb-and-named-colors
  64 48 screen
  0 255 0 rgb  0 0 8 8 box
  4 4 pixel@ $00ff00 =assert
  blue color  0 0 8 8 box
  4 4 pixel@ $0000ff =assert
  $102030 color  0 0 8 8 box
  4 4 pixel@ $102030 =assert
  white color  0 0 8 8 box
  4 4 pixel@ $ffffff =assert
  close-screen
;

: test-alpha-blend
  64 48 screen
  black color cls
  white color 128 alpha  0 0 8 8 box
  4 4 pixel@ $ff and dup 120 >assert 136 <assert
  close-screen
;

: test-dot-and-line
  64 48 screen
  black color cls
  white color  3 3 dot
  3 3 pixel@ $ffffff =assert
  4 3 pixel@ 0 =assert
  0 10 20 10 line
  0 10 pixel@ $ffffff =assert
  20 10 pixel@ $ffffff =assert
  10 10 pixel@ $ffffff =assert
  10 11 pixel@ 0 =assert
  close-screen
;

: test-frame
  64 48 screen
  black color cls
  white color  10 10 10 10 frame
  10 10 pixel@ $ffffff =assert
  19 19 pixel@ $ffffff =assert
  15 15 pixel@ 0 =assert
  close-screen
;

: test-disc
  64 48 screen
  black color cls
  yellow color  30 24 8 disc
  30 24 pixel@ $ffff00 =assert
  30 17 pixel@ $ffff00 =assert
  38 24 pixel@ $ffff00 =assert
  22 24 pixel@ $ffff00 =assert
  40 24 pixel@ 0 =assert
  30 14 pixel@ 0 =assert
  close-screen
;

: test-circle
  64 48 screen
  black color cls
  white color  30 24 8 circle
  38 24 pixel@ $ffffff =assert
  22 24 pixel@ $ffffff =assert
  30 16 pixel@ $ffffff =assert
  30 32 pixel@ $ffffff =assert
  30 24 pixel@ 0 =assert
  close-screen
;

: test-keys
  64 48 screen
  events
  [char] a pressed? 0= assert
  [char] a 4 SDL_KEYDOWN push-key   events
  [char] a pressed? assert
  [char] A pressed? assert
  [char] a last-key =assert
  [char] a 4 SDL_KEYUP push-key   events
  [char] a pressed? 0= assert
  close-screen
;

: test-special-keys
  64 48 screen
  $40000050 80 SDL_KEYDOWN push-key   events
  key-left pressed? assert
  key-right pressed? 0= assert
  $4000004f 79 SDL_KEYDOWN push-key   events
  key-right pressed? assert
  32 44 SDL_KEYDOWN push-key   events
  key-space pressed? assert
  27 41 SDL_KEYDOWN push-key   events
  key-esc pressed? assert
  close-screen
;

: test-mouse
  64 48 screen
  10 20 SDL_MOUSEMOTION 0 push-mouse   events
  mouse-x 10 =assert   mouse-y 20 =assert
  LEFT-BUTTON pressed? 0= assert
  11 21 SDL_MOUSEBUTTONDOWN 1 push-mouse   events
  LEFT-BUTTON pressed? assert
  RIGHT-BUTTON pressed? 0= assert
  11 21 SDL_MOUSEBUTTONUP 1 push-mouse   events
  LEFT-BUTTON pressed? 0= assert
  close-screen
;

: test-quit
  64 48 screen
  events
  quit? 0= assert
  0 0 SDL_QUIT push-event   events
  quit? assert
  close-screen
;

: test-timing
  ticks { t0 }
  20 delay
  ticks t0 - 15 >assert
  64 48 screen
  flip flip
  dt 0 >= assert
  close-screen
;

: test-beep
  64 48 screen
  audio-open
  audio-open? if
    beeping? 0= assert
    440 200 beep
    beeping? assert
    silence
    beeping? 0= assert
    1 to wave  880 50 beep  2 to wave  880 50 beep  3 to wave  880 50 beep
    beeping? assert
    silence
    0 to wave
  then
  close-screen
;

: reset-hits   0 to wave   0 to pitch-drop   3000 to beep-volume   clear-voices ;

create hit-buf 4096 2* allot
: peak ( a n -- max )   ( largest absolute sample of n starting at a )
  0 { a n m }  n 0 ?do a i 2* + sw@ abs m max to m loop  m ;
: crossings ( a n -- k )   ( how many times the wave changes sign )
  0 { a n k }  n 1 - 0 ?do
    a i 2* + sw@ 0<  a i 1+ 2* + sw@ 0<  <> if k 1+ to k then
  loop  k ;

: test-hit-one
  reset-hits
  2 to wave  440 100 start-voice
  voices-active 1 =assert
  1000 hit-buf render
  wave@ 2 =assert
  hit-buf 1000 peak dup 2900 >assert 3001 <assert
  reset-hits
;

: test-hit-mix
  reset-hits
  440 100 start-voice   440 100 start-voice
  voices-active 2 =assert
  1000 hit-buf render
  hit-buf 1000 peak 5000 >assert
  reset-hits
;

: test-hit-decay-and-end
  reset-hits
  440 100 start-voice
  4096 hit-buf render
  hit-buf 100 peak
  hit-buf 3996 2* + 100 peak   ( the end of the buffer, 9% of the volume left )
  5 * swap <assert
  voices-active 1 =assert
  400 hit-buf render
  voices-active 0 =assert
  reset-hits
;

: test-hit-pitch-drop
  reset-hits
  100 to pitch-drop  400 200 start-voice
  4096 hit-buf render
  hit-buf 1024 crossings
  hit-buf 3072 2* + 1024 crossings
  >assert
  reset-hits
;

: test-hit-voice-limit
  reset-hits
  12 0 do 440 100 start-voice loop
  voices-active 8 =assert
  reset-hits
;

: test-hit-noise-and-silence
  reset-hits
  3 to wave  200 100 start-voice
  1000 hit-buf render
  hit-buf 1000 peak 1000 >assert
  audio-open
  audio-open? if silence voices-active 0 =assert beeping? 0= assert then
  reset-hits
;

: test-hit-plays
  64 48 screen  audio-open
  audio-open? if
    reset-hits
    440 100 drum-hit
    voices-active 1 =assert
    beeping? assert
    200 delay
    voices-active 0 =assert
    silence
  then
  close-screen
;

( ---- play: the notes it asks for ---- )
create got 64 cells allot   0 value ngot
: log-note ( freq ms -- ) ngot 2* cells got + 2!  1 +to ngot ;   ( 2! stores ms then freq )
: note@ ( i -- freq ms ) 2* cells got + 2@ ;
: song ( a n -- ) 0 to ngot  ['] log-note is p-out  4 to p-oct  4 to p-len  120 to p-tempo
  play  ['] beep is p-out ;
: test-play-notes
  s" c d e" song
  ngot 3 =assert   0 note@ 500 =assert 262 =assert   2 note@ drop 330 =assert ;
: test-play-length
  s" l8 c4 c c." song
  0 note@ 500 =assert drop   1 note@ 250 =assert drop   2 note@ 375 =assert drop ;
: test-play-octave-accidentals
  s" o5 c o4 c# > c < < c f- >" song
  0 note@ drop 524 =assert   1 note@ drop 277 =assert   2 note@ drop 524 =assert
  3 note@ drop 131 =assert   4 note@ drop 165 =assert ;
: test-play-rest-tempo
  s" t240 r c r2" song
  0 note@ 250 =assert 0 =assert   1 note@ 250 =assert 262 =assert   2 note@ 500 =assert 0 =assert ;
: test-play-wave
  s" w2 c" song  wave@ 2 =assert  sine wave@ 4 =assert  square wave@ 0 =assert
  1 wave wave@ 1 =assert  3 to wave wave@ 3 =assert  0 wave ;
: test-play-compat
  s" c p4 n49 n0 n61" song
  ngot 5 =assert   0 note@ drop 262 =assert   1 note@ 500 =assert 0 =assert
  2 note@ drop 262 =assert   3 note@ drop 0 =assert   4 note@ drop 524 =assert ;
: test-play-tie
  s" c4&c8 c4 d4&e4" song
  ngot 4 =assert   0 note@ 750 =assert 262 =assert   1 note@ 500 =assert 262 =assert
  2 note@ 500 =assert 294 =assert   3 note@ 500 =assert 330 =assert ;
: test-play-volume
  beep-volume@ { keep }  s" v0 c" song  beep-volume@ 0 =assert  s" v15" song  beep-volume@ 6000 =assert
  keep beep-volume ;
: vnote@ ( v k -- freq ms ) p-slot 2@ swap ;
: test-play-voices
  4 to p-oct  4 to p-len  120 to p-tempo
  s" c d, o3 g2 w2 e, v8 c, r" p-parse
  p-nv 4 =assert   0 cells p-cnt + @ 2 =assert  1 cells p-cnt + @ 2 =assert
  2 cells p-cnt + @ 1 =assert   3 cells p-cnt + @ 1 =assert
  0 0 vnote@ 500 =assert 262 =assert   0 1 vnote@ 500 =assert 294 =assert
  1 0 vnote@ 1000 =assert 196 =assert   1 1 vnote@ 500 =assert 165 =assert
  1 0 p-slot 2 cells + @ 0 =assert   1 1 p-slot 2 cells + @ 2 =assert
  2 0 p-slot 3 cells + @ 3200 =assert   0 0 p-slot 3 cells + @ 3000 =assert
  3 0 vnote@ 500 =assert 0 =assert
  0 wave 3000 beep-volume ;
: test-play-voices-limit
  s" c,c,c,c,c,c" p-parse  p-nv 4 =assert ;
: test-play-no-comma-streams
  s" c d" song  ngot 2 =assert ;
create mixa 8 allot   create mixb 8 allot
: test-mix-add
  30000 mixa w!  -30000 mixa 2 + w!   30000 mixb w!  -30000 mixb 2 + w!
  mixa mixb 2 mix-add
  mixb w@s 32767 =assert   mixb 2 + w@s -32767 =assert ;
: test-adsr
  0 0 100 0 adsr  adsr@ 0 =assert 100 =assert 0 =assert 0 =assert
  10 20 50 30 adsr  adsr@ 30 =assert 50 =assert 20 =assert 10 =assert
  1000 ms>smp 44100 =assert
  44100 env-start
  0 env-gain 0 =assert   441 env-gain 1000 =assert   1500 env-gain 500 =assert
  44099 env-gain 20 < assert
  adsr-off env-on 0 =assert ;
create ebuf 9000 allot
: test-adsr-beep
  10 10 50 10 adsr  440 4410 ebuf fill-beep  adsr-off
  ebuf w@s 0 =assert   ebuf 4409 2* + w@s abs 300 < assert
  ebuf 2200 2* + w@s abs 2000 < assert ;
: test-voices-queued
  audio-open? if
    silence
    s" o4 c4 e4, o3 c2 g4 r4, o5 c1" play
    audio-dev SDL_GetQueuedAudioSize dup 170000 > assert 176400 > 0= assert
    silence
  then ;
: test-sine
  4 wave
  0 wave-sample 0 =assert   16384 wave-sample 32767 =assert   49152 wave-sample -32767 =assert
  0 to wave ;

forth definitions
sdl2
run-tests
bye
