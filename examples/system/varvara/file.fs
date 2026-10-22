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

( The File devices of Varvara: ports $a0 and $b0 )
\
\ Same behavior as the C uxn: a ROM names a file (or a directory), then reads,
\ writes, appends, stats or deletes it. Reading a directory gives one line
\ per entry ("size name", "---- dir/", "???? name" over 64 KB, "!!!! name" when
\ it cannot be read). Files outside the working directory are refused (the
\ result is 0 and a warning goes to stderr). Linux only: it reads the name of a
\ directory entry at offset 19 of struct dirent. On the bare-metal board of
\ ports/rpi (no posix vocabulary) the same code runs on the standard file words
\ of the FAT of the SD card: the working directory is its root, a name with ..
\ is refused (long names are fine).
\
\   setup-files ( -- )   installs the two devices

needs uxn.fs
also uxn definitions

[defined] posix [if]
also posix
z" fopen" 2 sysfunc libc-fopen
z" fclose" 1 sysfunc libc-fclose
z" fread" 4 sysfunc libc-fread
z" fwrite" 4 sysfunc libc-fwrite
z" fflush" 1 sysfunc libc-fflush
z" fseek" 3 sysfunc libc-fseek
z" ftell" 1 sysfunc libc-ftell
z" opendir" 1 sysfunc libc-opendir
z" readdir" 1 sysfunc libc-readdir
z" closedir" 1 sysfunc libc-closedir
z" unlink" 1 sysfunc libc-unlink
z" getcwd" 2 sysfunc libc-getcwd
z" realpath" 2 sysfunc libc-realpath
previous
[then]

: dev2! ( v p -- ) over 8 rshift over dev! 1+ dev! ;

( ---- one file device: five cells of state, then its name ---- )
5 cells 4096 + constant /file
create files 2 /file * allot
: file-struct ( id -- a ) /file * files + ;
: ffld ( n "name" -- ) create cells , does> @ + ;
0 ffld >f   1 ffld >dir   2 ffld >st   3 ffld >out   4 ffld >de
: >name ( c -- a ) 5 cells + ;
1 constant s-read   2 constant s-write   3 constant s-dir

create cwdbuf 4352 allot
create rpbuf 4352 allot
create pbuf 4352 allot
create ebuf 4400 allot
0 value en

: zlen ( z -- n ) 0 begin 2dup + c@ while 1+ repeat nip ;
: zeq ( z1 z2 -- f ) { a b } begin a c@ b c@ = a c@ 0<> and while 1 +to a 1 +to b repeat a c@ b c@ = ;
[defined] posix [if]
also posix
: err-z ( z -- )
  dup zlen { n } stderr swap n write drop ;
: err-nl ( -- ) 10 pad c! stderr pad 1 write drop ;
: warn ( z -- )
  flush-out
  stderr s" file warning: blocked attempt to access " write drop
  err-z  stderr s"  outside of sandbox" write drop  err-nl ;
previous
[else]
: warn ( z -- )
  flush-out  ." file warning: blocked attempt to access " dup zlen type ."  outside of sandbox" cr ;
[then]

0 value rd-p   0 value rd-n   0 value rd-c

[defined] posix [if]
: cwd ( -- ) cwdbuf 4096 libc-getcwd drop ;

: build-path ( z -- )   ( the absolute path of z in pbuf )
  { z }
  z c@ [char] / = if z pbuf z zlen 1+ cmove exit then
  cwdbuf pbuf cwdbuf zlen cmove
  [char] / pbuf cwdbuf zlen + c!
  z pbuf cwdbuf zlen + 1+ z zlen 1+ cmove ;

0 value rp-m
: strip-last ( -- f )   ( cut pbuf at its last slash )
  pbuf zlen to rp-m
  begin rp-m 0> while
    rp-m 1- to rp-m
    rp-m pbuf + c@ [char] / = if 0 rp-m pbuf + c! -1 exit then
  repeat 0 ;
: real-path ( z -- f )   ( the real path of z in rpbuf, dropping trailing parts that do not exist )
  cwd build-path
  begin pbuf rpbuf libc-realpath 0= while strip-last 0= if 0 exit then repeat -1 ;

: check-sandbox ( c -- )
  { c }  c >name real-path
  if
    cwdbuf zlen { n }
    n 0 ?do cwdbuf i + c@ rpbuf i + c@ <> if 1 c >out ! leave then loop
    rpbuf zlen n < if 1 c >out ! then
  else 1 c >out ! then
  c >out @ if c >name warn then ;
: skip-dotdot? ( -- f )   ( ".." of the directory being listed leads out of the sandbox )
  rd-c >name real-path if rpbuf cwdbuf zeq else 0 then ;
: dirent-name ( de -- z ) 19 + ;
: file-length ( h -- n ) dup 0 2 libc-fseek drop libc-ftell ;
[else]   ( the FAT: its root is the working directory )
: open-or-0 ( fh ior -- h ) if drop 0 then ;
: append-open ( z n -- h )
  2dup r/w open-file if drop w/o create-file open-or-0
  else nip nip dup file-size drop over reposition-file drop then ;
: libc-fopen ( z mode -- h )   ( mode: "rb" "wb" "ab" )
  c@ over zlen { z m n }
  m [char] r = if z n r/o open-file open-or-0 exit then
  m [char] w = if z n w/o create-file open-or-0 exit then
  z n append-open ;
: libc-fclose ( h -- n ) close-file ;
: libc-fread ( buf size len h -- n ) >r nip r> read-file if drop 0 then ;
: libc-fwrite ( buf size len h -- n ) { buf size len h } buf len h write-file if 0 else len then ;
: libc-fflush ( h -- n ) flush-file ;
: file-length ( h -- n ) file-size drop ;
: libc-opendir ( z -- d ) dup zlen open-dir if drop 0 then ;
also internals
: libc-readdir ( d -- de ) readdir ;
previous
: libc-closedir ( d -- n ) close-dir ;
: libc-unlink ( z -- n ) dup zlen delete-file ;
: dirent-name ( de -- z )   ( the board's readdir ends a directory with a slash: entry adds its own )
  dup zlen dup 0= if drop exit then  1- over + dup c@ [char] / = if 0 swap c! else drop then ;
: skip-dotdot? ( -- f ) -1 ;
: dotdot? ( z -- f )
  begin dup c@ while
    dup c@ [char] . = over 1+ c@ [char] . = and if drop -1 exit then 1+
  repeat drop 0 ;
: check-sandbox ( c -- )
  { c }  c >name dotdot? if 1 c >out !  c >name warn then ;
[then]

: f-reset ( c -- )
  { c }
  c >f @ ?dup if libc-fclose drop then  0 c >f !
  c >dir @ ?dup if libc-closedir drop then  0 c >dir !
  0 c >de !  0 c >st !  0 c >out ! ;

: f-init ( c addr lim -- )   ( names the file stored in memory at addr )
  { c addr lim }
  c f-reset
  4095 lim min to lim
  lim 0 ?do
    addr i + ram@ dup c >name i + c!
    0= if c check-sandbox unloop exit then
  loop
  0 c >name c! ;

( ---- directory entries ---- )
: e-char ( c -- ) ebuf en + c! 1 +to en ;
: e-str ( a n -- ) 0 ?do dup i + c@ e-char loop drop ;
: e-hex4 ( n -- ) 4 0 do dup 12 i 4 * - rshift $f and s" 0123456789abcdef" drop + c@ e-char loop drop ;

0 value ent-f   0 value ent-n
: entry ( dest len path ba bn fail -- n )
  { dest len path ba bn fail }
  0 to en
  len bn 8 + < fail 2 < and if 0 exit then
  path libc-opendir ?dup if
    libc-closedir drop  s" ---- " e-str  ba bn e-str  [char] / e-char  10 e-char
  else
    path s" rb" drop libc-fopen ?dup if
      to ent-f  ent-f file-length ent-f libc-fclose drop
      dup $10000 < if e-hex4 else drop s" ????" e-str then
      32 e-char  ba bn e-str  10 e-char
    else
      fail 0= if 0 exit then
      s" !!!! " e-str  ba bn e-str  10 e-char
    then
  then
  en fail 2 = if len min then to ent-n
  ebuf dest ram + ent-n cmove  ent-n ;

: basename-of ( z -- a n )
  dup zlen { z n }
  n begin dup 0> while
    1- dup z + c@ [char] / = if 1+ dup z + swap n swap - exit then
  repeat drop z n ;

: f-stat ( c dest len -- n )
  { c dest len }
  c >out @ if 0 exit then
  dest len c >name  c >name basename-of  2 entry ;

: dir-name ( c -- z ) >de @ dirent-name ;
: one-entry ( -- n )   ( 0 stop, -1 skipped, else bytes written )
  rd-c dir-name { nm }
  nm s" ." drop zeq if -1 exit then
  nm s" .." drop zeq if skip-dotdot? if -1 exit then then
  rd-c >name zlen nm zlen + 1+ 4352 < if
    rd-c >name pbuf rd-c >name zlen cmove
    [char] / pbuf rd-c >name zlen + c!
    nm pbuf rd-c >name zlen + 1+ over zlen 1+ cmove
  else 0 pbuf c! then
  rd-p rd-n pbuf nm dup zlen 1 entry ;

: read-dir ( c dest len -- n )
  to rd-n  to rd-p  to rd-c
  rd-p { start }
  rd-c >de @ 0= if rd-c >dir @ libc-readdir rd-c >de ! then
  begin rd-c >de @ while
    one-entry dup 0= if drop rd-p start - exit then
    dup 0> if dup +to rd-p negate +to rd-n else drop then
    rd-c >dir @ libc-readdir rd-c >de !
  repeat
  rd-p start - ;
\ read-dir needs its entries written where rd-p points, in the memory of the ROM
\ (entry copies into ram + dest)

: f-read ( c dest len -- n )
  { c dest len }
  c >out @ if 0 exit then
  c >st @ dup s-read <> swap s-dir <> and if
    c f-reset
    c >name libc-opendir ?dup if c >dir ! s-dir c >st !
    else c >name s" rb" drop libc-fopen ?dup if c >f ! s-read c >st ! then then
  then
  c >st @ s-read = if dest ram + 1 len c >f @ libc-fread $ffff and exit then
  c >st @ s-dir = if c dest len read-dir exit then
  0 ;

0 value wr-n
: f-write ( c dest len flags -- n )
  { c dest len flags }
  c >out @ if 0 exit then
  c >st @ s-write <> if
    c f-reset
    c >name  flags 1 and if s" ab" else s" wb" then drop libc-fopen ?dup if c >f ! s-write c >st ! then
  then
  c >st @ s-write = if
    dest ram + 1 len c >f @ libc-fwrite $ffff and to wr-n
    wr-n 0> if c >f @ libc-fflush 0<> if 0 exit then then
    wr-n exit
  then
  0 ;

: f-delete ( c -- n ) dup >out @ if drop 0 exit then >name libc-unlink $ffffffff and 0= if 1 else 0 then ;

( ---- the device ports ---- )
0 value fc   0 value fb   0 value fa   0 value fl
: span ( -- )   ( addr and length of the call, length cut at the end of memory )
  fl $10000 fa - min to fl ;

: file-deo ( id p -- )
  { id p }  id file-struct to fc  p $f0 and to fb
  p $f and case
    $5 of fb 4 + dev2@ to fa  fb $a + dev2@ to fl  span
          fc fa fl f-stat fb 2 + dev2! endof
    $6 of fc f-delete fb 2 + dev2! endof
    $9 of fb 8 + dev2@ to fa  fc fa $10000 fa - f-init  0 fb 2 + dev2! endof
    $d of fb $c + dev2@ to fa  fb $a + dev2@ to fl  span
          fc fa fl f-read fb 2 + dev2! endof
    $f of fb $e + dev2@ to fa  fb $a + dev2@ to fl  span
          fc fa fl fb 7 + dev@ f-write fb 2 + dev2! endof
  endcase ;
: file-a-deo ( p -- ) 0 swap file-deo ;
: file-b-deo ( p -- ) 1 swap file-deo ;

: setup-files ( -- )
  files 2 /file * erase
  ['] plain-dei ['] file-a-deo $a device!
  ['] plain-dei ['] file-b-deo $b device! ;

previous definitions
