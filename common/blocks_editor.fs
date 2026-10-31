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

\ Full screen editor of the blocks of blocks.fs: `blocks-editor` edits the
\ block last listed, `blocks-editor 5` the block 5, of the file chosen with
\ `use` (blocks.fb by default). 16 lines of 63 characters, the 64th of each
\ line being the newline that blocks.fs puts there. The code is a string
\ compiled the first time, as visual's is: it must hold no vertical bar.
\ It speaks ANSI only: terminal, lugforth --gui, telnet, the boards' console.

internals definitions
0 value be-loaded
defer be-go ( n -- )

: be-src r|
also internals also ansi
also [defined] posix [if] posix [then]
also [defined] termios [if] termios [then]
current @
vocabulary blocks-ed   also blocks-ed definitions

0 value bn   0 value cx   0 value cy   0 value ins   0 value done
0 value msg-a   0 value msg-n   0 value lfull   0 value last-draw
create lbuf 64 allot   create obuf 64 allot

: say ( a n -- )   to msg-n to msg-a ;
: blk ( -- a )   bn block ;
: line ( y -- a )   64 * blk + ;
: at ( -- a )   cy line cx + ;
: go-block ( n -- )   0 max dup to bn scr ! ;

( ---- Editing: a line has 63 places, 0 to 62 ---- )
: put ( c -- )
  ins if at dup 1+ 62 cx - cmove> then
  at c!  cx 1+ 62 min to cx  update ;
: del ( -- )   at dup 1+ swap 62 cx - cmove  bl cy line 62 + c!  update ;
: back ( -- )   cx 0= if exit then  cx 1- to cx  del ;
: line-end ( -- n )   \ after the last character that is not blank
  63 begin dup 0> while dup 1- cy line + c@ bl > if exit then 1- repeat ;
: copy-line ( -- )   cy line lbuf 64 cmove  -1 to lfull  s" Line copied" say ;
: open-line ( -- )   \ a blank line at the cursor, the last one is lost
  cy line dup 64 + 15 cy - 64 * cmove>  cy line clobber-line drop  update ;
: cut-line ( -- )
  cy line lbuf 64 cmove  -1 to lfull
  cy line dup 64 + swap 15 cy - 64 * cmove  15 line clobber-line drop  update ;
: paste-line ( -- )   \ above the cursor
  lfull 0= if s" Nothing to paste (Ctrl+C or Ctrl+X a line first)" say exit then
  open-line  lbuf cy line 64 cmove  update ;
: revert ( -- )   \ the block as it was written last
  0 to block-dirty  empty-buffers  s" Changes since the last save undone" say ;
: save ( -- )   save-buffers  s" Saved" say ;

( ---- Prompts and loading ---- )
[defined] new-termios [if]
0 value saved-tio
: be-raw ( -- )
  saved-tio 0= if sizeof(termios) allocate throw to saved-tio then
  stdin isatty if saved-tio termios@ then
  raw-mode
  stdin isatty if   \ Ctrl+S, Ctrl+Q, Ctrl+C, Ctrl+V reach the editor
    new-termios sl@ $400 invert and  new-termios l!
    new-termios .c_lflag sl@ 1 invert and $8000 invert and  new-termios .c_lflag l!
    new-termios termios!
  then ;
: be-normal ( -- )   delay-mode  stdin isatty if saved-tio termios! then ;
[else]   \ the boards and Windows: the console is raw already
: be-raw ( -- ) ;   : be-normal ( -- ) ;
[then]

: ask-block ( -- )   \ Ctrl+G: a block number, Enter goes there, Esc does not
  0 0 { n k }
  begin
    show  normal  0 18 at-xy  ." Go to block: " n n. clear-to-eol
    key to k
    k [char] 0 >= k [char] 9 <= and if n 10 * k [char] 0 - + to n then
    k 8 = k 127 = or if n 10 / to n then
    k 13 = k 10 = or k 27 = or
  until
  k 27 <> if n go-block 0 to cy 0 to cx then ;

: run-block ( -- )   \ Ctrl+L: save, load the block, show what it printed
  save-buffers  terminal-restore normal show be-normal page
  ." load " bn n. cr
  bn ['] load catch ?dup if nip cr ." Error " . else cr ." Done" then
  ."  - press a key" key drop
  be-raw terminal-save page ;

( ---- Drawing ---- )
: inverse ( -- )   esc ." [7m" ;
: ch ( c -- c' )   dup bl < if drop bl exit then  dup 126 > if drop [char] . then ;
: draw-line ( y -- )
  0 over 1+ at-xy  dup 10 < if space then  dup n. space  124 emit
  line  63 0 do dup i + c@ ch obuf i + c! loop drop
  obuf 63 type  124 emit ;
: blocks# ( -- n )   block-fid file-size throw d>s 1023 + 1024 /  bn 1+ max ;
: draw ( -- )
  hide  blk drop
  0 0 at-xy inverse
  ."  Block " bn n. ."  of " blocks# n.
  ins if ."    Insert" else ."    Overwrite" then
  block-dirty if ."    modified" then  clear-to-eol normal
  16 0 do i draw-line loop
  0 17 at-xy  msg-n if msg-a msg-n type then  clear-to-eol
  0 18 at-xy inverse
  ."  ^S save ^Q quit PgUp/PgDn ^G block ^L load ^X ^C ^V ^N line ^T ins ^U undo"
  clear-to-eol normal
  cx 4 + cy 1+ at-xy  show ;

( ---- Keys ---- )
256 constant k-up   257 constant k-down   258 constant k-left   259 constant k-right
260 constant k-home   261 constant k-end   262 constant k-pgup   263 constant k-pgdn
264 constant k-del   265 constant k-ins
0 value p1
: esc-final ( c -- code )
  dup [char] A = if drop k-up exit then
  dup [char] B = if drop k-down exit then
  dup [char] C = if drop k-right exit then
  dup [char] D = if drop k-left exit then
  dup [char] H = if drop k-home exit then
  dup [char] F = if drop k-end exit then
  [char] ~ = if
    p1 1 = p1 7 = or if k-home exit then
    p1 4 = p1 8 = or if k-end exit then
    p1 2 = if k-ins exit then
    p1 3 = if k-del exit then
    p1 5 = if k-pgup exit then
    p1 6 = if k-pgdn exit then
  then 0 ;
: read-escape ( -- code )
  key? 0= if 27 exit then
  key dup [char] O = if drop key esc-final exit then
  [char] [ <> if 0 exit then
  0 to p1
  begin key dup [char] 0 >= over [char] 9 <= and while [char] 0 - p1 10 * + to p1 repeat
  dup [char] ; = if drop begin key dup [char] 0 >= over [char] 9 <= and while drop repeat then
  esc-final ;
: read-key ( -- code )   key dup 27 = if drop read-escape then ;

: handle ( code -- )
  0 to msg-n
  dup k-up = if drop cy 1- 0 max to cy exit then
  dup k-down = if drop cy 1+ 15 min to cy exit then
  dup k-left = if drop cx 1- 0 max to cx exit then
  dup k-right = if drop cx 1+ 62 min to cx exit then
  dup k-home = over 1 = or if drop 0 to cx exit then
  dup k-end = over 5 = or if drop line-end 62 min to cx exit then
  dup k-pgup = if drop bn 1- go-block exit then
  dup k-pgdn = if drop bn 1+ go-block exit then
  dup k-del = over 4 = or if drop del exit then
  dup k-ins = over 20 = or if drop ins 0= to ins exit then
  dup 13 = over 10 = or if drop 0 to cx cy 1+ 15 min to cy exit then
  dup 9 = if drop cx 4 / 1+ 4 * 62 min to cx exit then
  dup 127 = over 8 = or if drop back exit then
  dup 19 = if drop save exit then
  dup 17 = if drop -1 to done exit then
  dup 7 = if drop ask-block exit then
  dup 12 = if drop run-block exit then
  dup 3 = if drop copy-line exit then
  dup 24 = if drop cut-line exit then
  dup 22 = if drop paste-line exit then
  dup 14 = if drop open-line exit then
  dup 21 = if drop revert exit then
  dup 31 > over 127 < and if put exit then
  drop ;

[defined] ms-ticks [if] : now ( -- ms ) ms-ticks ; [else] : now ( -- ms ) 0 ; [then]
: loop-keys ( -- )
  begin
    key? 0= now last-draw - 80 > or if draw now to last-draw then
    read-key ['] handle catch ?dup if nip s" Error (a throw): the file may not be writable" say drop then
    done
  until ;
: run ( n -- )
  go-block  blk drop  0 to cx  0 to cy  0 to done  0 to msg-n
  be-raw terminal-save page
  ['] loop-keys catch
  ['] save-buffers catch drop
  terminal-restore normal show be-normal page
  throw ;
' run is be-go

current !  previous previous previous previous previous
| ;

: be-eval ( a n -- )   \ a copy is evaluated: the source then stays whole
  dup allocate throw { a n b }
  a b n cmove  b n ['] evaluate catch  b free drop  throw ;

forth definitions internals
: blocks-editor ( "n" -- )   \ full screen editor of block n, or of the block last listed
  bl parse dup if s>number? 0= if scr @ then else 2drop scr @ then
  be-loaded 0= if  be-src be-eval  -1 to be-loaded  then
  be-go ;
only forth definitions
