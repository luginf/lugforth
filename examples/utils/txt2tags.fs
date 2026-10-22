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

( A small txt2tags to HTML converter, basic syntax only )
\
\ Usage:
\   lugforth examples/utils/txt2tags.fs doc.t2t > doc.html
\ or from a running Forth:
\   include examples/utils/txt2tags.fs
\   s" doc.t2t" t2t-file
\
\ Supported: header (3 lines: title, author, date; a blank first line means
\ no header), comments (% at the start of a line), titles (= to =====, and
\ + numbered ones, written as plain titles), paragraphs, **bold**, //italic//,
\ __underline__, --strike--, ``mono``, lists (- + and : for definitions, nested
\ by indentation), ``` verbatim blocks, tab indented quotes, rules (20 or more
\ of - = _), tables (| cell | cell | and || for a header row), links
\ ([label url], bare http:// https:// ftp:// addresses) and images ([pic.png]).
\ Not supported: macros, regular expressions, pre and post processing,
\ include, raw and tagged areas, automatic numbering of titles.

( ---- Output with HTML escapes ---- )
: .esc ( c -- )
  dup [char] & = if drop s" &amp;" type exit then
  dup [char] < = if drop s" &lt;" type exit then
  dup [char] > = if drop s" &gt;" type exit then
  dup [char] " = if drop s" &quot;" type exit then
  emit ;
: type-esc ( a n -- ) 0 ?do dup i + c@ .esc loop drop ;

( ---- Small string helpers ---- )
: skip-spaces ( a n -- a' n' )
  begin dup if over c@ 32 = else 0 then while 1 /string repeat ;
: trim-end ( a n -- a n' )
  begin dup if 2dup + 1- c@ 32 = else 0 then while 1- repeat ;
: trim ( a n -- a' n' ) skip-spaces trim-end ;
: first-char ( a n -- c ) if c@ else drop 0 then ;
: last-char ( a n -- c ) dup if + 1- c@ else 2drop 0 then ;
: ends-with? ( a n sa sn -- f )
  { a n sa sn }  n sn < if 0 else a n sn - + sn sa sn str= then ;
: run-length ( a n c -- k )   ( how many c at the start )
  { a n c }  0
  begin dup n < if a over + c@ c = else 0 then while 1+ repeat ;
: rev-run-length ( a n c -- k )   ( how many c at the end )
  { a n c }  0
  begin dup n < if a n + 1- over - c@ c = else 0 then while 1+ repeat ;
: all-same? ( a n -- f )   ( every character equals the first )
  dup 0= if 2drop 0 exit then
  over c@ { a n c }  -1
  n 0 ?do a i + c@ c <> if drop 0 then loop ;

( ---- Source text and lines ---- )
0 value src   0 value src-n   0 value pos   0 value ls
: next-line ( -- a n )
  pos to ls
  begin pos src-n < if src pos + c@ 10 <> else 0 then while 1 +to pos repeat
  src ls +  pos ls -
  pos src-n < if 1 +to pos then
  dup if 2dup + 1- c@ 13 = if 1- then then ;
: more? ( -- f ) pos src-n < ;

: slurp ( a n -- )   ( reads a whole file into src )
  r/o open-file throw { fh }
  fh file-size throw to src-n
  src-n 1+ allocate throw to src
  src src-n fh read-file throw drop
  fh close-file drop
  0 to pos ;

( ---- Inline marks ---- )
0 value ia   0 value in-n   0 value ii   0 value marks
: ichar ( off -- c )   ( the character at ii + off, or 0 past the end )
  ii + dup in-n < if ia + c@ else drop 0 then ;
: prev-char ( -- c ) ii 0> if ia ii + 1- c@ else 32 then ;
: rest ( -- a n ) ia ii +  in-n ii - ;

: tag-open ( k -- )
  dup 0 = if drop s" <b>" type exit then
  dup 1 = if drop s" <i>" type exit then
  dup 2 = if drop s" <u>" type exit then
  dup 3 = if drop s" <s>" type exit then
  drop s" <code>" type ;
: tag-close ( k -- )
  dup 0 = if drop s" </b>" type exit then
  dup 1 = if drop s" </i>" type exit then
  dup 2 = if drop s" </u>" type exit then
  dup 3 = if drop s" </s>" type exit then
  drop s" </code>" type ;
: mark-bit ( k -- n ) 1 swap lshift ;
: mark-open? ( k -- f ) mark-bit marks and 0<> ;
: toggle-mark ( k -- ) mark-bit marks xor to marks ;
: close-marks ( -- )
  5 0 do i mark-open? if i tag-close then loop  0 to marks ;

: mark-at ( -- k )   ( 0 to 4 when a doubled mark character starts here, else -1 )
  0 ichar 1 ichar over <> if drop -1 exit then
  dup [char] * = if drop 0 exit then
  dup [char] / = if drop 1 exit then
  dup [char] _ = if drop 2 exit then
  dup [char] - = if drop 3 exit then
  [char] ` = if 4 else -1 then ;
: open-mark ( k -- ) dup tag-open toggle-mark 2 +to ii ;
: close-mark ( k -- ) dup tag-close toggle-mark 2 +to ii ;
: try-mark ( -- f )
  mark-at dup 0< if drop 0 exit then
  dup mark-open? if
    prev-char 32 <> if close-mark -1 else drop 0 then
  else
    2 ichar dup 32 <> swap 0<> and if open-mark -1 else drop 0 then
  then ;

( Links: [label address], [picture.png] and bare addresses )
: image? ( a n -- f )
  2dup s" .png" ends-with? >r  2dup s" .jpg" ends-with? >r
  2dup s" .jpeg" ends-with? >r  s" .gif" ends-with? r> r> r> or or or ;
: last-space ( a n -- k )   ( index of the last space, or -1 )
  -1 { a n k }
  n 0 ?do a i + c@ 32 = if i to k then loop  k ;
: close-bracket ( -- k )   ( offset of the ] from ii, or -1 )
  -1 { k }
  in-n ii 1+ ?do ia i + c@ [char] ] = if i ii - to k leave then loop  k ;
: emit-link ( addr-a addr-n label-a label-n -- )
  s" <a href=" type [char] " emit  2swap type-esc [char] " emit [char] > emit
  type-esc s" </a>" type ;
: try-bracket ( -- f )
  0 ichar [char] [ <> if 0 exit then
  close-bracket { k }
  k 2 < if 0 exit then
  ia ii + 1+  k 1- { ca cn }
  ca cn last-space { sp }
  sp 0< if
    ca cn image? if
      s" <img src=" type [char] " emit ca cn type-esc [char] " emit s" >" type
      k 1+ +to ii -1
    else 0 then
  else
    ca sp 1+ +  cn sp - 1-   ca sp trim
    emit-link
    k 1+ +to ii -1
  then ;
: punct? ( c -- f )
  dup [char] . = over [char] , = or over [char] ; = or
  over [char] ) = or over [char] ! = or swap [char] ? = or ;
: url-len ( -- n )
  0 begin ii over + in-n < if ia ii + over + c@ 32 > else 0 then while 1+ repeat
  begin dup 1 > if ia ii + over + 1- c@ punct? else 0 then while 1- repeat ;
: url-start? ( -- f )
  prev-char 32 = prev-char [char] ( = or 0= if 0 exit then
  rest s" http://" startswith?
  rest s" https://" startswith? or
  rest s" ftp://" startswith? or ;
: try-url ( -- f )
  url-start? 0= if 0 exit then
  url-len { n }
  rest drop n  rest drop n  emit-link
  n +to ii -1 ;

: inline ( a n -- )
  to in-n  to ia  0 to ii  0 to marks
  begin ii in-n < while
    marks 16 and if
      try-mark 0= if ia ii + c@ .esc 1 +to ii then
    else
      try-mark 0= if try-bracket 0= if try-url 0= if
        ia ii + c@ .esc 1 +to ii
      then then then
    then
  repeat
  close-marks ;

( ---- Blocks ---- )
0 value mode   ( 0 none, 1 paragraph, 2 list, 3 table, 4 verbatim, 5 quote )
create lind 8 cells allot   create lty 8 cells allot   0 value ldepth
: ltop-ind ( -- n ) ldepth 1- cells lind + @ ;
: ltop-ty ( -- n ) ldepth 1- cells lty + @ ;
: close-item ( ty -- ) 2 = if s" </dd>" else s" </li>" then type cr ;
: close-list ( -- )
  ltop-ty close-item
  ltop-ty 0 = if s" </ul>" then
  ltop-ty 1 = if s" </ol>" then
  ltop-ty 2 = if s" </dl>" then
  type cr  -1 +to ldepth ;
: close-lists-above ( ind -- )   ( closes the lists indented more than ind )
  begin ldepth 0> if dup ltop-ind < else 0 then while close-list repeat drop ;
: close-block ( -- )
  mode 1 = if s" </p>" type cr then
  mode 2 = if -1 close-lists-above then
  mode 3 = if s" </table>" type cr then
  mode 4 = if s" </pre>" type cr then
  mode 5 = if s" </blockquote>" type cr then
  0 to mode ;

: open-list ( ind ty -- )
  ldepth cells lty + !  ldepth cells lind + !  1 +to ldepth
  ltop-ty 0 = if s" <ul>" then
  ltop-ty 1 = if s" <ol>" then
  ltop-ty 2 = if s" <dl>" then
  type cr ;
: open-item ( ty a n -- )
  rot 2 = if
    s" <dt>" type inline s" </dt>" type cr s" <dd>" type
  else
    s" <li>" type inline
  then ;

0 value li-ind   0 value li-ty   0 value li-a   0 value li-n
: list-item ( ind ty a n -- )
  to li-n to li-a to li-ty to li-ind
  mode 2 <> if close-block 2 to mode then
  li-ind close-lists-above
  ldepth 0= if li-ind li-ty open-list
  else
    ltop-ind li-ind < if li-ind li-ty open-list
    else
      ltop-ty li-ty = if ltop-ty close-item
      else close-list li-ind li-ty open-list then
    then
  then
  li-ty li-a li-n open-item ;

: item-mark? ( a n -- f )   ( a - + or : then a space, after the indentation )
  skip-spaces dup 2 < if 2drop 0 exit then
  over 1+ c@ 32 <> if 2drop 0 exit then
  drop c@ dup [char] - = swap dup [char] + = swap [char] : = or or ;
: item-parse ( a n -- ind ty a' n' )
  { a n }
  a n skip-spaces nip n swap - { ind }
  ind
  a ind + c@ [char] - = if 0 else a ind + c@ [char] + = if 1 else 2 then then
  a ind + 2 +   n ind - 2 - ;

( Titles )
: strip-label ( a n -- a n' )   ( drops a trailing [label] )
  2dup last-char [char] ] <> if exit then
  { a n }  -1 { j }
  n 0 ?do a i + c@ [char] [ = if i to j then loop
  j 0< if a n exit then
  a j + n j - last-space 0< if a j trim-end else a n then ;
: title-line? ( a n -- f )
  trim-end strip-label trim-end dup 3 < if 2drop 0 exit then
  over c@ { c }
  c [char] = <> c [char] + <> and if 2drop 0 exit then
  2dup c run-length { lead }
  lead 5 > if 2drop 0 exit then
  2dup c rev-run-length { trail }
  trail 0= if 2drop 0 exit then
  lead trail + over >= if 2drop 0 exit then
  2drop -1 ;
: .title ( a n -- )
  trim-end strip-label trim-end over c@ { c }
  2dup c run-length { lead }
  2dup c rev-run-length { trail }
  lead /string  trail - trim
  s" <h" type lead n. s" >" type  inline  s" </h" type lead n. s" >" type cr ;

( Tables )
: cell-end ( a n -- k )   ( offset of the next |, or the length )
  { a n }  n 0 ?do a i + c@ [char] | = if i unloop exit then loop  n ;
0 value ra   0 value rn   0 value ck
: table-cells ( a n head -- )
  { head }  to rn to ra
  begin rn 0> while
    ra rn cell-end to ck
    head if s" <th>" else s" <td>" then type
    ra ck trim inline
    head if s" </th>" else s" </td>" then type
    ck rn < if ck 1+ else rn then  dup +to ra  negate +to rn
  repeat ;
: table-row ( a n -- )
  mode 3 <> if
    close-block 3 to mode
    s" <table border=" type [char] " emit [char] 1 emit [char] " emit
    s" >" type cr
  then
  skip-spaces 2dup s" ||" startswith? { head }
  head if 2 else 1 then /string  trim-end
  dup if 2dup last-char [char] | = if 1- then then
  s" <tr>" type  head table-cells  s" </tr>" type cr ;

( Other lines )
: verbatim-line? ( a n -- f ) s" ```" startswith? ;
: rule-line? ( a n -- f )
  trim-end dup 20 < if 2drop 0 exit then
  2dup all-same? 0= if 2drop 0 exit then
  drop c@ dup [char] - = over [char] = = or swap [char] _ = or ;

: paragraph-line ( a n -- )
  mode 1 <> mode 2 <> and if close-block s" <p>" type 1 to mode then
  mode 2 = if trim then
  inline cr ;

: process-line ( a n -- )
  mode 4 = if
    2dup trim-end s" ```" str= if 2drop close-block else type-esc cr then exit
  then
  2dup first-char [char] % = if 2drop exit then
  2dup trim nip 0= if 2drop close-block exit then
  2dup trim-end s" ```" str= if 2drop close-block s" <pre>" type cr 4 to mode exit then
  2dup s" ``` " startswith? if
    close-block s" <pre>" type 4 /string type-esc s" </pre>" type cr exit then
  2dup title-line? if close-block .title exit then
  2dup rule-line? if 2drop close-block s" <hr>" type cr exit then
  2dup item-mark? if item-parse list-item exit then
  2dup skip-spaces first-char [char] | = if table-row exit then
  2dup first-char 9 = if
    mode 5 <> if close-block s" <blockquote>" type cr 5 to mode then
    1 /string trim inline cr exit
  then
  paragraph-line ;

( ---- Whole documents ---- )
: html-head ( ta tn -- )
  s" <!DOCTYPE html>" type cr
  s" <html><head><meta charset=" type [char] " emit s" utf-8" type [char] " emit
  s" >" type cr  s" <title>" type type-esc s" </title>" type cr
  s" </head><body>" type cr ;
: header-lines ( ta tn -- )
  { ta tn }
  next-line { aa an }  next-line { da dn }
  ta tn html-head
  s" <h1>" type ta tn inline s" </h1>" type cr
  an dn + if
    s" <p><em>" type aa an inline s" </em><br>" type da dn inline s" </p>" type cr
  then ;
: t2t-header ( -- )
  next-line dup 0= if 2drop s" " html-head else header-lines then ;
: t2t ( -- )
  0 to mode  0 to ldepth
  t2t-header
  begin more? while next-line process-line repeat
  close-block
  s" </body></html>" type cr ;
: t2t-file ( a n -- ) slurp t2t src free drop ;

argc 3 >= [IF] 2 argv t2t-file bye [THEN]
