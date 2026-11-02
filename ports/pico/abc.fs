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

( Tunes in ABC notation on a speaker or a piezo, from a pin of the Pico )
\
\ Usage:
\   pico-abc                         \ once: loads the reader (common/abc.fs), about 12 KB
\   18 buzzer                        \ the speaker, buzzer or audio jack on GP18
\   r~ X:1
\   K:C
\   CDEF GABc ~ abc-play             \ the USB serial line stops it: any key
\ Up to 4 voices, V:1 V:2 ... and chords, are mixed by software: a loop adds 4 square
\ waves and writes the sum as the level of an 8 bit PWM (wrap 255, clock divider 1:
\ about 488 kHz at 125 MHz, which a speaker does not follow: it hears the mix).
\ The same code as ports/esp32/abc.fs with the hardware layer of the RP2040 (the
\ pwm_ words of the pico vocabulary). On a Cytron Maker Pi Pico the buzzer and the
\ audio jack are on GP18. Written without a board at hand: it compiles with the
\ pico-sdk, the Forth was not run (tested only in the ESP32 form of the simulator).

also internals definitions
vocabulary sdl2   ( where common/abc.fs puts abc, tune-notes and tune-ms )
variable abc-loaded
forth definitions also internals
: pico-abc ( -- )
  abc-loaded @ if exit then  -1 abc-loaded !
  current @  forth definitions
  also internals  abc-parser evaluate  previous
  r~
also internals also sdl2 also abc-int also pico

0 value buzzer-pin   0 value pwm-slice

( 125 MHz system clock. Tone: the counter at 1 MHz with a divider of 125, wrap = 1e6/hz - 1 )
: set-buzzer ( pin -- )
  dup to buzzer-pin
  dup GPIO_FUNC_PWM gpio_set_function
  pwm_gpio_to_slice_num to pwm-slice
  pwm-slice -1 pwm_set_enabled ;
: pin-tone ( hz -- )
  dup 0= if drop buzzer-pin 0 pwm_set_gpio_level exit then
  pwm-slice 125 0 pwm_set_clkdiv_int_frac
  1000000 over / 65535 min 1-  dup pwm-slice swap pwm_set_wrap
  nip 2/  buzzer-pin swap pwm_set_gpio_level ;

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
create hst 8 cells allot   create hen 8 cells allot   0 value cur-st   0 value cur-en   \ start and end of each note, in ticks
0 value tt   0 value t0   0 value tune-end   0 value next-t
: vp@ ( v -- i ) cells vp + @ ;   : vp! ( i v -- ) cells vp + ! ;
: ev-end ( i -- ticks ) dup nt@ swap nd@ + ;
: find-ev { i v -- i' }   \ the first event from i that is in voice v
  begin i n-notes < if i nv@ v <> else 0 then while i 1+ to i repeat i ;
: ev-over? ( i -- f ) dup n-notes < if ev-end tt <= else drop 0 then ;
: hz+ ( hz v -- )
  hz-n 8 < if
    hz-n cells vws + !  hz-n cells hzs + !
    cur-st hz-n cells hst + !  cur-en hz-n cells hen + !  1 +to hz-n
  else 2drop then ;
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
  va-i nt@ to cur-st   va-i ev-end to cur-en
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

\ ---- What stops a tune: a key on the serial line, or the word given to abc-halt-with ----
0 value halt-xt
: halt? ( -- f )
  halt-xt ?dup if execute exit then
  key? dup if key drop then ;

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
    halt? if 0 pin-tone exit then
    n-slice slice-hz pin-tone
    1 +to n-slice
    t0 n-slice slice-ms * + ms-ticks - 0 max ms
  repeat 0 pin-tone ;

\ ---- abc-play: a real mix, 4 square waves added and sent as PWM ----
\ A loop makes a sample (the sum of the waves, around 128) and writes it as the
\ duty of an 8 bit PWM at 78 kHz, which the speaker does not follow: it only
\ hears the mix. The loop has no clock but its own speed: mix-rate! counts how
\ many samples it makes in 50 ms and the pitches are made from that.
: duty! ( n -- ) buzzer-pin swap pwm_set_gpio_level ;
: pwm-mix ( -- ) pwm-slice 1 0 pwm_set_clkdiv_int_frac  pwm-slice 255 pwm_set_wrap ;
: pwm-tone ( -- ) pwm-slice 125 0 pwm_set_clkdiv_int_frac ;
create ph 4 cells allot   create inc 4 cells allot   create amp 4 cells allot   create thr 4 cells allot
0 value rate   0 value cal-n   0 value cal-t   0 value cal-d
-1 value c-mix                   \ the mixer in C (mixer.h), 31250 samples a second; 0: the Forth loop above
: slot-step ( sum i -- sum' )
  cells >r
  r@ ph + @  r@ inc + @ +  $ffffff and  dup r@ ph + !
  r@ thr + @ < if r@ amp + @ else r@ amp + @ negate then  +
  r> drop ;
: mix-sample ( -- ) 128 0 slot-step 1 slot-step 2 slot-step 3 slot-step duty! ;
: mix-block ( n -- ) 0 ?do mix-sample loop ;
: mix-rate! ( -- )   \ samples a second, the slots silent
  amp 4 cells erase   inc 4 cells erase   ph 4 cells erase   thr 4 cells erase
  c-mix if 31250 to rate  buzzer-pin mix_start exit then
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
create amp0 4 cells allot   create sst 4 cells allot   create sen 4 cells allot   \ volume, start, end of each slot
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
  cnt 9 cells erase   slot-used 9 cells erase   inc 4 cells erase   amp 4 cells erase   amp0 4 cells erase
  hz-n 0 ?do i cells vws + @ cells cnt + 1 swap +! loop
  hz-n 0 ?do
    i cells vws + @ to sv
    sv slot-for to sl
    sl 0< 0= if
      1 sl cells slot-used + !
      i cells hzs + @ 16777216 rate */  sl cells inc + !
      sv vweight vol-unit *  sv cells cnt + @ /  sl cells amp0 + !
      i cells hst + @ sl cells sst + !   i cells hen + @ sl cells sen + !
      sv 1 = if $800000 else $400000 then  sl cells thr + !
    then
  loop ;
\ A note fades while it sounds, as a plucked string does: its volume goes down by
\ env-depth/256 (about a third) from its start to its end, which keeps the
\ notes apart and softens the square waves. 0 gives the flat volume of before.
90 value env-depth
: env-amp ( i -- amp )   \ the volume of slot i at the time tt
  >r
  r@ cells sen + @  r@ cells sst + @ -  dup 1 < if drop 1 then
  tt r@ cells sst + @ -  0 max over min  env-depth *  swap /
  256 swap -  r@ cells amp0 + @ *  256 /  r> drop ;
: env-apply ( -- ) 4 0 do i env-amp  i cells amp + ! loop ;
: push-slots ( -- )
  4 0 do i  i cells inc + @  i cells amp + @  i cells thr + @  mix_slot loop ;
0 value blk-n
: mix-end ( -- ) mix_stop 0 duty! pwm-tone 0 pin-tone ;
: mix-play ( -- )
  begin key? while key drop repeat
  tune-init  tune-vol-unit  pwm-mix  mix-rate!
  ms-ticks to t0  0 to blk-n  0 to next-t
  begin
    ms-ticks t0 - ms>ticks to tt
    tt tune-end <
  while
    tt next-t < 0= if gather slots-set then
    env-apply
    c-mix if
      push-slots                       \ the interrupt of the timer makes the samples meanwhile
      1 +to blk-n
      blk-n 7 and 0= if  halt? if mix-end exit then then
      4 ms
    else
      128 mix-block
      1 +to blk-n
      blk-n 15 and 0= if  halt? if mix-end exit then  pause then
    then
  repeat mix-end ;

\ The words to use, in forth; all the others stay in the hidden vocabulary sdl2
\ (internals sdl2 3 to melody-weight forth  changes the weight of voice 1).
forth-wordlist current !
: buzzer ( pin -- ) set-buzzer ;
: tone ( hz -- ) pin-tone ;
: tune-play ( -- ) mix-play ;
: abc-play ( a n -- ) abc mix-play ;
: abc-arp ( a n -- ) abc arp-play ;
: abc-decay ( n -- ) to env-depth ;           \ 0 to 256: how much a note fades (90 by default, 0 none)
: abc-halt-with ( xt -- ) to halt-xt ;   \ xt ( -- f ) true stops the tune; 0 for the key

previous previous previous previous
~ evaluate
  current ! ;
previous
