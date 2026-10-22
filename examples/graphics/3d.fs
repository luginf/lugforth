\ 3D wireframe objects: points, edges, rotation and perspective, without any display.
\ `sdl2_3d.fs` draws them.
\
\ Port of the 3Dforms project of Marc Petremann (eForth Windows,
\ https://github.com/MPETREMANN11/eForth-Windows), GNU General Public License.
\ His part: `3Dobject:`, `3Dedge:`, `3Dpoint,`, `3Dedge,`, `get3Dpoint`, `set3Dpoint`,
\ `project`, the sine table (`get-sin-90`, `get-cos-90`) and the cube. The original draws with
\ the Windows GDI; the rotations, the pyramid and the octahedron are new, and the
\ drawing is in sdl2_3d.fs.

\ ---- Sine table: 256 * sin(a) for a = 0 ... 90 degrees ----
create SINTAB 91 cells allot

: init-sintab ( -- )
  91 0 do
    i s>f 3.14159265e f* 180 s>f f/ fsin  256 s>f f* 0.5e f+ f>s
    i cells SINTAB + !
  loop ;
init-sintab

\ 256 * sin(angle), the angle in degrees, any integer
: get-sin ( angle -- n )
  360 mod dup 0< if 360 + then
  dup 91 < if cells SINTAB + @ exit then
  dup 181 < if 180 swap - cells SINTAB + @ exit then
  dup 271 < if 180 - cells SINTAB + @ negate exit then
  360 swap - cells SINTAB + @ negate ;
: get-cos ( angle -- n )   90 + get-sin ;

\ ---- Objects: a count, then the points (x y z) or the edges (p1 p2) ----
0 value nbItems

\ store and get the number of items, kept in the cell before the data
: setNbItemsInObject ( nbItems addr -- )   cell - ! ;
: getNbItemsInObject ( addr -- nbItems )   cell - @ ;
: reserveItemField ( -- )   0 ,  0 to nbItems ;

: 3Dobject: ( "name" -- )   create reserveItemField does> cell + ;
: 3Dedge: ( "name" -- )     create reserveItemField does> cell + ;

: 3Dpoint, ( x y z -- )   rot , swap , ,  1 +to nbItems ;
: 3Dedge, ( p1 p2 -- )    swap , ,  1 +to nbItems ;

3 cells constant 3Dpoint
2 cells constant 3Dedgesize

: get3Dpoint ( addr n -- x y z )   3Dpoint * +  dup @ swap cell+ dup @ swap cell+ @ ;
: set3Dpoint ( x y z addr n -- )   3Dpoint * +  >r  r@ 2 cells + !  r@ cell+ !  r> ! ;
: get3Dedge ( addr n -- p1 p2 )    3Dedgesize * +  dup @ swap cell+ @ ;

\ ---- The objects. The coordinates are in tenths of the original (+-100 for the cube) ----
3Dobject: CUBE-POINTS
  -100 -100 -100 3Dpoint,   100 -100 -100 3Dpoint,   100  100 -100 3Dpoint,  -100  100 -100 3Dpoint,
  -100 -100  100 3Dpoint,   100 -100  100 3Dpoint,   100  100  100 3Dpoint,  -100  100  100 3Dpoint,
nbItems CUBE-POINTS setNbItemsInObject

3Dedge: CUBE-EDGES
  0 1 3Dedge,  1 2 3Dedge,  2 3 3Dedge,  3 0 3Dedge,   \ back
  4 5 3Dedge,  5 6 3Dedge,  6 7 3Dedge,  7 4 3Dedge,   \ front
  0 4 3Dedge,  1 5 3Dedge,  2 6 3Dedge,  3 7 3Dedge,   \ links
nbItems CUBE-EDGES setNbItemsInObject

3Dobject: PYRAMID-POINTS
  -100 -100 -100 3Dpoint,   100 -100 -100 3Dpoint,   100 -100  100 3Dpoint,  -100 -100  100 3Dpoint,
     0  120    0 3Dpoint,
nbItems PYRAMID-POINTS setNbItemsInObject

3Dedge: PYRAMID-EDGES
  0 1 3Dedge,  1 2 3Dedge,  2 3 3Dedge,  3 0 3Dedge,   \ base
  0 4 3Dedge,  1 4 3Dedge,  2 4 3Dedge,  3 4 3Dedge,   \ apex
nbItems PYRAMID-EDGES setNbItemsInObject

3Dobject: OCTA-POINTS
  -120 0 0 3Dpoint,   120 0 0 3Dpoint,   0 -120 0 3Dpoint,   0 120 0 3Dpoint,
  0 0 -120 3Dpoint,   0 0 120 3Dpoint,
nbItems OCTA-POINTS setNbItemsInObject

3Dedge: OCTA-EDGES
  0 2 3Dedge,  0 3 3Dedge,  0 4 3Dedge,  0 5 3Dedge,
  1 2 3Dedge,  1 3 3Dedge,  1 4 3Dedge,  1 5 3Dedge,
  2 4 3Dedge,  4 3 3Dedge,  3 5 3Dedge,  5 2 3Dedge,
nbItems OCTA-EDGES setNbItemsInObject

\ the three objects: points and edges
create SHAPES  CUBE-POINTS , CUBE-EDGES ,  PYRAMID-POINTS , PYRAMID-EDGES ,  OCTA-POINTS , OCTA-EDGES ,
3 constant #SHAPES
: shape-points ( n -- addr )   2 * cells SHAPES + @ ;
: shape-edges ( n -- addr )    2 * 1+ cells SHAPES + @ ;

\ ---- Rotation (degrees) and perspective ----
0 value ax   0 value ay   0 value az      \ the angles of the object
variable FOCAL  300 FOCAL !
400 value distance                         \ the object is moved this far in z

0 value rx   0 value ry   0 value rz
\ rotate the point around x, then y, then z
: rotate ( x y z -- x' y' z' )
  to rz  to ry  to rx
  ry ax get-cos *  rz ax get-sin * -  256 /      \ y1
  ry ax get-sin *  rz ax get-cos * +  256 /      \ z1
  to rz  to ry
  rx ay get-cos *  rz ay get-sin * +  256 /      \ x2
  rz ay get-cos *  rx ay get-sin * -  256 /      \ z2
  to rz  to rx
  rx az get-cos *  ry az get-sin * -  256 /      \ x3
  rx az get-sin *  ry az get-cos * +  256 /      \ y3
  rz ;

\ perspective projection: xp = x * focal / z. A point too close to the eye is pushed to z = 1.
: project ( x y z -- xp yp )
  dup 1 < if drop 1 then
  >r  swap FOCAL @ * r@ /  swap FOCAL @ * r> / ;
