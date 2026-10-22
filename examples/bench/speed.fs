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

\ speed.fs: timings of the clock and of the console, to see why a PC is slow
\ (bare-metal ports: efi, rpi). On the stick: include speed.fs
\ Then read the figures, and count the seconds of the 3 s wait by your watch.
\   - the wait should take 3 s by the watch AND be measured about 3000 ms: if it
\     takes much longer, or the figure is far from 3000, the clock is wrong;
\   - 100 key? polls: about 100 ms (1 ms each); much more means a slow keyboard
\     or timer in the firmware;
\   - 2000 characters / 30 lines: a few tens of ms; seconds mean the screen is slow;
\   - sound: -1 if the HD Audio controller started (it starts at the first sound).

also rpi

: speed-report ( -- )
  ." clock (Hz): " cntfrq . cr
  ." waiting 3000 ms: count the seconds by your watch..." cr
  ms-ticks 3000 ms ms-ticks swap - ." ...measured " . ." ms" cr
  ." 100 key? polls: " ms-ticks 100 0 do key? drop loop ms-ticks swap - . ." ms" cr
  ." 2000 characters: " ms-ticks 2000 0 do 46 emit loop ms-ticks swap - . ." ms" cr
  cr ." 30 lines: " ms-ticks 30 0 do ." line " i . cr loop ms-ticks swap - . ." ms" cr
  ." sound started: " snd-ok . cr ;

previous

pager-off
speed-report
pager-on
