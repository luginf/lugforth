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

( Standard ANS Forth words missing from the core, found by comparing the )
( word list with pForth: constants, stack, double numbers, strings, )
( parsing, markers. Written with the primitives only, so they are portable. )

forth definitions

( NOTE: words with locals must not use >r, locals are addressed from rp@ )

( Constants and small helpers )
-1 constant true
0 constant false
\ Flag inversion (same as 0=): 0 gives -1 (true), any other value gives 0 (false).
: not ( f -- f ) 0= ;
\ Does nothing. Handy as the default of a defer.
: noop ( -- ) ;
: 0> ( n -- f ) 0 > ;
: u> ( u u -- f ) swap u< ;
: within ( n lo hi -- f ) over - >r - r> u< ;
\ Adds 2 to a number.
: 2+ ( n -- n ) 2 + ;
\ Subtracts 2 from a number.
: 2- ( n -- n ) 2 - ;
\ The address of the previous cell: a minus the size of a cell (8 bytes).
: cell- ( a -- a ) cell - ;
: chars ( n -- n ) ;
: char+ ( a -- a ) 1+ ;

( Stack )
: tuck ( a b -- b a b ) swap over ;
: 2swap ( a b c d -- c d a b ) rot >r rot r> ;
: 2over ( a b c d -- a b c d a b ) >r >r 2dup r> r> 2swap ;
: pick ( xu .. x0 u -- xu .. x0 xu ) 1+ cells sp@ swap - @ ;
: roll ( xu .. x0 u -- xu-1 .. x0 xu )
   dup 0= if drop exit then swap >r 1- recurse r> swap ;
: 2>r ( a b -- ) ( r: -- a b ) postpone swap postpone >r postpone >r ; immediate
: 2r> ( -- a b ) ( r: a b -- ) postpone r> postpone r> postpone swap ; immediate
: 2r@ ( -- a b ) ( r: a b -- a b )
   postpone r> postpone r> postpone 2dup postpone >r postpone >r
   postpone swap ; immediate

( Double numbers, a double is lo hi with hi on top )
: s>d ( n -- d ) dup 0< ;
: d>s ( d -- n ) drop ;
: d+ { lo1 hi1 lo2 hi2 -- lo hi }
   lo1 lo2 + dup lo1 u< negate hi1 hi2 + + ;
: dnegate ( d -- d ) invert swap invert swap 1 0 d+ ;
: d- ( d d -- d ) dnegate d+ ;
: dabs ( d -- d ) dup 0< if dnegate then ;
: d2* { lo hi -- lo hi } lo 2* hi 2* lo 0< negate or ;
: d2/ { lo hi -- lo hi }
   lo 1 rshift hi 1 and if 1 cell 8 * 1- lshift or then hi 2/ ;
: d0= ( d -- f ) or 0= ;
: d0< ( d -- f ) nip 0< ;
: d= { lo1 hi1 lo2 hi2 -- f } lo1 lo2 = hi1 hi2 = and ;
: du< { lo1 hi1 lo2 hi2 -- f } hi1 hi2 u< hi1 hi2 = lo1 lo2 u< and or ;
: d< { lo1 hi1 lo2 hi2 -- f } hi1 hi2 < hi1 hi2 = lo1 lo2 u< and or ;
\ True when the signed double d1 is greater than d2.
: d> ( d d -- f ) 2swap d< ;
: m+ ( d n -- d ) s>d d+ ;
: 2constant ( a b "name" -- ) create swap , , does> dup @ swap cell+ @ ;
: 2variable ( "name" -- ) create 0 , 0 , ;
: 2literal ( a b -- ) swap postpone literal postpone literal ; immediate

( Unsigned double multiply and divide, shift and add, no hardware help )
internals definitions
: (um*) { a b lo hi ah -- lo hi }
   cell 8 * 0 do
      b 1 and if lo hi a ah d+ to hi to lo then
      ah 2* a 0< negate or to ah
      a 2* to a
      b 1 rshift to b
   loop lo hi ;
forth definitions internals
: um* ( u u -- ud ) 0 0 0 (um*) ;
forth definitions
: m* ( n n -- d ) 2dup xor 0< >r abs swap abs um* r> if dnegate then ;
: um/mod { lo hi u -- rem quot }
   cell 8 * 0 do
      hi 0<
      hi 2* lo 0< negate or to hi
      lo 2* to lo
      hi u u< 0= or if hi u - to hi lo 1 or to lo then
   loop hi lo ;
: sm/rem { lo hi n -- rem quot }
   lo hi dabs n abs um/mod
   hi 0< n 0< xor if negate then
   swap hi 0< if negate then swap ;
: fm/mod { lo hi n -- rem quot }
   lo hi n sm/rem
   over 0<> 2 pick 0< n 0< xor and if 1- swap n + swap then ;

( Printing double numbers )
internals definitions
create dbuf 80 allot
variable dpos
: ud/base ( lo hi -- rem lo hi ) \ divide by the current base
   swap >r 0 base @ um/mod swap r> swap base @ um/mod rot ;
: digit-char ( n -- ch ) dup 9 > if 7 + then 48 + ;
: ud>s ( lo hi -- a n )
   dbuf 80 + dpos !
   begin
      ud/base rot digit-char dpos @ 1- dup dpos ! c!
      2dup or 0=
   until 2drop dpos @ dbuf 80 + over - ;
forth definitions internals
\ Prints an unsigned double number followed by a space.
: ud. ( ud -- ) ud>s type space ;
: d. ( d -- )
   dup 0< if dabs ud>s swap 1- 45 over c! swap 1+
   else ud>s then type space ;
forth definitions

( Printing right justified )
: .r ( n width -- ) >r dup >r abs <# #s r> sign #> r> over - spaces type ;
: u.r ( u width -- ) >r <# #s #> r> over - spaces type ;

( Memory and strings )
: move ( src dst n -- ) >r 2dup u< r> swap if cmove> else cmove then ;
: count ( a -- a n ) dup 1+ swap c@ ;
: /string ( a n k -- a n ) swap over - >r + r> ;
\ Copies the string a n to dst as a counted string: the length in the first byte, then the characters.
: place { a n dst -- } n dst c! a dst 1+ n cmove ;
: compare { a1 n1 a2 n2 -- -1|0|1 }
   0 begin
      dup n1 < over n2 < and if dup a1 + c@ over a2 + c@ = else 0 then
   while 1+ repeat
   dup n1 = if n2 = if 0 else -1 then
   else dup n2 = if drop 1
   else a1 over + c@ swap a2 + c@ u< if -1 else 1 then then then ;
internals definitions
: (search) { a1 n1 a2 n2 a0 n0 -- a n f }
   begin n1 n2 >= while
      a1 n2 a2 n2 compare 0= if a1 n1 -1 exit then
      1 +to a1 -1 +to n1
   repeat a0 n0 0 ;
forth definitions internals
: search ( a1 n1 a2 n2 -- a3 n3 f ) 3 pick 3 pick (search) ;
forth definitions
\ Skips characters of the string a n until the first ch. Leaves the rest, starting at that character (n is 0 if ch is not found).
: scan { a n ch -- a n }
   begin n if a c@ ch <> else 0 then while 1 +to a -1 +to n repeat a n ;
\ Skips the leading characters ch of the string a n. Leaves the rest, starting at the first character that is not ch.
: skip { a n ch -- a n }
   begin n if a c@ ch = else 0 then while 1 +to a -1 +to n repeat a n ;

( Parsing )
: source ( -- a n ) tib #tib @ ;
internals definitions
create wbuf 256 allot
forth definitions internals
: word { ch -- a }
   begin >in @ #tib @ < if tib >in @ + c@ ch = else 0 then
   while 1 >in +! repeat
   ch parse 255 min wbuf place wbuf ;
: .( ( -- ) [char] ) parse type ; immediate
forth definitions

( Number conversion )
internals definitions
: >digit ( ch -- n|-1 )
   dup 97 123 within if 32 - then
   dup 48 58 within if 48 - exit then
   dup 65 91 within if 55 - exit then
   drop -1 ;
: digit-at ( a n -- d -1 | 0 )
   0= if drop 0 exit then c@ >digit dup 0< if drop 0 exit then
   dup base @ u< 0= if drop 0 exit then -1 ;
forth definitions internals
: >number { lo hi a n -- lo hi a n }
   begin a n digit-at while
      lo base @ um* hi base @ * + rot 0 d+ to hi to lo
      1 +to a -1 +to n
   repeat lo hi a n ;
forth definitions

( Conditional compilation helpers and markers )
\ True when the next word is found in the dictionary. Mostly used with [if]: [defined] foo [if] ... [then].
: [defined] ( "name" -- f )
   bl parse find 0<> state @ if postpone literal then ; immediate
\ True when the next word is not found in the dictionary: the reverse of [defined].
: [undefined] ( "name" -- f )
   bl parse find 0= state @ if postpone literal then ; immediate
: [compile] ( "name" -- ) ' , ; immediate
: compile, ( xt -- ) , ;
: marker ( "name" -- )
   here current @ @ create , ,
   does> dup @ current @ ! cell+ @ here - allot ;
\ Like marker, but first forgets the word of that name and everything after it if it exists, so a file can be loaded again without piling up its words.
: anew ( "name" -- )
   >in @ dup bl parse find swap >in !
   if dup >in ! forget then >in ! marker ;
: environment? ( a n -- 0 | val -1 )
   2dup s" ADDRESS-UNIT-BITS" str= if 2drop 8 -1 exit then
   2dup s" /COUNTED-STRING" str= if 2drop 255 -1 exit then
   2dup s" MAX-U" str= if 2drop -1 -1 exit then
   2dup s" MAX-N" str= if 2drop -1 1 rshift -1 exit then
   2drop 0 ;

( Pseudo random numbers, a linear congruential generator )
\ The seed of random (a linear congruential generator). Change it to get another sequence: ms-ticks 1 or rand-seed !
variable rand-seed   1 rand-seed !
: random ( n -- r ) rand-seed @ 1103515245 * 12345 + dup rand-seed !
                    16 rshift 32767 and swap mod ;
