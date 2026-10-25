\ Tests of zmachine.fs:  lugforth examples/games/zmachine_tests.fs   (the output ends with PASS)
\ Also under gforth:  gforth examples/games/zmachine_tests.fs
\ Plays zmachine/test.z3 (version 3) and test.z5 (version 5), two small Inform 6 stories (test.inf)
\ (run it from the root of the repository) with canned input lines, and looks for the lines they must print. The text is captured, nothing is shown.

include zmachine.fs
also zm

variable tn   variable tfail
: check ( f -- ) 1 tn +!  0= if ." FAIL test " tn @ . cr 1 tfail +! then ;

\ ---- Captured output ----
create ocap 16384 allot   variable ocn
: cap-type ( a n -- ) tuck ocap ocn @ + swap cmove ocn +! ;
: cap-cr ( -- ) 10 ocap ocn @ + c!  1 ocn +! ;
' cap-type is ztype   ' cap-cr is zcr

\ ---- Canned input: the lines are taken one by one ----
create icap 600 allot   variable icn   variable ipos
: iline ( a n -- )   \ adds a line to the canned input
  tuck icap icn @ + swap cmove  icn +!  10 icap icn @ + c!  1 icn +! ;
: next-line ( -- n )   \ copies the next line of icap into zline
  ipos @ icn @ >= if 0 0 zrun ! exit then
  0  begin ipos @ icn @ < if icap ipos @ + c@ 10 <> else 0 then while
    icap ipos @ + c@ zline 2 pick + c!  1+  1 ipos +!
  repeat
  1 ipos +! ;
' next-line is zgetline

\ ---- Search in the capture ----
: has? ( a n -- f )
  ocn @ over - 1+ 0 max 0 ?do
    2dup ocap i + over compare 0= if 2drop -1 unloop exit then
  loop 2drop 0 ;

: new-input ( -- ) 0 icn !  0 ipos ! ;
: play ( a n -- ) 0 ocn !  0 ipos !  zstart ;

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

new-input   s" take ball" iline   s" x, y quit" iline   s"   hello   ball " iline
s" examples/games/zmachine/test.z3" play   common-checks
s" 2: BALL len 4 pos 6" has? check   s" 1: unknown len 5 pos 3" has? check      \ the text starts at byte 1
s" examples/games/zmachine/test.z5" play   common-checks
s" 2: BALL len 4 pos 7" has? check   s" 1: unknown len 5 pos 4" has? check      \ and at byte 2 in version 5

\ cave.z5, a game with the Inform 6 library: rooms, a locked door, a light, containers, a score
new-input
s" e" iline   s" look in stump" iline   s" take key" iline   s" w" iline   s" n" iline
s" take lantern" iline   s" turn on lantern" iline   s" ring bell" iline
s" unlock trapdoor with key" iline   s" open trapdoor" iline   s" d" iline   s" e" iline
s" take gem" iline   s" w" iline   s" u" iline   s" put gem in chest" iline   s" score" iline
s" examples/games/zmachine/cave.z5" play
s" Welcome to the cave of Lugforth" has? check
s" In the hollow stump is a brass key." has? check
s" You take the key out of the stump." has? check
s" Ding! The bell rings out cheerfully." has? check
s" You unlock the trapdoor." has? check
s" A low, damp cellar." has? check
s" Taken. You feel richer." has? check
s" (first opening the iron chest)" has? check
s" The gem rests safely in the chest. Well done!" has? check
s" scored 10 out of a possible 10, in 16 turns" has? check

\ a missing story file does not crash
s" examples/games/zmachine/nothing.z3" play

: report ( -- ) tfail @ 0= if ." PASS (" tn @ . ." checks)" cr else ." FAIL" cr then ;
report
bye
