\ Run by ninja windows-mingw under wine: the exceptions of the interpreter, which the MinGW build handles with a
\ vectored handler (MSVC has __try). Prints PASS or FAIL lines.
: ok? ( f "name" -- ) if ." PASS " else ." FAIL " then cr ;
: t1 1 0 / ;
: t2 0 @ ;
' t1 catch -10 = ok? 
' t2 catch -9 = ok? 
7 3 5 */ 4 = ok? 
-7 3 5 */ -5 = ok? 
bye
