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

( Run a Uxn ROM in the terminal, like uxncli )
\
\   lugforth examples/varvara/uxncli.fs program.rom [arguments]
\
\ The ROM sees the System and Console devices: what it writes goes to stdout
\ (or stderr), the arguments then stdin come in through the Console vector,
\ and the exit code is the one written to System/state.

needs uxn.fs
also uxn

also posix
: stdin-byte ( -- c t | -1 ) pad 1 stdin -rot read 1 = if pad c@ 1 else -1 then ;
previous

: run-args ( -- )
  argc 3 ?do
    i argv 0 ?do dup i + c@ 2 console-input loop drop
    10  i 1+ argc = if 4 else 3 then  console-input
  loop ;
: run-stdin ( -- )
  begin halted? 0= $10 dev2@ 0<> and while
    stdin-byte dup 0< if drop 0 4 console-input exit then
    console-input
  repeat ;

: uxncli ( -- )
  argc 3 < if ." usage: lugforth uxncli.fs program.rom [arguments]" cr 1 terminate then
  2 argv load-rom
  argc 3 > if 1 $17 dev! then
  $100 eval  run-args  run-stdin
  flush-out exit-code terminate ;
uxncli
