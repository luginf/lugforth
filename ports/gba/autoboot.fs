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

\ Unlike pico-ice (a USB-serial REPL, so autoboot.fs is still a TODO there),
\ this platform has no input at all yet (see platform.fs), so whatever runs
\ here *is* the whole program: the tile sheet (examples/sdl2_tiles.png,
\ converted by convert_tiles.py into tiles_data.h) drawn in a row, to check
\ gba-dot/draw-tile/VRAM addressing by eye, in lieu of a real emulator.

also gba
gba-mode3
: demo-frame
  tile-count 0 ?do
    i i tile-size * 4 + 4 draw-tile
  loop ;
demo-frame
previous

' ok
