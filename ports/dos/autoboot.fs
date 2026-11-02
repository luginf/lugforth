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

\ lugforth prog.fs: the file is included at start, then the prompt, unless
\ it ends with bye or script-bye, as the games do. Else autoexec.fs in the
\ current directory, if there is one.
\ lugforth --turnkey prog.fs [prog.exe [data files...]]: writes prog.exe, a
\ copy of lugforth.exe with prog.fs, the files it includes (include, needs,
\ require, s" f" included) and the data files given. It runs without lugforth
\ or the sources (it still needs a DPMI host): its files are read from the
\ .exe first (files.h, bundle_find), then from the disk, and it ends with bye.
\ The same from a prompt: turnkey-file data.bin  turnkey prog.fs prog.exe
\ After the banner, before the first prompt. An error in it prints and
\ leaves the prompt, as an error typed there would.
internals definitions

( ---- turnkey: the list of the files, lower case, in tk-names ---- )
128 constant tk-max
0 value tk-names   0 value tk-n   0 value tk-main
create tk-tmp 260 allot
: lc ( c -- c' ) dup 65 >= over 90 <= and if 32 + then ;
: tk-lower ( a n -- a' n' )   \ lower case, in tk-tmp
  255 min dup >r 0 ?do dup i + c@ lc tk-tmp i + c! loop drop tk-tmp r> ;
: tk-slot ( i -- a ) 256 * tk-names + ;
: tk-name ( i -- a n ) tk-slot count ;
: tk-find { a n -- i }   \ a n in lower case
  tk-n 0 ?do a n i tk-name str= if i unloop exit then loop -1 ;
: tk-init   tk-names 0= if 256 tk-max * allocate throw to tk-names 0 to tk-n then ;
: tk-exists? ( a n -- f ) r/o open-file if drop 0 else close-file throw -1 then ;

: tk-register { a n }   \ a name already joined with its folder
  a n tk-lower tk-find 0< 0= if exit then
  a n tk-exists? 0= if ." turnkey: not found: " a n type cr exit then
  tk-n tk-max = if ." turnkey: too many files" cr exit then
  a n tk-lower tk-n tk-slot 2dup c! 1+ swap cmove  1 +to tk-n ;
: tk-top ( a n -- ) 0 0 2swap path-join tk-register ;   \ a name typed by the user
: tk-in ( a n fa fn -- )   \ a name met in the file fa fn: relative to its folder
  dirname 2swap path-join tk-register ;

( the tokens of a text, to find the includes )
0 value sc-a   0 value sc-end
0 value pa   0 value pn   0 value qa   0 value qn   0 value ta   0 value tn
0 value fa   0 value fn
: sc-more ( -- f ) sc-a sc-end < ;
: sc-skip   begin sc-more if sc-a c@ 33 < else 0 then while 1 +to sc-a repeat ;
: sc-token ( -- a n )
  sc-skip sc-a begin sc-more if sc-a c@ 32 > else 0 then while 1 +to sc-a repeat sc-a over - ;
: sc-eol   begin sc-more if sc-a c@ 10 <> else 0 then while 1 +to sc-a repeat ;
: sc-paren   begin sc-more if sc-a c@ 41 <> else 0 then while 1 +to sc-a repeat 1 +to sc-a ;
: tk-is? ( a n s n -- f ) 2>r tk-lower 2r> str= ;
: tk-kind ( -- n )   \ the token ta tn: 1 a \ comment, 2 a ( comment, 3 include, 4 included, 0 else
  ta tn s" \" tk-is? if 1 exit then
  ta tn s" (" tk-is? if 2 exit then
  ta tn s" include" tk-is? ta tn s" needs" tk-is? or ta tn s" require" tk-is? or if 3 exit then
  ta tn s" included" tk-is? ta tn s" required" tk-is? or if 4 exit then 0 ;
: quote-name? ( -- f )   \ the two tokens before are  s"  and  name"
  qn 2 = pn 2 > and 0= if 0 exit then
  qa c@ lc 115 = qa 1+ c@ 34 = and pa pn + 1- c@ 34 = and ;
: tk-step
  tk-kind
  dup 1 = if drop sc-eol exit then
  dup 2 = if drop sc-paren exit then
  dup 3 = if drop sc-token dup if fa fn tk-in else 2drop then exit then
  4 = if quote-name? if pa pn 1- fa fn tk-in then then ;
: tk-scan ( a n -- )   \ the text of the file fa fn
  over to sc-a  over + to sc-end  drop  0 to pn  0 to qn
  begin sc-token dup while
    to tn to ta
    tk-step
    pa pn to qn to qa   ta tn to pn to pa
  repeat 2drop ;
: tk-scan-file { i }
  i tk-name to fn to fa
  fa fn r/o open-file if drop exit then
  >r r@ file-size throw d>s  dup allocate throw  dup rot r@ read-file throw
  over >r tk-scan  r> free throw
  r> close-file throw ;

( ---- turnkey: the .exe ---- )
4096 constant tk-buf#
0 value tk-buf   0 value tk-fh   0 value tk-pos   0 value tk-ioff
create tk-4 4 allot
create tk-offs tk-max cells allot
create tk-lens tk-max cells allot
: tk-w ( a n -- ) dup +to tk-pos tk-fh write-file throw ;
: tk-w32 ( u -- ) 4 0 do dup 255 and tk-4 i + c! 8 rshift loop drop tk-4 4 tk-w ;
: tk-w16 ( u -- ) dup 255 and tk-4 c! 8 rshift 255 and tk-4 1+ c! tk-4 2 tk-w ;
: tk-copy { fh } begin tk-buf tk-buf# fh read-file throw dup while tk-buf swap tk-w repeat drop ;
: tk-copy-file ( a n -- ) r/o open-file throw dup tk-copy close-file throw ;

: tk-make { oa on ma mn }   \ writes oa on: lugforth.exe, the files of the list, their index
  bundle-name nip if ." turnkey: this is a turnkey program already, use lugforth.exe" cr exit then
  0 0 ma mn path-join 2dup tk-register tk-lower tk-find to tk-main
  tk-main 0< if exit then
  0 begin dup tk-n < while dup tk-scan-file 1+ repeat drop
  tk-buf# allocate throw to tk-buf
  oa on w/o bin create-file throw to tk-fh   0 to tk-pos
  0 argv tk-copy-file
  tk-n 0 ?do
    tk-pos i cells tk-offs + !
    i tk-name tk-copy-file
    tk-pos i cells tk-offs + @ - i cells tk-lens + !
  loop
  tk-pos to tk-ioff
  tk-n 0 ?do
    i tk-name dup tk-w16 tk-w
    i cells tk-offs + @ tk-w32   i cells tk-lens + @ tk-w32
  loop
  s" LFTK" tk-w  tk-ioff tk-w32  tk-n tk-w32  tk-main tk-w32
  tk-fh close-file throw  tk-buf free throw
  ." turnkey: " oa on type ." , " tk-n . ." files, " tk-pos . ." bytes" cr ;
: tk-run ( oa on ma mn -- )
  ['] tk-make catch ?dup if ." turnkey: error " . cr 2drop 2drop then
  tk-names free throw 0 to tk-names ;

create tk-out 270 allot
: tk-ext ( a n -- n' )   \ the length of a n without its extension
  { a n }  n begin dup 0> while
    1- dup a + c@ dup 46 = if drop exit then
    dup 47 = swap 92 = or if drop n exit then
  repeat drop n ;
: tk-default-out ( a n -- a' n' )   \ prog.fs: prog.exe
  255 min 2dup tk-ext { a n len }
  a tk-out len cmove  s" .exe" tk-out len + swap cmove  tk-out len 4 + ;
: tk-cli
  argc 3 < if ." usage: lugforth --turnkey prog.fs [prog.exe [data files...]]" cr 1 terminate exit then
  tk-init
  argc 4 > if argc 4 do i argv tk-top loop then
  argc 3 > if 3 argv else 2 argv tk-default-out then
  2 argv tk-run
  bye ;

forth definitions internals
: turnkey-file ( "name" -- ) tk-init bl parse tk-top ;   \ a data file for the next turnkey
: turnkey ( "prog.fs" "prog.exe" -- ) tk-init bl parse 2>r bl parse 2r> tk-run ;
internals definitions

: autoexec
  bundle-name dup if   \ a turnkey program: its main file, then back to DOS
    ['] included catch if 2drop ." error in " bundle-name type cr 1 terminate then
    bye
  then 2drop
  argc 2 >= if
    1 argv s" --turnkey" str= if tk-cli exit then
    1 argv ['] included catch if 2drop ." error in " 1 argv type cr then exit
  then
  s" autoexec.fs" r/o open-file if drop exit then close-file drop
  s" autoexec.fs" ['] included catch if 2drop ." autoexec.fs: error" cr then ;
' autoexec boot-prompt !
forth definitions
' ok
