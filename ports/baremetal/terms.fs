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

( Several terminals: Alt+F1 to Alt+F4, or term. Each one is a Forth of its own, with
  its screen; they take turns at each pause. See ports/baremetal/term_run.h. )
also rpi
: terminal ( -- n )   term-cur 1+ ;       \ the number of this terminal, 1 to 4
: terminals ( -- n )   term-count ;       \ how many are open
: term ( n -- )   1- term-request pause ; \ go to terminal n (1-4), open it if needed
previous
