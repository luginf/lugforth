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

needs ../../common/testing.fs

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

: test-outline
  s" = A =
text
== B ==
# C
x" fresh   500 cells bytes to toc-pos   toc-collect
  toc-n 3 =assert   toc-pos @ 0 =assert   toc-pos cell+ @ 11 =assert
  0 level 1 =assert   11 level 2 =assert ;

run-tests
