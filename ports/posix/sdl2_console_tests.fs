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

( Tests of the settings of the console window: lugforth.ini, colors )
\
\ Needs libSDL2, so this is not part of the default build. Run with:
\   SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy lugforth ports/posix/sdl2_console_tests.fs

needs ../../common/testing.fs

gui-load
sdl2 definitions

: test-ini-line
  s" size=17" ini-line   ini-size 17 =assert
  s" fg=ff8800" ini-line   fg-col $ff8800 =assert
  s" bg=00102A" ini-line   bg-col $00102a =assert
  s" font=/a/b.ttf" ini-line   fpath z>s s" /a/b.ttf" compare 0 =assert
  s" # fg=123456" ini-line   fg-col $ff8800 =assert
  s" nothing" ini-line   fg-col $ff8800 =assert ;

: test-accents
  80 to con-cols  0 to cx  0 to cy  0 to u-need
  s" aé€’z中😀" con-type
  0 0 grid sl@ 97 =assert   1 0 grid sl@ 233 =assert   2 0 grid sl@ $20ac =assert
  3 0 grid sl@ $2019 =assert   4 0 grid sl@ 122 =assert   5 0 grid sl@ $4e2d =assert
  6 0 grid sl@ $1f600 =assert   cx 7 =assert
  s" ÿ" con-type   7 0 grid sl@ 255 =assert
  233 bm-base 101 32 - =assert   $20ac bm-base 69 32 - =assert   $4e2d bm-base 63 32 - =assert   97 bm-base 65 =assert
  0 to sl  1 0 grid sl@ sel-cell  2 0 grid sl@ sel-cell  5 0 grid sl@ sel-cell  6 0 grid sl@ sel-cell
  sl 12 =assert   selbuf c@ $c3 =assert   selbuf 1+ c@ $a9 =assert   selbuf 2 + c@ $e2 =assert
  selbuf 5 + c@ $e4 =assert   selbuf 8 + c@ $f0 =assert   selbuf 11 + c@ $80 =assert
  con-clear ;

: test-hex6
  $ff hex6 s" 0000FF" compare 0 =assert
  $d2d2d2 hex6 s" D2D2D2" compare 0 =assert ;

: test-ini-parse
  s" size=9" ibuf swap cmove   s" size=9" nip to ibn
  ini-parse   ini-size 9 =assert
  s" size=11
fg=ffffff
" ibuf swap cmove   18 to ibn  ini-parse   ini-size 11 =assert   fg-col $ffffff =assert ;

: test-theme
  1 theme-set   fg-col $202020 =assert   bg-col $f4f4f0 =assert
  0 theme-set   fg-col $d2d2d2 =assert   bg-col 0 =assert
  1 pick-open   pick-on 1 =assert
  0 pick-open ;

: test-picker-colors
  255 255 255 rgb>hsv  255 =assert  0 =assert  drop
  255 0 0 rgb>hsv  255 =assert  255 =assert  0 =assert
  0 255 0 rgb>hsv  255 =assert  255 =assert  120 =assert
  0 0 255 rgb>hsv  255 =assert  255 =assert  240 =assert
  0 255 255 hsv>rgb  0 =assert  0 =assert  255 =assert
  120 255 255 hsv>rgb  0 =assert  255 =assert  0 =assert
  240 255 255 hsv>rgb  255 =assert  0 =assert  0 =assert
  $10 $20 $30 rgb-pack $102030 =assert ;

: test-fullscreen-key
  s" F11" fs-set   fs-sym $40000044 =assert   fs-mod 0 =assert
  s" Ctrl+Shift+x" fs-set   fs-sym [char] x =assert   fs-mod 6 =assert
  s" alt+enter" fs-set   fs-sym 13 =assert   fs-mod 1 =assert
  $100 to kmod   13 fs-key? assert   0 to kmod   13 fs-key? 0 =assert ;

: test-rgb-split   $336699 rgb-split $99 =assert $66 =assert $33 =assert ;

forth definitions
sdl2
run-tests
bye
