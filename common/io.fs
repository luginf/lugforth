\ Copyright 2022 Bradley D. Nelson
\ Modifications Copyright 2026 Luginfo10
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

( Defer I/O to platform specific )
defer type
defer key
defer key?
defer terminate

( Hooks for prompt )
defer prompt

( Core I/O )
: bye   0 terminate ;
( bye for a script run as "lugforth file.fs" or on the web, not for a file included from the prompt )
: script-bye   'argc @ 1 <> if bye then ;
: emit ( n -- ) >r rp@ 1 type rdrop ;
: space bl emit ;   : cr 13 emit nl emit ;

( Numeric Output, on a double number as the standard has it: u. is 0 <# #s #> )
variable hld
: pad ( -- a ) here 160 + ;   ( the digits go down from pad: 128 for a binary double )
: digit ( u -- c ) 9 over < 7 and + 48 + ;
: extract ( n base -- n c ) u/mod swap digit ;
: <# ( -- ) pad hld ! ;
: hold ( c -- ) hld @ 1 - dup hld ! c! ;
( ud / base with u/mod only, in half cells: hi / base, then the remainder and )
( the upper half of lo, then the remainder and the lower half: each fits a cell )
: # ( ud -- ud' )
   base @ u/mod >r
   cell 4 * lshift over cell 4 * rshift or
   base @ u/mod >r
   cell 4 * lshift swap 1 cell 4 * lshift 1- and or
   base @ u/mod r> cell 4 * lshift or
   swap digit hold r> ;
: #s ( ud -- 0 0 ) begin # 2dup or 0= until ;
: sign ( n -- ) 0< if 45 hold then ;
: #> ( xd -- a n ) 2drop hld @ pad over - ;
: str ( n -- a n ) dup >r abs 0 <# #s r> sign #> ;
: hex ( -- ) 16 base ! ;   : octal ( -- ) 8 base ! ;
: decimal ( -- ) 10 base ! ;   : binary ( -- ) 2 base ! ;
: u. ( u -- ) 0 <# #s #> type space ;
: . ( w -- ) base @ 10 xor if u. exit then str type space ;
: ? ( a -- ) @ . ;
: n. ( n -- ) base @ swap decimal 0 <# #s #> type base ! ;

( A file is read one line at a time, as the standard has it: SOURCE is the current line and
  REFILL the next. file-end is the end of the text being included, 0 for the keyboard and for
  EVALUATE. A word that reads up to a delimiter, as a comment or a string does, goes on to the next lines
  when it does not find the delimiter on the first one, see parse-more. )
variable file-end
variable src-fid   ( the file being included, see source-id )
: line-end ( a -- a' ) begin dup file-end @ < while dup c@ 10 = if exit then 1+ repeat ;
: refill-file ( -- f )   ( the next line of the file is the input: false at the end of the file )
  file-end @ 0= if 0 exit then
  'tib @ #tib @ + 1+ dup file-end @ < 0= if drop 0 exit then
  dup 'tib !  dup line-end swap - #tib !  0 >in !  -1 ;
: extend-line ( -- f )   ( the input goes on to the end of the next line: false at the end of the file )
  file-end @ 0= if 0 exit then
  'tib @ #tib @ + 1+ dup file-end @ < 0= if drop 0 exit then
  line-end 'tib @ - #tib ! -1 ;
variable pm-start
: parse-more ( ch -- a n )   ( as parse, but over as many lines of a file as it takes to find ch )
  >in @ pm-start !  >r
  begin
    r@ parse 2dup + 'tib @ #tib @ + <
    if rdrop exit then
    extend-line if 2drop pm-start @ >in ! 0 else -1 then
  until rdrop ;
: parse-name-more ( -- a n )   ( the next name, over as many lines of a file as it takes: empty at the end )
  begin bl parse dup 0= while 2drop extend-line 0= if 0 0 exit then repeat ;
: (   [char] ) parse-more 2drop ; immediate

( Strings )
: parse-quote ( -- a n ) [char] " parse-more ;
: $place ( a n -- ) for aft dup c@ c, 1+ then next drop ;
: zplace ( a n -- ) $place 0 c, align ;
: $@   r@ dup cell+ swap @ r> dup @ 1+ aligned + cell+ >r ;
: s"   parse-quote state @ if postpone $@ dup , zplace
       else dup here swap >r >r zplace r> r> then ; immediate
: ."   postpone s" state @ if postpone type else type then ; immediate
: z"   postpone s" state @ if postpone drop else drop then ; immediate
variable src-ref   ( set when a string literal points into the text being read, see include-file )
variable r-copy   ( set at the end of the boot: r" r| r~ then copy their text, as s" does )
: r-lit ( a n -- a n | )
  state @ 0= if exit then
  r-copy @ if postpone $@ dup , zplace exit then
  swap aliteral aliteral -1 src-ref ! ;
: r"   parse-quote r-lit ; immediate
: r|   [char] | parse-more r-lit ; immediate
: r~   [char] ~ parse-more r-lit ; immediate
: s>z ( a n -- z ) here >r zplace r> ;
: s>zt ( a n -- z )   ( a transient s>z: here stays where it was, so a file word does not move pad )
   here >r zplace r@ here - allot r> ;
: z>s ( z -- a n ) 0 over begin dup c@ while 1+ swap 1+ swap repeat drop ;

( Better Errors )
: notfound ( a n n -- )
   dup if cr ." ERROR: " >r type r> ."  NOT FOUND!" cr drop -13 throw else drop then ;   ( -13: undefined word )
' notfound 'notfound !

( Abort )
: abort   -1 throw ;
: abort"   ( x -- ) postpone if postpone ." postpone cr -2 aliteral postpone throw postpone then ; immediate

( Input )
: raw.s   depth 0 max for aft sp@ r@ cells - @ . then next ;
variable echo -1 echo !   variable arrow -1 arrow !  0 value wascr
: *emit ( n -- ) dup 13 = if drop cr else emit then ;
: ?echo ( n -- ) echo @ if *emit else drop then ;
: ?arrow.   arrow @ if ." > " then ;
: *key ( -- n )
  begin
    key
    dup nl = if
      drop wascr if 0 else 13 exit then
    then
    dup 13 = to wascr
    dup if exit else drop then
  again ;
: eat-till-cr   begin *key dup 13 = if ?echo exit else drop then again ;
: accept ( a n -- n ) ?arrow. 0 swap begin 2dup < while
     *key
     dup 13 = if ?echo drop nip exit then
     dup 8 = over 127 = or if
       drop over if rot 1- rot 1- rot 8 ?echo bl ?echo 8 ?echo then
     else
       dup ?echo
       >r rot r> over c! 1+ -rot swap 1+ swap
     then
   repeat drop nip
   eat-till-cr
;
200 constant input-limit
: tib ( -- a ) 'tib @ ;
create input-buffer   input-limit allot
: tib-setup   input-buffer 'tib ! ;
( A platform can replace the line editor, see common/lineedit.fs )
defer edit-accept   ' accept is edit-accept
: refill-keys   tib-setup tib input-limit edit-accept #tib ! 0 >in ! -1 ;
\ REFILL: the next line of the file being read, the next line typed at the keyboard, and false
\ for the text of EVALUATE (or the end of the file).
: refill   refill-file if -1 exit then   file-end @ if 0 exit then
           'tib @ input-buffer = if refill-keys exit then 0 ;

( Stack Guards )
sp0 'stack-cells @ 2 3 */ cells + constant sp-limit
: ?stack   sp@ sp0 < if ." STACK UNDERFLOW " -4 throw then
           sp-limit sp@ < if ." STACK OVERFLOW " -3 throw then ;

( REPL )
: default-prompt   ."  ok" cr raw.s ;  ' default-prompt is prompt
: evaluate-buffer   begin >in @ #tib @ < while ?stack +evaluate1 repeat ?stack ;
: (evaluate) ( a n -- )   ( evaluate, the block being loaded stays: see load )
                      'tib @ >r #tib @ >r >in @ >r file-end @ >r
                      0 file-end ! #tib ! 'tib ! 0 >in !
                      ['] evaluate-buffer catch
                      r> file-end ! r> >in ! r> #tib ! r> 'tib !   \ the input back in place, after an error too
                      throw ;
variable blk   ( the block being loaded, 0 when the input is not a block )
: \   blk @ if >in @ 63 + -64 and >in ! exit then   ( a block line has 64 characters )
      nl parse drop drop ; immediate
: evaluate ( a n -- ) blk @ >r 0 blk ! ['] (evaluate) catch r> blk ! throw ;
: evaluate&fill
   begin >in @ #tib @ >= if prompt refill-keys drop then evaluate-buffer again ;
variable thrown
: quit
   #tib @ >in !
   begin ['] evaluate&fill catch dup thrown !
         if 0 state ! 0 file-end ! sp0 sp! fp0 fp! rp0 rp!
            thrown @ -28 = 0= if ."  ERROR " cr then   \ -28: stopped from the pager
         then
   again ;
variable boot-prompt
: free. ( nf nu -- ) 2dup swap . ." free + " . ." used = " 2dup + . ." total ("
                     over + 100 -rot */ n. ." % free)" ;
: info ( -- ) ." Lugforth v{{VERSION}} - rev {{REVISION}}" cr
             ." Date: {{DATE}}" cr
             ." Forth dictionary: " remaining used free. cr
             ." 3 x Forth stacks: " 'stack-cells @ cells . ." bytes each" cr
             ." words: list the words   see NAME: decompile   include FILE: load a file" cr
             ." bye: quit   docs: luginf.github.io/lugforth" cr ;
: help info ;
: raw-ok   ."  v{{VERSION}}" cr
           ." =:;============>>>" cr
           ." type 'info' for help" cr
           boot-prompt @ if boot-prompt @ execute then
           quit ;
