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

( A 2 operator FM synthesizer with MIDI input, for the POSIX build )
\
\ Not part of the interpreter: include it (examples/fm/sdl2_fm.fs does), then type sdl2-fm,
\ which loads the words like the other sdl2 extensions. Usage:
\   include engine_posix.fs
\   sdl2-fm
\   200 to fm-ratio   30 to fm-index        \ modulator:carrier ratio x100, index x10
\   60 100 fm-on   500 ms   60 fm-off       \ note 60 (middle C), velocity 100
\   midi-open drop   begin midi-poll flip quit? until   \ play from a MIDI keyboard
\ One modulator oscillator bends the phase of one carrier (the DX7 way), each with its
\ own envelope, 8 voices. The samples are computed in Forth, 32 at a time, and mixed
\ with the hits of drum-hit into the same queue by pump (see sdl2.fs), so call flip or
\ delay regularly. The same words exist in engine_web.fs, played by WebAudio.
\ MIDI comes from the ALSA sequencer (libasound), so a USB keyboard works once it is
\ connected to the port of lugforth (midi-connect-all, or aconnect on the command line).
\ NOTE: the block below is a string, so it must not contain a vertical bar.

[undefined] fm-midi-parser [if] include midi_parser.fs [then]

also internals
\ sdl2 is looked up when this runs, by evaluate: written directly it would be the
\ loader that existed when this was compiled, which would load everything again.
: sdl2-fm
  s" sdl2" evaluate
  r|
sdl2 definitions also posix

( ---- The patch: read when a note starts, except fm-release, read when it ends ---- )
200 value fm-ratio     ( modulator frequency / carrier frequency, times 100 )
30 value fm-index      ( modulation index, times 10: 30 is 3.0 radians )
5 value fm-attack      ( milliseconds )
400 value fm-decay     ( milliseconds to settle at the sustain level )
30 value fm-sustain    ( percent of the peak level )
250 value fm-release   ( milliseconds )
300 value fm-mdecay    ( milliseconds for the modulation to fall to fm-msus )
20 value fm-msus       ( percent of the index kept while the note is held )
70 value fm-volume     ( percent )

( ---- Tables ---- )
4096 constant fv-size
create fv-sine fv-size cells allot
: fv-make-sine ( -- )
  fv-size 0 ?do
    i s>f fv-size s>f f/ pi f* 2e f* fsin 32767e f* f>s fv-sine i cells + !
  loop ;
fv-make-sine
( Phase increment per sample of each MIDI key, in 1/16777216 of a cycle )
create fv-incs 128 cells allot
: fv-make-incs ( -- )
  128 0 ?do
    i 69 - s>f 12e f/ 0.6931472e f* fexp 440e f* 16777216e f* 44100e f/ f>s
    fv-incs i cells + !
  loop ;
fv-make-incs
: fv-k ( ms -- k )   ( share of the way to the target made per block, 16 bits )
  dup 1 < if drop 65536 exit then
  s>f 44100e f* 128000e fswap f/ fnegate fexp 1e fswap f- 65536e f* f>s ;

( ---- Voices. Fields, in cells ---- )
8 constant #fm
32 constant fv-block   ( samples between two envelope updates )
17 cells constant fv-bytes
create fv-all #fm fv-bytes * allot   fv-all #fm fv-bytes * erase
: fvoice ( i -- v ) fv-bytes * fv-all + ;
: fv-stage ( v -- a ) ;                 \ 0 free, 1 attack, 2 decay and sustain, 3 release
: fv-note ( v -- a ) 1 cells + ;
: fv-phc ( v -- a ) 2 cells + ;
: fv-phm ( v -- a ) 3 cells + ;
: fv-inc ( v -- a ) 4 cells + ;         \ carrier phase increment
: fv-ratio ( v -- a ) 5 cells + ;
: fv-ea ( v -- a ) 6 cells + ;          \ carrier envelope, 0 to 32767
: fv-em ( v -- a ) 7 cells + ;          \ modulator envelope
: fv-amp ( v -- a ) 8 cells + ;         \ peak level of the voice, 0 to 32767
: fv-idx ( v -- a ) 9 cells + ;         \ peak modulation index in 1/16777216 of a cycle
: fv-astep ( v -- a ) 10 cells + ;
: fv-kd ( v -- a ) 11 cells + ;
: fv-sus ( v -- a ) 12 cells + ;
: fv-kr ( v -- a ) 13 cells + ;
: fv-kmd ( v -- a ) 14 cells + ;
: fv-msus ( v -- a ) 15 cells + ;
: fv-age ( v -- a ) 16 cells + ;
0 value cv   ( the voice being worked on )
65536 value fv-bendmul   ( pitch bend as a factor, times 65536 )
0 value fv-clock

( ---- Envelopes, once per block ---- )
: fv-approach ( target k addr -- )   ( moves the value k/65536 of the way to target )
  dup >r @ rot over - rot * 16 arshift + r> ! ;
: fv-mod ( -- ) cv fv-msus @ cv fv-kmd @ cv fv-em fv-approach ;
: fv-attack ( -- )
  cv fv-ea @  cv fv-astep @ +
  dup 32767 < if cv fv-ea ! exit then
  drop 32767 cv fv-ea !   2 cv fv-stage ! ;
: fv-env ( -- )
  cv fv-stage @
  dup 1 = if drop fv-attack fv-mod exit then
  dup 2 = if drop cv fv-sus @ cv fv-kd @ cv fv-ea fv-approach fv-mod exit then
  3 = if
    0 cv fv-kr @ cv fv-ea fv-approach
    0 cv fv-kr @ cv fv-em fv-approach
    cv fv-ea @ 8 < if 0 cv fv-ea !  0 cv fv-stage ! then
  then ;

( ---- Oscillators: the modulator bends the phase of the carrier ---- )
: fv-osc { v at cnt }
  v fv-phc @ { phc }   v fv-phm @ { phm }
  v fv-inc @ fv-bendmul * 16 rshift { ic }
  ic v fv-ratio @ * 100 / { im }
  v fv-idx @ v fv-em @ * 15 rshift { ie }
  v fv-amp @ v fv-ea @ * 15 rshift { g }
  mix at cells + { out }
  cnt 0 ?do
    phc  phm 12 rshift $fff and cells fv-sine + @ ie * 15 arshift +
    12 rshift $fff and cells fv-sine + @ g * 15 arshift
    out i cells + +!
    ic +to phc   im +to phm
  loop
  phc $ffffff and v fv-phc !   phm $ffffff and v fv-phm ! ;

: fv-run { v n }   ( adds n samples of a voice to mix )
  0 { at }   0 { cnt }
  v to cv
  begin at n < while
    n at - fv-block min to cnt
    fv-env
    v at cnt fv-osc
    cnt +to at
    cv fv-stage @ 0= if n to at then
  repeat ;
: fv-one ( n v -- )
  dup fv-stage @ 0= if 2drop exit then
  swap fv-run ;
: fm-render ( n -- )   ( the render-hook of sdl2.fs )
  #fm 0 ?do dup i fvoice fv-one loop drop ;
: fm-active? ( -- f ) 0  #fm 0 ?do i fvoice fv-stage @ 0<> or loop ;
' fm-render to render-hook
' fm-active? to active-hook

( ---- Playing notes ---- )
: fv-score ( note v -- n )   ( the lower, the better a voice to take )
  dup fv-stage @ 0= if 2drop -1 exit then
  dup fv-stage @ 3 = if nip fv-ea @ exit then
  dup fv-note @ rot = if drop -2 exit then
  fv-age @ 40000 + ;
: fv-pick ( note -- v )
  0 fvoice { best }   0 { top }   { note }
  note best fv-score to top
  #fm 1 ?do
    note i fvoice fv-score  dup top < if to top  i fvoice to best else drop then
  loop
  best ;
: fm-bend ( -8192..8191 -- )   ( pitch bend, two semitones either way )
  s>f 49152e f/ 0.6931472e f* fexp 65536e f* f>s to fv-bendmul ;
: fm-on ( note vel -- )
  audio-open
  audio-open? 0= if 2drop exit then
  { vel }  $7f and { note }
  vel 1 max 127 min to vel
  note fv-pick to cv
  1 +to fv-clock
  fm-attack 1 < if 2 else 1 then  cv fv-stage !
  note cv fv-note !   0 cv fv-phc !   0 cv fv-phm !
  note cells fv-incs + @ cv fv-inc !   fm-ratio cv fv-ratio !
  fm-attack 1 < if 32767 else 0 then cv fv-ea !   32767 cv fv-em !
  32767 fm-volume * vel * 38100 / cv fv-amp !
  fm-index 267018 * vel 127 + * 254 / cv fv-idx !
  fm-attack 1 < if 32767 else 23778 fm-attack / 1 max then cv fv-astep !
  fm-decay fv-k cv fv-kd !   fm-sustain 32767 * 100 / cv fv-sus !
  fm-mdecay fv-k cv fv-kmd !   fm-msus 32767 * 100 / cv fv-msus !
  fv-clock cv fv-age !
  pump ;
: fm-off ( note -- )
  { note }
  #fm 0 ?do
    i fvoice to cv
    cv fv-stage @ 1 3 within  cv fv-note @ note =  and if
      3 cv fv-stage !   fm-release fv-k cv fv-kr !
    then
  loop ;
: fm-all-off ( -- )
  #fm 0 ?do
    i fvoice to cv
    cv fv-stage @ if 3 cv fv-stage !   100 fv-k cv fv-kr ! then
  loop ;
: fm-reset ( -- ) fv-all #fm fv-bytes * erase   0 fm-bend ;
: fm-active ( -- n ) 0 #fm 0 ?do i fvoice fv-stage @ 0<> if 1+ then loop ;

| evaluate
  fm-midi-parser evaluate
  r|
( ---- MIDI input from the ALSA sequencer ---- )
z" libasound.so.2" shared-library asound
z" snd_seq_open" 4 asound snd_seq_open ( a z n n -- n )
z" snd_seq_close" 1 asound snd_seq_close ( a -- n )
z" snd_seq_set_client_name" 2 asound snd_seq_set_client_name ( a z -- n )
z" snd_seq_client_id" 1 asound snd_seq_client_id ( a -- n )
z" snd_seq_create_simple_port" 4 asound snd_seq_create_simple_port ( a z n n -- n )
z" snd_seq_event_input" 2 asound snd_seq_event_input ( a a -- n )
z" snd_seq_connect_from" 4 asound snd_seq_connect_from ( a n n n -- n )
z" snd_seq_client_info_malloc" 1 asound snd_seq_client_info_malloc ( a -- n )
z" snd_seq_client_info_free" 1 asound snd_seq_client_info_free ( a -- void )
z" snd_seq_client_info_set_client" 2 asound snd_seq_client_info_set_client ( a n -- void )
z" snd_seq_client_info_get_client" 1 asound snd_seq_client_info_get_client ( a -- n )
z" snd_seq_query_next_client" 2 asound snd_seq_query_next_client ( a a -- n )
z" snd_seq_port_info_malloc" 1 asound snd_seq_port_info_malloc ( a -- n )
z" snd_seq_port_info_free" 1 asound snd_seq_port_info_free ( a -- void )
z" snd_seq_port_info_set_client" 2 asound snd_seq_port_info_set_client ( a n -- void )
z" snd_seq_port_info_set_port" 2 asound snd_seq_port_info_set_port ( a n -- void )
z" snd_seq_port_info_get_port" 1 asound snd_seq_port_info_get_port ( a -- n )
z" snd_seq_port_info_get_capability" 1 asound snd_seq_port_info_get_capability ( a -- n )
z" snd_seq_query_next_port" 2 asound snd_seq_query_next_port ( a a -- n )

create seqp 0 ,   create evp 0 ,   create infop 0 ,
0 value midi-port
-1 value raw-fd   create raw-name 256 allot   create raw-buf 64 allot
: midi-open? ( -- f ) seqp @ 0<> ;
: midi-open ( -- f )   ( opens the sequencer and a port to connect keyboards to )
  midi-open? if -1 exit then
  seqp z" default" 2 1 snd_seq_open sign-extend 0< if 0 seqp !  0 exit then
  seqp @ z" lugforth" snd_seq_set_client_name drop
  seqp @ z" FM synth" 66 1048578 snd_seq_create_simple_port sign-extend
  dup 0< if drop seqp @ snd_seq_close drop 0 seqp !  0 exit then
  to midi-port   midi-reset   -1 ;
: midi-raw ( a n -- f )   ( reads raw MIDI bytes from a device or a fifo: /dev/snd/midiC1D0 )
  255 min { n }  raw-name n move   0 raw-name n + c!
  raw-name O_RDONLY O_NONBLOCK or 0 open sign-extend
  dup 0< if drop 0 exit then
  to raw-fd   midi-reset   -1 ;
: midi-close ( -- )
  midi-open? if seqp @ snd_seq_close drop  0 seqp ! then
  raw-fd 0< 0= if raw-fd close drop  -1 to raw-fd then ;
: midi-client ( -- n )   ( the client number to connect to: aconnect 20:0 client:0 )
  midi-open? if seqp @ snd_seq_client_id sign-extend else -1 then ;

: midi-channel ( -- n ) evp @ 16 + c@ $0f and ;
: midi-3 ( status d1 d2 -- ) rot midi-feed swap midi-feed midi-feed ;
: midi-event ( -- )   ( turns the event of evp into MIDI bytes )
  evp @ c@
  dup 6 = if drop $90 midi-channel or  evp @ 17 + c@  evp @ 18 + c@  midi-3 exit then
  dup 7 = if drop $80 midi-channel or  evp @ 17 + c@  evp @ 18 + c@  midi-3 exit then
  dup 10 = if drop $b0 midi-channel or  evp @ 20 + sl@ $7f and  evp @ 24 + sl@ $7f and  midi-3 exit then
  13 = if
    evp @ 24 + sl@ 8192 +  dup $7f and  swap 7 rshift $7f and
    $e0 midi-channel or  -rot midi-3
  then ;
: raw-poll ( -- )
  raw-fd 0< if exit then
  4 0 ?do
    raw-fd raw-buf 64 read sign-extend  dup 1 < if drop unloop exit then
    0 ?do raw-buf i + c@ midi-feed loop
  loop ;
: midi-poll ( -- )   ( plays the MIDI messages that arrived; call it once per frame )
  raw-poll
  midi-open? 0= if exit then
  64 0 ?do
    seqp @ evp snd_seq_event_input sign-extend 0< if unloop exit then
    midi-event
  loop ;
: midi-in? ( -- f ) midi-open? raw-fd 0< 0= or ;

: midi-connect-all ( -- n )   ( connects every keyboard and other source: how many ports )
  midi-open? 0= if 0 exit then
  0 { n }   0 { ci }   0 { pi }   0 { cl }   0 { caps }
  infop snd_seq_client_info_malloc drop   infop @ to ci
  infop snd_seq_port_info_malloc drop   infop @ to pi
  ci -1 snd_seq_client_info_set_client drop
  begin seqp @ ci snd_seq_query_next_client sign-extend 0< 0= while
    ci snd_seq_client_info_get_client sign-extend to cl
    cl 0 > cl midi-client <> and if
      pi cl snd_seq_port_info_set_client drop
      pi -1 snd_seq_port_info_set_port drop
      begin seqp @ pi snd_seq_query_next_port sign-extend 0< 0= while
        pi snd_seq_port_info_get_capability sign-extend to caps
        caps 33 and 33 = caps 128 and 0= and if
          seqp @ midi-port cl pi snd_seq_port_info_get_port sign-extend
          snd_seq_connect_from sign-extend 0< 0= if 1 +to n then
        then
      repeat
    then
  repeat
  ci snd_seq_client_info_free drop   pi snd_seq_port_info_free drop
  n ;

previous
  | evaluate ;
previous
