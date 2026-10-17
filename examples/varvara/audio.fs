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

( Audio of Varvara: four sample channels, mixed in software )
\
\ Shared by varvara.fs (SDL2 queue) and varvara_rpi.fs (the board's PCM queue):
\ the front end sets AUDIO-DEV (nonzero when an output works) and gives
\ AUDIO-QUEUED and AUDIO-QUEUE by IS.

needs uxn.fs
also uxn definitions

\ Same algorithm as the C uxn: each channel steps through its sample at a pitch
\ (single cycle for samples up to 256 bytes, else a repeat at the note rate),
\ shaped by an ADSR envelope, left and right volume in one nibble each. The mix
\ of the four channels is queued ahead (about 60 ms) by `audio-pump` once a frame.

44100 constant rate
$4000 rate * 11025 / constant note-period
rate 15 / constant adsr-step
2646 constant lead   ( frames kept queued )

create advances
  $80000 , $879c8 , $8facd , $9837f , $a1451 , $aadc1 ,
  $b504f , $bfc88 , $cb2ff , $d7450 , $e411f , $f1a1c ,

: fld ( n "name" -- ) create cells , does> @ + ;   ( a field of a channel, in cells )
 0 fld a-adv    1 fld a-per    2 fld a-cnt    3 fld a-i      4 fld a-len
 5 fld a-addr   6 fld a-age    7 fld a-a      8 fld a-d      9 fld a-s
10 fld a-r     11 fld a-vl    12 fld a-vr    13 fld a-rep   14 fld a-done
create achan 4 16 cells * allot
: ch ( n -- a ) 16 cells * achan + ;

0 value audio-dev   ( an output works )
create amix 2048 2* cells allot     ( 2048 frames, left and right )
create aout 2048 4 * allot          ( the same as signed 16 bits )
defer audio-queued   ( -- n )   ( frames waiting to be played )
defer audio-queue    ( n -- )   ( plays the n frames of aout )

: envelope ( c age -- e )
  { c age }
  c a-r @ 0= if $888 exit then
  age c a-a @ < if $888 age * c a-a @ / exit then
  age c a-d @ < if $444 c a-d @ 2* c a-a @ - age - * c a-d @ c a-a @ - / exit then
  age c a-s @ < if $444 exit then
  age c a-r @ < if $444 c a-r @ age - * c a-r @ c a-s @ - / exit then
  0 c a-adv ! 0 ;

: render-chan ( c n -- )   ( adds n frames of a channel to amix )
  { c n }
  c a-adv @ 0= c a-per @ 0= or if exit then
  n 0 ?do
    c a-adv @ 0= if leave then
    c a-adv @ c a-cnt +!
    c a-cnt @ c a-per @ / c a-i +!
    c a-cnt @ c a-per @ mod c a-cnt !
    c a-i @ c a-len @ >= if
      c a-rep @ if c a-i @ c a-len @ mod c a-i ! else 0 c a-adv ! leave then
    then
    c a-addr @ c a-i @ + ram@ $80 -
    c a-age @ dup 1+ c a-age !  c swap envelope *
    dup c a-vl @ * $180 / amix i 2* cells + +!
        c a-vr @ * $180 / amix i 2* 1+ cells + +!
  loop
  c a-adv @ 0= if 1 c a-done ! then ;

: audio-start ( n -- )   ( the pitch port of channel n was written )
  { n }
  n $10 * $30 + { base }
  n ch { c }
  base $f + dev@ $7f and { pitch }
  base 8 + dev2@ { adsr }
  base $a + dev2@ c a-len !
  base $c + dev2@ c a-addr !
  base $e + dev@ dup 4 rshift c a-vl !  $f and c a-vr !
  base $f + dev@ $80 and 0= c a-rep !
  pitch 108 < c a-len @ 0<> and 0= if 0 c a-adv ! exit then
  advances pitch 12 mod cells + @  8 pitch 12 / -  rshift c a-adv !
  adsr-step adsr 12 rshift * c a-a !
  adsr-step adsr 8 rshift $f and * c a-a @ + c a-d !
  adsr-step adsr 4 rshift $f and * c a-d @ + c a-s !
  adsr-step adsr $f and * c a-s @ + c a-r !
  0 c a-age !  0 c a-i !
  c a-len @ $100 <= if note-period 337 * 2/ c a-len @ / else note-period then c a-per ! ;

: audio-deo ( p -- )
  dup $f and $f = if 4 rshift 3 - audio-start else drop then ;

: vu ( c i -- n )
  { c i }  c a-adv @ 0= c a-per @ 0= or if 0 exit then
  i if c a-vr @ else c a-vl @ then 0= if 0 exit then
  c c a-age @ envelope  i if c a-vr @ else c a-vl @ then * $800 / 1+ $f min ;

: audio-dei ( p -- b )
  { p }
  p $f and { port }  p $f0 and { base }  p 4 rshift 3 - ch { c }
  audio-dev 0= if base port + dev@ exit then
  port 4 = if c 0 vu 4 lshift c 1 vu or exit then
  port 2 = if c a-i @ dup 8 rshift base 2 + dev! base 3 + dev! then
  base port + dev@ ;

: audio-active? ( -- f ) 0 4 0 do i ch a-adv @ 0<> or loop ;

: audio-pump ( -- )
  audio-dev 0= if exit then
  audio-active? 0= if exit then
  lead audio-queued - 2048 min { n }
  n 1 < if exit then
  amix n 2* cells erase
  4 0 do i ch n render-chan loop
  n 2* 0 do amix i cells + @ 32767 min -32768 max aout i 2* + w! loop
  n audio-queue
  4 0 do
    i ch a-done @ if
      0 i ch a-done !
      i $10 * $30 + dev2@ ?dup if eval then
    then
  loop ;


previous definitions
