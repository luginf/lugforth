\ Test of the SDL2 bindings on Windows (CI): run from the folder of the package
\ with SDL_VIDEODRIVER=dummy and SDL_AUDIODRIVER=dummy.
ls lib
ls .
include lib/../sdl2_test2.fs
include lib\..\sdl2_test2.fs
sdl2
also sdl2
: check ( f -- ) 0= if ." FAILED" cr -1 throw then ;
320 240 screen
$ff0000 color  0 0 50 50 box  flip
0 0 pixel@ $ff0000 = check
." sdl2 ok" cr
sdl2-ttf
s" C:/Windows/Fonts/arial.ttf" 16 load-font font
$ffffff color 10 10 s" Hello" text
." ttf ok" cr
close-screen
gui-load
s" sdl2 desktop" eval-copy
also sdl2
fpath z>s type cr
ex-list ent-count . cr
s" lib/editor.fs" included
ed-src eval-copy
s" lib/writhdeck.fs" included
wd-src eval-copy
." editors ok" cr
." ALL PASSED" cr
bye
