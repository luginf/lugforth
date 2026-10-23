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

( Control structures outside a definition )

e: test-ctl-do
  3 0 do i . loop cr
  out: 0 1 2 
;e

e: test-ctl-if
  1 if ." yes" then cr
  out: yes
  0 if ." yes" then cr
  out: 
  0 if ." yes" else ." no" then cr
  out: no
;e

e: test-ctl-keeps-stack
  7 1 if 8 then 9 . . . cr
  out: 9 8 7 
;e

e: test-ctl-begin
  3 begin dup . 1- dup 0= until drop cr
  out: 3 2 1 
  0 begin dup 3 < while dup . 1+ repeat drop cr
  out: 0 1 2 
;e

e: test-ctl-nested
  2 0 do 3 0 do j 10 * i + . loop loop cr
  out: 0 1 2 10 11 12 
  3 0 ?do i 1 = if ." one" then loop cr
  out: one
;e

e: test-ctl-case
  2 case 1 of ." a" endof 2 of ." b" endof ." other" endcase cr
  out: b
;e

e: test-ctl-for
  3 for i . next cr
  out: 3 2 1 0 
;e

e: test-ctl-after-structure
  3 0 do i . loop 5 6 + . cr
  out: 0 1 2 11 
;e

e: test-ctl-multiline
  3 0 do
    i .
  loop cr
  out: 0 1 2 
;e

e: test-ctl-defined-word-still-works
  : foo 3 0 do i . loop ;
  foo cr
  out: 0 1 2 
;e

e: test-ctl-allot-kept
  here 3 0 do 7 , loop here swap - . cr
  out: 24 
;e

e: test-ctl-create-table
  create ctl-tbl 3 0 do i , loop
  ctl-tbl @ . ctl-tbl cell+ @ . ctl-tbl 2 cells + @ . cr
  out: 0 1 2 
;e

