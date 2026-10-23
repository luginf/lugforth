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

\ The words of ueForth that ed and writhdeck (ports/posix/editor.fs, writhdeck.fs) use and gforth lacks.
\ Loaded by ports/gforth/writhdeck-gforth.fs before the editors. Tested with gforth 0.7.9.

decimal
forth-wordlist set-current   warnings off
require mkdir.fs
vocabulary ansi   vocabulary internals   vocabulary posix   vocabulary termios

\ ---- Strings and memory ----
: z>s ( z -- a n )   dup begin dup c@ while 1+ repeat over - ;
: zcopy ( a n z -- )   ( the text a n as a zero ended string at z )
  2dup + >r swap cmove  0 r> c! ;
: z" ( "text" -- z )   ( a zero ended string, kept in the heap )
  [char] " parse  dup 1+ allocate throw  dup >r  zcopy  r>
  state @ if postpone literal then ; immediate
: zlen ( z -- n )   z>s nip ;
: bytes ( n -- a )   allocate throw ;
: n. ( n -- )   dup abs 0 <# #s rot sign #> type ;
: lower ( c -- c' )   dup 65 91 within if 32 + then ;
: noop ;

\ ---- ANSI terminal (common/ansi.fs of lugforth) ----
: esc   27 emit ;   : bel   7 emit ;
: clear-to-eol   esc ." [0K" ;
: hide   esc ." [?25l" ;
: show   esc ." [?25h" ;
: terminal-save   esc ." [?1049h" ;
: terminal-restore   esc ." [?1049l" ;
: fg ( n -- )   esc ." [38;5;" n. ." m" ;
: bg ( n -- )   esc ." [48;5;" n. ." m" ;
: normal   esc ." [0m" ;
: page   esc ." [2J" esc ." [H" ;
: set-title ( a n -- )   esc ." ]0;" type bel ;

\ ---- Time ----
: ms-ticks ( -- n )   utime 1000 um/mod nip ;
: unix-time ( -- secs )   time&date 2drop 2drop 2drop  utime 1000000 um/mod nip ;

\ ---- usleep and sysfunc ----
: usleep ( us -- n )   1000 / 1 max ms 0 ;
: sf-getenv ( z -- z|0 )   z>s getenv dup 0= if 2drop 0 else drop then ;
: sf-mkdir ( z mode -- n )   >r z>s r> mkdir-parents 0<> ;
: sf-chdir ( z -- n )   z>s set-dir 0<> ;
: sf-getcwd ( buf n -- buf|0 )   over swap 1- get-dir + 0 swap c! ;
: sf-remove ( z -- n )   z>s delete-file 0<> ;
: sf-rename ( z1 z2 -- n )   >r z>s r> z>s rename-file 0<> ;
: sf-time ( z -- secs )   drop unix-time ;
: sysfunc ( z n "name" -- )   ( the word is the C function of that name, by our sf- words )
  drop z>s { a n }
  3 n + allocate throw { p }
  s" sf-" p swap cmove   a p 3 + n cmove
  p 3 n + find-name dup 0= abort" sysfunc: unknown"
  name>interpret parse-name nextname alias  p free throw ;

\ ---- Directories: readdir gives an entry, .d_name its zero ended name, .d_type 4 for a directory ----
\ Shape of an entry: the type (a cell), then the name (256 bytes).
create dent 264 allot
: is-dir? ( a n -- f )   open-dir if drop 0 else close-dir drop -1 then ;
: readdir ( dh -- ent|0 )
  >r dent cell+ 255 r> read-dir throw   ( n flag )
  0= if drop 0 exit then
  dent cell+ + 0 swap c!
  dent cell+ z>s is-dir? if 4 else 8 then dent !
  dent ;
: .d_name ( ent -- z )   cell+ ;
: .d_type ( ent -- n )   @ ;
: closedir ( dh -- n )   close-dir ;

\ ---- The terminal: gforth reads keys itself; the modes are set through stty ----
: stdin-tty? ( -- f )   0 0 ;
: isatty ( fd -- f )   drop 0 ;
: raw-mode ( -- )   s" stty raw -echo" system ;
: delay-mode ( -- )   s" stty sane" system ;
: normal-mode ( -- )   delay-mode ;
: sizeof(termios) ( -- n )   64 ;
: new-termios ( -- a )   dent ;
: .c_lflag ( a -- a' )   12 + ;
: termios@ ( a -- )   drop ;
: termios! ( a -- )   drop ;

\ ---- Numbers: <# takes one cell in ueForth (a double in ANS) ----
' <# alias <#d
: <# ( n -- )   0 <#d ;

\ ---- file-size gives one cell in ueForth (a double in ANS) ----
: file-size ( fid -- n ior )   file-size >r d>s r> ;
