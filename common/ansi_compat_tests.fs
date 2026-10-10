\ Copyright 2026 luginf
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

( Tests for ansi_compat.fs )

: max-u   -1 ;
: max-n   -1 1 rshift ;
: min-n   max-n invert ;

: test-true-false   true -1 =assert false 0 =assert true not 0 =assert ;
: test-0>   1 0> assert 0 0> 0 =assert -1 0> 0 =assert ;
: test-u>   -1 1 u> assert 1 -1 u> 0 =assert 2 2 u> 0 =assert ;
: test-within
  5 3 8 within assert   3 3 8 within assert   8 3 8 within 0 =assert
  2 3 8 within 0 =assert   -1 -3 2 within assert   3 -3 2 within 0 =assert ;
: test-2+2-   5 2+ 7 =assert 5 2- 3 =assert ;
: test-chars   7 chars 7 =assert 100 char+ 101 =assert ;
: test-tuck   1 2 tuck 2 =assert 1 =assert 2 =assert depth 0 =assert ;
: test-2swap   1 2 3 4 2swap 2 =assert 1 =assert 4 =assert 3 =assert depth 0 =assert ;
: test-2over   1 2 3 4 2over 2 =assert 1 =assert 4 =assert 3 =assert
               2 =assert 1 =assert depth 0 =assert ;
: test-pick   10 20 30 0 pick 30 =assert 30 =assert 20 =assert 10 =assert
              10 20 30 2 pick 10 =assert 3 0 do drop loop depth 0 =assert ;
: test-roll   10 20 30 2 roll 10 =assert 30 =assert 20 =assert depth 0 =assert
              10 20 30 0 roll 30 =assert 2drop
              10 20 30 1 roll 20 =assert 30 =assert 10 =assert depth 0 =assert ;
: test-2>r   1 2 2>r 3 2r> 2 =assert 1 =assert 3 =assert ;
: t-2r@ 5 6 2>r 2r@ + 2r> + ;
: test-2r@   t-2r@ 11 =assert 11 =assert depth 0 =assert ;

( Doubles )
: test-s>d   5 s>d 0 =assert 5 =assert   -5 s>d -1 =assert -5 =assert ;
: test-d>s   123 0 d>s 123 =assert ;
: test-d+    1 0 2 0 d+ 0 =assert 3 =assert
             max-u 0 1 0 d+ 1 =assert 0 =assert
             max-u max-u 1 0 d+ 0 =assert 0 =assert ;
: test-d-    5 0 3 0 d- 0 =assert 2 =assert
             3 0 5 0 d- -1 =assert -2 =assert ;
: test-dnegate   1 0 dnegate -1 =assert -1 =assert
                 0 0 dnegate 0 =assert 0 =assert
                 0 1 dnegate -1 =assert 0 =assert ;
: test-dabs   -5 -1 dabs 0 =assert 5 =assert   5 0 dabs 0 =assert 5 =assert ;
: test-d2*    3 0 d2* 0 =assert 6 =assert
              min-n 0 d2* 1 =assert 0 =assert
              1 1 d2* 2 =assert 2 =assert ;
: test-d2/    6 0 d2/ 0 =assert 3 =assert
              0 1 d2/ 0 =assert min-n =assert
              -2 -1 d2/ -1 =assert -1 =assert ;
: test-d0=    0 0 d0= assert 1 0 d0= 0 =assert 0 1 d0= 0 =assert ;
: test-d0<    0 -1 d0< assert 0 0 d0< 0 =assert -1 0 d0< 0 =assert ;
: test-d=     5 1 5 1 d= assert 5 1 6 1 d= 0 =assert 5 1 5 2 d= 0 =assert ;
: test-d<     1 0 2 0 d< assert 2 0 1 0 d< 0 =assert 2 0 2 0 d< 0 =assert
              -1 -1 0 0 d< assert 0 0 -1 -1 d< 0 =assert
              max-u 0 0 1 d< assert ;
: test-d>     2 0 1 0 d> assert 1 0 2 0 d> 0 =assert ;
: test-du<    1 0 -1 -1 du< assert -1 -1 1 0 du< 0 =assert ;
: test-m+     5 0 3 m+ 0 =assert 8 =assert   5 0 -7 m+ -1 =assert -2 =assert ;
: test-um*    3 4 um* 0 =assert 12 =assert
              0 5 um* 0 =assert 0 =assert
              max-u 2 um* 1 =assert -2 =assert
              max-u max-u um* max-u 1- =assert 1 =assert ;
: test-m*     3 -4 m* -1 =assert -12 =assert
              -3 -4 m* 0 =assert 12 =assert
              min-n 2 m* -1 =assert 0 =assert ;
: test-um/mod 100 0 7 um/mod 14 =assert 2 =assert
              0 1 2 um/mod min-n =assert 0 =assert
              max-u 0 max-u um/mod 1 =assert 0 =assert ;
: test-sm/rem
  7 0 2 sm/rem 3 =assert 1 =assert
  -7 -1 2 sm/rem -3 =assert -1 =assert
  7 0 -2 sm/rem -3 =assert 1 =assert
  -7 -1 -2 sm/rem 3 =assert -1 =assert ;
: test-fm/mod
  7 0 2 fm/mod 3 =assert 1 =assert
  -7 -1 2 fm/mod -4 =assert 1 =assert
  7 0 -2 fm/mod -4 =assert -1 =assert
  -7 -1 -2 fm/mod 3 =assert -1 =assert
  -8 -1 2 fm/mod -4 =assert 0 =assert ;
2 3 2constant two-three
2variable twovar
: test-2constant   two-three 3 =assert 2 =assert ;
: test-2variable   7 8 twovar 2! twovar 2@ 8 =assert 7 =assert ;
: t-2lit [ 11 22 ] 2literal ;
: test-2literal   t-2lit 22 =assert 11 =assert ;

e: test-d.
  123 0 d.
  -123 -1 d.
  0 0 d.
  max-u 0 ud.
  cr
  out: 123 -123 0 18446744073709551615 
;e
e: test-d.-base
  255 0 16 base ! d. decimal cr
  out: FF 
;e
e: test-.r
  42 6 .r cr -42 6 .r cr 42 1 .r cr 42 6 u.r cr
  out:     42
  out:    -42
  out: 42
  out:     42
;e

( Strings and memory )
create sbuf 16 allot
: test-count   3 sbuf c! sbuf count 3 =assert sbuf 1+ =assert ;
: test-/string   s" hello" 2 /string 3 =assert c@ [char] l =assert ;
: test-place   s" xyz" sbuf place sbuf c@ 3 =assert sbuf 1+ c@ [char] x =assert ;
: test-move-forward   s" abcdef" sbuf swap cmove  sbuf dup 2 + 4 move
                      sbuf 6 s" ababcd" str= assert ;
: test-move-backward  s" abcdef" sbuf swap cmove  sbuf 2 + sbuf 4 move
                      sbuf 6 s" cdefef" str= assert ;
: test-compare
  s" abc" s" abc" compare 0 =assert
  s" abc" s" abd" compare -1 =assert
  s" abd" s" abc" compare 1 =assert
  s" ab" s" abc" compare -1 =assert
  s" abc" s" ab" compare 1 =assert
  s" " s" " compare 0 =assert
  s" " s" a" compare -1 =assert ;
: test-search
  s" hello world" s" wor" search assert 5 =assert c@ [char] w =assert
  s" hello" s" xyz" search 0 =assert 5 =assert drop
  s" hello" s" " search assert 5 =assert drop ;
: test-scan   s" hello" [char] l scan 3 =assert c@ [char] l =assert
              s" hello" [char] z scan 0 =assert drop ;
: test-skip   s" aaabc" [char] a skip 2 =assert c@ [char] b =assert
              s" aaa" [char] a skip 0 =assert drop ;

( Parsing )
: t-word1 bl word count ;
: t-word2 [char] , word count ;
: t-word3 bl word c@ ;
e: test-word
  t-word1 hello s" hello" str= assert
  t-word2 a, s" a" str= assert
  t-word3    xyz 3 =assert
;e
e: test-.(
  .( hi there) cr
  out: hi there
;e
: test-source   source nip #tib @ =assert ;

( Number conversion )
: test->number
  0 0 s" 123" >number 0 =assert drop 0 =assert 123 =assert
  0 0 s" 12x" >number 1 =assert drop 0 =assert 12 =assert
  5 0 s" 10" >number 2drop 0 =assert 510 =assert
  0 0 s" ff" >number 2 =assert drop 0 =assert 0 =assert ;
: test->number-hex   16 base ! 0 0 s" ff" >number 2drop 0 =assert 255 =assert
                     0 0 s" FF" >number 2drop 0 =assert 255 =assert decimal ;
: test->number-double
  0 0 s" 10000000000" >number 2drop
  cell 8 = if 0 =assert 10000000000 =assert else 2drop then ;

( Conditional compilation and markers )
: test-[defined]   [defined] dup assert [defined] nonexistent-word-xyz 0 =assert ;
: test-[undefined]   [undefined] nonexistent-word-xyz assert [undefined] dup 0 =assert ;
: t-defd [defined] dup ;
: test-[defined]-compiled   t-defd assert ;
: t-comp [compile] dup ;
: test-[compile]   5 t-comp + 10 =assert ;
: t-compc [ ' dup compile, ] + ;
: test-compile,   4 t-compc 8 =assert ;
e: test-marker
  marker mk1
  : zzz-marker 42 ;
  zzz-marker 42 =assert
  mk1
  [undefined] zzz-marker assert
  [undefined] mk1 assert
;e
e: test-anew
  anew anew-test1
  : zzz-anew 1 ;
  anew anew-test1
  [undefined] zzz-anew assert
  anew anew-test1
;e
: test-environment?
  s" ADDRESS-UNIT-BITS" environment? assert 8 =assert
  s" /COUNTED-STRING" environment? assert 255 =assert
  s" MAX-U" environment? assert -1 =assert
  s" NO-SUCH-THING" environment? 0 =assert ;

( Random )
: test-random
  100 0 do 10 random dup 0 >= assert 10 < assert loop
  1 rand-seed ! 1000 random 1 rand-seed ! 1000 random =assert ;
