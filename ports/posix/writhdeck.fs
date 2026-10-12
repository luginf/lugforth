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

( writhdeck: the text editor `ed` for writing, after WrithDeck by the same author. )
( `writhdeck file` opens a text with soft wrapped lines in a centered column, a )
( focus mode that dims all but the current paragraph, Hemingway mode, word )
( counts with a daily goal, a stopwatch and a countdown, an outline of the )
( headings, replace, autosave. Esc opens a one line menu. Txt2tags and )
( Markdown are highlighted, Forth files as in `ed`. Like `ed` it is a string )
( compiled the first time, on top of the code of ed. No vertical bar inside. )

needs editor.fs

0 value wd-loaded
defer wd-go ( a n -- )

: wd-src r|
only forth also internals also ansi also posix also termios also editor definitions

( ---- Settings ---- )
0 value focus-on   0 value hem-on
0 value words0   0 value autosave   0 value last-auto
0 value words-now   -1 value words-edits
0 value timer-mode   0 value timer-t0   0 value timer-len   0 value alerted
0 value last-sec   0 value ww   0 value pad
0 value toc-pos   0 value toc-n   0 value toc-sel   0 value toc-top
0 value repl   0 value repl-n
0 value wrow

: now-ms ( -- n )   ms-ticks ;
: layout-update ( -- )
  wrap-on if colw cols 2 - min 20 max else cols then to ww
  wrap-on if cols ww - 2/ else 0 then to pad ;

( ---- Visual lines: a paragraph is cut into segments of at most ww columns ---- )
0 value sq   0 value sc   0 value sbrk   0 value sp   0 value sch
: seg ( p -- e next )   \ the end of the visual line starting at p, and where the next starts
  dup to sp  to sq  0 to sc  -1 to sbrk
  begin
    sq len < if sq b@ 10 <> else 0 then
  while
    sq b@ to sch
    sc sch step-col  dup ww > if
      drop
      sbrk sp > if sbrk dup exit then
      sq sp = if sq 1+ dup exit then
      sq dup exit
    then
    to sc
    sch 32 = if sq 1+ to sbrk then
    sq 1+ to sq
  repeat
  sq sq 1+ ;
0 value vs   0 value vp
: vstart ( p -- v )   \ start of the visual line holding p
  dup bol to vs  to vp
  begin vs seg nip dup vp <= while to vs repeat
  drop vs ;
: vnext ( v -- v' )   seg nip ;
: vprev ( v -- v' )   dup 0= if exit then 1- vstart ;
: vcol ( p -- n )   \ column of p in its visual line
  dup vstart { p s }  0 { c }
  begin s p < while  c s b@ step-col to c  s 1+ to s  repeat
  c ;
0 value ve   0 value vc
: vat ( v col -- p )   \ the position at that column of the visual line v
  to vc  dup to vp  dup to vs  seg drop to ve
  0 { c }
  begin vs ve < if c vc < else 0 then while
    c vs b@ step-col to c  vs 1+ to vs
  repeat
  vs ve = if ve len < if ve b@ 10 <> if ve 1- vp max to vs then then then
  begin vs len < if vs b@ cont? else 0 then while vs 1+ to vs repeat
  vs ;

: wrap-want ( -- )   wrap-on if cur vcol else cur column then to want ;
: wrap-up
  wrap-on 0= if plain-up exit then
  begin-move  cur vstart dup 0= if drop exit then  vprev want vat to cur ;
: wrap-down
  wrap-on 0= if plain-down exit then
  begin-move  cur vstart vnext dup len > if drop len to cur exit then  want vat to cur ;
: wrap-home   wrap-on if cur vstart go else cur bol go then ;
: wrap-end
  wrap-on 0= if cur eol go exit then
  cur vstart dup seg { st e nx }
  e nx = if e 1- st max else e then go ;

( ---- Words, session, goal ---- )
: count-words ( -- n )
  0 { n }  0 { inw }
  len 0 ?do i b@ space? if 0 to inw else inw 0= if 1 +to n -1 to inw then then loop
  n ;
: word-count ( -- n )
  edits words-edits <> if count-words to words-now  edits to words-edits then
  words-now ;

( ---- Timer ---- )
: timer-secs ( -- s )
  timer-mode 1 = if now-ms timer-t0 - 1000 / exit then
  timer-mode 2 = if timer-len now-ms timer-t0 - - 1000 / 0 max exit then
  0 ;
: two ( n -- )   dup 10 < if s" 0" stype then num-type ;
: mmss ( s -- )   60 /mod two  s" :" stype  two ;
: tick ( -- )
  now-ms 1000 / dup last-sec = if drop exit then  to last-sec
  timer-mode 2 = if
    timer-secs 0= alerted 0= and if
      -1 to alerted  7 emit  s" Time is up" say
    then
  then
  autosave if modified if flen if now-ms last-auto - 60000 > if
    ['] save-file catch if s" Autosave failed" say else s" Autosaved" say then
    now-ms to last-auto
  then then then then
  refresh-size scroll draw ;

( ---- Highlighting of txt2tags and Markdown: classes 1 comment, 3 heading, 4 list, 5 underline, 6 bold, 7 italic ---- )
: pair-at? ( p e c -- f )
  { p e c }  p 1+ e < if p b@ c = p 1+ b@ c = and else 0 then ;
: find-close ( p e c -- q )   \ the closing pair after the opener at p, or -1
  { p e c }  p 2 + { q }
  begin q 1+ e < if q b@ c <> q 1+ b@ c <> or else 0 then while q 1+ to q repeat
  q 1+ e < if q else -1 then ;
: try-mark ( p e c k -- p' )
  { p e c k }
  p e c pair-at? 0= if p exit then
  c [char] / = if p 0> if p 1- b@ [char] : = if p exit then then then
  p e c find-close { q }
  q 0< if p exit then
  p q 2 + k hl-fill  q 2 + ;
: md-inline ( s e -- )
  { s e }  s { p }
  begin p e < while
    p e [char] * 6 try-mark  dup p = if drop
      p e [char] / 7 try-mark  dup p = if drop
        p e [char] _ 5 try-mark  dup p = if drop p 1+ then
      then
    then
    to p
  repeat ;
: md-line ( s e -- )
  { s e }
  s e = if exit then
  s b@ { c }
  c [char] = = c [char] # = or if s e 3 hl-fill exit then
  c [char] % = if s e 1 hl-fill exit then
  c [char] - = c [char] + = or if
    s 1+ e < if s 1+ b@ 32 = if s s 1+ 4 hl-fill then then
  then
  s e md-inline ;
0 value mle
: md-scan ( -- )
  cbuf-ready
  0 { p }
  begin p len < while  p eol to mle   p mle md-line   mle 1+ to p  repeat
  0 to hl-stale ;
: wd-scan ( -- )
  hl-on 0= if exit then
  forth-name? if forth-scan exit then
  md-scan ;

( ---- The wrapped view ---- )
0 value fs   0 value fe   0 value de   0 value rnext
: dim? ( p -- f )   focus-on if dup fs < swap fe > or else drop 0 then ;
: wd-attr ( p -- a )   dup attr-at swap dim? if 16 + then ;
: md-style ( k -- )
  dup 5 = if drop esc ." [4m" exit then
  dup 6 = if drop esc ." [1m" exit then
  7 = if esc ." [3m" then ;
: wd-class ( k -- )
  forth-name? 0= over 4 > and if md-style exit then
  class-fg ;
: wd-set-attr ( a -- )
  dup cur-attr = if drop exit then
  dup to cur-attr  normal
  dup 16 and if theme-dim fg else dup 7 and wd-class then
  8 and if esc ." [7m" then ;
: draw-seg ( p e -- )
  to de  to dp  0 to dcol  -1 to cur-attr
  begin dp de < while
    dp b@ to ch
    dp wd-attr wd-set-attr
    ch 9 = if
      dcol tab-col to stop
      begin dcol stop < while 32 emit 1 +to dcol repeat
    else ch cont? if ch emit
    else
      ch 32 < ch 127 = or if [char] . to ch then
      ch emit 1 +to dcol
    then then
    1 +to dp
  repeat
  normal ;
: wrap-row ( r -- )
  to wrow
  0 wrow at-xy clear-to-eol
  rp len > if exit then
  pad wrow at-xy
  rp seg to rnext to de
  rp de draw-seg
  rnext to rp ;
: wrap-scroll ( -- )
  layout-update
  top vstart to top
  cur vstart { cv }
  cv top < if cv to top then
  focus-on if
    cv to top  text-rows 2/ 0 ?do top vprev to top loop
  then
  0 { n }  top { s }
  begin s cv < while  s vnext to s  1 +to n  repeat
  n text-rows >= if
    n text-rows - 1+ 0 ?do top vnext to top loop
    text-rows 1- to n
  then
  n to crow   pad cur vcol + to ccol
  0 to left ;
: wrap-draw ( -- )
  hl-stale if hl-scan then
  has-sel? if sel-lo sel-hi else 0 0 then to s-hi to s-lo
  cur bol to fs   cur eol to fe
  hide
  top to rp
  text-rows 0 ?do i wrap-row loop
  draw-status
  ccol crow at-xy
  show ;
: wd-scroll   wrap-on if wrap-scroll else plain-scroll then ;
: wd-draw     wrap-on if wrap-draw else plain-draw then ;

( ---- The status bar ---- )
: wd-status ( -- )
  0 text-rows at-xy  esc ." [7m"  0 to outcol
  s"  " stype  flen 0= if s" (new)" else name-tail then stype
  modified if s" *" stype then
  s"   W " stype word-count num-type
  s"  +" stype word-count words0 - num-type
  goal if s"  /" stype goal num-type  s"  " stype word-count 100 * goal / num-type s" %" stype then
  timer-mode if s"   " stype timer-secs mmss then
  focus-on if s"  [focus]" stype then
  hem-on if s"  [hem]" stype then
  autosave if s"  [auto]" stype then
  s"   " stype
  mlen if msg mlen stype else s" Esc: menu" stype then
  cols outcol - 0 max 0 ?do 32 emit loop
  normal ;

( ---- Overlays ---- )
: bar ( a n -- )   \ a line of text in reverse on the last row
  0 text-rows at-xy esc ." [7m"  0 to outcol  stype
  cols outcol - 0 max 0 ?do 32 emit loop  normal ;
0 value orow
: pl ( a n -- )   2 orow at-xy type  1 +to orow ;
: kv ( a n v -- )   >r 2 orow at-xy type  r> 24 orow at-xy n.  1 +to orow ;
: wait-any ( -- )   2 orow 1+ at-xy ." Press a key"  key drop ;
: help-page ( -- )
  page 0 to orow
  s" writhdeck" pl  1 +to orow
  s" Esc then a letter:" pl
  s"   w wrap   f focus (dims other paragraphs)   h Hemingway (no going back)" pl
  s"   t timer (s stopwatch, c countdown, x off)   o outline of headings" pl
  s"   s statistics   r replace all   g daily goal   a autosave   + - width   c theme   x run the file" pl  1 +to orow
  s" Keys as in ed: Ctrl+S save, Ctrl+Q quit, Ctrl+F find, Ctrl+N next, Ctrl+G line," pl
  s" Ctrl+Z undo, Ctrl+C X V copy cut paste, Ctrl+K mark, Shift+arrows select," pl
  s" Ctrl+T next color theme (default colorforth light solarized mono), Ctrl+R run." pl
  wait-any ;
: stat-page ( -- )
  page 0 to orow
  s" Statistics" pl  1 +to orow
  s" Words" word-count kv
  s" Characters" len kv
  s" This session" word-count words0 - kv
  s" Daily goal" goal kv
  s" Reading minutes" word-count 200 / 1+ kv
  0 0 { lines paras }
  len 0 ?do i b@ 10 = if 1 +to lines then loop
  s" Lines" lines 1+ kv
  wait-any ;

( ---- Outline: the headings, = Title = in txt2tags, # Title in Markdown ---- )
: heading? ( p -- f )   dup len < if b@ dup [char] = = swap [char] # = or else drop 0 then ;
: level ( p -- n )   dup b@ { c }  0 { n }  begin dup n + b@ c = while 1 +to n repeat drop n ;
: toc-collect ( -- )
  0 to toc-n  0 { p }
  begin p len < while
    p heading? toc-n 500 < and if p toc-pos toc-n cells + !  1 +to toc-n then
    p next-line to p
    p len = if exit then
  repeat ;
: toc-row ( i -- )
  { i }  i toc-top - 1+ { r }
  0 r at-xy clear-to-eol
  i toc-sel = if esc ." [7m" then
  toc-pos i cells + @ { p }  p level 1- 2 * 2 + r at-xy
  p dup level + { a }
  begin a b@ 32 = while a 1+ to a repeat
  a eol { e }
  begin e a > if e 1- b@ dup [char] = = swap 32 = or else 0 then while e 1- to e repeat
  e a - cols 6 - min text a + swap type
  normal ;
: toc-draw ( -- )
  page  2 0 at-xy  esc ." [7m" s" Outline  Enter go  Esc cancel" type normal
  toc-n toc-top - text-rows 1- min 0 ?do toc-top i + toc-row loop ;
: toc-show ( -- )
  toc-collect
  toc-n 0= if s" No headings" say exit then
  0 to toc-sel  0 to toc-top
  begin
    toc-sel toc-top < if toc-sel to toc-top then
    toc-sel toc-top - text-rows 2 - > if toc-sel text-rows 2 - - to toc-top then
    toc-draw
    read-key
    dup k-up = if drop toc-sel 1- 0 max to toc-sel 0 else
    dup k-down = if drop toc-sel 1+ toc-n 1- min to toc-sel 0 else
    dup k-pgup = if drop toc-sel 10 - 0 max to toc-sel 0 else
    dup k-pgdn = if drop toc-sel 10 + toc-n 1- min to toc-sel 0 else
    dup 13 = over 10 = or if drop toc-pos toc-sel cells + @ go -1 else
    27 = if -1 else 0 then then then then then then
  until ;

: number-say ( n a len -- )   \ "n text" on the message line
  { n a l }  0 { k }
  n <# #s #> { d dl }
  d msg dl cmove  32 msg dl + c!  a msg dl + 1+ l cmove
  dl 1+ l + 78 min to mlen ;
: num-type-say ( n -- )   s" replaced" number-say ;

( ---- Replace ---- )
: replace-all ( -- )
  s" Replace: " ask 0= if drop exit then
  dup 0= if drop exit then
  dup to lastfind-n  pbuf lastfind rot cmove
  s" With: " ask 0= if drop exit then
  dup to repl-n  pbuf repl rot cmove
  0 { p }  0 { n }
  begin p search-from dup 0< 0= while
    to p
    p lastfind-n cut
    repl-n if repl repl-n p put then
    repl-n +to p  1 +to n
  repeat
  drop
  n if n num-type-say else s" Not found" say then ;

( ---- The Esc menu ---- )
: timer-menu ( -- )
  s" Timer: s stopwatch, c countdown, x off" bar
  key dup [char] s = if drop 1 to timer-mode  now-ms to timer-t0  0 to alerted  s" Stopwatch" say exit then
  dup [char] c = if drop
    s" Minutes: " ask if
      pbuf swap to-number 60000 * dup to timer-len
      0> if 2 to timer-mode  now-ms to timer-t0  0 to alerted  s" Countdown" say then
    else drop then exit then
  [char] x = if 0 to timer-mode  s" Timer off" say then ;
: ask-goal ( -- )
  s" Daily goal in words: " ask if pbuf swap to-number to goal ini-save else drop then ;
: wd-menu ( -- )
  -1 to anchor  0 to sticky
  s" Esc  w wrap  f focus  h hem  t timer  o outline  s stats  r replace  g goal  c theme  x run  ? help" bar
  key
  dup [char] w = if drop wrap-on 0= to wrap-on  cur bol to top  0 to left
                    wrap-on if s" Wrap on" else s" Wrap off" then say  ini-save exit then
  dup [char] f = if drop focus-on 0= to focus-on
                    focus-on if s" Focus on" else s" Focus off" then say exit then
  dup [char] h = if drop hem-on 0= to hem-on
                    hem-on if s" Hemingway on: no going back" else s" Hemingway off" then say exit then
  dup [char] t = if drop timer-menu exit then
  dup [char] o = if drop toc-show exit then
  dup [char] s = if drop stat-page exit then
  dup [char] r = if drop replace-all exit then
  dup [char] g = if drop ask-goal exit then
  dup [char] a = if drop autosave 0= to autosave
                    autosave if s" Autosave on" else s" Autosave off" then say  now-ms to last-auto exit then
  dup [char] + = if drop colw 4 + cols 2 - min to colw  ini-save  s" Wider" say exit then
  dup [char] - = if drop colw 4 - 30 max to colw  ini-save  s" Narrower" say exit then
  dup [char] c = if drop next-theme exit then
  dup [char] x = if drop run-file exit then
  dup [char] ? = if drop help-page exit then
  drop ;

( ---- Keys, Hemingway ---- )
: hem-block? ( code -- f )
  dup 127 = over 8 = or over k-del = or over k-left = or over k-cleft = or
  over k-up = or over k-pgup = or over k-home = or over k-chome = or
  over 24 = or over 26 = or over 1 = or nip ;
: wd-pre ( code -- code' )
  dup 27 = if drop wd-menu 0 exit then
  hem-on if dup hem-block? if drop s" Hemingway: only forward" say 0 exit then then ;
: wd-wait ( -- code )
  begin key? 0= while  tick  50000 usleep drop  repeat
  read-key ;

( ---- Running ---- )
: wd-after-load ( -- )
  -1 to hl-on  layout-update
  repl 0= if 80 bytes to repl then
  toc-pos 0= if 500 cells bytes to toc-pos then
  word-count to words0  now-ms to last-auto  0 to last-sec ;
: wd-install ( -- )
  ['] wd-scroll is scroll   ['] wd-draw is draw   ['] wd-status is draw-status
  ['] wd-scan is hl-scan   ['] wrap-want is set-want
  ['] wrap-up is up-key   ['] wrap-down is down-key
  ['] wrap-home is home-key   ['] wrap-end is end-key
  ['] wd-pre is pre-key   ['] wd-wait is wait-key   ['] wd-after-load is after-load ;
: wd-main ( a n -- )
  wd-install
  ['] ed-main catch { err }
  ed-plain
  err throw ;
' wd-main is wd-go
only forth also internals definitions
| ;

: writhdeck ( "file" -- )   \ edit a text for writing
  bl parse 255 min dup to ed-arg-n  ed-arg swap cmove
  ed-loaded 0= if  ed-src eval-copy  -1 to ed-loaded  then
  wd-loaded 0= if  wd-src eval-copy  -1 to wd-loaded  then
  ed-arg ed-arg-n wd-go ;
