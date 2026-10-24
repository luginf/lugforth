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

\ Flag: the code of writhdeck (the string wd-src) has been compiled.
0 value wd-loaded
\ Runs writhdeck on the file a n. Set to wd-main when wd-src is compiled.
defer wd-go ( a n -- )

\ The source of writhdeck, a string compiled the first time writhdeck is used.
: wd-src r|
only forth also internals also ansi
[defined] posix [if] also posix also termios [then]
also editor definitions

( ---- Settings ---- )
0 value focus-on   0 value hem-on
0 value words0   0 value autosave   0 value last-auto
0 value words-now   -1 value words-edits
0 value timer-mode   0 value timer-t0   0 value timer-len   0 value alerted
0 value last-sec   0 value ww   0 value pad
0 value toc-pos   0 value toc-n   0 value toc-sel   0 value toc-top
0 value repl   0 value repl-n
defer stats-hook ( -- )   defer stats-extra ( -- )   ( set once the browser words exist )
0 value st-dirty   0 value st-last   0 value tz-hours   0 value st-base
0 value wrow   0 value nums-on   0 value lno
0 value cl-pos   0 value cl-line   -1 value cl-len   ( the last line count asked: where, which line, text length )

: now-ms ( -- n )   ms-ticks ;
: layout-update ( -- )
  wrap-on nums-on and if colw cols 8 - min 20 max to ww  cols 6 - ww - 2/ 6 + to pad exit then
  wrap-on if colw cols 2 - min 20 max else cols then to ww
  wrap-on if cols ww - 2/ else 0 then to pad ;

( ---- Settings: writhdeck.ini, in the format of WrithDeck, the Tcl program ---- )
0 value hem-cfg   0 value auto-cfg   1 value auto-min   -1 value bar-on
0 value msg-shown   0 value help-seen   0 value fresh
create zl 128 allot   create zc 128 allot   create zr 128 allot   create hbar 128 allot
: zset ( a n z -- )   >r 120 min dup r@ ! r> cell+ swap cmove ;
: zget ( z -- a n )   dup cell+ swap @ ;
: wd-defaults ( -- )
  s" filename dirty words session goal timer modes help_bar" zl zset
  s" " zc zset   s" " zr zset   s" Esc: menu" hbar zset   -1 to bar-on   0 to nums-on ;
wd-defaults
: wd-defname ( -- a n )   s" writhdeck.ini" ;
: wd-ini-kv ( ka kn va vn -- )
  { ka kn va vn }
  ka kn s" hemingway_mode" compare 0= if va vn yes? dup to hem-cfg to hem-on exit then
  ka kn s" autosave_enabled" compare 0= if va vn yes? dup to auto-cfg to autosave exit then
  ka kn s" autosave_interval" compare 0= if va vn to-number 1 max 60 min to auto-min exit then
  ka kn s" bar" compare 0= if va vn yes? to bar-on exit then
  ka kn s" line_numbers" compare 0= if va vn yes? to nums-on exit then
  ka kn s" utc_offset" compare 0= if va vn to-number -12 max 14 min to tz-hours exit then
  ka kn s" help_bar" compare 0= if va vn hbar zset exit then
  ka kn s" status_left" compare 0= if va vn zl zset exit then
  ka kn s" status_center" compare 0= if va vn zc zset exit then
  ka kn s" status_right" compare 0= if va vn zr zset exit then
  ka kn va vn ed-ini-kv ;
: wd-ini-body ( -- )
  s" = WrithDeck configuration =" wl
  s" % The format of WrithDeck (Tcl), only the keys that this writhdeck uses." wl
  s" % It is not the file of the Tcl program (in ~/Documents/writhdeck)." wl
  eol+  s" = editor =" wl  s" [editor]" wl
  s" % scheme: default colorforth light solarized mono (the schemes of this editor)" wl
  s" scheme" key= theme theme-name wl
  eol+  s" = behaviour =" wl  s" [behaviour]" wl
  s" % limit_width: columns of the centered column of text" wl
  s" limit_width" key= colw wn eol+
  s" % wrap: soft wrapped lines in that column (this editor only)" wl
  s" wrap" key= wrap-on wyes eol+
  s" % hemingway_mode: no deleting or moving back" wl
  s" hemingway_mode" key= hem-cfg wyes eol+
  s" % word_goal: daily target of words, 0 for none" wl
  s" word_goal" key= goal wn eol+
  s" % line_numbers: numbers in the margin of the wrapped view, Ctrl+L" wl
  s" line_numbers" key= nums-on wyes eol+
  s" % utc_offset: hours added to UTC to find the day of the daily statistics" wl
  s" utc_offset" key= tz-hours wn eol+
  s" % bar: show the status bar (a message still shows in it)" wl
  s" bar" key= bar-on wyes eol+
  s" % help_bar: text of the help_bar token when there is no message" wl
  s" help_bar" key= hbar zget wl
  s" % status bar zones. Tokens: filename dirty words session goal timer modes help_bar space" wl
  s" % session (words written) and modes ([focus] [hem] [auto]) are of this editor only; other text is shown as is" wl
  s" status_left" key= zl zget wl
  s" status_center" key= zc zget wl
  s" status_right" key= zr zget wl
  eol+  s" = misc =" wl  s" [misc]" wl
  s" autosave_enabled" key= auto-cfg wyes eol+
  s" autosave_interval" key= auto-min wn eol+ ;
: ini-from ( a n -- flag )   \ the file in the current folder, else in the settings folder
  2dup ini-read if inic! -1 exit then
  ini-in-dir 2dup ini-read if inic! -1 exit then
  2drop 0 ;
: wd-ini-load ( -- )
  ini-init  0 to inicn  wd-defaults
  s" writhdec.ini" ini-from if exit then
  s" writhd.ini" ini-from if exit then
  s" wrid.ini" ini-from if exit then
  s" writhdeck.ini" ini-from if exit then
  s" editor.ini" ini-in-dir ini-read drop ;   \ the settings of older versions, kept in writhdeck.ini from now on

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
  autosave if modified if flen if now-ms last-auto - auto-min 60000 * > if
    ['] save-file catch if s" Autosave failed" say else s" Autosaved" say then
    now-ms to last-auto
  then then then then
  modified if now-ms st-last - 10000 > if now-ms to st-last stats-hook then then
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

( ---- Line numbers: the count is kept from the last draw, so a key costs a few lines, not the whole text ---- )
: cl-ok? ( -- f )   cl-len 0< if 0 exit then  cl-len len = cl-pos cur < or ;
: line-at ( p -- n )   \ the number of the line holding position p, from 1
  { p }  0 { n }
  cl-ok? 0= if 0 to cl-pos  1 to cl-line then
  p cl-pos > if
    p cl-pos ?do i b@ 10 = if 1 +to n then loop  n +to cl-line
  else
    cl-pos p ?do i b@ 10 = if 1 +to n then loop  n negate +to cl-line
  then
  p to cl-pos  len to cl-len  cl-line ;
: lno-type ( n -- )   <# #s #> dup 5 swap - 0 max 0 ?do 32 emit loop type ;
: nums-flip ( -- )
  wrap-on 0= if s" Line numbers need the wrapped view (Esc w)" say exit then
  nums-on 0= to nums-on  layout-update  ini-save
  nums-on if s" Line numbers on" else s" Line numbers off" then say ;

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
  nums-on if rp 0= rp 1- b@ 10 = or if
    normal theme-dim fg  pad 6 - 0 max wrow at-xy  lno lno-type  normal then then
  pad wrow at-xy
  rp seg to rnext to de
  rp de draw-seg
  nums-on if rnext 0> if rnext 1- b@ 10 = if 1 +to lno then then then
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
  nums-on if top line-at to lno then
  text-rows 0 ?do i wrap-row loop
  draw-status
  ccol crow at-xy
  show ;
: wd-scroll   wrap-on if wrap-scroll else plain-scroll then ;
: wd-draw     wrap-on if wrap-draw else plain-draw then ;

( ---- The status bar ---- )
: gap ( n -- )   fresh if drop else 0 ?do s"  " stype loop then 0 to fresh ;
: t-name ( -- )   1 gap  flen 0= if s" (new)" else name-tail then stype ;
: t-dirty ( -- )   modified if 0 gap s" *" stype then ;
: t-words ( -- )   2 gap s" W " stype word-count num-type ;
: t-session ( -- )   1 gap s" +" stype word-count words0 - num-type ;
: t-goal ( -- )
  goal if 1 gap s" /" stype goal num-type s"  " stype word-count 100 * goal / num-type s" %" stype then ;
: t-timer ( -- )   timer-mode if 2 gap timer-secs mmss then ;
: t-modes ( -- )
  focus-on if 1 gap s" [focus]" stype then
  hem-on if 1 gap s" [hem]" stype then
  autosave if 1 gap s" [auto]" stype then ;
: t-help ( -- )   2 gap  mlen if msg mlen stype else hbar zget stype then  -1 to msg-shown ;
: tok ( a n -- )
  2dup s" filename" compare 0= if 2drop t-name exit then
  2dup s" dirty" compare 0= if 2drop t-dirty exit then
  2dup s" words" compare 0= if 2drop t-words exit then
  2dup s" session" compare 0= if 2drop t-session exit then
  2dup s" goal" compare 0= if 2drop t-goal exit then
  2dup s" timer" compare 0= if 2drop t-timer exit then
  2dup s" modes" compare 0= if 2drop t-modes exit then
  2dup s" help_bar" compare 0= if 2drop t-help exit then
  2dup s" space" compare 0= if 2drop 1 gap exit then
  1 gap stype ;
: zone-run ( z -- )
  zget { a n }  0 { s }  0 { e }
  begin s n < while
    s begin dup n < if a over + c@ 32 = else 0 then while 1+ repeat to s
    s n < if
      s begin dup n < if a over + c@ 32 <> else 0 then while 1+ repeat to e
      a s +  e s -  tok
      e to s
    then
  repeat ;
: run-l ( -- )   0 to fresh  zl zone-run ;
: run-c ( -- )   -1 to fresh  zc zone-run ;
: run-r ( -- )   -1 to fresh  zr zone-run
  mlen help-seen 0= and if 2 gap msg mlen stype then ;
: measure ( xt -- w )   -1 to dry  0 to outcol  execute  outcol  0 to dry ;
: pad-to ( col -- )   cols min outcol - 0 max dup +to outcol 0 ?do 32 emit loop ;
: wd-status ( -- )
  0 text-rows at-xy
  bar-on 0= if
    mlen 0= if normal esc ." [K" exit then
    esc ." [7m"  0 to outcol  s"  " stype msg mlen stype  cols pad-to  normal exit
  then
  0 to msg-shown
  ['] run-l measure drop   ['] run-c measure drop   ['] run-r measure drop
  msg-shown to help-seen
  ['] run-c measure { cw }   ['] run-r measure { rw }
  esc ." [7m"  0 to outcol
  run-l
  cw if cols cw - 2/ outcol 1+ max pad-to run-c then
  rw if cols rw - outcol 1+ max pad-to run-r then
  cols pad-to
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
  s"   v word occurrences (most used words)   q quit" pl
  s"   s statistics   r replace all   g daily goal   a autosave   + - width   c theme   x run the file" pl  1 +to orow
  s" Keys as in ed: Ctrl+S save, Ctrl+Q quit, Ctrl+F find, Ctrl+N next, Ctrl+G line," pl
  s" Ctrl+Z undo, Ctrl+Y redo, Ctrl+C X V copy cut paste, Ctrl+K mark, Shift+arrows select," pl
  s" Ctrl+O save and back to the file browser, Ctrl+L line numbers, Ctrl+T next color theme (default colorforth light solarized mono), Ctrl+R run." pl
  wait-any ;
: stat-page ( -- )
  page 0 to orow
  s" Statistics" pl  1 +to orow
  s" Words" word-count kv
  s" Characters" len kv
  s" This session" word-count words0 - kv
  s" Daily goal" goal kv
  s" Reading minutes" word-count 200 / 1+ kv
  stats-extra
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
  drop  -1 to cl-len
  n if n num-type-say else s" Not found" say then ;

( ---- Word occurrences: the words of the text by frequency ---- )
0 value wo-tab   0 value wo-n   0 value wo-sel   0 value wo-top
: wo-e ( i -- a )   3 * cells wo-tab + ;
: wo-lc ( c -- c' )   dup 65 >= over 90 <= and if 32 + then ;
: wo-char? ( c -- f )   dup 128 >= swap wo-lc dup 97 >= swap 122 <= and or ;
: wo-same? ( p q n -- f )   \ the n bytes at p and q are one word, case apart
  -1 { p q n f }
  n 0 ?do  p i + b@ wo-lc  q i + b@ wo-lc <> if 0 to f then loop
  f ;
: wo-find ( p n -- i )   \ the entry of that word, or -1
  { p n }  -1 { r }  0 { i }
  begin i wo-n < r 0< and while
    i wo-e cell+ @ n = if  p i wo-e @ n wo-same? if i to r then  then
    1 +to i
  repeat
  r ;
: wo-add ( p n -- )
  { p n }  p n wo-find { i }  wo-n wo-e { e }
  i 0< if
    wo-n 1000 < if  p e !  n e cell+ !  1 e 2 cells + !  1 +to wo-n  then
  else  1 i wo-e 2 cells + +!  then ;
: wo-collect ( -- )
  0 to wo-n  0 { p }  0 { q }
  begin p len < while
    p b@ wo-char? if
      p to q
      begin q len < if q b@ wo-char? else 0 then while 1 +to q repeat
      q p - 3 >= if p q p - wo-add then
      q to p
    else 1 +to p then
  repeat ;
: wo-cnt ( i -- n )   wo-e 2 cells + @ ;
: wo-swap ( i j -- )
  wo-e swap wo-e { a b }
  3 0 ?do  a i cells + @  b i cells + @  a i cells + !  b i cells + !  loop ;
: wo-sort ( -- )   \ selection sort, most frequent first
  0 { m }
  wo-n 0 ?do
    i to m
    wo-n i 1+ ?do  i wo-cnt m wo-cnt > if i to m then loop
    i m <> if i m wo-swap then
  loop ;
: wo-row ( i -- )
  dup wo-top - 1+ { i r }
  0 r at-xy clear-to-eol
  i wo-sel = if esc ." [7m" then
  2 r at-xy  i wo-cnt num-type
  10 r at-xy  i wo-e dup @ text + swap cell+ @ cols 12 - min type
  normal ;
: wo-draw ( -- )
  page  2 0 at-xy  esc ." [7m" s" Word occurrences  Enter go to the first  Esc close" type normal
  wo-n wo-top - text-rows 1- min 0 ?do wo-top i + wo-row loop ;
: wo-show ( -- )
  wo-tab 0= if 3000 cells bytes to wo-tab then
  wo-collect
  wo-n 0= if s" No words" say exit then
  wo-sort  0 to wo-sel  0 to wo-top
  begin
    wo-sel wo-top < if wo-sel to wo-top then
    wo-sel wo-top - text-rows 2 - > if wo-sel text-rows 2 - - to wo-top then
    wo-draw
    read-key
    dup k-up = if drop wo-sel 1- 0 max to wo-sel 0 else
    dup k-down = if drop wo-sel 1+ wo-n 1- min to wo-sel 0 else
    dup k-pgup = if drop wo-sel 10 - 0 max to wo-sel 0 else
    dup k-pgdn = if drop wo-sel 10 + wo-n 1- min to wo-sel 0 else
    dup 13 = over 10 = or if drop wo-sel wo-e @ go -1 else
    27 = if -1 else 0 then then then then then then
  until ;

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
  s" Esc  w wrap  f focus  h hem  t timer  o outline  v words  s stats  r replace  q quit  g goal  c theme  x run  ? help" bar
  key
  dup [char] w = if drop wrap-on 0= to wrap-on  cur bol to top  0 to left
                    wrap-on if s" Wrap on" else s" Wrap off" then say  ini-save exit then
  dup [char] f = if drop focus-on 0= to focus-on
                    focus-on if s" Focus on" else s" Focus off" then say exit then
  dup [char] h = if drop hem-on 0= to hem-on
                    hem-on if s" Hemingway on: no going back" else s" Hemingway off" then say exit then
  dup [char] t = if drop timer-menu exit then
  dup [char] o = if drop toc-show exit then
  dup [char] v = if drop wo-show exit then
  dup [char] q = if drop try-quit exit then
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

( ---- The file browser: `writhdeck` alone, or Ctrl+O from a text ---- )
[defined] posix [if]
z" chdir" 1 sysfunc wd-chdir
z" getcwd" 2 sysfunc wd-getcwd
z" remove" 1 sysfunc wd-remove
z" rename" 2 sysfunc wd-rename
z" time" 1 sysfunc wd-time
[else]   \ the board: the FAT's own words (rpi vocabulary), names as zero-ended strings
also rpi
: zlen ( z -- n ) 0 begin 2dup + c@ while 1+ repeat nip ;
: wd-chdir ( z -- n ) dup zlen chdir ;
: wd-getcwd ( a n -- a ) drop { buf }  getcwd 289 min { ca cn }  ca buf cn cmove  0 buf cn + c!  buf ;
: wd-remove ( z -- n ) dup zlen delete-file ;
: wd-rename ( z1 z2 -- n ) { z1 z2 }  z1 z1 zlen  z2 z2 zlen  rename-file ;
: wd-time ( n -- secs ) drop unix-time ;
: usleep ( us -- n ) 1000 / ms 0 ;
previous
[then]
0 value bpool   0 value bpn   0 value btab   0 value bn   0 value bsel   0 value btop
0 value bfilt   0 value bfilt-n   0 value bz1   0 value bz2
create bcwd 300 allot
0 value bdone   0 value bname   0 value bnl   0 value ba   0 value bnn
0 value want-browser   0 value werr
: b-alloc ( -- )
  60000 bytes to bpool   3000 cells bytes to btab   64 bytes to bfilt
  300 bytes to bz1   300 bytes to bz2   300 bytes to bname
  0 to bfilt-n  0 to bsel  0 to btop ;
: b-free ( -- )   bpool free drop  btab free drop  bfilt free drop
  bz1 free drop  bz2 free drop  bname free drop ;
: zcopy ( a n buf -- buf )   dup >r swap dup >r cmove  0 r> r@ + c!  r> ;
: b-e ( i -- a )   3 * cells btab + ;
: b-name ( i -- a n )   b-e dup @ bpool + swap cell+ @ ;
: b-dir? ( i -- f )   b-e 2 cells + @ ;
: b-add ( a n dir -- )
  { a n d }  bn 1000 < bpn n + 59000 < and 0= if exit then
  bn b-e { e }
  bpn e !  n e cell+ !  d e 2 cells + !
  a bpool bpn + n cmove  n +to bpn  1 +to bn ;
: b-match? ( a n -- f )   \ the filter is inside the name, case apart
  bfilt-n 0= if 2drop -1 exit then
  { a n }  0 { r }  0 { k }
  begin k bfilt-n + n <= r 0= and while
    -1 to r
    bfilt-n 0 ?do a k + i + c@ wo-lc  bfilt i + c@ wo-lc <> if 0 to r then loop
    1 +to k
  repeat
  r ;
: b-before? ( i j -- f )   \ directories first, then by name
  { i j }
  i b-dir? j b-dir? <> if i b-dir? exit then
  i b-name j b-name compare 0< ;
: b-swap ( i j -- )
  b-e swap b-e { a b }
  3 0 ?do  a i cells + @  b i cells + @  a i cells + !  b i cells + !  loop ;
: b-sort ( -- )
  0 { m }
  bn 0 ?do
    i to m
    bn i 1+ ?do  i m b-before? if i to m then loop
    i m <> if i m b-swap then
  loop ;
[defined] posix [if]
: b-scan-dir ( -- )
  0 to bn  0 to bpn  0 { dh }  0 { ent }
  s" .." -1 b-add
  s" ." open-dir if drop exit then  to dh
  begin dh readdir dup while
    to ent
    ent .d_name z>s to bnn to ba
    ba c@ [char] . <> if
      ent .d_type 4 = if ba bnn -1 b-add
      else ba bnn b-match? if ba bnn 0 b-add then then
    then
  repeat
  drop dh closedir drop
  b-sort ;
[else]
: b-scan-dir ( -- )
  0 to bn  0 to bpn  0 { dh }  0 { ent }
  s" .." -1 b-add
  s" ." open-dir if drop exit then  to dh
  begin dh readdir dup while
    to ent
    ent z>s to bnn to ba
    ba c@ [char] . <> if
      ba bnn 1- + c@ [char] / = if ba bnn 1- -1 b-add
      else ba bnn b-match? if ba bnn 0 b-add then then
    then
  repeat
  drop dh close-dir drop
  b-sort ;
[then]
: b-cwd ( -- a n )   bcwd 290 wd-getcwd drop  bcwd dup begin dup c@ while 1+ repeat over - ;
( ---- Favorites and recent files: writhdeck.rec in the settings folder, lines "F path" and "R path" ---- )
256 constant sl-sz
create fav-tab 20 sl-sz * allot   create rec-tab 10 sl-sz * allot
variable fav-cnt   variable rec-cnt   0 fav-cnt !   0 rec-cnt !
: sl-a ( tab k -- z )   sl-sz * + ;
( Daily statistics: the words written in a file in a day, never lowered; the day is the number of days since 1970 )
100 constant st-max
create st-tab st-max sl-sz * allot   create st-d st-max cells allot   create st-w st-max cells allot
variable st-cnt   0 st-cnt !
: st-find ( a n day -- k )   \ the entry of that path that day, or -1
  { a n day }  -1 { r }
  st-cnt @ 0 ?do  st-d i cells + @ day = if st-tab i sl-a z>s a n compare 0= if i to r leave then then  loop
  r ;
: st-put ( a n day w -- )   \ keeps the highest count
  { a n day w }
  a n day st-find { k }
  k 0< if
    st-cnt @ st-max >= if exit then
    st-cnt @ to k  1 st-cnt +!
    a n 255 min st-tab k sl-a zcopy drop  day st-d k cells + !  0 st-w k cells + !
  then
  st-w k cells + @ w max  st-w k cells + ! ;
: today ( -- day )   0 wd-time  tz-hours 3600 * +  86400 / ;
: st-today-all ( -- n )   0 { t }  st-cnt @ 0 ?do  st-d i cells + @ today = if st-w i cells + @ +to t then  loop  t ;
create babs 700 allot   0 value bal   0 value bview   ( bview: 0 folder, 1 favorites, 2 recent files )
: sl-find ( a n tab cnt -- k )   \ the slot holding that path, or -1
  { a n tab cnt }  -1 { r }
  cnt 0 ?do  tab i sl-a z>s a n compare 0= if i to r leave then  loop
  r ;
: sl-del ( k tab cp -- )   \ cp is the address of the count
  { k tab cp }
  cp @ 1- k ?do  tab i 1+ sl-a  tab i sl-a  sl-sz cmove  loop
  -1 cp +! ;
: sl-add ( a n tab cp mx -- )   \ at the end, when there is room
  { a n tab cp mx }
  cp @ mx >= if exit then
  a n 255 min tab cp @ sl-a zcopy drop  1 cp +! ;
: sl-front ( a n tab cp mx -- )   \ first of the list, the oldest one dropped when it is full
  { a n tab cp mx }
  a n tab cp @ sl-find dup 0< if drop else tab cp sl-del then
  cp @ mx >= if -1 cp +! then
  cp @ 0 ?do  tab cp @ i - 1- sl-a  tab cp @ i - sl-a  sl-sz cmove  loop
  a n 255 min tab 0 sl-a zcopy drop  1 cp +! ;
: fav? ( a n -- f )   fav-tab fav-cnt @ sl-find 0< 0= ;
: fav-flip ( a n -- f )   \ toggles, true when it is a favorite now
  2dup fav? if fav-tab fav-cnt @ sl-find fav-tab fav-cnt sl-del 0 exit then
  fav-tab fav-cnt 20 sl-add -1 ;
: field ( a n -- rest-a rest-n a' n' )   \ the text up to the first blank, and what follows it
  { a n }  0 { k }
  begin k n < if a k + c@ 32 <> else 0 then while 1 +to k repeat
  a k + 1+  n k - 1- 0 max  a k ;
: st-line ( a n -- )   \ day, words, path
  field to-number { d }  field to-number { w }
  dup 0= if 2drop drop exit then
  d w st-put ;
: lst-line ( a n -- )
  dup 3 < if 2drop exit then
  over c@ [char] F = if 2 /string fav-tab fav-cnt 20 sl-add exit then
  over c@ [char] R = if 2 /string rec-tab rec-cnt 10 sl-add exit then
  over c@ [char] S = if 2 /string st-line exit then
  2drop ;
: each-line ( b n -- )
  { b n }  0 { s }  0 { e }
  begin s n < while
    s begin dup n < if b over + c@ 10 <> else 0 then while 1+ repeat to e
    b s +  e s -  lst-line
    e 1+ to s
  repeat ;
: lst-load ( -- )
  0 fav-cnt !  0 rec-cnt !  0 st-cnt !  ini-init
  s" writhdeck.rec" ini-in-dir r/o open-file if drop exit then  { fh }
  fh file-size throw { sz }
  sz 1+ bytes { b }
  b sz fh read-file throw { got }
  fh close-file drop
  b got each-line  b free drop ;
: st-write ( k -- )   \ today's entries, and those of the favorites
  { k }  st-d k cells + @ { d }  st-tab k sl-a z>s { pa pn }
  d today =  pa pn fav? or 0= if exit then
  s" S " w+  d wn s"  " w+  st-w k cells + @ wn s"  " w+  pa pn wl ;
: lst-save ( -- )
  ini-init  ini-dir mk-dirs
  s" writhdeck.rec" ini-in-dir w/o create-file if drop exit then  to fhs
  fav-cnt @ 0 ?do  s" F " w+  fav-tab i sl-a z>s wl  loop
  rec-cnt @ 0 ?do  s" R " w+  rec-tab i sl-a z>s wl  loop
  st-cnt @ 0 ?do i st-write loop
  fhs close-file drop ;
: b-abs ( a n -- a' n' )   \ the path of a name of the folder
  { a n }  b-cwd { ca cn }
  ca babs cn cmove  cn to bal
  bal 1 > if [char] / babs bal + c!  1 +to bal then
  a babs bal + n 200 min cmove  n 200 min +to bal
  babs bal ;
: abs-name ( -- a n )   fname c@ [char] / = if fname flen else fname flen b-abs then ;
: st-note ( -- )   \ today's words of the open file, kept when they grew
  flen 0= if exit then
  st-base word-count words0 - 0 max + { w }
  w 0= if exit then
  abs-name today w st-put   -1 to st-dirty ;
: st-hook ( -- )   st-note  st-dirty if 0 to st-dirty lst-save then ;
' st-hook is stats-hook
: st-start ( -- )   \ at load: what was already written today in that file
  0 to st-base  flen 0= if exit then
  lst-load  abs-name today st-find dup 0< if drop else cells st-w + @ to st-base then ;
: st-extra ( -- )
  st-note
  s" Today, this file" st-base word-count words0 - 0 max + kv
  s" Today, all files" st-today-all kv ;
' st-extra is stats-extra
create dbuf 12 allot
: digits ( n k a -- )   \ k digits of n at a
  { n k a }  k 0 ?do  n 10 /mod to n [char] 0 + a k 1- i - + c!  loop ;
: dstr ( day -- a n )   \ yyyy-mm-dd
  719468 + { z }
  z 146097 / { era }  z era 146097 * - { doe }
  doe  doe 1460 / -  doe 36524 / +  doe 146096 / -  365 / { yoe }
  yoe era 400 * + { y }
  doe  365 yoe * yoe 4 / + yoe 100 / -  - { doy }
  5 doy * 2 + 153 / { mp }
  doy 153 mp * 2 + 5 / - 1+ { d }
  mp 10 < if mp 3 + else mp 9 - then { m }
  m 2 <= if 1 +to y then
  y 4 dbuf digits  [char] - dbuf 4 + c!  m 2 dbuf 5 + digits  [char] - dbuf 7 + c!  d 2 dbuf 8 + digits
  dbuf 10 ;
: day-total ( day -- n )   { day }  0 { t }  st-cnt @ 0 ?do  st-d i cells + @ day = if st-w i cells + @ +to t then  loop  t ;
: st-page ( -- )
  st-note  page 0 to orow
  s" Writing statistics: words written per day, all files" pl  1 +to orow
  14 0 ?do  today i - day-total ?dup if today i - dstr rot kv then  loop
  wait-any ;
: b-scan-list ( -- )
  0 to bn  0 to bpn
  bview 1 = if fav-tab fav-cnt @ else rec-tab rec-cnt @ then { tab cnt }
  cnt 0 ?do  tab i sl-a z>s 2dup b-match? if 0 b-add else 2drop then  loop ;
: b-scan ( -- )   bview if b-scan-list else b-scan-dir then ;
: b-row ( i -- )
  dup btop - 1+ { i r }
  0 r at-xy clear-to-eol
  bview 0= if i b-dir? 0= if i b-name b-abs fav? if 0 r at-xy ." *" then then then
  i bsel = if esc ." [7m" then
  2 r at-xy  i b-name cols 8 - min type  i b-dir? if ." /" then
  normal ;
: b-draw ( -- )
  page  0 0 at-xy  esc ." [7m"  0 to outcol
  s"  " stype
  bview 0= if b-cwd cols 30 - 10 max min else bview 1 = if s" Favorites" else s" Recent files" then then stype
  bfilt-n if s"   [" stype bfilt bfilt-n stype s" ]" stype then
  cols outcol - 0 max 0 ?do 32 emit loop  normal
  bn btop - text-rows 1- min 0 ?do btop i + b-row loop
  0 text-rows at-xy  esc ." [7m"  0 to outcol
  mlen if s"  " stype msg mlen stype 0 to mlen
  else s"  Enter open  n new  / filter  r rename  d del  b backup  f fav  v view  q quit" stype then
  cols outcol - 0 max 0 ?do 32 emit loop  normal ;
: b-cd ( i -- )   b-name bz1 zcopy wd-chdir drop  0 to bsel 0 to btop  b-scan ;
: b-pick ( a n -- )   \ choose this file, noted in the recent files
  { a n }
  bview 0= if a n b-abs else a n then  rec-tab rec-cnt 10 sl-front  lst-save
  n 255 min dup to ed-arg-n  a ed-arg rot cmove  -1 to bdone ;
: b-sel-name ( -- a n )   bsel b-name ;
: b-ask-name ( a n -- f )   \ a name typed in pbuf, f true when given
  ask 0= if drop 0 exit then  0<> ;
: b-new ( -- )
  s" New file: " ask 0= if drop exit then
  dup 0= if drop exit then
  pbuf swap b-pick ;
: b-delete ( -- )
  bn 0= bsel b-dir? or bview 0<> or if exit then
  0 text-rows at-xy esc ." [7m" s"  Delete " stype b-sel-name stype s"  ? (y/n)" stype
  cols outcol - 0 max 0 ?do 32 emit loop normal
  key [char] y <> if exit then
  b-sel-name bz1 zcopy wd-remove if s" Cannot delete" say else s" Deleted" say then
  b-scan  bsel bn 1- min 0 max to bsel ;
: b-rename ( -- )
  bn 0= bsel b-dir? or bview 0<> or if exit then
  s" New name: " ask 0= if drop exit then
  dup 0= if drop exit then
  pbuf swap bz2 zcopy drop
  b-sel-name bz1 zcopy bz2 wd-rename if s" Cannot rename" say else s" Renamed" say then
  b-scan ;
: bn+ ( a n -- )   dup >r bname bnl + swap cmove  r> +to bnl ;
: b-backup ( -- )
  bn 0= bsel b-dir? or bview 0<> or if exit then
  b-sel-name { a n }
  s" backups" bname zcopy drop  bname 493 ed-mkdir drop
  0 to bnl  s" backups/" bn+  a n bn+  s" _" bn+  0 wd-time <# #s #> bn+
  a n r/o open-file if drop s" Cannot read" say exit then
  { fh }
  fh file-size throw { size }
  size 1+ bytes { buf }
  buf size fh read-file throw drop  fh close-file drop
  bname bnl w/o create-file if drop buf free drop s" Cannot write" say exit then
  { out }
  buf size out write-file drop  out close-file drop  buf free drop
  s" Backed up in backups/" say ;
: b-fav ( -- )
  bn 0= bsel b-dir? or if exit then
  bview 0= if b-sel-name b-abs else b-sel-name then
  fav-flip if s" Added to the favorites" else s" Removed from the favorites" then say
  lst-save
  bview 1 = if b-scan  bsel bn 1- min 0 max to bsel then ;
: b-view ( -- )   bview 1+ 3 mod to bview  0 to bsel  0 to btop  b-scan ;
: b-key ( code -- )
  dup k-up = if drop bsel 1- 0 max to bsel exit then
  dup k-down = if drop bsel 1+ bn 1- min 0 max to bsel exit then
  dup k-pgup = if drop bsel text-rows 2 - - 0 max to bsel exit then
  dup k-pgdn = if drop bsel text-rows 2 - + bn 1- min 0 max to bsel exit then
  dup k-home = if drop 0 to bsel exit then
  dup k-end = if drop bn 1- 0 max to bsel exit then
  dup 13 = over 10 = or if drop
    bn 0= if exit then
    bsel b-dir? if bsel b-cd else b-sel-name b-pick then exit then
  dup [char] n = if drop b-new exit then
  dup [char] d = if drop b-delete exit then
  dup [char] r = if drop b-rename exit then
  dup [char] b = if drop b-backup exit then
  dup [char] f = if drop b-fav exit then
  dup [char] s = if drop st-page exit then
  dup [char] v = if drop b-view exit then
  dup [char] / = if drop
    s" Filter: " ask if dup to bfilt-n pbuf bfilt rot cmove  0 to bsel 0 to btop  b-scan else drop then exit then
  dup 27 = if drop
    bview if 0 to bview  0 to bsel 0 to btop  b-scan exit then
    bfilt-n if 0 to bfilt-n  0 to bsel 0 to btop  b-scan else -1 to bdone then exit then
  dup [char] q = if drop -1 to bdone  0 to ed-arg-n exit then
  drop ;
: b-loop ( -- )
  0 to bdone  0 to ed-arg-n  0 to mlen  b-scan
  begin
    refresh-size
    bsel btop < if bsel to btop then
    bsel btop - text-rows 2 - > if bsel text-rows 2 - - to btop then
    b-draw
    read-key b-key
    bdone
  until ;
: browse ( -- f )   \ choose a file, which is left in ed-arg; false when none
  ed-init  b-alloc  lst-load  0 to bview
  editor-raw  terminal-save  page
  ['] b-loop catch { err }
  terminal-restore  normal  show  restore-tty  page
  b-free  ed-free
  err throw
  ed-arg-n 0<> ;

( ---- Keys, Hemingway ---- )
: hem-block? ( code -- f )
  dup 127 = over 8 = or over k-del = or over k-left = or over k-cleft = or
  over k-up = or over k-pgup = or over k-home = or over k-chome = or
  over 24 = or over 26 = or over 25 = or over 1 = or nip ;
: wd-pre ( code -- code' )
  dup 17 = over 19 = or over 15 = or if stats-hook then
  dup 27 = if drop wd-menu 0 exit then
  dup 12 = if drop nums-flip 0 exit then
  dup 15 = if drop do-save modified 0= if -1 to want-browser -1 to done then 0 exit then
  hem-on if dup hem-block? if drop s" Hemingway: only forward" say 0 exit then then ;
: wd-wait ( -- code )
  begin key? 0= while  tick  50000 usleep drop  repeat
  read-key ;

( ---- Running ---- )
: wd-after-load ( -- )
  -1 to cl-len  st-start
  -1 to hl-on  layout-update
  repl 0= if 80 bytes to repl then
  toc-pos 0= if 500 cells bytes to toc-pos then
  word-count to words0  now-ms to last-auto  0 to last-sec ;
: wd-install ( -- )
  ['] wd-scroll is scroll   ['] wd-draw is draw   ['] wd-status is draw-status
  ['] wd-scan is hl-scan   ['] wrap-want is set-want
  ['] wrap-up is up-key   ['] wrap-down is down-key
  ['] wrap-home is home-key   ['] wrap-end is end-key
  ['] wd-pre is pre-key   ['] wd-wait is wait-key   ['] wd-after-load is after-load
  ['] wd-ini-kv is ini-kv   ['] wd-ini-body is ini-body   ['] wd-defname is ini-defname   ['] wd-ini-load is ini-load ;
: wd-main ( a n -- )
  wd-install
  begin
    dup 0= if 2drop browse 0= if ed-plain exit then ed-arg ed-arg-n then
    0 to want-browser
    ['] ed-main catch to werr
    werr if ed-plain werr throw then
    want-browser
  while 0 0 repeat
  ed-plain ;
' wd-main is wd-go
[defined] posix [if] only forth also internals definitions [else] only forth definitions [then]
| ;

: writhdeck ( "file" -- )   \ edit a text for writing
  bl parse 255 min dup to ed-arg-n  ed-arg swap cmove
  ed-loaded 0= if  ed-src eval-copy  -1 to ed-loaded  then
  wd-loaded 0= if  wd-src eval-copy  -1 to wd-loaded  then
  ed-arg ed-arg-n wd-go ;
