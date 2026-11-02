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

( Tunes in ABC notation on a passive piezo buzzer )
\
\ Usage:
\   esp32-abc                        \ once: loads the reader (common/abc.fs), about 12 KB
\   4 buzzer                         \ the piezo between GPIO 4 and GND
\   r~ X:1
\   K:C
\   CDEF GABc ~ abc-play             \ the serial line stops it: any key
\ Voices: lines V:1 and V:2 in the tune, each with its own notes.
\ Voices V:1 V:2 ... and chords: the pin makes one tone at a time, so every
\ slice-ms it plays the next of the notes sounding (a fast arpeggio; voice 1,
\ the melody, gets melody-weight slices out of the others). The tone
\ comes from the LEDC PWM of the chip (ledcWriteTone), at 50 percent.
\ Written without a board at hand: tested in the simulator for the Forth only.

also internals definitions
vocabulary sdl2   ( where common/abc.fs puts abc, tune-notes and tune-ms )
variable abc-loaded
forth definitions also internals
: esp32-abc ( -- )
  abc-loaded @ if exit then  -1 abc-loaded !
  current @  forth definitions
  also internals  abc-parser evaluate  previous
  r~
also internals also sdl2 also abc-int also ledc

0 value buzzer-pin

( v3 of the Arduino core takes the pin and Hz, v2 a channel and milli-Hz )
DEFINED? ledcAttach [IF]
: set-buzzer ( pin -- ) dup to buzzer-pin  2000 10 ledcAttach drop ;
: pin-tone ( hz -- ) buzzer-pin swap ledcWriteTone drop ;
[ELSE]
: set-buzzer ( pin -- ) to buzzer-pin  0 2000000 10 ledcSetup drop  buzzer-pin 0 ledcAttachPin ;
: pin-tone ( hz -- ) 1000 * 0 swap ledcWriteTone drop ;
[THEN]

( C4 to B4; the other octaves are shifts )
create key-hz 262 , 277 , 294 , 311 , 330 , 349 , 370 , 392 , 415 , 440 , 466 , 494 ,
: key>hz ( midi-key -- hz )
  dup 12 / 5 -  swap 12 mod cells key-hz + @  swap
  dup 0< if negate rshift else lshift then ;

: ticks>ms ( ticks -- ms ) 125 * tune-bpm / ;
: ms>ticks ( ms -- ticks ) tune-bpm * 125 / ;

\ ---- Several voices, V:1 V:2 ... and chords ----
\ Where the notes are at a time tt (in ticks): gather puts the Hz of every note
\ sounding in hzs, with its voice in vws, and next-t is when that list changes.
-1 to voices?
2 value melody-weight
create vp 9 cells allot            \ by voice: the event it plays now
create hzs 8 cells allot   create vws 8 cells allot   0 value hz-n
0 value tt   0 value t0   0 value tune-end   0 value next-t
: vp@ ( v -- i ) cells vp + @ ;   : vp! ( i v -- ) cells vp + ! ;
: ev-end ( i -- ticks ) dup nt@ swap nd@ + ;
: find-ev { i v -- i' }   \ the first event from i that is in voice v
  begin i n-notes < if i nv@ v <> else 0 then while i 1+ to i repeat i ;
: ev-over? ( i -- f ) dup n-notes < if ev-end tt <= else drop 0 then ;
: hz+ ( hz v -- ) hz-n 8 < if hz-n cells vws + !  hz-n cells hzs + !  1 +to hz-n else 2drop then ;
: due ( t -- ) next-t min to next-t ;
0 value va-v   0 value va-i
: voice-add ( v -- )   \ the notes of voice v now: its chord, or nothing in a rest
  to va-v
  begin va-v vp@ ev-over? while  va-v vp@ 1+ va-v find-ev va-v vp!  repeat
  va-v vp@ to va-i
  va-i n-notes < 0= if exit then
  va-i nt@ tt > if va-i nt@ due exit then               \ not started: a rest
  va-i ev-end tt - 10 * va-i nd@ < if va-i ev-end due exit then   \ the last tenth is silence: repeated notes stay apart
  va-i ev-end va-i nd@ 10 / - due
  begin va-i n-notes < if va-i nt@ va-v vp@ nt@ = va-i nv@ va-v = and else 0 then while
    va-i nk@ key>hz  va-v hz+  1 +to va-i
  repeat ;
: tune-init
  0 to tune-end
  n-notes 0 ?do i ev-end tune-end max to tune-end loop
  n-voices 1+ 1 ?do 0 i find-ev i vp! loop ;
: gather ( -- )
  0 to hz-n  tune-end to next-t
  n-voices 1+ 1 ?do i voice-add loop ;

\ ---- abc-arp: the notes in turn, one every slice-ms (an arpeggio) ----
10 value slice-ms
0 value n-slice
: slice-hz ( n -- hz )
  gather
  hz-n 0= if drop 0 exit then
  hz-n mod cells hzs + @ ;
: arp-play ( -- )
  begin key? while key drop repeat   \ the LF after the CR of the line typed would stop it
  tune-init  ms-ticks to t0  0 to n-slice
  begin
    ms-ticks t0 - ms>ticks to tt
    tt tune-end <
  while
    key? if key drop 0 pin-tone exit then
    n-slice slice-hz pin-tone
    1 +to n-slice
    t0 n-slice slice-ms * + ms-ticks - 0 max ms
  repeat 0 pin-tone ;

\ ---- abc-play: a real mix, 4 square waves added and sent as PWM ----
\ A loop makes a sample (the sum of the waves, around 128) and writes it as the
\ duty of an 8 bit PWM at 78 kHz, which the speaker does not follow: it only
\ hears the mix. The loop has no clock but its own speed: mix-rate! counts how
\ many samples it makes in 50 ms and the pitches are made from that.
DEFINED? ledcAttach [IF]
: duty! ( n -- ) buzzer-pin swap ledcWrite ;
: pwm-mix buzzer-pin 78125 8 ledcChangeFrequency drop ;
: pwm-tone buzzer-pin 2000 10 ledcChangeFrequency drop ;
[ELSE]
: duty! ( n -- ) 0 swap ledcWrite ;
: pwm-mix 0 78125000 8 ledcSetup drop ;
: pwm-tone 0 2000000 10 ledcSetup drop ;
[THEN]
create ph 4 cells allot   create inc 4 cells allot   create amp 4 cells allot   create thr 4 cells allot
0 value rate   0 value cal-n   0 value cal-t   0 value cal-d
: slot-step ( sum i -- sum' )
  cells >r
  r@ ph + @  r@ inc + @ +  $ffffff and  dup r@ ph + !
  r@ thr + @ < if r@ amp + @ else r@ amp + @ negate then  +
  r> drop ;
: mix-sample ( -- ) 128 0 slot-step 1 slot-step 2 slot-step 3 slot-step duty! ;
: mix-block ( n -- ) 0 ?do mix-sample loop ;
: mix-rate! ( -- )   \ samples a second, the slots silent
  amp 4 cells erase   inc 4 cells erase   ph 4 cells erase   thr 4 cells erase
  0 to cal-n   ms-ticks to cal-t
  begin 64 mix-block 64 +to cal-n  ms-ticks cal-t - dup to cal-d 50 < 0= until
  cal-n 1000 * cal-d / to rate ;

\ A voice has a slot of its own (voice 1 slot 0...) so its wave goes on from one
\ note to the next, and a volume that does not change in the tune: vol-unit, the
\ part of the 120 steps of one weight, is made from the voices the tune has,
\ not from the notes sounding. The notes of a chord share the volume of their
\ voice and take the free slots above the voices. Voice 1, the melody, is a
\ square wave (half of the period high), the others a pulse of a quarter.
create slot-used 9 cells allot   create cnt 9 cells allot
0 value vol-unit   0 value sl   0 value sv
: vweight ( v -- w ) 1 = if melody-weight else 1 then ;
: tune-vol-unit ( -- )
  0  n-voices 4 min 1+ 1 ?do i vweight + loop  120 swap / to vol-unit ;
: free-slot ( -- s )   \ a slot above the voices that is free, or -1
  -1 to sl
  4 n-voices 4 min ?do sl 0< if i cells slot-used + @ 0= if i to sl then then loop  sl ;
: slot-for ( v -- s )
  dup 5 < over 1- cells slot-used + @ 0= and if 1- else drop free-slot then ;
: slots-set ( -- )   \ the 4 slots from the notes of gather
  cnt 9 cells erase   slot-used 9 cells erase   inc 4 cells erase   amp 4 cells erase
  hz-n 0 ?do i cells vws + @ cells cnt + 1 swap +! loop
  hz-n 0 ?do
    i cells vws + @ to sv
    sv slot-for to sl
    sl 0< 0= if
      1 sl cells slot-used + !
      i cells hzs + @ 16777216 rate */  sl cells inc + !
      sv vweight vol-unit *  sv cells cnt + @ /  sl cells amp + !
      sv 1 = if $800000 else $400000 then  sl cells thr + !
    then
  loop ;
0 value blk-n
: mix-end ( -- ) 0 duty! pwm-tone 0 pin-tone ;
: mix-play ( -- )
  begin key? while key drop repeat
  tune-init  tune-vol-unit  pwm-mix  mix-rate!
  ms-ticks to t0  0 to blk-n  0 to next-t
  begin
    ms-ticks t0 - ms>ticks to tt
    tt tune-end <
  while
    tt next-t < 0= if gather slots-set then
    128 mix-block
    1 +to blk-n
    blk-n 15 and 0= if  key? if key drop mix-end exit then  pause then
  repeat mix-end ;

\ The words to use, in forth; all the others stay in the hidden vocabulary sdl2
\ (internals sdl2 3 to melody-weight forth  changes the weight of voice 1).
forth-wordlist current !
: buzzer ( pin -- ) set-buzzer ;
: tone ( hz -- ) pin-tone ;
: tune-play ( -- ) mix-play ;
: abc-play ( a n -- ) abc mix-play ;
: abc-arp ( a n -- ) abc arp-play ;

previous previous previous previous
~ evaluate
  current ! ;
previous
