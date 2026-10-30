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

\ Magnoquill Driftflare Quiverbloom on the Game Boy Advance: the point cloud of
\ examples/graphics/gfx_magnoquill.fs (a few sines, rainbow colors), drawn in mode 3
\ (240x160). The formulas are the same, but they run in C, in 16.16 fixed point
\ with a sine table (`gba_magno` in main.c, opcode GBA-MAGNO): the first version,
\ in Forth floats, took more than ten seconds per picture for 250 points (the
\ ARM7TDMI has no FPU, every sin and cos is software). Forth keeps the boot, the
\ keys and the loop. The picture is 160x160, centered; mode 3 has a single
\ framebuffer and no additive blending, so each point erases its own pixel of the
\ previous frame instead of the screen being cleared (it would flicker).
\
\ Keys: UP / DOWN speed, LEFT / RIGHT number of points, A rainbow or white,
\ B pause, START time back to 0. There is nowhere to quit to on a cartridge.

also gba

100 constant npts-min   1500 constant npts-max   100 constant npts-step
262 constant t-step     ( 0.004 in 16.16: time step per frame at speed 1 )
variable npts   variable speed-level   variable paused   variable colormode
variable qt             ( time, 16.16 )

0 value cur-keys   0 value prev-keys
: poll-keys
   cur-keys to prev-keys
   REG_KEYINPUT h@ invert $3ff and to cur-keys ;
: key-down? ( mask -- f )   cur-keys and 0<> ;
: key-edge? ( mask -- f )   dup key-down? swap prev-keys and 0= and ;

: adjust-speed ( d -- )   speed-level @ + 1 max 10 min speed-level ! ;
: adjust-density ( d -- )   npts @ + npts-min max npts-max min npts ! ;

: handle-keys
   KEY_A key-edge? if colormode @ 0= colormode ! then
   KEY_B key-edge? if paused @ 0= paused ! then
   KEY_START key-edge? if 0 qt ! then
   KEY_UP key-down?    if  1 adjust-speed then
   KEY_DOWN key-down?  if -1 adjust-speed then
   KEY_RIGHT key-down? if  npts-step adjust-density then
   KEY_LEFT key-down?  if  npts-step negate adjust-density then ;

: step
   poll-keys handle-keys
   paused @ 0= if speed-level @ t-step * qt +! then
   qt @ npts @ colormode @ gba-magno
   pause ;

: run   begin step again ;

gba-mode3
0 gba-cls
400 npts !  5 speed-level !  0 qt !  0 paused !  1 colormode !
previous

' run
