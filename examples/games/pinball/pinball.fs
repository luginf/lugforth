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

\ The pinball menu: pick a table.
\   1 Night Raid    the first table, tunable physics (raid.fs)
\   2 Night Strike  a table after a coin-op of the early 80s (night.fs)
\ Up/down and Enter, or 1 and 2. Esc in a game comes back here, Esc here quits.
\ On the web the page loads the three files itself, so each include is only
\ done when the words are missing.

sdl2
[undefined] pb-common [if] include common.fs [then]
[undefined] raid [if] include raid.fs [then]
[undefined] night [if] include night.fs [then]

320 constant menu-w   240 constant menu-h
0 value choice   0 value picked   0 value up-was   0 value down-was   0 value enter-was   0 value esc-was
2 constant #modes

: draw-menu ( -- )
  $000000 color cls
  $c050ff color  s" PINBALL" 100 28 4 say
  #modes 0 do
    i choice = if $ffffff else $ff7020 then color
    i choice = if s" >" 64 90 i 40 * + 3 say then
    i 0= if s" 1 NIGHT RAID" else s" 2 NIGHT STRIKE" then 84 90 i 40 * + 3 say
  loop
  $30a0ff color s" UP DOWN ENTER   ESC QUIT" 60 210 2 say
  flip ;

: menu-step ( -- )
  events
  key-up pressed? { u }  key-down pressed? { d }  key-enter pressed? { e }  key-esc pressed? { x }
  u up-was 0= and if choice 1- #modes + #modes mod to choice then
  d down-was 0= and if choice 1+ #modes mod to choice then
  [char] 1 pressed? if 0 to choice then
  [char] 2 pressed? if 1 to choice then
  e enter-was 0= and [char] 1 pressed? or [char] 2 pressed? or if -1 to picked then
  x esc-was 0= and if quit! then
  u to up-was  d to down-was  e to enter-was  x to esc-was ;

: keys-as-now ( -- )   ( a key still down from the game must not act: wait for its release )
  events  key-up pressed? to up-was  key-down pressed? to down-was
  key-enter pressed? to enter-was  key-esc pressed? to esc-was ;

: pinball ( -- )
  begin
    menu-w menu-h open-screen  s" Pinball" title  0 cursor
    0 to picked
    keys-as-now
    begin menu-step draw-menu 16 delay quit? picked or until
    picked quit? 0= and if
      choice 0= if raid else night then
    then
    quit?
  until
  close-screen ;

pinball
script-bye
