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

( Tests of the text editor `ed`, on its buffer: no terminal is needed )

needs ../../common/testing.fs

only forth definitions
vocabulary editor-tests
ed-src eval-copy
only forth also editor also editor-tests definitions

: fresh ( a n -- )   ed-init  tuck  text swap cmove  to len ;
: text= ( a n -- )   text len compare 0 =assert ;

: test-lines
  s" one" fresh
  s" ab" fresh   0 bol 0 =assert   1 eol 2 =assert
  s" ab
cd
" fresh
  4 bol 3 =assert   0 eol 2 =assert   0 next-line 3 =assert   4 prev-line 0 =assert
  6 next-line 6 =assert ;

: test-columns
  s" a	b" fresh   2 column 4 =assert
  s" a	b" fresh   0 4 at-col 2 =assert
  s" abcd" fresh   0 9 at-col 4 =assert
  s" é" fresh   2 column 1 =assert   1 prev-char 0 =assert   0 next-char 2 =assert ;

: test-line-no
  s" a
b
c" fresh   0 line-no 1 =assert   2 line-no 2 =assert   4 line-no 3 =assert ;

: test-insert-delete
  s" abc" fresh
  s" XY" 1 raw-insert   s" aXYbc" text=
  1 2 raw-delete   s" abc" text= ;

: test-undo
  s" abc" fresh
  s" ZZ" 3 put   s" abcZZ" text=   undo   s" abc" text=
  1 1 cut   s" ac" text=   undo   s" abc" text=   2 cur =assert ;

: test-redo
  s" abc" fresh
  s" ZZ" 3 put   undo   s" abc" text=   redo   s" abcZZ" text=   undo   s" abc" text=
  1 1 cut   undo   redo   s" ac" text=   undo   undo   s" abc" text=
  redo   s" ac" text=   s" Q" 0 put   redo   s" Qac" text= ;

: test-typed-undo
  s" " fresh
  [char] h ins-byte [char] i ins-byte   s" hi" text=   2 cur =assert
  undo   0 len =assert ;

: test-selection
  s" abcdef" fresh
  1 to anchor   4 to cur   copy-sel   clip-n 3 =assert   kill-sel   s" aef" text=
  1 cur =assert   paste   s" abcdef" text=   4 cur =assert ;

: test-indent
  s"   ab" fresh   4 to cur   indent-of 2 =assert
  newline   s"   ab
  " text= ;

: test-goto
  s" a
bb
ccc" fresh
  2 goto-line   cur 2 =assert   3 goto-line   cur 5 =assert   9 goto-line   cur 8 =assert ;

: test-numbers
  s" 123" num? assert   s" -5" num? assert   s" $ff" num? assert   s" 1+" num? 0 =assert
  s" add" num? 0 =assert   s" $" num? 0 =assert ;

: test-keywords
  s" if" kw? assert   s" THEN" kw? assert   s" dup" kw? 0 =assert   s" :" kw? assert ;

: test-highlight
  s" : sq dup * ; \ c" fresh   -1 to hl-on  hl-scan
  cbuf c@ 3 =assert   cbuf 2 + c@ 5 =assert   cbuf 5 + c@ 0 =assert
  cbuf 14 + c@ 1 =assert
  s" ( a b ) 12 ." fresh   hl-scan
  cbuf 2 + c@ 1 =assert   cbuf 8 + c@ 4 =assert
  s" .(" fresh   hl-scan
  0 to hl-on ;

: test-name-ext
  s" x.fs" set-name   forth-name? assert
  s" x.txt" set-name   forth-name? 0 =assert
  s" .fs" set-name   forth-name? assert ;

: test-search
  s" Hello world" fresh   s" WOR" lastfind swap cmove   3 to lastfind-n
  0 search-from 6 =assert   7 search-from -1 =assert ;

: test-themes
  s" colorforth" theme-find 1 =assert   s" mono" theme-find 4 =assert   s" nothing" theme-find -1 =assert
  1 to theme   0 theme-col 46 =assert   1 theme-col 255 =assert   5 theme-col 196 =assert
  0 to theme   1 theme-col 244 =assert   0 theme-col 0 =assert
  4 to theme   3 theme-col 0 =assert
  0 to theme ;

: test-ini-kv
  s" theme" s" solarized" ini-kv   theme 3 =assert
  s" width" s" 60" ini-kv   colw 60 =assert
  s" width" s" 5" ini-kv   colw 30 =assert
  s" wrap" s" 0" ini-kv   wrap-on 0 =assert
  s" wrap" s" 1" ini-kv   wrap-on assert
  s" theme" s" bogus" ini-kv   theme 3 =assert
  0 to theme   72 to colw ;

run-tests
