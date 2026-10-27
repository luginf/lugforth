#! /usr/bin/env lugforth

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

\ CPU load test for the bare-metal PC (ports/efi): is the machine asleep when it
\ has nothing to do? The boot counts the time it spends asleep (IDLE-US, the hlt of
\ the waits); load = 100% - sleep / elapsed time. Three phases of 3 seconds:
\   ms      a program that waits with `ms` (or `delay`): should be near 0%
\   key     the wait for a key, as at the prompt: should be near 0%
\   spin    a loop that never waits: 100%, the figure of a hot fan
\ then the speed of the CPU: a million empty loops, in ms (compare with another PC).
\ A low figure in the first two phases and a loud fan anyway: the heat comes from
\ the firmware or the screen, not from Lugforth. Run: include examples/system/cpu_load.fs

also pc also internals

variable t0   variable i0

: mark ( -- )   us-ticks t0 !  idle-us i0 ! ;

: load ( -- percent )
   us-ticks t0 @ -  dup 0= if drop 0 exit then
   idle-us i0 @ -  100 * swap /  100 swap - 0 max ;

: phase ( c-addr u -- )   type ."  : " load . ." %" cr ;

: wait-ms ( -- )    mark  300 0 do 10 ms loop  s" ms 10 x 300" phase ;
: spin ( -- )       mark  ms-ticks 3000 + begin dup ms-ticks - 0< until drop  s" spin" phase ;

: wait-key ( -- )
   mark  ms-ticks 3000 +
   begin 1000 getchar_timeout_us drop  dup ms-ticks - 0< until drop
   s" key wait (1 ms polls)" phase ;

: speed ( -- )   ms-ticks  1000000 0 do loop  ms-ticks swap -
   ." 1000000 empty loops: " . ." ms" cr ;

: cpu-load ( -- )
   cr ." CPU load test, about 12 seconds." cr
   wait-ms wait-key spin speed
   ." Total asleep since boot: " idle-us 1000 / . ." ms of " us-ticks 1000 / . ." ms" cr ;

previous previous
cpu-load
