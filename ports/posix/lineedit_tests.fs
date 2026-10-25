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

( Tests of the line editor, on its buffer: no terminal is needed )
\ The editor writes its escape sequences to the output as it goes, the
\ results are checked in the buffer.

needs ../../common/testing.fs

only forth definitions
vocabulary lineedit-tests
only forth also posix also internals also lineedit-tests definitions

create tbuf 200 allot
: quiet ( a n -- )   2drop ;
: fresh   ['] quiet is le-type  0 arrow !  0 hist-n !  0 hist-i !  tbuf ea !  200 en !  0 el !  0 ep ! ;
: put ( a n -- )   0 ?do dup c@ le-insert 1+ loop drop ;   ( typed text, case untouched )
: line= ( a n -- )   le-line compare 0 =assert ;

: test-le-ieq
  s" Dup" drop s" dUP" drop 3 le-ieq assert
  s" dup" drop s" dud" drop 3 le-ieq 0 =assert
  s" dup" drop s" dud" drop 2 le-ieq assert ;

: test-le-upper
  s" DR" le-upper? assert
  s" dr" le-upper? 0 =assert
  s" Dr" le-upper? 0 =assert
  s" 2" le-upper? 0 =assert ;

: test-edit
  fresh
  s" ac" put   le-home   le-forward   s" b" put
  s" abc" line=   ep @ 2 =assert
  le-bs   s" ac" line=   ep @ 1 =assert
  le-del   s" a" line=
  le-end   s" xyz" put   le-home   le-forward   le-forward
  le-kill-eol   s" ax" line=
  le-kill-bol   s" " line=   ep @ 0 =assert ;

: test-edit-utf8
  fresh
  s" aéb" put   ep @ 4 =assert   0 4 le-cols 3 =assert   1 3 le-cols 1 =assert
  le-back   ep @ 3 =assert   le-back   ep @ 1 =assert   le-back   ep @ 0 =assert
  le-forward   le-forward   ep @ 3 =assert
  le-bs   s" ab" line=   ep @ 1 =assert
  s" €" put   s" a€b" line=   ep @ 4 =assert
  le-home   le-forward   le-del   s" ab" line=   ep @ 1 =assert
  s" é" put   le-end   le-kill-bol   s" " line=   ep @ 0 =assert ;

: test-edit-limit
  fresh   3 en !
  s" abcdef" put   s" abc" line= ;

: test-history
  fresh
  s" one" hist-add   s" two" hist-add   s" two" hist-add   s" " hist-add
  hist-n @ 2 =assert
  s" typed" put
  le-up   s" two" line=
  le-up   s" one" line=
  le-up   s" one" line=
  le-down   s" two" line=
  le-down   s" typed" line=
  le-down   s" typed" line= ;

: test-history-wraps
  fresh
  70 0 do i str hist-add loop
  hist-avail 64 =assert
  le-up   s" 69" line=
  63 0 do le-up loop   s" 6" line=
  le-up   s" 6" line= ;

: complete ( a n -- )   fresh put le-complete ;
: test-complete-unique
  s" 2dro" complete   s" 2drop " line=   ep @ 6 =assert
  s" 1 2dro" complete   s" 1 2drop " line= ;
: test-complete-case
  s" DR" complete   s" DROP " line= ;
: test-complete-ambiguous
  s" du" complete   s" du" line=   ep @ 2 =assert
  s" dum" complete   s" dump" line= ;
: test-complete-none
  s" zzzzq" complete   s" zzzzq" line=
  s" " complete   s" " line= ;
: test-complete-middle
  fresh   s" dr x" put   le-back le-back   le-complete
  s" drop x" line=   ep @ 5 =assert ;
: lineedit-test-word ;
: test-complete-user-word   ( run-test starts with only forth )
  also lineedit-tests
  s" lineedit-test-w" complete   s" lineedit-test-word " line= ;

: test-file-context
  s" include" le-filectx? assert   s" INCLUDED" le-filectx? assert
  s" needs" le-filectx? assert     s" dup" le-filectx? 0 =assert
  s" included?" le-filectx? 0 =assert ;
create q2 2 allot
: q? ( c1 c2 -- f )   q2 1+ c!  q2 c!  q2 2 le-quoter? ;
: test-quoters
  115 34 q? assert   122 34 q? assert   114 124 q? assert   114 126 q? assert
  115 35 q? 0 =assert   99 34 q? 0 =assert ;
: test-prevword
  fresh   s" include  foo" put   le-wstart wsv !
  le-prevword s" include" compare 0 =assert
  s" x" put ;

( Files are looked up from the directory the tests run in, the root of the repository )
: test-complete-file
  s" include ports/posix/lineedit_t" complete
  s" include ports/posix/lineedit_tests.fs" line= ;
: test-complete-directory
  s" include ports/pos" complete   s" include ports/posix/" line=   ep @ 20 =assert ;
: test-complete-file-prefix
  s" include ports/posix/sdl2_a" complete   s" include ports/posix/sdl2_abc" line= ;
: test-complete-file-case
  s" include ports/POSIX/" complete   s" include ports/POSIX/" line= ;
: test-complete-path
  s" ./ports/pos" complete   s" ./ports/posix/" line=
  s" ports/posix/lineedit_t" complete   s" ports/posix/lineedit_tests.fs" line= ;
: test-complete-quoted
  fresh   115 le-insert 34 le-insert 32 le-insert   ( s" followed by a space )
  s" ports/posix/lineedit_t" put   le-complete
  ea @ 3 + el @ 3 -   s" ports/posix/lineedit_tests.fs" compare 0 =assert ;
: test-complete-file-none
  s" include nosuchdir/a" complete   s" include nosuchdir/a" line= ;
: test-complete-file-list
  s" include ports/posix/sdl2_" complete   s" include ports/posix/sdl2_" line=
  ep @ 25 =assert ;

: test-pool-sorted
  0 pool-cnt !  0 pool-used !  0 pl !
  s" beta" le-pool   s" Alpha" le-pool   s" beta" le-pool   s" alpha" le-pool   le-sort
  pool-cnt @ 4 =assert
  pool-ptr @ pool@ s" Alpha" compare 0 =assert
  pool-ptr cell+ @ pool@ s" alpha" compare 0 =assert
  pool-ptr 2 cells + @ pool@ s" beta" compare 0 =assert ;

run-tests
