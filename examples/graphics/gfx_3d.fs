#! /usr/bin/env lugforth

\ 3D wireframe objects in an SDL2 window: a cube, a pyramid and an octahedron that turn.
\ Port of the 3Dforms project of Marc Petremann (eForth Windows, GNU General Public
\ License), see 3d.fs. The original draws with the Windows GDI.
\   space or 1 2 3   next object, or choose it
\   arrows           spin faster or slower around x and y (z with a and d)
\   p / o            nearer / farther  (perspective)
\   r                stop the spin
\   esc              quits (or close the window)
\ Needs libSDL2.

\ On the web the page loads 3d.fs itself first (terminal.html?run=3d.fs,gfx_3d.fs).
[undefined] init-sintab [if] include 3d.fs [then]

sdl2

640 value w   480 value h
0 value shape
0 value sx   2 to sx     \ the spin, in degrees per frame
0 value sy   3 to sy
0 value sz
0 value was-space

\ the point n of the current object: rotated, moved away and projected, on the screen
: screen-point ( n -- x y )
  shape shape-points swap get3Dpoint  rotate  distance +  project
  negate h 2/ +  swap w 2/ + swap ;

: draw-shape ( -- )
  shape shape-edges getNbItemsInObject 0 ?do
    shape shape-edges i get3Dedge   ( p1 p2 )
    swap screen-point rot screen-point line
  loop ;

: spin ( -- )
  sx +to ax   sy +to ay   sz +to az
  ax 360 mod to ax   ay 360 mod to ay   az 360 mod to az ;

: change-shape ( -- )   shape 1+ #SHAPES mod to shape ;

: keys ( -- )
  key-space pressed? dup was-space 0= and if change-shape then to was-space
  [char] 1 pressed? if 0 to shape then
  [char] 2 pressed? if 1 to shape then
  [char] 3 pressed? if 2 to shape then
  key-left pressed? if -1 +to sy then
  key-right pressed? if 1 +to sy then
  key-up pressed? if -1 +to sx then
  key-down pressed? if 1 +to sx then
  [char] a pressed? if -1 +to sz then
  [char] d pressed? if 1 +to sz then
  [char] p pressed? if distance 4 - 150 max to distance then
  [char] o pressed? if distance 4 + 1500 min to distance then
  [char] r pressed? if 0 to sx 0 to sy 0 to sz then ;

: draw-all ( -- )
  $101018 color cls
  shape 0 = if green else shape 1 = if yellow else cyan then then color
  draw-shape
  flip ;

: step ( -- )
  events
  key-esc pressed? if quit! then
  keys spin draw-all
  dt 16 < if 16 dt - delay then ;

: run ( -- ) begin step quit? until close-screen ;

w h screen
s" Lugforth 3D" title
run
script-bye
