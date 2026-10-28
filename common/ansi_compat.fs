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
: .r ( n width -- ) >r dup >r abs 0 <# #s r> sign #> r> over - spaces type ;
: u.r ( u width -- ) >r 0 <# #s #> r> over - spaces type ;

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
: .( ( -- ) [char] ) parse-more type ; immediate
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
   bl parse (find) 0<> state @ if postpone literal then ; immediate
\ True when the next word is not found in the dictionary: the reverse of [defined].
: [undefined] ( "name" -- f )
   bl parse (find) 0= state @ if postpone literal then ; immediate
: [compile] ( "name" -- ) ' , ; immediate
: compile, ( xt -- ) , ;
: marker ( "name" -- )
   here current @ @ create , ,
   does> dup @ current @ ! cell+ @ here - allot ;
\ Like marker, but first forgets the word of that name and everything after it if it exists, so a file can be loaded again without piling up its words.
: anew ( "name" -- )
   >in @ dup bl parse (find) swap >in !
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

( ---- More words of the standard, by word set: CORE EXT, DOUBLE, EXCEPTION, )
( FACILITY, FLOATING, LOCALS, SEARCH, STRING, TOOLS. The FILE words are with )
( the files: common/filetools.fs and including.fs. TIME&DATE needs a clock. )

forth definitions internals

( Core extensions )
: buffer: ( u "name" -- ) create allot ;
: parse-name ( "name" -- a n ) bl parse ;
: unused ( -- u ) remaining ;
: -trailing ( a n -- a n' ) begin dup if 2dup + 1- c@ bl = else 0 then while 1- repeat ;
: holds ( a n -- ) begin dup while 1- 2dup + c@ hold repeat 2drop ;
: defer@ ( xt -- xt' ) >body @ ;
: defer! ( xt' xt -- ) >body ! ;
: action-of ( "name" -- xt )
   ' state @ if aliteral postpone defer@ else defer@ then ; immediate
\ 0 when the text comes from the keyboard, -1 from evaluate (and from a file).
: source-id ( -- 0 | -1 ) 'tib @ input-buffer = if 0 else -1 then ;
: save-input ( -- x3 x2 x1 3 ) 'tib @ #tib @ >in @ 3 ;
: restore-input ( xn .. x1 n -- f )
   dup 3 <> if 0 ?do drop loop -1 exit then drop >in ! #tib ! 'tib ! 0 ;
internals definitions
create c"buf 256 allot
forth definitions internals
\ A counted string: the length in the first byte. Compiled as an s" whose text
\ starts with that byte, so see and the r| copies treat it as any string.
: c" ( "ccc<quote>" -- c-addr )
   [char] " parse-more 255 min dup c"buf c! c"buf 1+ swap cmove c"buf dup c@ 1+
   state @ if postpone $@ dup , zplace postpone drop else here >r $place align r> then ; immediate
internals definitions
create s\buf 256 allot
variable s\len
: s\c ( ch -- ) s\len @ 255 < if s\buf s\len @ + c! 1 s\len +! else drop then ;
: s\next ( -- ch | -1 ) >in @ #tib @ < if tib >in @ + c@ 1 >in +! else -1 then ;
: s\hex ( -- n ) s\next >digit 4 lshift s\next >digit 15 and or ;
: s\esc ( ch -- )
   case
      [char] a of 7 s\c endof      [char] b of 8 s\c endof
      [char] e of 27 s\c endof     [char] f of 12 s\c endof
      [char] l of 10 s\c endof     [char] m of 13 s\c 10 s\c endof
      [char] n of 10 s\c endof     [char] q of 34 s\c endof
      [char] r of 13 s\c endof     [char] t of 9 s\c endof
      [char] v of 11 s\c endof     [char] z of 0 s\c endof
      [char] x of s\hex s\c endof
      dup s\c
   endcase ;
: s\parse ( "ccc<quote>" -- a n )
   0 s\len !
   begin s\next dup -1 <> over [char] " <> and while
      dup [char] \ = if drop s\next s\esc else s\c then
   repeat drop s\buf s\len @ ;
forth definitions internals
\ Like s" with the escapes of the standard: \n \t \" \\ \xHH ...
: s\" ( "ccc<quote>" -- a n )
   s\parse state @ if postpone $@ dup , zplace else dup here swap >r >r zplace r> r> then ; immediate
forth definitions internals

( Doubles )
: 2rot ( d1 d2 d3 -- d2 d3 d1 ) 5 roll 5 roll ;
: dmax ( d1 d2 -- d ) 2over 2over d< if 2swap then 2drop ;
: dmin ( d1 d2 -- d ) 2over 2over d< 0= if 2swap then 2drop ;
: d.r ( d width -- )
   >r dup 0< if dabs ud>s swap 1- 45 over c! swap 1+ else ud>s then
   r> over - spaces type ;
internals definitions
variable m-t0   variable m-t1   variable m-t2   variable m-neg
forth definitions internals
\ d1 * n1 / n2 through a triple-cell product, rounded toward zero.
: m*/ ( d1 n1 +n2 -- d2 )
   >r 2dup xor 0< m-neg ! abs >r dabs r>
   tuck um* m-t2 ! >r um* swap m-t0 ! 0 r> 0 d+ m-t2 @ + m-t2 ! m-t1 !
   m-t1 @ m-t2 @ r@ um/mod m-t2 ! m-t0 @ swap r> um/mod nip m-t2 @
   m-neg @ if dnegate then ;
: 2value ( x1 x2 "name" -- ) create , , does> 2@ ;
forth definitions internals

( Exceptions: the codes of the standard are -1 to -79; CATCH and THROW are in boot.fs )

( Facility: structures of Forth 2012 and the extended keys )
: begin-structure ( "name" -- addr 0 ) create here 0 0 , does> @ ;
: end-structure ( addr n -- ) swap ! ;
: +field ( n1 n2 "name" -- n3 ) create over , + does> @ + ;
: field: ( n1 "name" -- n2 ) aligned cell +field ;
: cfield: ( n1 "name" -- n2 ) 1 +field ;
: ekey ( -- u ) key ;
: ekey? ( -- f ) key? ;
: ekey>char ( u -- u false | ch true ) -1 ;
: emit? ( -- f ) -1 ;

( Floating point: the floats here are single precision, as the standard allows. )
( F@ F! FLOAT+ FLOATS are the SF words: there is no double precision float. )
: f@ ( a -- r ) sf@ ;
: f! ( r a -- ) sf! ;
: float+ ( a -- a' ) sfloat+ ;
: floats ( n -- n' ) sfloats ;
: sfaligned ( a -- a' ) 3 + -4 and ;
: sfalign ( -- ) here sfaligned here - allot ;
: faligned ( a -- a' ) sfaligned ;
: falign ( -- ) sfalign ;
: ffield: ( n1 "name" -- n2 ) faligned sfloat +field ;
: fvalue ( r "name" -- ) create sf, align does> sf@ ;
: ftrunc ( r -- r' ) fdup f0< if fnegate floor fnegate else floor then ;
: fround ( r -- r' ) 0.5e f+ floor ;
: ftan ( r -- r' ) fsincos f/ ;
: fatan ( r -- r' ) 1e fatan2 ;
: fasin ( r -- r' ) fdup fdup f* 1e fswap f- fsqrt fatan2 ;
: facos ( r -- r' ) fdup fdup f* 1e fswap f- fsqrt fswap fatan2 ;
: fsinh ( r -- r' ) fexp fdup 1/f f- 2e f/ ;
: fcosh ( r -- r' ) fexp fdup 1/f f+ 2e f/ ;
: ftanh ( r -- r' ) fdup fsinh fswap fcosh f/ ;
: fasinh ( r -- r' ) fdup fdup f* 1e f+ fsqrt f+ fln ;
: facosh ( r -- r' ) fdup fdup f* 1e f- fsqrt f+ fln ;
: fatanh ( r -- r' ) fdup 1e f+ fswap 1e fswap f- f/ fln 2e f/ ;
: flog ( r -- r' ) fln 10e fln f/ ;
: falog ( r -- r' ) 10e fswap f** ;
: fexpm1 ( r -- r' ) fexp 1e f- ;
: flnp1 ( r -- r' ) 1e f+ fln ;
internals definitions
fvariable f~a   fvariable f~b   fvariable f~c
: 2^cell ( -- r ) 2e cell 8 * s>f f** ;
: u>f ( u -- r ) dup 0< if 1 rshift s>f 2e f* else s>f then ;
forth definitions internals
\ r3 > 0: |r1-r2| < r3; r3 = 0: r1 = r2; r3 < 0: |r1-r2| < |r3| * (|r1|+|r2|).
: f~ ( r1 r2 r3 -- f )
   f~c sf! f~b sf! f~a sf!
   f~c sf@ f0= if f~a sf@ f~b sf@ f= exit then
   f~a sf@ f~b sf@ f- fabs
   f~c sf@ f0< if f~a sf@ fabs f~b sf@ fabs f+ f~c sf@ fabs f* else f~c sf@ then f< ;
internals definitions
0 0 2value 2value-sample   0e fvalue fvalue-sample
: does-like? ( xt xt' -- f ) over @ over @ = >r cell+ @ swap cell+ @ = r> and ;
forth definitions internals
\ TO also stores in a 2value and in an fvalue.
: to ( x | x1 x2 | r "name" -- )
   >in @ ' dup ['] 2value-sample does-like? if
      nip >body state @ if aliteral postpone 2! else 2! then exit then
   dup ['] fvalue-sample does-like? if
      nip >body state @ if aliteral postpone sf! else sf! then exit then
   drop >in ! postpone to ; immediate
: d>f ( d -- r ) dup 0< >r dabs u>f 2^cell f* u>f f+ r> if fnegate then ;
\ Within the range of a cell.
: f>d ( r -- d ) f>s s>d ;
internals definitions
create fbuf 80 allot
forth definitions internals
\ The standard text of a float: the exponent may be left out, a blank text is 0.
: >float ( a n -- r true | false )
   -trailing dup 0= if 2drop 0e -1 exit then
   dup 78 > if 2drop 0 exit then
   over c@ [char] + = if 1 /string then
   fbuf swap dup >r cmove r>
   fbuf over [char] e scan nip 0= if fbuf over [char] E scan nip 0= if
      [char] e over fbuf + c! 1+ then then
   fbuf swap s>float? ;
internals definitions
: fexp10 ( r -- r' n ) 0 >r
   fdup f0= if r> exit then
   begin fdup fabs 10e f>= while 10e f/ r> 1+ >r repeat
   begin fdup fabs 1e f< while 10e f* r> 1- >r repeat r> ;
forth definitions internals
\ Scientific notation: one digit before the point.
: fs. ( r -- ) fexp10 >r <# #fs #> type [char] E emit r> . ;
\ Engineering notation: an exponent that is a multiple of 3.
: fe. ( r -- ) fexp10 begin dup 3 mod while 1- >r 10e f* r> repeat
   >r <# #fs #> type [char] E emit r> . ;
forth definitions internals

( Locals of Forth 2012: {: in | uninitialized -- out :} and LOCALS| )
internals definitions
: skip-:} ( "... :}" -- ) begin parse-name-more dup 0= if 2drop exit then s" :}" str= until ;
: :}? ( a n -- f ) s" :}" str= ;
: {:uninit ( "names" -- )
   begin parse-name-more
      dup 0= if 2drop exit then
      2dup :}? if 2drop exit then
      2dup --? if 2drop skip-:} exit then
      0 aliteral (local)
   again ;
forth definitions internals
: {: ( "args | vals -- outs :}" -- )
   parse-name-more
   dup 0= if 2drop exit then
   2dup :}? if 2drop exit then
   2dup --? if 2drop skip-:} exit then
   2dup s" |" str= if 2drop {:uninit exit then
   recurse (local) ; immediate
\ The first name takes the top of the stack.
: locals| ( "names |" -- )
   begin parse-name-more
      dup 0= if 2drop exit then
      2dup s" |" str= if 2drop exit then
      (local)
   again ; immediate
forth definitions internals

( Search order )
' forth >body constant forth-wordlist
: wordlist ( -- wid ) here 0 , ;
: get-current ( -- wid ) current @ ;
: set-current ( wid -- ) current ! ;
: get-order ( -- widn .. wid1 n )
   0 begin dup cells context + @ while 1+ repeat
   dup >r begin dup while 1- dup cells context + @ swap repeat drop r> ;
: set-order ( widn .. wid1 n -- )
   dup -1 = if drop only exit then
   dup 15 > if -49 throw then
   0 over cells context + !
   0 ?do i cells context + ! loop ;
internals definitions
create saved-order 16 cells allot
forth definitions internals
: search-wordlist ( a n wid -- 0 | xt 1 | xt -1 )
   context saved-order 16 cells cmove
   context ! 0 context cell+ ! (find)
   saved-order context 16 cells cmove
   dup 0= if exit then dup immediate? if 1 else -1 then ;
forth definitions internals

( Strings )
: sliteral ( a n -- ) postpone $@ dup , zplace ; immediate
internals definitions
variable substitutions   ( nodes: link, name a n, text a n )
variable node-at
: node-name ( node -- a n ) dup cell+ @ swap 2 cells + @ ;
: node-text ( node -- a n ) dup 3 cells + @ swap 4 cells + @ ;
: subst-find ( a n -- node | 0 )
   substitutions @ begin dup while
      dup >r node-name 2over compare 0= if 2drop r> exit then r> @
   repeat nip nip ;
: $copy ( a n -- a' n ) here swap dup >r cmove here r@ allot align r> ;
variable sub-dst   variable sub-cap   variable sub-len
variable sub-count   variable sub-over
: sub-c ( ch -- )
   sub-len @ sub-cap @ < if sub-dst @ sub-len @ + c! 1 sub-len +! else drop -1 sub-over ! then ;
: sub-s ( a n -- ) 0 ?do dup i + c@ sub-c loop drop ;
: sub-pct ( a n -- a' n' )   \ a is on a %
   over 1+ over 1- [char] % scan
   dup 0= if 2drop sub-s 0 0 exit then
   2swap drop 1+ 2 pick over -
   dup 0= if 2drop [char] % sub-c 1 /string exit then
   2dup subst-find ?dup if nip nip node-text sub-s 1 sub-count +! 1 /string exit then
   [char] % sub-c sub-s [char] % sub-c 1 /string ;
forth definitions internals
\ Gives the name a2 n2 the text a1 n1, for SUBSTITUTE.
: replaces ( a1 n1 a2 n2 -- )
   2dup subst-find ?dup if node-at ! 2drop $copy node-at @ 4 cells + ! node-at @ 3 cells + ! exit then
   $copy 2swap $copy align here node-at ! substitutions @ ,
   2swap swap , , swap , , node-at @ substitutions ! ;
\ Copies a1 n1 to a2 (room for n2 characters), %name% replaced by its text and %% by %.
\ n4: the number of replacements, negative when the result does not fit.
: substitute ( a1 n1 a2 n2 -- a2 n3 n4 )
   sub-cap ! sub-dst ! 0 sub-len ! 0 sub-count ! 0 sub-over !
   begin dup while
      over c@ [char] % = if sub-pct else over c@ sub-c 1 /string then
   repeat 2drop
   sub-dst @ sub-len @ sub-over @ if -1 else sub-count @ then ;
\ Doubles each % of a1 n1, copied to a2.
: unescape ( a1 n1 a2 -- a2 n2 )
   0 2swap over + swap ?do
      i c@ [char] % = if 2dup + [char] % swap c! 1+ then
      2dup + i c@ swap c! 1+
   loop ;
forth definitions internals

( Tools )
: cs-pick ( n -- ) pick ;
: cs-roll ( n -- ) roll ;
: name>string ( nt -- a n ) >name ;
: name>interpret ( nt -- xt ) ;
: name>compile ( nt -- x xt ) dup immediate? if ['] execute else ['] compile, then ;
: synonym ( "new" "old" -- ) create ' dup , immediate? if immediate then does> @ execute ;
\ The standard FIND: a counted string in, 1 for an immediate word, -1 for the others, the string and 0 if missing.
: find ( c-addr -- c-addr 0 | xt 1 | xt -1 )
   dup count (find) dup if nip dup immediate? if 1 else -1 then else drop 0 then ;
internals definitions
variable nr-ret
forth definitions internals
: n>r ( i*x n -- ) ( R: -- i*x n )
   r> nr-ret ! dup begin dup while rot >r 1- repeat drop >r nr-ret @ >r ;
: nr> ( -- i*x n ) ( R: i*x n -- )
   r> nr-ret ! r> dup begin dup while r> rot rot 1- repeat drop nr-ret @ >r ;
\ Calls xt ( nt -- f ) on each word of wid, the newest first, while it gives true.
\ The words of the kernel (the builtins of C) and the smudged ones are not in the walk.
: traverse-wordlist ( i*x xt wid -- j*x )
   @ begin dup while
      dup >flags 6 and 0= if 2dup 2>r swap execute 0= if 2r> 2drop exit then 2r> then
      >link
   repeat 2drop ;
forth definitions
