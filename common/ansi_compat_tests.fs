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
create rcopy-buf 64 allot
: test-r-copy   ( a string with r| keeps its text after the source is overwritten )
  s" : rtmp r| hello | ;" { a n }
  a rcopy-buf n cmove   rcopy-buf n evaluate
  rcopy-buf 64 [char] x fill
  s" rtmp" evaluate  s" hello " compare 0 =assert ;

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
: test-[defined]   [ [defined] dup ] literal assert [ [defined] nonexistent-word-xyz ] literal 0 =assert ;
: test-[undefined]   [ [undefined] nonexistent-word-xyz ] literal assert [ [undefined] dup ] literal 0 =assert ;
: t-defd [defined] dup [if] 1 [else] 2 [then] ;
: test-[defined]-in-definition   t-defd 1 =assert ;
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

( Core extensions )
10 buffer: tbuf
: test-buffer:   tbuf 10 erase  tbuf 9 + c@ 0 =assert ;
: tpn ( -- a n ) parse-name ;
: test-parse-name   s" tpn   word  " evaluate s" word" str= assert ;
: test-holds   <# s" xyz" holds 0 0 #s #> s" 0xyz" str= assert ;
: test--trailing   s" ab  " -trailing nip 2 =assert   s"    " -trailing nip 0 =assert ;
defer tdefer
: test-defer@   ['] dup ['] tdefer defer!  ['] tdefer defer@ ['] dup =assert
                action-of tdefer ['] dup =assert ;
: test-save-input   save-input dup 4 =assert restore-input 0 =assert ;
: test-restore-input-other   1 2 2 restore-input -1 =assert depth 0 =assert ;
: test-c"   c" hello" dup c@ 5 =assert count s" hello" str= assert ;
: test-s\"   s\" a\tb\n\x41\"\\" nip 7 =assert
             s\" \x41\x7a" s" Az" str= assert
             s\" \q\m" nip 3 =assert ;
: test-source-id   source-id dup -1 <> swap 0<> and assert ;   ( a file: neither the keyboard nor EVALUATE )
: test-unused   unused 0 >assert ;

( Doubles )
: test-2rot   1 2 3 4 5 6 2rot 2 =assert 1 =assert 6 =assert 5 =assert 4 =assert 3 =assert ;
: test-dmax   1 0 5 0 dmax 0 =assert 5 =assert   -1 -1 1 0 dmin -1 =assert -1 =assert ;
: test-m*/   10 0 3 2 m*/ 0 =assert 15 =assert
             -10 -1 3 2 m*/ -1 =assert -15 =assert
             max-n 0 4 2 m*/ 0 =assert -2 =assert
             max-n 0 max-n max-n m*/ 0 =assert max-n =assert
             5 0 -7 11 m*/ -1 =assert -4 =assert   ( floored, as / )
             -5 -1 7 11 m*/ -1 =assert -4 =assert ;
1 2 2value t2v
: test-2value   1 2 to t2v t2v 2 =assert 1 =assert  3 4 to t2v t2v 4 =assert 3 =assert ;
: t2v-set   7 8 to t2v ;
: test-2value-compiled   t2v-set t2v 8 =assert 7 =assert ;
e: test-d.r
  -42 -1 6 d.r cr
  out:    -42
;e

( Facility )
begin-structure tpoint
  field: tp.x
  cfield: tp.c
  field: tp.y
end-structure
: test-structure   tpoint 3 cells =assert  0 tp.x 0 =assert  0 tp.c cell =assert
                   0 tp.y 2 cells =assert ;
: test-ekey>char   65 ekey>char -1 =assert 65 =assert ;

( Floating point )
1.5e fvalue tfv
: test-fvalue   tfv 1.5e f= assert  2.5e to tfv tfv 2.5e f= assert ;
fvariable tfvar
: test-f@   3e tfvar f! tfvar f@ 3e f= assert ;
: test-ftrunc   -2.5e ftrunc -2e f= assert  2.5e ftrunc 2e f= assert ;
: test-fround   2.4e fround 2e f= assert  2.6e fround 3e f= assert ;
: test-f~   1e 1e 0e f~ assert   1e 1.01e 0.1e f~ assert   1e 2e 0.1e f~ 0 =assert
            1e 1.001e -0.01e f~ assert ;
: test->float   s" 1.5" >float assert 1.5e f= assert
                s" 2E1" >float assert 20e f= assert
                s" abc" >float 0 =assert
                s"  " >float assert 0e f= assert ;
: test-d>f   5 0 d>f 5e f= assert  -5 -1 d>f -5e f= assert ;
: test-trig   0.5e fasin fsin 0.5e 0.001e f~ assert
              1e fatan 4e f* pi 0.001e f~ assert
              100e flog 2e 0.001e f~ assert  2e falog 100e 0.01e f~ assert ;
e: test-fs.
  1234.5e fs. cr
  out: 1.234500E3 
;e

( Locals )
: tl1 {: a b | c -- d :} a b + to c c 2* ;
: test-{:   1 2 tl1 6 =assert ;
: tl2 locals| a b | a b - ;
: test-locals|   10 3 tl2 -7 =assert ;

( Search order )
wordlist constant twl
: test-wordlist
   get-current twl set-current  s" : twl-word 42 ;" evaluate  set-current
   s" twl-word" twl search-wordlist -1 =assert execute 42 =assert
   s" nothing-here" twl search-wordlist 0 =assert
   s" dup" forth-wordlist search-wordlist -1 =assert ['] dup =assert ;
: test-get-order   get-order dup >r set-order get-order dup r> =assert
                   dup 0 >assert 0 ?do drop loop ;
: test-set-order   get-order  twl forth-wordlist 2 set-order
                   get-order 2 =assert forth-wordlist =assert twl =assert  set-order ;
wordlist constant twl2
get-current twl2 set-current : tw-a ; : tw-b ; set-current
: count-words ( n nt -- n' f ) drop 1+ -1 ;
: first-word ( n nt -- n' f ) drop 1+ 0 ;
: test-traverse-wordlist   0 ['] count-words twl2 traverse-wordlist 2 =assert
                           0 ['] first-word twl2 traverse-wordlist 1 =assert ;
: test-name>string   ['] twl >name nip 3 =assert  ['] dup name>string s" DUP" str= assert ;

( Strings )
create sub-buf 80 allot
: test-substitute
   s" World" s" who" replaces
   s" Hello %who%, 100%% sure, %nobody%" sub-buf 80 substitute 1 =assert
   s" Hello World, 100% sure, %nobody%" str= assert
   s" Hi %who%" sub-buf 3 substitute 0 <assert 2drop ;
: test-unescape   s" 5%" sub-buf unescape s" 5%%" str= assert ;
: tsl [ char l pad c! char i pad 1+ c! char t pad 2 + c! pad 3 ] sliteral ;
: test-sliteral   tsl s" lit" str= assert ;

( Tools )
synonym tdup dup
: test-synonym   5 tdup =assert ;
: test-n>r   1 2 3 3 n>r nr> 3 =assert 3 =assert 2 =assert 1 =assert ;
: test-cs-roll   1 2 3 2 cs-roll 1 =assert 3 =assert 2 =assert ;

( Pictured numeric output on a double, as the standard has it )
: test-<#-single   12 0 <# #s #> s" 12" str= assert   0 0 <# #s #> s" 0" str= assert ;
: test-<#-double   0 1 <# #s #>
                   cell 8 = if s" 18446744073709551616" else s" 4294967296" then str= assert ;
: test-<#-max      -1 -1 <# #s #> nip cell 8 = if 39 else 20 then =assert ;
: test-<#-s>d      -42 dup >r abs s>d <# #s r> sign #> s" -42" str= assert ;
: test-<#-hex      base @ hex 255 0 <# # # #> rot base ! s" FF" str= assert ;
: test-<#-binary   base @ 2 base ! -1 -1 <# #s #> nip swap base ! cell 16 * =assert ;

( FIND, as the standard has it: a counted string, 1 for an immediate word, -1 for the others )
create find-cs 3 c, char d c, char u c, char p c,
create find-imm 4 c, char t c, char h c, char e c, char n c,
create find-none 6 c, char n c, char o c, char s c, char u c, char c c, char h c,
: test-find-word   find-cs find over ['] dup =assert -1 =assert drop ;
: test-find-immediate   find-imm find over ['] then =assert 1 =assert drop ;
: test-find-missing   find-none find 0 =assert find-none =assert ;

( Number prefixes and character literals of Forth 2012 )
: test-prefix-dec   #1289 1289 =assert   #-1289 -1289 =assert   [ hex ] #10 [ decimal ] 10 =assert ;
: test-prefix-hex   $ff 255 =assert   $-12eF -4847 =assert   -$ff -255 =assert ;
: test-prefix-bin   %10010110 150 =assert   %-10010110 -150 =assert ;
: test-char-lit     'z' 122 =assert   ''' 39 =assert   'Z' 90 =assert ;
: t1 #8327 $-2cbe %011010111 ''' ;
: test-prefix-in-def   t1 39 =assert 215 =assert -11454 =assert 8327 =assert ;

( 2@ and 2! as the standard has them: x2 at the address, x1 in the next cell )
create two-cells 5 , 6 ,
create two-cells2 0 , 0 ,
: test-2@-layout    two-cells 2@ 5 =assert 6 =assert ;
: test-2!-layout    1 2 two-cells2 2!  two-cells2 @ 2 =assert  two-cells2 cell+ @ 1 =assert ;
2 3 2value two-val-init
4 5 2value two-val-to
: test-2value-init  two-val-init 3 =assert 2 =assert ;
: test-2value-to    7 8 to two-val-to  two-val-to 8 =assert 7 =assert ;

( A file is read one line at a time: SOURCE is the line, REFILL the next one )
create src-len 0 ,
source nip src-len !
: test-source-is-line   src-len @ 20 =assert ;
refill 0 constant refill-skipped
drop 1 constant refill-next
: test-refill-file   [ [defined] refill-next ] literal assert   [ [defined] refill-skipped ] literal 0= assert ;
( a comment
  on two lines ) 7 constant after-ml-comment
: test-ml-comment   after-ml-comment 7 =assert ;
: ml-string   s" a
bc" ;
: test-ml-string   ml-string nip 4 =assert ;
: ml-locals   { a b
   -- d }  a b + ;
: test-ml-locals   2 3 ml-locals 5 =assert ;
: ml-locals2   {: a b
   | c -- d :}  a b * ;
: test-ml-locals2   2 3 ml-locals2 6 =assert ;
s" -1 throw" ' evaluate catch nip nip constant eval-caught
2 constant after-eval-error
: test-eval-error-keeps-file   eval-caught -1 =assert   [ [defined] after-eval-error ] literal assert ;
: rf-eval   s" REFILL" evaluate ;
: test-refill-evaluate   rf-eval 0 =assert ;
( The files are not emulated on the ESP32 simulator. )
DEFINED? esp 0= [IF]
: test-file-ud   ( the file size and position are double cell numbers )
   s" fileud.tmp" w/o create-file throw >r
   s" 0123456789" r@ write-file throw
   r@ file-size throw 10 0 d= assert
   r> close-file throw
   s" fileud.tmp" r/w open-file throw >r
   4 0 r@ reposition-file throw
   r@ file-position throw 4 0 d= assert
   3 0 r@ resize-file throw
   r@ file-size throw 3 0 d= assert
   r> close-file throw
   s" fileud.tmp" delete-file throw ;
create rl-buf 16 allot
: test-read-line
   s" readline.tmp" w/o create-file throw >r
   s" ab" r@ write-line throw   s" " r@ write-line throw   s" cde" r@ write-file throw
   r> close-file throw
   s" readline.tmp" r/o open-file throw >r
   rl-buf 16 r@ read-line throw -1 =assert 2 =assert
   rl-buf 16 r@ read-line throw -1 =assert 0 =assert
   rl-buf 2 r@ read-line throw -1 =assert 2 =assert
   rl-buf 16 r@ read-line throw -1 =assert 1 =assert
   rl-buf 16 r@ read-line throw 0 =assert 0 =assert
   r> close-file throw
   s" readline.tmp" delete-file throw ;
: test-file-status
   s" fileud.tmp" file-status nip 0<> assert
   s" fileud.tmp" w/o create-file throw close-file throw
   s" fileud.tmp" file-status nip 0= assert
   s" fileud.tmp" delete-file throw ;
[THEN]
