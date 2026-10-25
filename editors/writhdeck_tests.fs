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

( Tests of writhdeck, on its buffer: no terminal is needed )

needs ../common/testing.fs

only forth definitions
vocabulary writhdeck-tests
ed-src eval-copy
wd-src eval-copy
only forth also editor also writhdeck-tests definitions

: fresh ( a n -- )   ed-init  tuck  text swap cmove  to len  10 to ww ;

: test-segments
  s" aaa bbb ccc ddd" fresh
  0 seg 8 =assert 8 =assert
  8 seg 16 =assert 15 =assert
  s" abcdefghijklmnop" fresh
  0 seg 10 =assert 10 =assert
  s" ab
cd" fresh
  0 seg 3 =assert 2 =assert ;

: test-visual-lines
  s" aaa bbb ccc ddd" fresh
  0 vstart 0 =assert   9 vstart 8 =assert   15 vstart 8 =assert
  8 vprev 0 =assert   0 vnext 8 =assert
  10 vcol 2 =assert ;

: test-vat
  s" aaa bbb ccc ddd" fresh
  0 2 vat 2 =assert
  0 9 vat 7 =assert
  8 3 vat 11 =assert ;

: test-count-words
  s" one two  three
four" fresh   count-words 4 =assert
  s" " fresh   count-words 0 =assert ;

: test-markup
  s" = Title =
% note
- item **b** and //i//" fresh   -1 to hl-on  md-scan
  cbuf c@ 3 =assert   cbuf 10 + c@ 1 =assert
  cbuf 17 + c@ 4 =assert   cbuf 24 + c@ 6 =assert   cbuf 34 + c@ 7 =assert
  s" see http://x.org/a" fresh   md-scan   cbuf 9 + c@ 0 =assert
  0 to hl-on ;

: test-window
  s" = A =
b
c
d
**x** e" fresh   -1 to hl-on  2 to text-rows  0 to top  md-window
  cbuf c@ 3 =assert   cbuf 12 + c@ 0 =assert   0 to hl-on ;

: test-cursor-memory
  0 cp-cnt !
  s" /a/f" 40 cp-put   s" /a/g" 7 cp-put   s" /a/f" 55 cp-put
  cp-cnt @ 2 =assert   s" /a/f" cp-find 0 =assert   0 cells cp-pos + @ 55 =assert
  s" 123 /a/h x" cp-line   s" /a/h x" cp-find 2 =assert   2 cells cp-pos + @ 123 =assert
  s" /nowhere" cp-find -1 =assert
  0 cp-cnt ! ;

: test-ini-open-last
  0 to open-last
  s" open_last" s" yes" wd-ini-kv   open-last 0<> assert
  s" open_last" s" no" wd-ini-kv   open-last 0 =assert ;

: test-outline
  s" = A =
text
== B ==
# C
x" fresh   500 cells bytes to toc-pos   toc-collect
  toc-n 3 =assert   toc-pos @ 0 =assert   toc-pos cell+ @ 11 =assert
  0 level 1 =assert   11 level 2 =assert ;

: test-word-occurrences
  s" The cat and the Cat saw a dog; the end" fresh
  3000 cells bytes to wo-tab  wo-collect  wo-sort
  wo-n 6 =assert   0 wo-cnt 3 =assert   1 wo-cnt 2 =assert
  0 wo-e @ 0 =assert   wo-tab free drop ;

\ writhdeck.ini written, then read back (not in the settings folder of the user)
: test-wd-ini
  s" x" fresh   ini-init
  s" filename words" zl zset   s" modes" zc zset   s" goal help_bar" zr zset   s" hi there" hbar zset
  3 to theme   44 to colw   123 to goal   0 to wrap-on   -1 to hem-cfg   -1 to auto-cfg   5 to auto-min   0 to bar-on
  s" out/tests/wd_ini_test.ini" w/o create-file throw to fhs
  wd-ini-body   fhs close-file drop
  wd-defaults   0 to theme   72 to colw   500 to goal   -1 to wrap-on   0 to hem-cfg   0 to auto-cfg   1 to auto-min
  ['] wd-ini-kv is ini-kv
  s" out/tests/wd_ini_test.ini" ini-read assert
  ['] ed-ini-kv is ini-kv
  theme 3 =assert   colw 44 =assert   goal 123 =assert   wrap-on 0 =assert
  hem-cfg assert   auto-cfg assert   auto-min 5 =assert   bar-on 0 =assert
  zl zget s" filename words" compare 0 =assert
  zc zget s" modes" compare 0 =assert
  zr zget s" goal help_bar" compare 0 =assert
  hbar zget s" hi there" compare 0 =assert
  0 to theme   72 to colw   500 to goal   -1 to wrap-on   0 to hem-cfg   0 to hem-on   0 to auto-cfg   0 to autosave   1 to auto-min
  wd-defaults ;

: test-wd-lists
  0 fav-cnt !   0 rec-cnt !
  s" /a/one" fav-flip assert   s" /a/two" fav-flip assert   fav-cnt @ 2 =assert
  s" /a/one" fav? assert   s" /a/zzz" fav? 0 =assert
  s" /a/one" fav-flip 0 =assert   fav-cnt @ 1 =assert   s" /a/one" fav? 0 =assert   s" /a/two" fav? assert
  s" /r/1" rec-tab rec-cnt 3 sl-front   s" /r/2" rec-tab rec-cnt 3 sl-front   s" /r/3" rec-tab rec-cnt 3 sl-front
  s" /r/2" rec-tab rec-cnt 3 sl-front
  rec-cnt @ 3 =assert
  rec-tab 0 sl-a z>s s" /r/2" compare 0 =assert   rec-tab 1 sl-a z>s s" /r/3" compare 0 =assert   rec-tab 2 sl-a z>s s" /r/1" compare 0 =assert
  s" /r/4" rec-tab rec-cnt 3 sl-front
  rec-cnt @ 3 =assert   rec-tab 0 sl-a z>s s" /r/4" compare 0 =assert   rec-tab 2 sl-a z>s s" /r/3" compare 0 =assert
  0 fav-cnt !   0 rec-cnt !
  s" F /x/y" lst-line   s" R /x/z" lst-line   s" junk" lst-line   s" F " lst-line
  fav-cnt @ 1 =assert   rec-cnt @ 1 =assert   s" /x/y" fav? assert
  0 fav-cnt !   0 rec-cnt ! ;

: test-line-at
  s" a
bb
ccc

e" fresh   -1 to cl-len
  0 line-at 1 =assert   5 line-at 3 =assert   9 line-at 4 =assert   10 line-at 5 =assert
  3 line-at 2 =assert   0 line-at 1 =assert
  s" a
b" fresh   0 to cur   2 line-at 2 =assert
  s" x
a
b" fresh   0 to cur   4 line-at 3 =assert   -1 to cl-len ;

: test-daily-stats
  0 dstr s" 1970-01-01" compare 0 =assert   19782 dstr s" 2024-02-29" compare 0 =assert
  20000 dstr s" 2024-10-04" compare 0 =assert   20750 dstr s" 2026-10-24" compare 0 =assert
  0 st-cnt !
  s" /a/f" 100 40 st-put   s" /a/f" 100 25 st-put   s" /a/f" 101 10 st-put   s" /a/g" 100 5 st-put
  st-cnt @ 3 =assert
  s" /a/f" 100 st-find 0 =assert   0 cells st-w + @ 40 =assert   100 day-total 45 =assert   101 day-total 10 =assert
  s" S 100 7 /a/h x" lst-line   s" /a/h x" 100 st-find 3 =assert   3 cells st-w + @ 7 =assert
  0 st-cnt ! ;

run-tests
