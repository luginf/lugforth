\ 2048, a text game for a terminal (also the web terminal and the Android console).
\
\ Port of the 2048 project of Marc Petremann (eForth Windows,
\ https://github.com/MPETREMANN11/eForth-Windows), GNU General Public License.
\ His part: the grid (`grid-init`, `getXYaddr`, `xyVal@`, `xyVal!`) and its display
\ (`.exp2N`, `grid-display`); the original stops there. The moves, the merging, the
\ score, the keys and the end of the game are new.
\
\     include examples/games/2048.fs
\     2048-play        \ arrows or w a s d (or h j k l) move, r restarts, q quits

also ansi

\ ---- Grid: one byte per cell, the exponent N of the tile 2^N (0 = empty) ----
4 constant GRID_WIDTH
GRID_WIDTH dup * constant GRID_SIZE
create 2048grid GRID_SIZE allot

: grid-init ( -- )   2048grid GRID_SIZE erase ;

\ address of the cell x y in the grid
: getXYaddr ( x y -- addr )   GRID_WIDTH * + 2048grid + ;
: xyVal@ ( x y -- n )   getXYaddr c@ ;
: xyVal! ( n x y -- )   getXYaddr c! ;

0 value score   0 value best   0 value moved

\ ---- Moves: direction 0 left, 1 right, 2 up, 3 down ----
0 value dir
\ the cell k of the line r, seen from the side the tiles move to
: line-xy ( r k -- x y )
  dir 0 = if swap exit then                       \ left:  x = k, y = r
  dir 1 = if GRID_WIDTH 1- swap - swap exit then  \ right: x = 3-k, y = r
  dir 2 = if exit then                            \ up:    x = r, y = k
  GRID_WIDTH 1- swap - ;                          \ down:  x = r, y = 3-k

create lbuf GRID_WIDTH allot   \ the line as it is
create nbuf GRID_WIDTH allot   \ the line after the move
0 value outn   -1 value pend

: line-load ( r -- )   GRID_WIDTH 0 ?do dup i line-xy xyVal@ lbuf i + c! loop drop ;
: line-store ( r -- )   GRID_WIDTH 0 ?do nbuf i + c@ over i line-xy xyVal! loop drop ;
: nbuf-add ( n -- )   nbuf outn + c!  1 +to outn ;

\ slide the tiles of lbuf to the front of nbuf, merging two equal neighbours once
: compact ( -- )
  nbuf GRID_WIDTH erase  0 to outn  -1 to pend
  GRID_WIDTH 0 ?do
    lbuf i + c@ dup if
      dup pend = if
        drop pend 1+ dup 1 swap lshift +to score  nbuf-add  -1 to pend
      else
        pend 0< 0= if pend nbuf-add then
        to pend
      then
    else drop then
  loop
  pend 0< 0= if pend nbuf-add then ;

: line-changed? ( -- f )
  0  GRID_WIDTH 0 ?do lbuf i + c@ nbuf i + c@ <> or loop ;

\ move one line, true when something changed
: slide-line ( r -- f )   dup line-load  compact  line-changed? swap line-store ;

\ move all the tiles in direction d, true when something moved
: move-tiles ( d -- f )
  to dir  0 to moved
  GRID_WIDTH 0 ?do i slide-line moved or to moved loop
  moved ;

\ ---- New tiles, end of the game ----
: empty-count ( -- n )   0  GRID_SIZE 0 ?do 2048grid i + c@ 0= if 1+ then loop ;

\ put a 2 (nine times out of ten) or a 4 on an empty cell chosen at random
: add-random ( -- )
  empty-count dup 0= if drop exit then
  random 1+                       \ the number of the cell among the empty ones
  GRID_SIZE 0 ?do
    2048grid i + c@ 0= if
      1- dup 0= if
        drop  10 random 0= if 2 else 1 then  2048grid i + c!
        unloop exit
      then
    then
  loop
  drop ;

: 2048? ( -- f )   0  GRID_SIZE 0 ?do 2048grid i + c@ 11 >= or loop ;   \ a 2048 tile

: same-right? ( x y -- f )   over GRID_WIDTH 1- < if 2dup xyVal@ >r swap 1+ swap xyVal@ r> = else 2drop 0 then ;
: same-down? ( x y -- f )   dup GRID_WIDTH 1- < if 2dup xyVal@ >r 1+ xyVal@ r> = else 2drop 0 then ;

\ a move is possible: an empty cell, or two equal tiles side by side
: can-move? ( -- f )
  empty-count 0<> if -1 exit then
  0  GRID_WIDTH 0 ?do GRID_WIDTH 0 ?do  i j 2dup same-right? >r same-down? r> or or  loop loop ;

\ ---- Display ----
\ the tile 2^n in a field of 5 characters
: .tile ( n -- )
  ?dup 0= if ."     ." exit then
  1 swap lshift <# #s #>  5 over - 0 max spaces type ;

create tile-colors 16 c,  229 c, 223 c, 215 c, 209 c, 203 c, 196 c, 227 c, 226 c, 220 c, 214 c, 208 c, 201 c, 201 c,
: tile-color ( n -- c )   12 min tile-colors + c@ ;

: .cell ( n -- )
  dup 0= if drop 237 bg  ."     ." normal exit then
  dup tile-color bg  0 fg  .tile normal ;

: grid-display ( -- )
  0 0 at-xy  ." 2048    score " score 6 .r ."   best " best 6 .r  clear-to-eol cr cr
  GRID_WIDTH 0 ?do
    GRID_WIDTH 0 ?do  i j xyVal@ .cell space  loop  cr cr
  loop
  ." arrows or wasd move, r restart, q quit" clear-to-eol ;

\ ---- Keys: arrows, wasd, hjkl; -1 quit, -2 restart ----
: arrow-key ( -- code )   key? 0= if -1 exit then  key [char] [ <> if -3 exit then  key
  dup [char] A = if drop 2 exit then  dup [char] B = if drop 3 exit then
  dup [char] C = if drop 1 exit then  [char] D = if 0 exit then  -3 ;
: get-move ( -- code )
  begin
    key dup 27 = if drop arrow-key else
    dup [char] a = over [char] h = or if drop 0 else
    dup [char] d = over [char] l = or if drop 1 else
    dup [char] w = over [char] k = or if drop 2 else
    dup [char] s = over [char] j = or if drop 3 else
    dup [char] q = if drop -1 else
    [char] r = if -2 else -3 then then then then then then then
    dup -3 =
  while drop repeat ;

\ ---- The game ----
: new-game ( -- )   grid-init  0 to score  add-random add-random ;

: 2048-play ( -- )
  ms-ticks 1 or rand-seed !
  page hide new-game
  begin
    grid-display
    2048? if s" You made 2048! Continue with r or q" else s" " then type
    can-move? 0= if score best max to best  grid-display cr s" No move left" type then
    get-move
    dup -1 = if drop show normal cr exit then
    dup -2 = if drop score best max to best  new-game else
      move-tiles if add-random then
      score best max to best
    then
  again ;
