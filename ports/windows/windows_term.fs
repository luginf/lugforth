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

( The terminal for ed and writhdeck: the console is raw already, with VT input and output )
( A termios vocabulary for the editors: size of the window, a key that is really there, )
( UTF-8, Ctrl+C given to the program. )

windows definitions
z" PeekConsoleInputA" 4 Kernel32 PeekConsoleInputA
z" ReadConsoleInputA" 4 Kernel32 ReadConsoleInputA
z" GetConsoleScreenBufferInfo" 2 Kernel32 GetConsoleScreenBufferInfo
z" SetConsoleOutputCP" 1 Kernel32 SetConsoleOutputCP
z" SetConsoleCP" 1 Kernel32 SetConsoleCP
forth definitions also windows

create inrec 24 allot   variable inrn
: input-key? ( -- f )   \ a key press waits: the other records (key up, focus) are dropped
   init-console
   begin
     stdin inrec 1 inrn PeekConsoleInputA $ffffffff and 0= if win-key? exit then
     inrn @ 0= if 0 exit then
     inrec sw@ 1 =  inrec 4 + sl@ 0<> and  inrec 14 + sw@ 0<> and if -1 exit then
     stdin inrec 1 inrn ReadConsoleInputA drop
   again ;
' input-key? is key?

create csbi 24 allot
variable in-mode
also posix termios definitions also windows
64 constant sizeof(termios)
create new-termios 64 allot
: .c_lflag ( a -- a ) 12 + ;
: termios@ ( a -- ) drop ;
: termios! ( a -- ) drop ;
: raw-mode ( -- )
   65001 SetConsoleOutputCP drop  65001 SetConsoleCP drop
   stdin in-mode GetConsoleMode drop
   stdin in-mode @ ENABLE_PROCESSED_INPUT invert and SetConsoleMode drop ;
: delay-mode ( -- )   in-mode @ ?dup if stdin swap SetConsoleMode drop 0 in-mode ! then ;
: normal-mode ( -- ) delay-mode ;
0 value form-hook   ( a console that is not the tty gives its own size here )
: form ( -- h w )
   form-hook ?dup if execute exit then
   init-console
   stdout csbi GetConsoleScreenBufferInfo $ffffffff and 0= if 24 80 exit then
   csbi 16 + sw@ csbi 12 + sw@ - 1+   csbi 14 + sw@ csbi 10 + sw@ - 1+ ;
previous previous previous forth definitions
