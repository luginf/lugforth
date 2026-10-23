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

previous forth definitions

: sdl2 ( -- ) s" lib/sdl2.fs" included s" sdl2" evaluate ;
: sdl2-ttf ( -- ) s" lib/sdl2_ttf.fs" included s" sdl2-ttf" evaluate ;
