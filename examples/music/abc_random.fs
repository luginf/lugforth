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

( A random tune in ABC notation: 12 bars of chords and arpeggios, played in a loop )
\
\ Voice 1 is the melody, an arpeggio of the chord of each bar (eighth and quarter
\ notes going up and down the notes of the chord). Voice 2 accompanies it with
\ chords, a bass line or a counterpoint (notes of the chord moving against the
\ melody). The chords come from a few usual progressions (I V vi IV...), in a major
\ or a natural minor key, 3 phrases of 4 bars, the last one ending on V then I.
\ The tune is only made of notes of the key, so it needs no accidentals.
\
\ On a PC with the SDL2 mixer:      sdl2-abc  include examples/music/abc_random.fs
\                                   random-music        \ any key: another tune
\ On a Pico (GP18 buzzer, GP20 button to ground):
\                                   pico-abc  18 buzzer  include abc_random.fs
\                                   20 music-button  random-music
\ On an ESP32:                      esp32-abc  4 buzzer  include /spiffs/abc_random.fs
\                                   5 music-button  random-music
\ The tune plays again and again; the button (or, without one, a key) makes another
\ tune; on a board a key typed on the serial line ends random-music.
\ The number shown ("tune 495408161") is the seed of the tune:
\   495408161 music-seed                 plays that tune again
\   495408161 rand-seed !  new-tune type   shows it as ABC text (to copy and keep)
\   495408161 rand-seed !  save-tune t1.abc   writes it in a file (cat t1.abc shows it)
\   495408161 rand-seed !  new-tune abc-play  plays it once
\ Do not set the seed and then call random-music: it mixes the clock into the seed.
\ new-tune ( -- a n ) is the ABC text of a tune; its title T: shows the seed.

\ On a PC or in the browser the player is loaded when there is none yet
defined? sdl2-abc defined? abc-play 0= and [if] sdl2-abc [then]

\ ---- The text goes in two buffers, one by voice ----
create rt-v1 1600 allot   0 value rt-n1
create rt-v2 1000 allot   0 value rt-n2
0 value rt-w                               \ 0: voice 1, 1: voice 2
: t+c ( c -- )
  rt-w if
    rt-n2 990 < if rt-v2 rt-n2 + c! 1 +to rt-n2 else drop then
  else
    rt-n1 1590 < if rt-v1 rt-n1 + c! 1 +to rt-n1 else drop then
  then ;
: t+s ( a n -- ) 0 ?do dup i + c@ t+c loop drop ;
: t+n ( n -- ) s>d <# #s #> t+s ;
: t+len ( n -- ) dup 1 > if t+n else drop then ;

\ ---- A note is a step of the diatonic scale of C: 0 is C, 7 is c, -7 is C, ----
create rt-letters 67 c, 68 c, 69 c, 70 c, 71 c, 65 c, 66 c,     \ C D E F G A B
0 value rt-l   0 value rt-o
: t+note ( step len -- )
  >r  70 + dup 7 mod to rt-l  7 / 10 - to rt-o
  rt-letters rt-l + c@  rt-o 0> if 32 or then  t+c
  rt-o 0> if rt-o 1 - 0 ?do 39 t+c loop then
  rt-o 0< if rt-o negate 0 ?do 44 t+c loop then
  r> t+len ;
: t+rest ( len -- ) 122 t+c t+len ;
: t+step ( step -- ) 1 t+note ;                 \ inside a chord
: t+chord ( root len -- )                      \ the triad of the root: root, third, fifth
  >r  91 t+c  dup t+step  dup 2 + t+step  4 + t+step  93 t+c  r> t+len ;

\ ---- Dice ----
: rt-chance ( n -- f ) random 0= ;                \ one time in n

\ ---- The key and the chords ----
10 constant n-keys
create rt-key-letter 0 c, 4 c, 1 c, 3 c, 6 c, 5 c,  5 c, 2 c, 1 c, 4 c,   \ letter of the tonic
: t+key ( i -- )
  dup 0 = if drop s" C" t+s exit then    dup 1 = if drop s" G" t+s exit then
  dup 2 = if drop s" D" t+s exit then    dup 3 = if drop s" F" t+s exit then
  dup 4 = if drop s" Bb" t+s exit then   dup 5 = if drop s" A" t+s exit then
  dup 6 = if drop s" Am" t+s exit then   dup 7 = if drop s" Em" t+s exit then
  8 = if s" Dm" t+s else s" Gm" t+s then ;
\ Roots of the chords (degree 0 to 6) of four bars, five progressions
create rt-maj 0 c, 4 c, 5 c, 3 c,   0 c, 5 c, 3 c, 4 c,   0 c, 3 c, 4 c, 0 c,
              0 c, 2 c, 3 c, 4 c,   5 c, 3 c, 0 c, 4 c,
create rt-min 0 c, 6 c, 5 c, 6 c,   0 c, 5 c, 2 c, 6 c,   0 c, 3 c, 4 c, 0 c,
              0 c, 5 c, 3 c, 6 c,   5 c, 3 c, 0 c, 4 c,
0 value rt-t   0 value rt-minor   0 value rt-p1   0 value rt-p2   0 value rt-mode
: rt-degree ( prog bar -- d ) swap 4 * +  rt-minor if rt-min else rt-maj then + c@ ;
: mel-root ( d -- step )                       \ the root in the octave of the melody
  rt-t + begin dup 9 > while 7 - repeat begin dup 3 < while 7 + repeat ;
: bass-root ( d -- step )                      \ and in the octave below
  rt-t + begin dup 0> while 7 - repeat begin dup -6 < while 7 + repeat ;

\ ---- Voice 1: an arpeggio ----
create rt-ct 4 cells allot                     \ the notes of the chord: root, third, fifth, octave
create rt-mb 4 cells allot                     \ the first note of each beat
0 value rt-ai   1 value rt-ad
: rt-ct@ ( i -- step ) cells rt-ct + @ ;
: rt-next ( -- )                               \ the next note of the chord, up or down
  6 rt-chance if rt-ad negate to rt-ad then
  rt-ad +to rt-ai
  8 rt-chance if rt-ad +to rt-ai then
  rt-ai 3 > if 2 to rt-ai -1 to rt-ad then
  rt-ai 0< if 1 to rt-ai 1 to rt-ad then ;
: mel-beat ( beat -- )
  rt-ai rt-ct@ over cells rt-mb + !
  10 random 3 < if
    drop rt-ai rt-ct@ 2 t+note rt-next
  else
    drop rt-ai rt-ct@ 1 t+note rt-next
    14 rt-chance if 1 t+rest else
      rt-ai rt-ct@  7 rt-chance if 1- then  1 t+note
    then
    rt-next
  then
  32 t+c ;
: mel-bar ( root -- )
  dup rt-ct !  dup 2 + rt-ct cell+ !  dup 4 + rt-ct 2 cells + !  7 + rt-ct 3 cells + !
  4 rt-chance if 2 else 0 then to rt-ai   1 to rt-ad
  4 0 do i mel-beat loop ;

\ ---- Voice 2: chords or a counterpoint ----
create rt-tn 4 cells allot
0 value rt-prev   0 value cp-bd   0 value cp-bs   0 value cp-t
: rt-sgn ( n -- -1|0|1 ) dup 0> if drop 1 else 0< if -1 else 0 then then ;
: cp-pick ( want -- step )                     \ the tone of the chord nearest to want
  99 to cp-bd
  4 0 do
    i cells rt-tn + @ to cp-t
    cp-t over - abs  cp-t rt-prev = if 2 + then
    dup cp-bd < if to cp-bd cp-t to cp-bs else drop then
  loop drop cp-bs ;
: cp-bar ( root -- )                           \ 4 quarters against the melody
  dup 3 - rt-tn !  dup rt-tn cell+ !  dup 2 + rt-tn 2 cells + !  4 + rt-tn 3 cells + !
  rt-tn cell+ @ to rt-prev
  rt-prev 2 t+note 32 t+c
  4 1 do
    i cells rt-mb + @  i 1- cells rt-mb + @ -  rt-sgn
    dup 0= if drop 3 random 1 - then negate 2 *  rt-prev +  cp-pick
    dup to rt-prev 2 t+note 32 t+c
  loop ;
: acc-bar ( root style -- )
  dup 0 = if drop 8 t+chord exit then
  dup 1 = if drop dup 4 t+chord 32 t+c 4 t+chord exit then
  2 = if
    dup 2 t+note 32 t+c  dup 4 + 2 t+note 32 t+c  dup 2 + 2 t+note 32 t+c  4 + 2 t+note
  else
    cp-bar
  then ;
: acc-style ( -- style )
  rt-mode 0 = if 2 random exit then
  rt-mode 1 = if 2 random 2 + exit then
  4 random ;

\ ---- The tune: 12 bars, three phrases of four ----
12 constant n-bars
: bar-end ( bar -- ) 1+ 4 mod 0= if 32 t+c 124 t+c 10 t+c else 32 t+c 124 t+c 32 t+c then ;
0 value rt-bi
: rt-bar ( bar -- )
  to rt-bi
  rt-bi 4 /  1 = if rt-p2 else rt-p1 then  rt-bi 4 mod rt-degree     ( d )
  rt-bi 10 = if drop 4 then                    \ the cadence: V then I
  rt-bi 11 = if drop 0 then
  0 to rt-w
  rt-bi 11 = if dup mel-root 8 t+note else dup mel-root mel-bar then
  rt-bi 11 = if 124 t+c 93 t+c 10 t+c else rt-bi bar-end then
  1 to rt-w
  rt-bi 11 = if bass-root 8 t+chord 124 t+c 93 t+c 10 t+c
  else bass-root acc-style acc-bar rt-bi bar-end then ;
variable rt-seed
: new-tune ( -- a n )
  rand-seed @ $7fffffff and dup rand-seed ! rt-seed !
  0 to rt-w  0 to rt-n1  0 to rt-n2
  n-keys random >r
  r@ rt-key-letter + c@ to rt-t   r@ 5 > to rt-minor
  5 random to rt-p1   5 random to rt-p2   3 random to rt-mode
  s" X:1" t+s 10 t+c  s" T:Random " t+s rt-seed @ t+n 10 t+c
  s" M:4/4" t+s 10 t+c  s" L:1/8" t+s 10 t+c
  s" Q:1/4=" t+s 80 53 random + t+n 10 t+c
  s" K:" t+s r> t+key 10 t+c
  s" V:1" t+s 10 t+c
  1 to rt-w  s" V:2" t+s 10 t+c  0 to rt-w
  n-bars 0 ?do i rt-bar loop
  0 to rt-w  rt-v2 rt-n2 t+s
  rt-v1 rt-n1 ;
defined? w/o [if]                          \ where there are files (not in the browser)
: save-tune ( "name" -- )                    \ the tune of rand-seed, as ABC text in a file
  bl parse w/o create-file throw >r
  new-tune r@ write-file throw  r> close-file throw ;
[then]

\ ---- Playing it in a loop ----
0 value rm-new   0 value rm-quit   -1 value rm-pin
-1 value rm-key-quits                       \ a key ends random-music (on a PC it asks for another tune)
: music-button ( pin -- ) drop ;
: rm-pressed? ( -- f ) 0 ;
defined? pico-abc [if]
  also pico
  : music-button ( pin -- ) dup to rm-pin  dup gpio_init  dup GPIO_IN gpio_set_dir  gpio_pull_up ;
  : rm-pressed? ( -- f ) rm-pin 0< if 0 else rm-pin gpio_get 0= then ;
  previous
[then]
defined? esp32-abc [if]
  : music-button ( pin -- ) dup to rm-pin  5 pinMode ;       \ 5: INPUT_PULLUP
  : rm-pressed? ( -- f ) rm-pin 0< if 0 else rm-pin digitalRead 0= then ;
[then]
: rm-halt? ( -- f )
  rm-pressed? if
    -1 to rm-new  begin rm-pressed? while 20 ms repeat  -1 exit
  then
  key? if
    key drop  rm-key-quits if -1 to rm-quit else -1 to rm-new then  -1 exit
  then
  0 ;
defined? abc-halt-with [if]
  : rm-hook ( -- ) ['] rm-halt? abc-halt-with ;
[then]
defined? abc-halt-with 0= [if]
  : rm-hook ( -- ) ;
[then]
defined? usleep [if]
  : rm-wait ( -- ) 50000 usleep ;
[then]
defined? usleep 0= [if]
  : rm-wait ( -- ) 50 ms ;
[then]
defined? tune-playing? [if]                 \ a PC or the browser: the music plays by itself, in a loop, until a key
  0 to rm-key-quits
  : rm-play ( a n -- ) abc-loop  begin rm-halt? 0= while rm-wait repeat tune-stop ;   \ the player loops the tune itself
[then]
defined? tune-playing? 0= defined? abc-play and [if]   \ a board: abc-play returns at the end or when halted
  : rm-play ( a n -- ) abc-play ;
[then]

defined? rm-play [if]               \ a player is loaded (abc-play): else only new-tune
: music-loop ( -- )                         \ plays the tune of rand-seed, then others
  0 to rm-quit  rm-hook
  begin
    0 to rm-new  new-tune  ." tune " rt-seed @ . cr
    begin 2dup rm-play  rm-new rm-quit or until
    2drop
    rm-quit
  until ;
: random-music ( -- ) ms-ticks rand-seed +!  music-loop ;      \ another tune each time
: music-seed ( n -- ) rand-seed !  music-loop ;                \ the tune of the number shown
[then]
