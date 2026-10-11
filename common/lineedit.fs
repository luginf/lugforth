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

( Line editor for the terminal: cursor keys, history and Tab completion )
( On POSIX it is used only when stdin and stdout are terminals and echo is on, )
( or when a console sets le-force, otherwise the plain accept of io.fs reads )
( the line. File names are completed on POSIX only. Lines longer than the )
( terminal width are not redrawn correctly. )

only forth also internals definitions
DEFINED? posix [IF] also posix [THEN]

variable le-force   ( a console whose key and type are not the tty sets it )
DEFINED? posix [IF]
  : le-tty? ( -- f )   le-force @ stdin isatty stdout isatty and or ;
[ELSE]
  : le-tty? ( -- f )   -1 ;
[THEN]

variable ea   variable en   variable el   variable ep
( buffer address, its size, the length of the line, the cursor )

defer le-type   ' type is le-type   ( the tests send the output elsewhere )
: le-emit ( c -- )   >r rp@ 1 le-type rdrop ;
: le-cr   13 le-emit 10 le-emit ;
: le-n. ( n -- )   base @ swap decimal <# #s #> le-type base ! ;
: le-esc[   27 le-emit [char] [ le-emit ;
: le-left ( n -- )    dup 0= if drop exit then le-esc[ le-n. [char] D le-emit ;
: le-right ( n -- )   dup 0= if drop exit then le-esc[ le-n. [char] C le-emit ;
: le-clear-eol   le-esc[ [char] K le-emit ;
: le-line ( -- a n )   ea @ el @ ;
: le-uc ( c -- C )   dup 96 > over 123 < and if 32 - then ;

( Redraw the line when the cursor was at column oldpos and is now at ep )
: le-redraw ( oldpos -- )
  le-left   le-line le-type   le-clear-eol   el @ ep @ - le-left ;

: le-insert ( c -- )
  el @ en @ >= if drop exit then
  ep @ el @ = if
    dup ea @ ep @ + c!   1 ep +!   1 el +!   le-emit exit
  then
  ea @ ep @ + dup 1+ el @ ep @ - cmove>
  ea @ ep @ + c!   1 el +!
  ep @ 1 ep +! le-redraw ;
variable fmode   ( completing file names: case matters and is kept )
: le-fold ( c -- c' )   fmode @ 0= if le-uc then ;
( Completed text follows the case of what was typed: capitals only if all the typed letters are )
: le-lc ( c -- c' )   dup 64 > over 91 < and if 32 + then ;
variable lowc   variable upc   variable lup
: le-upper? ( a n -- f )
  0 lowc !  0 upc !
  0 ?do dup c@
    dup 96 > over 123 < and if 1 lowc ! then
    dup 64 > over 91 < and if 1 upc ! then
    drop 1+
  loop drop
  upc @ lowc @ 0= and ;
: le-insert-str ( a n -- )
  0 ?do dup c@ fmode @ 0= if lup @ if le-uc else le-lc then then le-insert 1+ loop drop ;

: le-bs
  ep @ 0= if exit then
  ea @ ep @ + dup 1- el @ ep @ - cmove
  ep @ -1 ep +!   -1 el +!   le-redraw ;
: le-del
  ep @ el @ = if exit then
  ea @ ep @ + 1+ dup 1- el @ ep @ - 1- cmove
  -1 el +!   ep @ le-redraw ;

: le-goto ( p -- )
  dup ep @ < if ep @ over - le-left else dup ep @ - le-right then ep ! ;
: le-back     ep @ 0= if exit then ep @ 1- le-goto ;
: le-forward  ep @ el @ < if ep @ 1+ le-goto then ;
: le-home   0 le-goto ;
: le-end    el @ le-goto ;
: le-kill-eol   ep @ el !   le-clear-eol ;
: le-kill-bol
  ea @ ep @ + ea @ el @ ep @ - cmove
  ep @ dup negate el +!   0 ep !   le-redraw ;
: le-clear-screen
  le-esc[ s" 2J" le-type   le-esc[ s" H" le-type
  ?arrow. le-line le-type   el @ ep @ - le-left ;

: le-set ( a n -- )   \ replace the whole line
  en @ min >r   ea @ r@ cmove
  ep @   r@ el !   r> ep !   le-redraw ;

( History: the last 64 lines, as count byte + text )
64 constant hist-max
create hist   hist-max input-limit 1+ * allot
create hist-save   input-limit 1+ allot
variable hist-n    ( lines added so far )
variable hist-i    ( how far back we are, 0 is the line being typed )
variable ha   variable hl

: hist-slot ( k -- a )   hist-max mod input-limit 1+ * hist + ;
: hist@ ( k -- a n )   hist-slot dup 1+ swap c@ ;
: hist-avail ( -- n )   hist-n @ hist-max min ;

: le-ieq ( a b n -- f )   \ same text, ignoring case
  begin dup while
    >r  over c@ le-fold  over c@ le-fold <>
    if 2drop rdrop 0 exit then
    1+ swap 1+ swap  r> 1-
  repeat drop 2drop -1 ;

: hist-add ( a n -- )
  hl ! ha !
  hl @ 0= if exit then
  hist-n @ if
    hist-n @ 1- hist@  hl @ = if ha @ hl @ le-ieq if exit then else drop then
  then
  hist-n @ hist-slot  hl @ over c!  1+ ha @ swap hl @ cmove
  1 hist-n +! ;

: le-up
  hist-i @ hist-avail < 0= if exit then
  hist-i @ 0= if le-line dup hist-save c! hist-save 1+ swap cmove then
  1 hist-i +!   hist-n @ hist-i @ - hist@ le-set ;
: le-down
  hist-i @ 0= if exit then
  -1 hist-i +!
  hist-i @ if hist-n @ hist-i @ - hist@ else hist-save 1+ hist-save c@ then
  le-set ;

( Tab completion: the words of the search order, or file names )
variable cb     ( executed for each word of the dictionary )
variable sink   ( executed for each candidate: a n -- )
variable pa   variable pl   ( the text to complete: the whole word, or its file name part )
create cand 64 allot   variable cl   variable cfirst   variable cn   variable cuniq
variable la

: le-vins ( voc -- )
  >r 'builtins begin dup >link while
    dup >params r@ = if dup cb @ execute then
    3 cells +
  repeat drop rdrop ;
: le-ins ( xt -- )   dup >flags BUILTIN_FORK and if cell+ @ le-vins else drop then ;
: le-each
  context begin dup @ while
    dup @ @  begin dup while dup le-ins dup cb @ execute >link repeat drop
    cell+
  repeat drop ;
: le-word-cb ( xt -- )   >name sink @ execute ;

: le-match? ( a n -- f )
  dup pl @ < if 2drop 0 exit then
  drop pa @ pl @ le-ieq ;
: le-common ( a n -- c )   \ length in common with cand
  cl @ min >r la !  0
  begin dup r@ < while
    dup la @ + c@ le-fold  over cand + c@ le-fold <> if rdrop exit then
    1+
  repeat rdrop ;
: le-cand ( a n -- )
  2dup le-match? 0= if 2drop exit then
  dup 63 > if 2drop exit then
  cn @ if
    dup >r le-common dup cl ! cfirst @ <> r> cfirst @ <> or if 0 cuniq ! then
  else
    dup cl ! dup cfirst ! cand swap cmove -1 cuniq !
  then
  1 cn +! ;
: le-print ( a n -- )
  dup 1+ line-pos + line-width > if le-cr 0 to line-pos then
  dup 1+ line-pos + to line-pos   le-type 32 le-emit ;

DEFINED? posix [IF]
( File names )
create fname 66 allot   variable fl   variable fdir   variable da   variable dn
create dpath 300 allot   variable dl
z" getenv" 1 sysfunc le-getenv
: le-dpath ( a n -- )   \ the directory typed, ~ expanded, "." when empty
  dn ! da !
  dn @ 0= if s" ." dpath swap cmove 1 dl ! exit then
  da @ c@ [char] ~ = if
    z" HOME" le-getenv ?dup if
      z>s dup dl !  dpath swap cmove
      da @ 1+ dpath dl @ + dn @ 1- cmove   dn @ 1- dl +!
      exit
    then
  then
  da @ dpath dn @ cmove   dn @ dl ! ;
: le-hidden? ( a n -- f )   \ a dot file, not wanted unless the typed name starts with a dot
  drop c@ [char] . <>  if 0 exit then
  pl @ 0= if -1 exit then   pa @ c@ [char] . <> ;
: le-entry ( ent -- )
  dup .d_type 4 = fdir !   .d_name z>s
  dup 62 > if 2drop exit then
  over c@ [char] . = over 1 = and if 2drop exit then
  2dup le-hidden? if 2drop exit then
  dup fl !  fname swap cmove
  fdir @ if [char] / fname fl @ + c! 1 fl +! then
  fname fl @ sink @ execute ;
: le-files ( dir-a dir-n -- )
  le-dpath  dpath dl @ open-dir if drop exit then
  begin dup readdir dup while le-entry repeat drop
  closedir drop ;
[ELSE]
: le-files ( dir-a dir-n -- )   2drop ;
[THEN]

( Which kind of completion )
: le-wstart ( -- n )   \ start of the word before the cursor
  ep @ begin
    dup 0= if exit then
    ea @ over + 1- c@ 33 < if exit then
    1-
  again ;
: le-space   \ a space after a completed word; if one follows already, step over it
  ep @ el @ < if ea @ ep @ + c@ 33 < if le-forward exit then then   32 le-insert ;

variable wsv   variable wlen   ( the word to complete: start and length )
: le-prevword ( -- a n )   \ the word before it
  wsv @
  begin dup if ea @ over + 1- c@ 33 < else 0 then while 1- repeat
  dup >r
  begin dup if ea @ over + 1- c@ 32 > else 0 then while 1- repeat
  ea @ over + r> rot - ;
: le-is? ( a n b m -- f )   rot over <> if 2drop drop 0 exit then le-ieq ;
: le-quoter? ( a n -- f )   \ s" z" r| r~
  2 <> if drop 0 exit then
  dup 1+ c@ >r  c@ le-uc
  dup [char] S = over [char] Z = or  r@ 34 = and  if rdrop drop -1 exit then
  [char] R =  r@ [char] | = r> [char] ~ = or and ;
: le-filectx? ( a n -- f )   \ a word that takes a file name
  2dup s" include" le-is? if 2drop -1 exit then
  2dup s" needs" le-is? if 2drop -1 exit then
  2dup s" required" le-is? if 2drop -1 exit then
  2dup s" included" le-is? if 2drop -1 exit then
  le-quoter? ;
: le-pathy? ( -- f )   \ looks like a path: starts with . or ~, or has a /
  ea @ wsv @ + c@ dup [char] . = swap [char] ~ = or
  wlen @ 0 ?do ea @ wsv @ + i + c@ [char] / = or loop ;

: le-split ( -- dir-a dir-n )   \ the word is dir/name: pa pl get the name
  wlen @ begin dup if ea @ wsv @ + over + 1- c@ [char] / <> else 0 then while 1- repeat
  ea @ wsv @ + swap
  2dup + pa !   dup wlen @ swap - pl ! ;
: le-collect ( -- )   \ send each candidate to sink
  fmode @ if
    le-split le-files
  else
    ea @ wsv @ + pa !   wlen @ pl !
    ['] le-word-cb cb !   le-each
  then ;

( The candidates to list are gathered, sorted and printed without duplicates )
8192 constant pool-size   256 constant pool-max
create pool pool-size allot   variable pool-used
create pool-ptr pool-max cells allot   variable pool-cnt
variable sk   variable sj
: le-pool ( a n -- )
  2dup le-match? 0= if 2drop exit then
  pool-cnt @ pool-max = if 2drop exit then
  dup 1+ pool-used @ + pool-size > if 2drop exit then
  dup >r  dup pool pool-used @ + c!  pool pool-used @ + 1+ swap cmove
  pool pool-used @ +  pool-ptr pool-cnt @ cells + !   1 pool-cnt +!
  r> 1+ pool-used +! ;
: pool@ ( p -- a n )   dup 1+ swap c@ ;
: le-cmp ( p q -- -1|0|1 )   >r pool@ r> pool@ compare ;
: le-sort
  pool-cnt @ 1 ?do
    pool-ptr i cells + @ sk !   i sj !
    begin
      sj @ 0 > if sk @ pool-ptr sj @ 1- cells + @ le-cmp -1 = else 0 then
    while
      pool-ptr sj @ 1- cells + @  pool-ptr sj @ cells + !   -1 sj +!
    repeat
    sk @ pool-ptr sj @ cells + !
  loop ;
: le-list
  0 pool-cnt !  0 pool-used !  ['] le-pool sink !   le-collect   le-sort
  0 sk !
  pool-cnt @ 0 ?do
    pool-ptr i cells + @
    sk @ if  dup sk @ le-cmp if dup pool@ le-print then  else dup pool@ le-print then
    sk !
  loop ;

: le-complete1
  0 fmode !
  le-wstart wsv !   ep @ wsv @ - wlen !
  le-prevword le-filectx? if -1 fmode ! else wlen @ 0= if exit then then
  fmode @ 0= if ea @ wsv @ + wlen @ le-upper? lup ! then
  0 cn !   ['] le-cand sink !   le-collect
  cn @ 0= fmode @ 0= and if
    le-pathy? if -1 fmode !  0 cn !  le-collect then
  then
  cn @ 0= if 7 le-emit exit then
  cl @ pl @ > if
    cand pl @ + cl @ pl @ - le-insert-str
    cuniq @ fmode @ 0= and if le-space then
    exit
  then
  cuniq @ if fmode @ 0= if le-space then exit then
  le-cr 0 to line-pos   le-list   le-cr
  ?arrow. le-line le-type   el @ ep @ - le-left ;
: le-complete   le-complete1  0 fmode ! ;


( Escape sequences: ESC [ params final, or ESC O final )
variable lp   variable lf
: le-final
  lf @ [char] A = if le-up exit then
  lf @ [char] B = if le-down exit then
  lf @ [char] C = if le-forward exit then
  lf @ [char] D = if le-back exit then
  lf @ [char] H = if le-home exit then
  lf @ [char] F = if le-end exit then
  lf @ [char] ~ = if
    lp @ [char] 3 = if le-del exit then
    lp @ [char] 1 = lp @ [char] 7 = or if le-home exit then
    lp @ [char] 4 = lp @ [char] 8 = or if le-end exit then
  then ;
: le-escape
  key? 0= if exit then   key lp !
  lp @ [char] O = if key? 0= if exit then key dup lp ! lf ! le-final exit then
  lp @ [char] [ = 0= if exit then
  key? 0= if exit then   key dup lp !
  begin dup 64 < while
    drop key? 0= if exit then key
  repeat lf !
  le-final ;

: le-key ( c -- )
  dup 27 = if drop le-escape exit then
  dup 9 = if drop le-complete exit then
  dup 8 = over 127 = or if drop le-bs exit then
  dup 1 = if drop le-home exit then
  dup 5 = if drop le-end exit then
  dup 2 = if drop le-back exit then
  dup 6 = if drop le-forward exit then
  dup 4 = if drop le-del exit then
  dup 11 = if drop le-kill-eol exit then
  dup 21 = if drop le-kill-bol exit then
  dup 12 = if drop le-clear-screen exit then
  dup 16 = if drop le-up exit then
  dup 14 = if drop le-down exit then
  dup 31 > if le-insert exit then
  drop ;

: le-accept ( a n -- n )
  echo @ 0= le-tty? 0= or if accept exit then
  en !  ea !   0 el !  0 ep !  0 hist-i !  0 fmode !
  ?arrow.
  begin
    *key dup 13 = if
      drop le-cr le-line hist-add el @ exit
    then
    le-key
  again ;

' le-accept is edit-accept

only forth definitions
