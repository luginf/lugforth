\ Copyright 2021 Bradley D. Nelson
\ Modifications Copyright 2026 Luginfo10
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

( Including Files )

internals definitions

: ends/ ( a n -- f ) 1- + c@ dup 47 = swap 92 = or ;   ( / or a Windows backslash )
: dirname ( a n -- )
   dup if
     2dup ends/ if 1- then
   then
   begin dup while
     2dup ends/ if exit then 1-
   repeat ;

: starts./ ( a n -- f )
   2 < if drop 0 exit then
   2 s" ./" str= ;

: starts../ ( a n -- f )
   3 < if drop 0 exit then
   3 s" ../" str= ;

0 value sourcefilename&
0 value sourcefilename#
: sourcefilename ( -- a n ) sourcefilename& sourcefilename# ;
: sourcefilename! ( a n -- ) to sourcefilename# to sourcefilename& ;
: sourcedirname ( -- a n ) sourcefilename dirname ;

: run-lines   begin refill-file while evaluate-buffer repeat ;
: evaluate-lines ( a n fh -- )   ( the text a n, one line at a time: SOURCE is the line being read )
   'tib @ >r #tib @ >r >in @ >r file-end @ >r src-fid @ >r
   src-fid !  over + file-end !  1- 'tib !  0 #tib !
   ['] run-lines catch
   r> src-fid ! r> file-end ! r> >in ! r> #tib ! r> 'tib !  throw ;

: include-fid ( fh -- )
   dup file-size throw d>s
   dup allocate throw           ( fh n a )
   dup >r                       ( the text buffer waits on the return stack )
   swap rot dup >r              ( a n fh )
   read-file throw              ( n' )
   r> r@ -rot                   ( a n' fh )
   src-ref @ >r  0 src-ref !
   evaluate-lines
   src-ref @ r> src-ref !
   r> swap if drop else free throw then ;   ( an r| r~ r" string points into the text: keep it )

: raw-included ( a n -- )
   r/o open-file throw
   dup >r include-fid
   r> close-file throw ;

0 value included-files

: path-join { a a# b b# -- a n }
  b if
    b c@ [char] / = if 0 to a# then
  then
  a# b# + { r# } r# cell+ cell+ allocate throw { r }
  2 cells +to r
  begin b b# starts./ while
    2 +to b -2 +to b#
    a# b# + to r#
  repeat
  begin b b# starts../ a# 0<> and while
    3 +to b -3 +to b#
    a a# dirname to a# to a
    a# b# + to r#
  repeat
  a r a# cmove b r a# + b# cmove
  r# r cell - !
  r r# ;
: include+ 2 cells - { a }
  included-files a ! a to included-files ;

forth definitions internals

: included ( a n -- )
   sourcefilename >r >r
   >r >r sourcedirname r> r> path-join 2dup sourcefilename!
   ['] raw-included catch if
      ." Error including: " sourcefilename type cr
      r> r> sourcefilename!   \ the caller's file name back, or later relative includes resolve against this one
      -38 throw
   then
   sourcefilename& include+
   r> r> sourcefilename! ;

: include ( "name" -- ) bl parse included ;
: include-file ( fh -- ) include-fid ;

: included? { a n -- f }
  sourcedirname a n path-join to n to a
  included-files begin dup while
    dup cell+ cell+ over cell+ @ a n str= if
      a 2 cells - free throw drop -1 exit
    then @
  repeat
  a 2 cells - free throw ;

: required ( a n -- ) 2dup included? if 2drop else included then ;
: needs ( "name" -- ) bl parse required ;
: require ( "name" -- ) needs ;

: file-exists? ( "name" -- f ) r/o open-file if drop 0 else close-file throw -1 then ;
: file-status ( a n -- x ior ) r/o open-file if drop 0 -37 else close-file throw 0 0 then ;

( Reading and writing a line of a file. A line ends at a line feed, a carriage return before it goes. )
internals definitions
create eol-char 10 c,
forth definitions internals
: write-line ( a n fh -- ior ) dup >r write-file ?dup if r> drop exit then eol-char 1 r> write-file ;
: read-line { a u fh -- n f ior }
   0 { n }
   begin n u < while
     a n + 1 fh read-file ?dup if nip n 0 rot exit then   ( n2 )
     0= if n if n -1 else 0 0 then 0 exit then
     a n + c@ 10 = if
       n dup if a n 1- + c@ 13 = if 1- then then -1 0 exit
     then
     1 +to n
   repeat
   n -1 0 ;

forth
