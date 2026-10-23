#! /usr/bin/env lugforth

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

\ A small file manager in an SDL2 window: `filer` at the prompt.
\   click / up / down   select          wheel   scroll
\   double click, Enter open a folder, or run a .fs, or edit another file (ed)
\   Backspace           parent folder   D then Y   delete the selected file
\   Esc                 quit
\ Needs libSDL2 and libSDL2_ttf, and a TrueType font (see font-file below).

sdl2
sdl2-ttf

vocabulary fm   also fm definitions   ( the words of the file manager and of the desktop, out of the way of the programs they start )

: font-file ( -- a n ) s" /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf" ;

also posix
z" chdir" 1 sysfunc fl-chdir
z" getcwd" 2 sysfunc fl-getcwd
z" remove" 1 sysfunc fl-remove

( ---- The list: records of 64 bytes, byte 0 = 1 for a folder, then the name, zero-ended ---- )
512 constant max-ent
64 constant ent-size
create ents max-ent ent-size * allot
create swap-buf ent-size allot
0 value nent
: ent ( i -- a ) ent-size * ents + ;
: ent-name ( i -- a ) ent 1+ ;
: ent-dir? ( i -- f ) ent c@ 0<> ;
: ent-add ( a n dir? -- )
  nent max-ent >= if 2drop drop exit then
  nent ent { e }
  e ent-size erase
  if 1 e c! then
  62 min e 1+ swap cmove
  1 +to nent ;

: ent-before? ( i j -- f )   \ folders first, then by name
  { i j }
  i ent-dir? j ent-dir? <> if i ent-dir? exit then
  i ent-name z>s j ent-name z>s compare 0< ;
: ent-swap ( i j -- )
  { i j }
  i ent swap-buf ent-size cmove
  j ent i ent ent-size cmove
  swap-buf j ent ent-size cmove ;
: ent-sort ( -- )
  0 { j }
  nent 1 <= if exit then
  nent 1 do
    i 1- to j
    begin j 0 >= if j 1+ j ent-before? else 0 then while  j 1+ j ent-swap  j 1- to j  repeat
  loop ;

create cwd 300 allot
: get-cwd ( -- ) cwd 290 fl-getcwd drop ;

: fl-scan ( -- )
  0 { dh }  0 { e }  0 { na }  0 { nn }
  0 to nent
  s" .." -1 ent-add
  s" ." open-dir if drop ent-sort exit then
  to dh
  begin dh readdir dup while
    to e
    e .d_name z>s to nn to na
    na c@ [char] . <> if na nn e .d_type 4 = ent-add then
  repeat drop
  dh closedir drop
  ent-sort ;

( ---- The list in a rectangle: the origin is the corner of the area, so a desktop can place it in a window ---- )
640 value fl-w   480 value fl-h
24 constant row-h
44 constant top-h
0 value fl-top     ( first row shown )
0 value fl-sel
0 value pending     ( 0 nothing, 1 asks to delete )
0 value last-click   0 value last-sel
0 value fl-quit     ( Esc was pressed )
create picked 300 allot   0 value picked?
create msg 80 allot   0 value msg-n
defer fl-open ( a n -- )   ( a file was opened: its name )

: rows-shown ( -- n ) fl-h top-h - row-h 2* - row-h / ;
: say ( a n -- ) 79 min dup to msg-n msg swap cmove ;
: go-dir ( z -- ) fl-chdir drop get-cwd 0 to fl-top 0 to fl-sel fl-scan ;
: fl-clamp ( -- )
  fl-sel 0 max nent 1- min to fl-sel
  fl-sel fl-top < if fl-sel to fl-top then
  fl-sel fl-top rows-shown + >= if fl-sel rows-shown - 1+ to fl-top then
  fl-top 0 max to fl-top ;
: fl-init ( -- ) get-cwd fl-scan 0 to fl-top 0 to fl-sel 0 to msg-n 0 to pending 0 to fl-quit ;

: draw-row ( i y -- )
  { i y }
  i fl-top - 0< if exit then
  i fl-sel = if $3060a0 color 0 y fl-w row-h box then
  i ent-dir? if $ffd060 color else white color then
  8 y 2 + i ent-name z>s text ;
: fl-draw ( -- )
  fl-clamp
  $202030 color 0 0 fl-w fl-h box
  $303050 color 0 0 fl-w top-h box
  white color  8 6 cwd z>s text
  gray color   8 26 s" click: select, twice: open   BackSpace: up   D: delete" text
  nent fl-top - rows-shown min 0 max 0 ?do
    fl-top i + top-h i row-h * + draw-row
  loop
  $303050 color 0 fl-h row-h 2* - fl-w row-h 2* box
  pending if red color 8 fl-h row-h 2* - 4 + s" Delete this file? Y to confirm, any other key cancels" text
  else
    white color 8 fl-h row-h 2* - 4 + msg msg-n text
  then ;

: open-sel ( -- )
  fl-sel ent-dir? if fl-sel ent-name go-dir exit then
  fl-sel ent-name z>s fl-open ;

: delete-sel ( -- )
  fl-sel ent-dir? if s" Only files can be deleted here" say exit then
  fl-sel ent-name fl-remove 0= if s" Deleted" say else s" Delete failed" say then
  fl-scan fl-clamp ;

: fl-key ( k -- )   \ k as key-index gives it: letters in lower case, key-up, key-enter...
  pending if
    0 to pending
    dup [char] y = swap [char] Y = or if delete-sel else s" Cancelled" say then
    exit then
  dup key-esc = if drop -1 to fl-quit exit then
  dup key-enter = if drop open-sel exit then
  dup key-backspace = if drop z" .." go-dir exit then
  dup key-up = if drop -1 +to fl-sel exit then
  dup key-down = if drop 1 +to fl-sel exit then
  dup [char] d = over [char] D = or if drop
    fl-sel 0> fl-sel ent-dir? 0= and if 1 to pending then exit then
  drop ;

: fl-click ( x y -- )   \ a press at x y of the area
  nip top-h - dup 0< if drop exit then
  row-h / fl-top + dup nent >= if drop exit then
  dup fl-sel = ticks last-click - 600 < and if drop open-sel 0 to last-click exit then
  to fl-sel  ticks to last-click ;
: fl-wheel ( n -- )   \ n lines of the wheel, positive = up
  negate 3 * +to fl-top  fl-top 0 max nent rows-shown - 0 max min to fl-top ;

( ---- Alone in its window ---- )
0 value was-down
: (pick-file) ( a n -- )
  { na nn }
  na picked nn cmove  0 picked nn + c!
  -1 to picked?  quit! ;
: fl-step ( -- )
  events
  last-key ?dup if 0 to last-key fl-key then
  wheel ?dup if fl-wheel then
  LEFT-BUTTON pressed? dup was-down <> if
    dup to was-down  if mouse-x mouse-y fl-click then
  else drop then
  fl-quit if quit! then
  fl-draw  flip
  30 delay ;

: filer-run ( -- )
  1 to zoom
  640 to fl-w  480 to fl-h
  fl-w fl-h screen
  s" Lugforth files" title
  font-file 15 load-font font
  fl-init
  begin fl-step quit? until
  close-screen ;

: file-open-now ( a n -- )   \ a .fs is loaded, any other file goes to ed
  { na nn }
  nn 3 >= if na nn + 3 - 3 s" .fs" compare 0= else 0 then if
    na nn included
  else
    s" ed " pad swap cmove  na pad 3 + nn cmove
    pad nn 3 + evaluate
  then ;

: filer-go ( -- )
  0 to picked?
  ['] (pick-file) is fl-open
  filer-run
  picked? 0= if exit then
  picked z>s file-open-now ;

previous also forth definitions previous
also fm ' filer-go previous constant filer-xt
: filer ( -- ) also fm filer-xt execute previous ;

[defined] filer-no-run [if] [else]
filer
script-bye
[then]
