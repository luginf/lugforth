\ Tests of zmachine.fs:  lugforth examples/games/zmachine_tests.fs   (the output ends with PASS)
\ Plays zmachine/test.z3 (version 3) and test.z5 (version 5), two small Inform 6 stories (test.inf)
\ (run it from the root of the repository) with canned input lines, and looks for the lines they must print. The text is captured, nothing is shown.

include zmachine.fs
also zm

variable tn   variable tfail
: check ( f -- ) 1 tn +!  0= if ." FAIL test " tn @ . cr 1 tfail +! then ;

\ ---- Captured output ----
create ocap 16384 allot   variable on
: cap-type ( a n -- ) tuck ocap on @ + swap cmove on +! ;
: cap-cr ( -- ) 10 ocap on @ + c!  1 on +! ;
' cap-type is ztype   ' cap-cr is zcr

\ ---- Canned input: the lines are taken one by one ----
create icap 200 allot   variable icn   variable ipos
r| take ball
x, y quit
  hello   ball 
| dup icn !  icap swap cmove
: next-line ( -- n )   \ copies the next line of icap into zline
  ipos @ icn @ >= if 0 0 zrun ! exit then
  0  begin ipos @ icn @ < if icap ipos @ + c@ 10 <> else 0 then while
    icap ipos @ + c@ zline 2 pick + c!  1+  1 ipos +!
  repeat
  1 ipos +! ;
' next-line is zgetline

\ ---- Search in the capture ----
: has? ( a n -- f )
  on @ over - 1+ 0 max 0 ?do
    2dup ocap i + over compare 0= if 2drop -1 unloop exit then
  loop 2drop 0 ;

: play ( a n -- ) 0 on !  0 ipos !  zstart ;

: common-checks ( -- )
  s" Hello, Z-machine!" has? check
  s" Number: 42 -3 -2 30000" has? check
  s" Fact(7)=5040" has? check
  s" Test room / red ball" has? check
  s" child of room: red ball sibling: blue cube" has? check
  s" colour ball 9 room 5 has shiny: 1" has? check
  s" cube shiny: 1" has? check
  s" cube in ball: 1" has? check
  s" counter 30" has? check
  s" chars: Az" has? check
  s" words: 2" has? check
  s" BALL len 4 pos " has? check
  s" 4: QUIT len 4 pos " has? check ;

s" examples/games/zmachine/test.z3" play   common-checks
s" 2: BALL len 4 pos 6" has? check   s" 1: unknown len 5 pos 3" has? check      \ the text starts at byte 1
s" examples/games/zmachine/test.z5" play   common-checks
s" 2: BALL len 4 pos 7" has? check   s" 1: unknown len 5 pos 4" has? check      \ and at byte 2 in version 5

\ a missing story file does not crash
s" examples/games/zmachine/nothing.z3" play

tfail @ 0= if ." PASS (" tn @ . ." checks)" cr else ." FAIL" cr then
bye
