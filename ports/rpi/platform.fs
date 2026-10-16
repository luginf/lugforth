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

( Add a yielding task so pause yields )
internals definitions
: yield-step   raw-yield yield ;
' yield-step 100 100 task yield-task
yield-task start-task
forth definitions

( Basic I/O: the PL011 UART, see main.c. No filesystem. )
also internals definitions
: setup-saving-base ;  ( no saved image )
variable last-key  -1 last-key !
forth definitions internals
: default-type ( a n -- ) raw-type ;
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

( The board: name, timer, and the Pi's GPIO, memory-mapped 32-bit registers )
vocabulary rpi   rpi definitions
transfer rpi-builtins
: ?gpio   gpio-base 0= if -21 throw then ;   ( qemu virt has no Pi GPIO )
: gpio-mode ( mode pin -- )   ( 0 input, 1 output, 4 alt0 ... )
   ?gpio 10 /mod 4 * gpio-base + swap 3 *
   rot over lshift -rot 7 swap lshift invert
   over ul@ and rot or swap l! ;
: gpio-reg ( pin offset -- mask a )
   ?gpio swap 32 /mod 4 * rot + gpio-base + >r 1 swap lshift r> ;
: gpio-on ( pin -- )    $1c gpio-reg l! ;
: gpio-off ( pin -- )   $28 gpio-reg l! ;
: gpio@ ( pin -- f )    $34 gpio-reg ul@ and 0<> ;
forth definitions

( Setup entry )
internals also rpi
: ok   ." Lugforth (" board type ." )" raw-ok ;
previous forth
