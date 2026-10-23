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

( SDL2 on Windows: the bindings of the POSIX port are loaded as they are from lib/sdl2.fs )
( and lib/sdl2_ttf.fs, beside the SDL2.dll and SDL2_ttf.dll of the package. They ask for )
( a few words of the posix vocabulary: this one gives them with the Windows DLL calls. )

vocabulary posix
posix definitions also windows

: shared-library ( z "name" -- ) dll ;
: sign-extend ( n -- n ) >r rp@ sl@ rdrop ;
: sdl2-lib ( -- z ) z" SDL2.dll" ;
: sdl2-ttf-lib ( -- z ) z" SDL2_ttf.dll" ;
: sdl2-image-lib ( -- z ) z" SDL2_image.dll" ;


( What the console window asks for: the C library, directories, no terminal )
z" msvcrt.dll" dll msvcrt
: c-name { z -- z }   \ the POSIX names that msvcrt has with an underscore
   z z>s s" mkdir" compare 0= if z" _mkdir" exit then
   z z>s s" chdir" compare 0= if z" _chdir" exit then
   z z>s s" getcwd" compare 0= if z" _getcwd" exit then
   z ;
: sysfunc ( z n "name" -- ) >r c-name r> msvcrt ;
: usleep ( us -- n ) 1000 / 1 max ms 0 ;
: dlsym ( h z -- a ) GetProcAddress ;
: isatty ( n -- f ) drop 0 ;
0 constant stdin
: readdir ( dh -- ent|0 ) find-next ;
: .d_name ( ent -- z ) 44 + ;
: .d_type ( ent -- n ) @ $10 and if 4 else 8 then ;
: closedir ( dh -- ior ) close-dir ;
vocabulary termios
previous forth definitions

( ed and writhdeck: lib/editor.fs and lib/writhdeck.fs, loaded when first asked, then the command runs again )
create lazy-buf 320 allot
: lazy-run { na nn la ln ca cn }
   la ln included
   ca lazy-buf cn cmove   32 lazy-buf cn + c!   na lazy-buf cn 1+ + nn cmove
   lazy-buf cn 1+ nn + evaluate ;
: ed ( "file" -- ) bl parse 255 min s" lib/editor.fs" s" ed" lazy-run ;
: writhdeck ( "file" -- ) bl parse 255 min s" lib/writhdeck.fs" s" writhdeck" lazy-run ;

: sdl2 ( -- ) s" lib/sdl2.fs" included s" sdl2" evaluate ;
: sdl2-ttf ( -- ) s" lib/sdl2_ttf.fs" included s" sdl2-ttf" evaluate ;
: gui-tty? ( -- f ) 0 ;
\ lugforth --gui, or gui at the prompt: the console window (lib/sdl2_console.fs)
: gui-load ( -- )
   s" lib/sdl2_console_ttf.fs" included  s" lib/sdl2_console.fs" included  s" gui-load" evaluate ;
: gui ( -- ) s" gui-load sdl2 desktop console" eval-copy ;
