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

\ ed and writhdeck of Lugforth under gforth (tested with 0.7.9), from the files of editors/, unchanged:
\   gforth ports/gforth/writhdeck-gforth.fs -e "writhdeck notes.txt"
\ The editors are Forth source held in a string between r| and a vertical bar. The loader cuts that text out,
\ writes it to a temporary file and includes it, on top of compat.fs (the ueForth words gforth lacks).

\ The directory of this file, with its final slash, to find the other files
create here-dir 300 allot   0 value here-n
: find-here ( -- )
  sourcefilename { a n }
  n begin dup 0> if dup 1- a + c@ [char] / <> else 0 then while 1- repeat
  dup to here-n   a here-dir rot cmove ;
find-here

: in-here ( a n -- a' n' )   here-dir here-n + swap dup >r cmove  here-dir here-n r> + ;

s" compat.fs" in-here included

create ed-arg 256 allot   0 value ed-arg-n
defer ed-go ( a n -- )
defer wd-go ( a n -- )

: slurp ( a n -- a' n' )
  r/o open-file throw >r
  r@ file-size throw d>s dup allocate throw swap
  2dup r@ read-file throw drop
  r> close-file throw ;
: body ( a n -- a' n' )   ( the text between the first r| and the next line starting with a vertical bar )
  s" r|" search 0= abort" no r| in the source"  2 /string
  2dup s\" \n| ;" search 0= abort" no end of the source"
  nip - ;
: spit ( a n name-a name-n -- )
  w/o create-file throw >r  r@ write-file throw  r> close-file throw ;
: tmp-name ( -- a n )   s" /tmp/lugforth-gforth-body.fs" ;
: load-source ( name-a name-n -- )   ( the path is relative to this file )
  in-here slurp body tmp-name spit  tmp-name included ;

s" ../../editors/editor.fs" load-source
s" ../../editors/writhdeck.fs" load-source

only forth definitions
: ed ( "file" -- )   parse-name 255 min dup to ed-arg-n  ed-arg swap cmove  ed-arg ed-arg-n ed-go ;
: writhdeck ( "file" -- )   parse-name 255 min dup to ed-arg-n  ed-arg swap cmove  ed-arg ed-arg-n wd-go ;
