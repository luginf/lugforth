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

( A small text editor for the terminal, in the spirit of WrithDeck: `ed file` )
( opens or creates a file. Plain text, with a light highlighting for Forth. )
( The code is a string compiled the first time `ed` is used, as the console )
( window is. It speaks ANSI only, so it runs in a terminal, in the window of )
( lugforth --gui, and over telnet. The string must hold no vertical bar. )

needs eval_copy.fs

\ Flag: the code of ed (the string ed-src) has been compiled.
0 value ed-loaded
\ ed-arg, ed-arg-n: the file name typed after ed (a buffer and its length).
create ed-arg 256 allot   0 value ed-arg-n
\ Runs the editor on the file a n. Set to ed-main when ed-src is compiled.
defer ed-go ( a n -- )

\ The source of the editor ed, a string compiled the first time ed is used.
: ed-src r|
only forth also internals also ansi
[defined] posix [if] also posix also termios [then]
vocabulary editor   also editor definitions

\ What a derived editor (writhdeck) replaces: the view, the moves, the keys
defer scroll   defer draw   defer draw-status   defer hl-scan   defer set-want
defer up-key   defer down-key   defer home-key   defer end-key
defer pre-key ( code -- code' )   defer wait-key ( -- code )   defer after-load
: same ;

( ---- The state: a text buffer, a cursor, an optional selection ---- )
0 value text   0 value cap   0 value len
0 value cur   -1 value anchor   0 value sticky   0 value shifted
0 value top   0 value left   0 value want
0 value modified   0 value hl-on   0 value hl-stale   0 value edits
0 value cbuf   0 value cbuf-cap
0 value fname   0 value flen   0 value msg   0 value mlen
0 value pbuf   0 value kwbuf   0 value onebyte   0 value ibuf
0 value lastfind   0 value lastfind-n
0 value clip   0 value clip-n   0 value clip-cap
0 value rows   0 value cols   0 value text-rows
0 value quit-asked   0 value done
0 value theme   -1 value wrap-on   72 value colw   500 value goal

: say ( a n -- )   78 min dup to mlen msg swap cmove ;
: need ( n -- )   \ room for n more bytes
  len + 1+ dup cap > if
    dup 2* 1024 max dup >r  text swap resize throw to text  r> to cap
  then drop ;
: bytes ( n -- a )   allocate throw ;
: b@ ( p -- c )   text + c@ ;
: cont? ( c -- f )   $c0 and $80 = ;
: lower ( c -- c' )   dup [char] A >= over [char] Z <= and if 32 + then ;
: space? ( c -- f )   33 < ;

( ---- Lines and columns ---- )
: bol ( p -- p' )   \ start of the line
  begin dup 0> if dup 1- b@ 10 <> else 0 then while 1- repeat ;
: eol ( p -- p' )   \ end of the line, before the newline
  begin dup len < if dup b@ 10 <> else 0 then while 1+ repeat ;
: next-line ( p -- p' )   eol dup len < if 1+ then ;
: prev-line ( p -- p' )   bol dup 0> if 1- bol then ;
: prev-char ( p -- p' )
  dup 0> if 1- begin dup 0> if dup b@ cont? else 0 then while 1- repeat then ;
: next-char ( p -- p' )
  dup len < if 1+ begin dup len < if dup b@ cont? else 0 then while 1+ repeat then ;

4 constant tabw
: tab-col ( col -- col' )   tabw / 1+ tabw * ;
: step-col ( col c -- col' )
  dup 9 = if drop tab-col exit then
  cont? if exit then 1+ ;
: column ( p -- n )   \ display column of p in its line
  dup bol { p s }  0 { c }
  begin s p < while  c s b@ step-col to c  s 1+ to s  repeat
  c ;
: at-col ( start col -- p )   \ offset on the line that reaches col, or the end of the line
  { s want-c }  0 { c }
  begin
    s len < if s b@ 10 <> else 0 then
    c want-c < and
  while
    c s b@ step-col to c   s 1+ to s
  repeat
  begin s len < if s b@ cont? else 0 then while s 1+ to s repeat
  s ;
: line-no ( p -- n )   1 swap 0 ?do i b@ 10 = if 1+ then loop ;

( ---- Highlighting of Forth: a class per byte in cbuf ---- )
\ 0 text, 1 comment, 2 string, 3 word of the language, 4 number, 5 name being defined
: hl-fill ( from to class -- )   { f t k }  t f ?do k cbuf i + c! loop ;
: tok-end ( p -- p' )   begin dup len < if dup b@ space? 0= else 0 then while 1+ repeat ;
: tok-start ( p -- p' )   begin dup len < if dup b@ space? else 0 then while 1+ repeat ;
: skip-to ( p ch -- p' )   \ just after the next ch, or the end
  { ch }
  begin dup len < if dup b@ ch <> else 0 then while 1+ repeat
  dup len < if 1+ then ;
: hl-span ( p ch k -- q )   \ color from p up to and including the next ch
  { p ch k }  p ch skip-to { q }  p q k hl-fill  q ;
: kws s"  if else then begin until while repeat again do ?do loop +loop leave unloop case of endof endcase exit recurse does> create variable value constant defer is to ; : immediate postpone literal [ ] ['] [char] needs include included required marker 2variable " ;
: kw-copy ( a n -- n' )   \ the token in lower case between two spaces, in kwbuf
  38 min dup >r  32 kwbuf c!  kwbuf 1+ swap cmove  32 kwbuf r@ 1+ + c!
  kwbuf 1+  r@ 0 ?do dup c@ lower over c! 1+ loop drop
  r> ;
: kw? ( a n -- f )   kw-copy 2 + >r kws kwbuf r> search nip nip ;
0 value hexmode
: digit? ( c -- f )
  lower  dup [char] 0 >= over [char] 9 <= and  swap
  hexmode if dup [char] a >= swap [char] f <= and or else drop then ;
: num? ( a n -- f )
  dup 0= if 2drop 0 exit then
  0 to hexmode
  over c@ [char] $ = if -1 to hexmode then
  over c@ [char] - = hexmode or if 1 /string then
  dup 0= if 2drop 0 exit then
  -1 { ok }
  0 ?do dup i + c@ digit? 0= if 0 to ok then loop
  drop ok ;
: tok= ( a n s m -- f )   compare 0= ;
: quote-token? ( a n -- f )   \ ." s" z" c" abort" s-backslash-quote
  dup 2 < if 2drop 0 exit then
  + 1- c@ 34 = ;
: pair? ( a n ch -- f )   \ r followed by ch
  >r 2 <> if drop rdrop 0 exit then
  dup c@ [char] r = swap 1+ c@ r> = and ;
0 value want-name
: hl-token ( p -- p' )
  { p }  p tok-end { e }  text p + e p - { a n }
  a n s" \" tok= if  p 10 1 hl-span exit then
  a n s" (" tok= if  p [char] ) 1 hl-span exit then
  a n quote-token? if  p e 3 hl-fill  e 1+ len min 34 2 hl-span exit then
  a n 124 pair? if  p e 3 hl-fill  e 1+ len min 124 2 hl-span exit then
  a n [char] ~ pair? if  p e 3 hl-fill  e 1+ len min [char] ~ 2 hl-span exit then
  want-name if 5 else a n kw? if 3 else a n num? if 4 else 0 then then then { k }
  a n s" :" tok= to want-name
  p e k hl-fill  e ;
: cbuf-ready ( -- )   \ a cleared class per byte
  len 1+ cbuf-cap > if
    cbuf if cbuf free drop then
    len 1+ 2* 1024 max dup to cbuf-cap allocate throw to cbuf
  then
  cbuf cbuf-cap 0 fill ;
: forth-scan
  hl-on 0= if exit then
  cbuf-ready
  0 to want-name  0 { p }
  begin p tok-start dup len < while  hl-token to p  repeat
  drop  0 to hl-stale ;

( ---- The file ---- )
: set-name ( a n -- )   255 min dup to flen fname swap cmove ;
: ext? ( a n -- f )   \ the name ends with this string
  { a n }  n flen > if 0 exit then
  fname flen + n -  n  a n  compare 0= ;
: forth-name? ( -- f )
  s" .fs" ext?  s" .fth" ext? or  s" .4th" ext? or  s" .forth" ext? or ;
: load-file ( -- )
  0 to len  0 to cur  0 to top  0 to left
  flen 0= if exit then
  fname flen r/o open-file if drop s" New file" say exit then
  { fh }
  fh file-size throw { n }
  n need
  text n fh read-file throw to len
  fh close-file throw ;
: save-file ( -- )
  fname flen w/o create-file throw { fh }
  text len fh write-file throw
  fh close-file throw
  0 to modified ;

( ---- Undo: a log of the changes, the newest at the end ---- )
\ a record is its bytes, then the count, the position and the kind (1 insert, 2 delete)
32768 constant log-max
0 value log   0 value log-n   0 value rlog   0 value rlog-n
: log-push ( kind pos a n -- )
  { kind pos a n }
  n 3 cells + log-max > if exit then
  n 3 cells + log-n + log-max > if 0 to log-n then
  a log log-n + n cmove
  n +to log-n
  n log log-n + !   pos log log-n + cell+ !   kind log log-n + 2 cells + !
  3 cells +to log-n ;
: log-pop ( -- kind pos a n )
  3 cells negate +to log-n
  log log-n + @ { n }  log log-n + cell+ @ { pos }  log log-n + 2 cells + @ { kind }
  n negate +to log-n
  kind pos log log-n + n ;
: swap-logs ( -- )   log rlog to log to rlog   log-n rlog-n to log-n to rlog-n ;
: rlog-push ( kind pos a n -- )   swap-logs log-push swap-logs ;
: rlog-pop ( -- kind pos a n )   swap-logs log-pop swap-logs ;
: log-extend ( c pos -- f )   \ add a typed byte to the last insert when it continues it
  { c pos }
  0 to rlog-n
  log-n 0= if 0 exit then
  log log-n + cell - @ 1 <> if 0 exit then
  log log-n + 3 cells - @ 3 cells + log-n + log-max > if 0 exit then
  log-pop { kind p a n }
  p n + pos <> if kind p a n log-push 0 exit then
  c a n + c!
  kind p a n 1+ log-push  -1 ;

: raw-insert ( a n pos -- )
  { a n pos }
  n need
  text pos + dup n + len pos - cmove>
  a text pos + n cmove
  n +to len  -1 to modified  -1 to hl-stale  1 +to edits ;
: raw-delete ( pos n -- )
  { pos n }
  text pos + n + text pos + len pos - n - cmove
  n negate +to len  -1 to modified  -1 to hl-stale  1 +to edits ;
: put ( a n pos -- )   \ insert, with undo
  { a n pos }  0 to rlog-n  1 pos a n log-push  a n pos raw-insert ;
: cut ( pos n -- )   \ delete, with undo
  { pos n }  0 to rlog-n  2 pos text pos + n log-push  pos n raw-delete ;
: undo
  log-n 0= if s" Nothing to undo" say exit then
  log-pop { kind pos a n }
  kind pos a n rlog-push
  kind 1 = if pos n raw-delete pos to cur else a n pos raw-insert pos n + to cur then
  -1 to anchor ;
: redo
  rlog-n 0= if s" Nothing to redo" say exit then
  rlog-pop { kind pos a n }
  kind pos a n log-push
  kind 1 = if a n pos raw-insert pos n + to cur else pos n raw-delete pos to cur then
  -1 to anchor ;

( ---- Selection and clipboard ---- )
: has-sel? ( -- f )   anchor 0< 0=  anchor cur <> and ;
: sel-lo ( -- p )   anchor cur min ;
: sel-hi ( -- p )   anchor cur max ;
: kill-sel ( -- )
  has-sel? if sel-lo sel-hi over - cut  sel-lo to cur then
  -1 to anchor ;
: copy-sel ( -- )
  has-sel? 0= if s" Nothing selected" say exit then
  sel-hi sel-lo - to clip-n
  clip-n clip-cap > if
    clip if clip free drop then
    clip-n 2* dup to clip-cap allocate throw to clip
  then
  text sel-lo + clip clip-n cmove
  s" Copied" say ;
: cut-sel ( -- )   copy-sel kill-sel ;
: paste ( -- )
  clip-n 0= if exit then
  kill-sel clip clip-n cur put  clip-n +to cur ;

( ---- Editing ---- )
: plain-want ( -- )   cur column to want ;
: ins-byte ( c -- )
  kill-sel  onebyte c!
  onebyte c@ 10 <> if onebyte c@ cur log-extend else 0 then
  if onebyte 1 cur raw-insert else onebyte 1 cur put then
  1 +to cur  set-want ;
: indent-of ( -- n )   \ blanks at the start of the current line, up to the cursor
  cur bol { s }  0 { n }
  begin s cur < if s b@ dup 32 = swap 9 = or else 0 then while  s 1+ to s  n 1+ to n repeat
  n ;
: newline ( -- )
  kill-sel
  indent-of { n }
  10 ibuf c!  cur bol { s }
  n 0 ?do s i + b@ ibuf 1+ i + c! loop
  ibuf n 1+ cur put  n 1+ +to cur  set-want ;
: tab-key ( -- )   32 ins-byte 32 ins-byte ;
: backspace ( -- )
  has-sel? if kill-sel exit then
  cur 0= if exit then
  cur prev-char { p }  p cur p - cut  p to cur  set-want ;
: delete ( -- )
  has-sel? if kill-sel exit then
  cur len = if exit then
  cur dup next-char over - cut ;

( ---- Moving ---- )
: begin-move ( -- )   \ a move extends the selection with shift or after Ctrl+K
  shifted if anchor 0< if cur to anchor then
  else sticky 0= if -1 to anchor then then ;
: go ( p -- )   begin-move to cur  set-want ;
: left-key    cur prev-char go ;
: right-key   cur next-char go ;
: plain-home    cur bol go ;
: plain-end     cur eol go ;
: plain-up
  begin-move
  cur bol 0> if cur prev-line want at-col to cur then ;
: plain-down
  begin-move
  cur eol len = if len to cur else cur next-line want at-col to cur then ;
: page-up     text-rows 1- 0 ?do up-key loop ;
: page-down   text-rows 1- 0 ?do down-key loop ;
: word-left ( -- )
  cur { p }
  begin p 0> if p 1- b@ space? else 0 then while p 1- to p repeat
  begin p 0> if p 1- b@ space? 0= else 0 then while p 1- to p repeat
  p go ;
: word-right ( -- )
  cur { p }
  begin p len < if p b@ space? 0= else 0 then while p 1+ to p repeat
  begin p len < if p b@ space? else 0 then while p 1+ to p repeat
  p go ;
: goto-line ( n -- )
  0 { p }
  1- 0 max 0 ?do p next-line to p loop
  p go ;

( ---- Color themes: the 256-color code of each class, one byte each, class 0 lowest ---- )
\ the classes: 0 text, 1 comment, 2 string, 3 word of the language, 4 number, 5 name; 0 is the default color
5 constant n-themes
: theme-name ( n -- a n )
  dup 0 = if drop s" default" exit then
  dup 1 = if drop s" colorforth" exit then
  dup 2 = if drop s" light" exit then
  dup 3 = if drop s" solarized" exit then
  drop s" mono" ;
: theme-find ( a n -- idx )   \ -1 when there is no such theme
  n-themes 0 ?do 2dup i theme-name compare 0= if 2drop i unloop exit then loop
  2drop -1 ;
: theme-pack ( -- n )
  theme 0 = if $728D4BB4F400 exit then
  theme 1 = if $C4C9E233FF2E exit then
  theme 2 = if $1C5A1982F400 exit then
  theme 3 = if $A6214025F500 exit then
  0 ;
: theme-col ( class -- n )   theme-pack swap 8 * rshift $ff and ;
: theme-dim ( -- n )
  theme 1 = if 238 exit then
  theme 2 = if 250 exit then
  240 ;

( ---- The screen ---- )
0 value dry   0 value outcol   0 value cur-attr   0 value dcol   0 value dp   0 value ch
0 value stop   0 value shown   0 value rp   0 value crow   0 value ccol
0 value s-lo   0 value s-hi

: refresh-size ( -- )
  ['] form catch if 24 80 then
  dup 20 < if drop 80 then to cols
  dup 4 < if drop 24 then to rows
  rows 1- to text-rows ;
: class-fg ( k -- )   theme-col dup if fg else drop esc ." [39m" then ;
: set-attr ( a -- )   \ low 3 bits the class, 8 the selection
  dup cur-attr = if drop exit then
  dup to cur-attr  normal
  dup 7 and class-fg
  8 and if esc ." [7m" then ;
: attr-at ( p -- a )
  dup s-lo >= over s-hi < and if 8 else 0 then
  swap hl-on if cbuf + c@ else drop 0 then + ;
: cell-out ( c -- )   \ one cell at the column dcol, if it is on the screen
  dcol left >= dcol left cols + < and if emit else drop then
  1 +to dcol ;
: draw-line ( p -- )
  to dp  0 to dcol  -1 to cur-attr  0 to shown
  begin
    dp len < if dp b@ 10 <> else 0 then
    dcol left cols + < and
  while
    dp b@ to ch
    dp attr-at set-attr
    ch 9 = if
      dcol tab-col to stop
      begin dcol stop < while 32 cell-out repeat
    else ch cont? if
      shown if ch emit then
    else
      dcol left >= dcol left cols + < and to shown
      ch 32 < ch 127 = or if [char] . to ch then
      ch cell-out
    then then
    1 +to dp
  repeat
  normal  clear-to-eol ;
: draw-row ( r -- )
  0 swap at-xy
  rp len > if clear-to-eol exit then
  rp draw-line
  rp eol dup len = if drop len 1+ else 1+ then to rp ;
: stype ( a n -- )   cols outcol - 0 max min dup +to outcol dry if 2drop else type then ;
: num-type ( n -- )   <# #s #> stype ;
: name-tail ( -- a n )   flen 40 min >r fname flen + r@ - r> ;
: plain-status ( -- )
  0 text-rows at-xy  esc ." [7m"  0 to outcol
  s"  " stype  flen 0= if s" (new)" else name-tail then stype
  modified if s"  *" stype then
  s"   Ln " stype cur line-no num-type  s" , Col " stype cur column 1+ num-type
  s"   " stype
  mlen if msg mlen stype else s" ^S save  ^Q quit  ^F find  ^G line  ^Z undo  ^K mark" stype then
  cols outcol - 0 max 0 ?do 32 emit loop
  normal ;
: plain-scroll ( -- )
  cur bol top < if cur bol to top then
  0 { n }  top { p }
  begin p cur bol < while p next-line to p  1 +to n repeat
  n text-rows >= if
    n text-rows - 1+ 0 ?do top next-line to top loop
    text-rows 1- to n
  then
  n to crow
  cur column to ccol
  ccol left < if ccol to left then
  ccol left - cols >= if ccol cols - 1+ to left then ;
: plain-draw ( -- )
  hl-stale if hl-scan then
  has-sel? if sel-lo sel-hi else 0 0 then to s-hi to s-lo
  hide
  top to rp
  text-rows 0 ?do i draw-row loop
  draw-status
  ccol left - crow at-xy
  show ;

( ---- Prompts and commands ---- )
: ask ( a n -- len flag )   \ the length of the answer in pbuf
  { a n }  0 { l }
  begin
    0 text-rows at-xy  esc ." [7m"  a n type  pbuf l type  normal  clear-to-eol
    key
    dup 13 = over 10 = or if drop l -1 exit then
    dup 27 = if drop 0 0 exit then
    dup 127 = over 8 = or if drop l 1- 0 max to l else
      dup 31 > l 70 < and if pbuf l + c! 1 +to l else drop then
    then
  again ;
: to-number ( a n -- n )
  0 { v }
  0 ?do dup i + c@ dup [char] 0 >= over [char] 9 <= and if [char] 0 - v 10 * + to v else drop then loop
  drop v ;
: ask-goto
  s" Go to line: " ask if pbuf swap to-number goto-line else drop then ;
: match? ( p -- f )   \ the search text is at p, ignoring case
  dup lastfind-n + len > if drop 0 exit then
  -1 { ok }
  lastfind-n 0 ?do dup i + b@ lower lastfind i + c@ lower <> if 0 to ok then loop
  drop ok ;
: search-from ( p -- p' )   \ the next match at or after p, or -1
  begin dup len < if dup match? 0= else 0 then while 1+ repeat
  dup len < if exit then drop -1 ;
: find-next ( -- )
  lastfind-n 0= if s" Nothing to find" say exit then
  cur search-from { p }
  p 0< if 0 search-from to p then
  p 0< if s" Not found" say exit then
  p to anchor  p lastfind-n + to cur  set-want ;
: ask-find
  s" Find: " ask 0= if drop exit then
  dup 0= if drop find-next exit then
  dup to lastfind-n  pbuf lastfind rot cmove
  find-next ;
: ask-save
  s" Save as: " ask 0= if drop 0 exit then
  dup 0= if drop 0 exit then
  pbuf swap set-name -1 ;
: do-save ( -- )
  flen 0= if ask-save 0= if exit then then
  ['] save-file catch if s" Cannot save" say else s" Saved" say then ;
: try-quit
  modified quit-asked 0= and if
    s" Unsaved changes: Ctrl+Q again to quit, Ctrl+S to save" say  -1 to quit-asked  exit
  then
  -1 to done ;

( ---- Settings: an ini file in the format of WrithDeck, in the directory of lugforth.ini ---- )
( `key = value`, `[section]` lines ignored, `#` and `%` comments, yes/no booleans. )
( ed reads and writes editor.ini; writhdeck.fs does the same with writhdeck.ini. )
[defined] posix [if]
z" getenv" 1 sysfunc ed-getenv
z" mkdir" 2 sysfunc ed-mkdir
0 value inip   0 value inil   0 value fhs
: inip+ ( a n -- )   dup >r inip inil + swap cmove  r> +to inil ;
: ini-dir ( -- )
  0 to inil
  z" LUGFORTH_HOME" ed-getenv ?dup if z>s inip+ exit then
  z" HOME" ed-getenv ?dup if z>s inip+ s" /.local/share/lugforth" inip+ exit then
  z" APPDATA" ed-getenv ?dup if z>s inip+ s" /lugforth" inip+ exit then
  s" ." inip+ ;
: mk-dirs ( -- )
  0 inip inil + c!
  inil 1 ?do
    inip i + c@ [char] / = if  0 inip i + c!  inip 493 ed-mkdir drop  [char] / inip i + c!  then
  loop
  inip 493 ed-mkdir drop ;
[else]   \ the board: the ini files are in the root of the disk, no directory to make
0 value inip   0 value inil   0 value fhs
: inip+ ( a n -- )   dup >r inip inil + swap cmove  r> +to inil ;
: ed-mkdir ( z mode -- n )   drop [ also rpi ] z>s mkdir [ previous ] ;
: ini-dir ( -- )   0 to inil ;
: mk-dirs ( -- ) ;
[then]
defer ini-kv ( ka kn va vn -- )     \ one `key = value` of the file
defer ini-body ( -- )               \ writes the file, with w+ wn eol+
defer ini-defname ( -- a n )        \ the name of the file when none was found
defer ini-load   defer ini-save
0 value inic   0 value inicn       \ the file the settings came from, kept to write it back
: ini-init ( -- )   inip 0= if 400 bytes to inip then ;
: inic! ( a n -- )   inic 0= if 400 bytes to inic then  dup to inicn  inic swap cmove ;
: ini-in-dir ( a n -- a' n' )   ini-dir s" /" inip+ inip+  inip inil ;
: ini-target ( -- a n )   inicn if inic inicn exit then  ini-defname ini-in-dir ;
: trim ( a n -- a' n' )   \ without the blanks around
  begin dup 0> if over c@ 33 < else 0 then while 1 /string repeat
  begin dup 0> if 2dup + 1- c@ 33 < else 0 then while 1- repeat ;
: no-comment ( a n -- a n' )   \ cut at a blank followed by # or %
  dup 2 < if exit then
  { a n }  n  n 1- 0 ?do
    a i + c@ 33 <  a i + 1+ c@ dup [char] # = swap [char] % = or  and if drop i leave then
  loop  a swap ;
: ini-line ( a n -- )
  trim { a n }  -1 { eq }  0 { c }
  n 0= if exit then
  a c@ to c
  c [char] # = c [char] % = or c [char] [ = or c [char] = = or if exit then
  n 0 ?do a i + c@ [char] = = if i to eq leave then loop
  eq 0< if exit then
  a eq trim
  a eq 1+ + n eq - 1- trim no-comment trim
  ini-kv ;
: ini-text ( b n -- )
  { b n }  0 { s }  0 { e }
  begin s n < while
    s begin dup n < if b over + c@ 10 <> else 0 then while 1+ repeat to e
    b s +  e s -  ini-line
    e 1+ to s
  repeat ;
: ini-read ( a n -- flag )   \ reads the file; false when it cannot be opened
  r/o open-file if drop 0 exit then  { fh }
  fh file-size throw { sz }
  sz 1+ bytes { b }
  b sz fh read-file throw { got }
  fh close-file drop
  b got ini-text  b free drop  -1 ;
: w+ ( a n -- )   fhs write-file drop ;
: wn ( n -- )   <# #s #> w+ ;
: eol+ ( -- )   10 onebyte c!  onebyte 1 w+ ;
: wl ( a n -- )   w+ eol+ ;
: key= ( a n -- )   w+ s"  = " w+ ;
: wyes ( f -- )   if s" yes" else s" no" then w+ ;
: yes? ( a n -- f )
  2dup s" yes" compare 0= if 2drop -1 exit then
  2dup s" 1" compare 0= if 2drop -1 exit then
  2dup s" true" compare 0= if 2drop -1 exit then
  s" on" compare 0= ;
: ed-defname ( -- a n )   s" editor.ini" ;
: ed-ini-kv ( ka kn va vn -- )
  { ka kn va vn }
  ka kn s" theme" compare 0=  ka kn s" scheme" compare 0=  or if va vn theme-find dup 0< if drop else to theme then exit then
  ka kn s" width" compare 0=  ka kn s" limit_width" compare 0=  or if va vn to-number 30 max 300 min to colw exit then
  ka kn s" goal" compare 0=  ka kn s" word_goal" compare 0=  or if va vn to-number to goal exit then
  ka kn s" wrap" compare 0= if va vn yes? to wrap-on then ;
: ed-ini-body ( -- )
  s" = ed configuration =" wl
  s" % The format of WrithDeck (writhdeck.ini); writhdeck keeps its own settings in writhdeck.ini." wl
  eol+  s" = editor =" wl  s" [editor]" wl
  s" % scheme: default colorforth light solarized mono" wl
  s" scheme" key= theme theme-name wl
  eol+  s" = behaviour =" wl  s" [behaviour]" wl
  s" limit_width" key= colw wn eol+
  s" word_goal" key= goal wn eol+
  s" wrap" key= wrap-on wyes eol+ ;
: ed-ini-load ( -- )
  ini-init  0 to inicn
  ed-defname ini-in-dir 2dup ini-read if inic! else 2drop then ;
: ed-ini-save ( -- )
  ini-init
  inicn 0= if ini-dir mk-dirs then
  ini-target w/o create-file if drop exit then  to fhs
  ini-body
  fhs close-file drop ;
' ed-ini-kv is ini-kv   ' ed-ini-body is ini-body   ' ed-defname is ini-defname
' ed-ini-load is ini-load   ' ed-ini-save is ini-save
: next-theme ( -- )
  theme 1+ n-themes mod to theme  ini-save
  theme theme-name { a n }
  s" Theme: " { p l }
  p msg l cmove  a msg l + n cmove  l n + to mlen ;

[defined] posix [if]
0 value saved-tio
: editor-raw ( -- )
  saved-tio 0= if sizeof(termios) bytes to saved-tio then
  stdin isatty if saved-tio termios@ then   \ the mode the prompt works in, to give back
  raw-mode
  stdin isatty if
    new-termios sl@ $400 invert and  new-termios l!
    new-termios .c_lflag sl@ 1 invert and $8000 invert and  new-termios .c_lflag l!
    new-termios termios!
  then ;
: restore-tty ( -- )
  delay-mode
  stdin isatty if saved-tio termios! then ;
[else]   \ the board: the console is the terminal, nothing to switch
: editor-raw ( -- ) ;
: restore-tty ( -- ) ;
[then]
: run-file ( -- )   \ include the saved file, then come back
  flen 0= modified or if s" Save the file first (Ctrl+S)" say exit then
  terminal-restore  normal  show  restore-tty  page
  fname flen ['] included catch { err }
  cr err if ." Error " err . else ." Done" then  ."  - press a key"
  key drop
  editor-raw  terminal-save  page ;

( ---- Keys ---- )
256 constant k-up   257 constant k-down   258 constant k-left   259 constant k-right
260 constant k-home   261 constant k-end   262 constant k-pgup   263 constant k-pgdn
264 constant k-del   265 constant k-f3   266 constant k-cleft   267 constant k-cright
268 constant k-chome   269 constant k-cend
0 value p1   0 value p2   0 value pn
: more? ( -- f )   key? ;
: esc-final ( c -- code )   \ a final byte of an escape sequence, with p1 and p2
  p2 1- 1 and 0<> to shifted
  dup [char] A = if drop k-up exit then
  dup [char] B = if drop k-down exit then
  dup [char] C = if drop p2 5 >= p2 8 <= and if k-cright else k-right then exit then
  dup [char] D = if drop p2 5 >= p2 8 <= and if k-cleft else k-left then exit then
  dup [char] H = if drop p2 5 >= p2 8 <= and if k-chome else k-home then exit then
  dup [char] F = if drop p2 5 >= p2 8 <= and if k-cend else k-end then exit then
  dup [char] R = if drop k-f3 exit then
  [char] ~ = if
    p1 1 = p1 7 = or if k-home exit then
    p1 4 = p1 8 = or if k-end exit then
    p1 3 = if k-del exit then
    p1 5 = if k-pgup exit then
    p1 6 = if k-pgdn exit then
    p1 13 = if k-f3 exit then
  then
  0 ;
: read-escape ( -- code )
  more? 0= if 27 exit then
  key dup [char] O = if drop key 0 to p1 1 to p2 esc-final exit then
  [char] [ <> if 0 exit then
  0 to p1  1 to p2  0 to pn
  begin
    key dup [char] 0 >= over [char] 9 <= and
  while
    [char] 0 -  pn if p2 10 * + to p2 else p1 10 * + to p1 then
  repeat
  dup [char] ; = if drop 1 to pn 0 to p2
    begin key dup [char] 0 >= over [char] 9 <= and while [char] 0 - p2 10 * + to p2 repeat
  then
  esc-final ;
: read-key ( -- code )
  0 to shifted
  key dup 27 = if drop read-escape then ;

: handle ( code -- )
  pre-key dup 0= if drop exit then
  dup 17 <> if 0 to quit-asked then
  0 to mlen
  dup k-up = if drop up-key exit then
  dup k-down = if drop down-key exit then
  dup k-left = if drop left-key exit then
  dup k-right = if drop right-key exit then
  dup k-home = if drop home-key exit then
  dup k-end = if drop end-key exit then
  dup k-pgup = if drop page-up exit then
  dup k-pgdn = if drop page-down exit then
  dup k-del = if drop delete exit then
  dup k-f3 = if drop find-next exit then
  dup k-cleft = if drop word-left exit then
  dup k-cright = if drop word-right exit then
  dup k-chome = if drop 0 go exit then
  dup k-cend = if drop len go exit then
  dup 27 = if drop -1 to anchor 0 to sticky exit then
  dup 13 = over 10 = or if drop newline exit then
  dup 9 = if drop tab-key exit then
  dup 127 = over 8 = or if drop backspace exit then
  dup 1 = if drop home-key exit then
  dup 5 = if drop end-key exit then
  dup 3 = if drop copy-sel exit then
  dup 24 = if drop cut-sel exit then
  dup 22 = if drop paste exit then
  dup 11 = if drop sticky 0= to sticky  sticky if cur to anchor then exit then
  dup 19 = if drop do-save exit then
  dup 17 = if drop try-quit exit then
  dup 6 = if drop ask-find exit then
  dup 14 = if drop find-next exit then
  dup 7 = if drop ask-goto exit then
  dup 26 = if drop undo exit then
  dup 25 = if drop redo exit then
  dup 20 = if drop next-theme exit then
  dup 18 = if drop run-file exit then
  dup 12 = if drop exit then
  dup 31 > over 256 < and if ins-byte exit then
  drop ;

( ---- Running ---- )
: ed-init ( -- )
  256 bytes to fname  80 bytes to msg  128 bytes to pbuf  64 bytes to kwbuf
  4 bytes to onebyte  300 bytes to ibuf  80 bytes to lastfind
  log-max 3 cells + bytes to log   log-max 3 cells + bytes to rlog   0 to rlog-n
  4096 bytes to text  4096 to cap  0 to len
  0 to flen  0 to mlen  0 to lastfind-n  0 to log-n
  0 to cbuf-cap  0 to cbuf  0 to clip-n  0 to clip-cap  0 to clip
  -1 to anchor  0 to sticky  0 to modified  0 to done  0 to quit-asked
  0 to cur  0 to top  0 to left  0 to want  -1 to hl-stale ;
: ed-free ( -- )
  fname free drop  msg free drop  pbuf free drop  kwbuf free drop
  onebyte free drop  ibuf free drop  lastfind free drop  log free drop  rlog free drop  text free drop
  cbuf if cbuf free drop then   clip if clip free drop then ;
: loop-keys ( -- )
  begin
    refresh-size scroll
    key? 0= if draw then
    wait-key handle
    done
  until ;
: ed-main ( a n -- )
  ed-init
  ini-load  set-name  load-file
  forth-name? to hl-on  after-load
  editor-raw  terminal-save  page
  ['] loop-keys catch { err }
  terminal-restore  normal  show  restore-tty  page
  ed-free
  err throw ;

' ed-main is ed-go
: ed-plain ( -- )   \ the views and keys of the plain editor
  ['] plain-scroll is scroll   ['] plain-draw is draw   ['] plain-status is draw-status
  ['] forth-scan is hl-scan   ['] plain-want is set-want
  ['] plain-up is up-key   ['] plain-down is down-key
  ['] plain-home is home-key   ['] plain-end is end-key
  ['] same is pre-key   ['] read-key is wait-key   ['] noop is after-load
  ['] ed-ini-kv is ini-kv   ['] ed-ini-body is ini-body   ['] ed-defname is ini-defname   ['] ed-ini-load is ini-load ;
ed-plain
[defined] posix [if] only forth also internals definitions [else] only forth definitions [then]
| ;

: ed ( "file" -- )   \ edit a file; no name opens an empty text
  bl parse 255 min dup to ed-arg-n  ed-arg swap cmove
  ed-loaded 0= if  ed-src eval-copy  -1 to ed-loaded  then
  ed-arg ed-arg-n ed-go ;
