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

\ Add a yielding task so pause yields once per VBlank -- same shape as
\ pico-ice/platform.fs, but here a "yield" is a whole video frame.
internals definitions
: yield-step   raw-yield yield ;
' yield-step 100 100 task yield-task
yield-task start-task
forth definitions

\ No filesystem, no stdin/stdout/stderr handles: RAW-TYPE, in builtins.h, is
\ the only output, and there is no key input yet -- see README.txt.
\ `forth definitions internals`, not `also internals definitions`: these
\ words must land in the FORTH vocabulary, same as pico-ice/platform.fs, or
\ `' default-type` below finds nothing once internals drops out of the
\ search order -- and printing that "not found" error is itself not
\ possible yet, since it needs `type`, the very word being bound: a silent
\ crash, not a message, the one real trap in this whole file. Also a trap
\ for whoever edits this file: a nested ( paren ) inside a ( comment ) like
\ this one closes it early and leaks the rest as code -- same silent-crash
\ symptom, since by the time that happens `type` usually still is not
\ bound; stick to \ line comments here until it is.
forth definitions internals
: setup-saving-base ;   \ fini.fs calls this unconditionally; no save-image support yet
: default-type ( a n -- ) raw-type ;
: default-key? ( -- f ) 0 ;
: default-key ( -- ch ) begin default-key? 0= while pause repeat 0 ;
forth

' default-type is type
' default-key is key
' default-key? is key?
' raw-terminate is terminate
-1 echo !

\ Every platform defines `ok` itself (posix/posix.fs and pico-ice/platform.fs
\ both do); there is no default. fini.fs does `' ok execute` as its very
\ last act, and raw-ok ends by calling `quit`, the real interactive loop --
\ this is what was missing before, the silent-crash symptom one more time
\ (`' ok` found nothing, `type` was fine by then, but still no message).
internals : ok   ." uEforth" raw-ok ; forth

\ ---- the GBA itself: fixed memory-mapped addresses, no opcodes needed.
\ See GBATEK for the full register map; these are just enough to get a dot
\ on screen in mode 3 (240x160, 16 bits/pixel, one direct framebuffer, no
\ tiles or sprites yet) and read the d-pad/buttons.
vocabulary gba   gba definitions

gba_reg_dispcnt constant REG_DISPCNT
gba_reg_vcount constant REG_VCOUNT
gba_reg_keyinput constant REG_KEYINPUT   ( active low - 0 bit means pressed )
gba_vram constant VRAM
240 constant gba-width   160 constant gba-height

$1 constant KEY_A        $2 constant KEY_B
$4 constant KEY_SELECT   $8 constant KEY_START
$10 constant KEY_RIGHT   $20 constant KEY_LEFT
$40 constant KEY_UP      $80 constant KEY_DOWN
$100 constant KEY_R      $200 constant KEY_L

\ The kernel only has byte (c@ c!) and cell (@ !) access, and a cell is not
\ 16 bits here, but every GBA register and VRAM word is: two plain byte
\ stores, little-endian, work everywhere and need no assumption about the
\ host's cell size.
: h! ( n addr -- )   { n a } n a c!  n 8 rshift a 1+ c! ;
: h@ ( addr -- n )   { a } a 1+ c@ 8 lshift  a c@ or ;

: gba-key? ( mask -- f )   REG_KEYINPUT h@ invert and 0<> ;

( mode 3: BG2 on, direct 16-bit color framebuffer at VRAM )
: gba-mode3   $0403 REG_DISPCNT h! ;

: rgb15 ( r g b -- $rrggb )   ( 0..31 each -> a GBA color, BG2 bitmap format )
  10 lshift swap 5 lshift or swap or ;
: gba-dot ( x y color -- )
  { x y c } c  VRAM  gba-width y * x +  2 *  +  h! ;

\ examples/sdl2_tiles.png, converted by convert_tiles.py: plain RGB15
\ pixels, no palette (mode 3 has none), one tile after another, row-major
\ within a tile. $ffff (rgb15 never sets bit 15: max is $7fff) marks a
\ transparent pixel, skipped rather than drawn.
gba_tiles constant TILES
8 constant tile-size   8 constant tile-count   $ffff constant tile-transparent

: tile-pixel ( tile px py -- color )
  { t px py }
  t tile-size tile-size * *  py tile-size * +  px +  2 *  TILES + h@ ;

: draw-tile-pixel ( tile x y px py -- )
  { t sx sy px py }
  t px py tile-pixel { c }
  c tile-transparent <> if sx px + sy py + c gba-dot then ;

: draw-tile ( tile x y -- )
  { t sx sy }
  tile-size 0 ?do
    tile-size 0 ?do
      t sx sy i j draw-tile-pixel
    loop
  loop ;

\ ---- added while porting examples/sdl2_roguelike.fs (see gba/roguelike.fs):
\ a per-channel tint (SDL2's image-tint, done by hand: no surface blending
\ here, just a multiply on each 5-bit channel of a color already in hand)
\ and a couple of plain rectangle-fill helpers, since mode 3 has no box/cls
\ primitive of its own -- everything is still a `gba-dot` loop underneath.

: color15 ( $rrggbb -- color )   ( quantizes a 24-bit color to RGB15 )
  { c }
  c 16 rshift $ff and 3 rshift
  c 8 rshift $ff and 3 rshift
  c $ff and 3 rshift
  rgb15 ;

\ Channel order here has to match rgb15's own packing: the first ( r )
\ argument lands in the LOW 5 bits (see rgb15's definition above -- despite
\ the ( r g b -- $rrggb ) comment reading left-to-right high-to-low, `10
\ lshift swap 5 lshift or swap or` actually ORs the un-shifted top-of-stack
\ arg in last, at the bottom), so the first channel extracted below has to
\ be the low bits ($1f and, no shift), not the high ones -- got this
\ backwards once while porting examples/sdl2_roguelike.fs (red and blue
\ swapped, not a crash, just wrong colors) and fixed it here.
: tint ( color tint -- color' )   ( each channel: color * tint / 31 )
  { c t }
  c $1f and             t $1f and             *  31 /
  c 5 rshift $1f and    t 5 rshift $1f and    *  31 /
  c 10 rshift $1f and   t 10 rshift $1f and   *  31 /
  rgb15 ;

\ draw-tile-tinted and gba-box below used to be plain Forth, a `gba-dot` (or
\ `tile-pixel`+`tint`+`gba-dot`) loop same as draw-tile above. That worked,
\ but a redraw is 500+ tiles, 30000+ pixels, and each pixel cost several
\ interpreted word dispatches -- confirmed unplayable by the user on real
\ mgba (about 20 seconds per redraw) once examples/sdl2_roguelike.fs was
\ ported and actually redrawing that much every turn. GBA-BLIT-TILE and
\ GBA-FILL-RECT (gba_blit_tile/gba_fill_rect in main.c) do the exact same
\ pixel math natively instead; same Forth-level names and stack effects as
\ before, so roguelike.fs needed no changes at all.
: draw-tile-tinted ( tile x y tint -- )   GBA-BLIT-TILE ;

: gba-box ( x y w h color -- )   GBA-FILL-RECT ;

: gba-cls ( color -- )   { c }   0 0 gba-width gba-height c gba-box ;

forth definitions
