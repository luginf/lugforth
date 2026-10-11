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

( Run by the AppImage when it is clicked on a desktop: a window with a menu bar )
( and a console. Next to this file: sdl2_console.fs. Exit code 1 when the window )
( cannot be opened, so that AppRun can fall back to a terminal. )

: need ( xt -- )   catch if 1 terminate then ;
' sdl2 need
include sdl2_console.fs
: go ( -- )   desktop console ;
' go need
s" Use the Examples menu, or type: words" type cr
( examples with their own X11 window want the plain search order, and must not end the session )
only forth also internals 1 'argc ! previous definitions
