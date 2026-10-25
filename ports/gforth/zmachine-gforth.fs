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

\ The Z-machine interpreter of Lugforth (examples/games/zmachine.fs) under gforth, unchanged. Tested with gforth 0.7.9:
\   gforth ports/gforth/zmachine-gforth.fs story.z5
\ zmachine.fs itself holds the three differences, each in a [defined] gforth test: the line is read with accept on a terminal (echo, editing), else with read-line
\ (which sees the end of the input), file-size gives a double, ms-ticks comes from utime. No compat.fs needed.

s" ../../examples/games/zmachine.fs" included   \ relative to this file

: zgo ( -- )
  next-arg dup 0= if 2drop ." usage: gforth zmachine-gforth.fs story.z5" cr else zstart then bye ;
zgo
