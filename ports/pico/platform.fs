\ Copyright 2023 Bradley D. Nelson
\ Modifications Copyright 2026 Luginfo10
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

( Add a yielding task so pause yields )
internals definitions
: yield-step   raw-yield yield ;
' yield-step 100 100 task yield-task
yield-task start-task
forth definitions

( Initial file handles )
0 constant stdin
1 constant stdout
2 constant stderr

( Set up Basic I/O )
also internals definitions
: setup-saving-base ;  ( TODO: implement. )
variable last-key  -1 last-key !
forth definitions internals
: default-type ( a n -- ) stderr write-file throw ;
: default-key? ( -- f )
   last-key @ 0< if 1000 getchar_timeout_us last-key ! then
   last-key @ 0< 0=
;
: default-key ( -- ch )
   begin default-key? 0= while pause repeat
   last-key @ -1 last-key !
;
previous

' default-type is type
' default-key is key
' default-key? is key?
' raw-terminate is terminate
-1 echo !

vocabulary pico   pico definitions
transfer pico-builtins
forth definitions

( paste: a whole file over the serial line, evaluated at once )
\ Send `paste`, then the text, then Ctrl-D (4): the text is kept and
\ evaluated as a whole, so strings and comments may span lines (r~ ... ~
\ tunes, for example), which line by line sending breaks, and a line may
\ be longer than the 200 characters of the prompt. tools/pico_evaluate.py
\ does it by itself for such a file. 32 KB at most.
32768 value paste-max
: paste ( -- )
  paste-max allocate throw 0 { buf n }
  begin key dup 4 <> while
    n paste-max < if buf n + c! n 1+ to n else drop then
  repeat drop
  buf n ['] evaluate catch  buf free drop  throw ;

( Setup entry )
internals : ok   ." Lugforth" raw-ok ; forth
