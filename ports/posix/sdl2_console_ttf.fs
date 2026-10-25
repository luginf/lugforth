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

( The TrueType part of the console window, run by sdl2_console.fs when SDL_ttf and a font )
( are found. A string, so that the console works without SDL_ttf. It must not contain )
( a vertical bar. Loaded at boot: nothing is evaluated until the console asks. )

\ The source of the TrueType font part of the console window, compiled only when a font is asked for.
: gui-ttf-src r|
sdl2-ttf
sdl2 definitions also posix
0 value tfont   0 value font-file   0 value tex-gen
create tex-t 96 cells allot   create tex-v 1024 cells allot   create tex-k 1024 cells allot
z" TTF_FontFaceIsFixedWidth" 1 sdlttf TTF_FaceFixed ( a -- n )
0 value cur   0 value ca   0 value cn
create gz 8 allot
: font-ok? ( z -- f )
  dup 0= if drop 0 exit then
  12 TTF_OpenFont ?dup if TTF_CloseFont drop -1 else 0 then ;
: font-try ( z -- f )   ( on success the file name is kept in fpath )
  dup font-ok? 0= if drop 0 exit then
  z>s 299 min dup >r fpath swap cmove   0 fpath r> + c!
  fpath to font-file -1 ;
: font-find ( -- f )
  fpath c@ if fpath font-try if -1 exit then then
  z" LUGFORTH_FONT" c-getenv font-try if -1 exit then
  fcand begin dup @ ?dup while font-try if drop -1 exit then cell+ repeat drop 0 ;
: bname ( z -- a n )   ( the file name without directory and extension )
  z>s { a n }
  n begin dup 0> if dup 1- a + c@ [char] / <> else 0 then while 1- repeat
  a over +   n rot -   4 - 0 max ;
: has? ( a n pa pn -- f )   ( the pattern is in the text )
  { a n pa pn }
  n pn - 1+ 0 max 0 ?do
    a i + pn pa pn compare 0= if -1 unloop exit then
  loop 0 ;
: ttf-ext? ( a n -- f )
  dup 4 < if 2drop 0 exit then
  + 4 - { p }
  p c@ 46 = p 1+ c@ lower dup 116 = swap 111 = or and
  p 2 + c@ lower 116 = and   p 3 + c@ lower 102 = and ;
: plain-name? ( a n -- f )   ( a regular face: no Bold, Italic... in its name )
  2dup s" Bold" has? if 2drop 0 exit then
  2dup s" Italic" has? if 2drop 0 exit then
  2dup s" Oblique" has? if 2drop 0 exit then
  2dup s" Light" has? if 2drop 0 exit then
  2dup s" Thin" has? if 2drop 0 exit then
  2dup s" Black" has? if 2drop 0 exit then
  2dup s" Heavy" has? if 2drop 0 exit then
  2dup s" -B" has? if 2drop 0 exit then
  2dup s" -I" has? if 2drop 0 exit then
  2dup s" -RI" has? if 2drop 0 exit then
  2dup s" Narrow" has? if 2drop 0 exit then
  2dup s" Condensed" has? if 2drop 0 exit then
  2drop -1 ;
: font-add ( -- )   ( spath is a font file: keep it when it is fixed width )
  fcount fl-max >= if exit then
  spath bname plain-name? 0= if exit then
  spath 12 TTF_OpenFont ?dup 0= if exit then
  dup TTF_FaceFixed $ffffffff and 0<> swap TTF_CloseFont drop
  0= if exit then
  fused splen 1+ + fpool-size > if exit then
  spath fpool fused + splen 1+ cmove
  fpool fused + fcount cells flist + !
  1 +to fcount   splen 1+ +to fused ;
: sp+ ( a n -- )   spath splen + swap cmove ;
: scan-dir ( -- )   ( the directory in spath, splen bytes )
  sdepth 5 > if exit then
  0 spath splen + c!
  spath splen open-dir if drop exit then
  { dh }
  1 +to sdepth
  splen { len }
  begin dh readdir dup while
    to cur
    cur .d_name z>s to cn  to ca
    cn 62 < if
      ca c@ [char] . <> if
        len to splen
        s" /" sp+   1 +to splen
        ca cn sp+   cn +to splen
        0 spath splen + c!
        cur .d_type 4 = if recurse
        else ca cn ttf-ext? if font-add then then
      then
    then
  repeat drop
  dh closedir drop
  len to splen   -1 +to sdepth ;
: scan-root ( a n -- )   dup to splen   spath swap cmove   0 to sdepth   scan-dir ;
: scan-home ( a n sa sn -- )   ( HOME followed by a suffix )
  { a n sa sn }
  a spath n cmove   sa spath n + sn cmove
  spath n sn + scan-root ;
: scan-fonts ( -- )
  0 to fcount   0 to fused
  win? if s" C:/Windows/Fonts" scan-root then
  s" /usr/share/fonts" scan-root
  s" /usr/local/share/fonts" scan-root
  z" HOME" c-getenv ?dup if
    z>s 100 min 2dup s" /.local/share/fonts" scan-home
    s" /.fonts" scan-home
  then
  -1 to fscanned ;
: ttf-fill ( -- )
  hrel 0= if no-fonts exit then
  fscanned 0= if scan-fonts then
  fcount 0 ?do
    flist i cells + @ bname  i 100 + ent-add
  loop
  ent-count 0= if no-fonts exit then
  ent-sort ;
: ttf-pick ( k -- )
  cells flist + @ font-try 0= if exit then
  0 to last-built   con-layout   save-ini ;
: ttf-cell ( pt -- cw ch )
  tfont ?dup if TTF_CloseFont drop then
  font-file swap TTF_OpenFont to tfont
  tfont z" M" tsize tsize 4 + TTF_SizeUTF8 drop
  tsize sl@ tsize 4 + sl@ ;
: ttf-free ( -- )
  tex-gen con-gen = if
    95 0 do tex-t i cells + @ ?dup if SDL_DestroyTexture drop then loop
    1024 0 do tex-v i cells + @ ?dup if SDL_DestroyTexture drop then loop
  then
  tex-t 96 cells 0 fill   tex-v 1024 cells 0 fill   tex-k 1024 cells 0 fill ;
: utf8! ( cp -- )   \ the letter at gz, ended by 0
  dup $80 < if gz c! 0 gz 1+ c! exit then
  dup $800 < if
    dup 6 rshift $c0 or gz c!  $3f and $80 or gz 1+ c!  0 gz 2 + c! exit then
  dup $10000 < if
    dup 12 rshift $e0 or gz c!  dup 6 rshift $3f and $80 or gz 1+ c!  $3f and $80 or gz 2 + c!  0 gz 3 + c! exit then
  dup 18 rshift $f0 or gz c!  dup 12 rshift $3f and $80 or gz 1+ c!
  dup 6 rshift $3f and $80 or gz 2 + c!  $3f and $80 or gz 3 + c!  0 gz 4 + c! ;
0 value tr   0 value tg   0 value tb   ( the color of the text, given to a glyph when it is drawn )
: ttf-render ( cp -- texture or 0 )
  utf8!
  tfont gz $ffffffff TTF_RenderUTF8_Blended
  dup 0= if exit then
  dup renderer swap SDL_CreateTextureFromSurface
  swap SDL_FreeSurface drop ;
: ttf-one ( i -- )   \ the ASCII glyphs 33 to 126 are made at once
  dup 32 + ttf-render
  swap cells tex-t + ! ;
: ttf-build ( -- )
  ttf-free
  95 1 do i ttf-one loop
  con-gen to tex-gen ;
: ttf-other ( cp -- texture or 0 )   \ any other letter is made when first shown (a table of 1024, a clash replaces)
  dup 1021 mod { k }
  tex-k k cells + @ over = if drop tex-v k cells + @ exit then
  tex-v k cells + @ ?dup if SDL_DestroyTexture drop then
  dup ttf-render
  dup tex-v k cells + !   swap tex-k k cells + ! ;
: ttf-glyph ( cp x y -- )
  { cp x y }
  cp 33 127 within if cp 32 - cells tex-t + @ else cp ttf-other then
  ?dup if
    dup tr tg tb SDL_SetTextureColorMod drop
    x y con-cw con-ch gdst rect-at!
    renderer swap 0 gdst SDL_RenderCopy drop
  then ;
: ttf-tint ( r g b -- )   to tb to tg to tr ;
: ttf-install ( -- )
  TTF_Init drop
  font-find if
    ['] ttf-cell is font-cell   ['] ttf-build is atlas-build
    ['] ttf-glyph is glyph-at   ['] ttf-tint is glyph-tint
    ['] ttf-fill is font-fill   ['] ttf-pick is font-pick
    8 to hmin   72 to hmax   2 to hstep   28 to hdiv   8 to hrel
  then ;
ttf-install
previous
| ;
