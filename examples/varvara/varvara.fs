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

( Run a Uxn ROM in an SDL2 window, like uxnemu )
\
\   lugforth examples/varvara/varvara.fs program.rom
\
\ Devices: System (colors), Console (output), Screen (two layers of four
\ colors, pixels, fills and sprites), Controller, Mouse and Datetime.
\ The two layers live in one 8-bit SDL surface, a pixel being bg + 4 * fg:
\ its 16 entries palette shows fg when it is not 0, so a change of the
\ System colors costs nothing. VARVARA_ZOOM sets the zoom (default 2).

needs uxn.fs
sdl2
also uxn definitions

z" SDL_CreateRGBSurfaceWithFormat" 5 sdl SDL_CreateRGBSurfaceWithFormat ( flags w h depth format -- a )
z" SDL_SetPaletteColors" 4 sdl SDL_SetPaletteColors ( palette colors first n -- n )
z" SDL_SetWindowSize" 3 sdl SDL_SetWindowSize ( window w h -- void )
z" SDL_StartTextInput" 0 sdl SDL_StartTextInput ( -- void )
$13000801 constant SDL_PIXELFORMAT_INDEX8
$0303 constant SDL_TEXTINPUT

( ---- Screen: two layers in one surface ---- )

0 value surface
0 value pixels
0 value pitch
0 value scr-w
0 value scr-h
0 value dirty
2 value scale

create colors 16 4 * allot   ( SDL_Color: r g b a )
: palette ( -- a ) surface 8 + @ 8 + @ ;
: nib ( port c -- n ) swap dev2@ 12 rot 4 * - rshift $f and ;
: system-color ( c -- $rgb )   ( System/r g b hold one nibble per color )
  >r $08 r@ nib 8 lshift  $0a r@ nib 4 lshift or  $0c r> nib or ;
: nibble>byte ( n -- b ) $f and 17 * ;
: set-colors ( -- )
  16 0 do
    i 2 rshift ?dup 0= if i 3 and then system-color
    dup 8 rshift nibble>byte  i 4 * colors + c!
    dup 4 rshift nibble>byte  i 4 * colors + 1+ c!
    nibble>byte i 4 * colors + 2 + c!
    255 i 4 * colors + 3 + c!
  loop
  palette colors 0 16 SDL_SetPaletteColors drop  -1 to dirty ;

: open-surface ( w h -- )
  surface if surface SDL_FreeSurface drop then
  2dup to scr-h to scr-w
  0 -rot 8 SDL_PIXELFORMAT_INDEX8 SDL_CreateRGBSurfaceWithFormat
  dup 0= if sdl-error type cr -1 throw then to surface
  surface 24 + sl@ to pitch  surface 32 + @ to pixels
  pixels pitch scr-h * erase
  scr-w 8 rshift $22 dev!  scr-w $23 dev!  scr-h 8 rshift $24 dev!  scr-h $25 dev!
  set-colors ;

: resize ( -- )
  $22 dev2@ $24 dev2@ 2dup scr-h = swap scr-w = and if 2drop exit then
  2dup open-surface
  window-handle -rot swap scale * swap scale * SDL_SetWindowSize drop
  renderer scr-w scr-h SDL_RenderSetLogicalSize drop ;

: present ( -- )
  renderer surface SDL_CreateTextureFromSurface ?dup if
    renderer SDL_RenderClear drop
    renderer over 0 0 SDL_RenderCopy drop
    renderer SDL_RenderPresent drop
    SDL_DestroyTexture drop
  then 0 to dirty ;

0 value fg?   ( drawing on the foreground layer )
: plot ( c x y -- )
  dup scr-h < 0= if drop 2drop exit then
  over scr-w < 0= if drop 2drop exit then
  pitch * + pixels +
  fg? if dup c@ 3 and rot 2 lshift or else dup c@ $0c and rot or then swap c! ;

: fill-rect ( c x1 y1 x2 y2 -- )
  { c x1 y1 x2 y2 }
  x2 scr-w min to x2  y2 scr-h min to y2
  y2 y1 > x2 x1 > and if
    y2 y1 do x2 x1 do c i j plot loop loop
  then -1 to dirty ;

: screen-x ( -- x ) $28 dev2@ ;
: screen-y ( -- y ) $2a dev2@ ;
: screen-x! ( x -- ) $ffff and dup 8 rshift $28 dev! $29 dev! ;
: screen-y! ( y -- ) $ffff and dup 8 rshift $2a dev! $2b dev! ;
: auto ( -- b ) $26 dev@ ;

: pixel ( -- )
  $2e dev@ { ctrl }
  ctrl $40 and 0<> to fg?
  ctrl $80 and if
    ctrl 3 and
    ctrl $10 and if 0 screen-x else screen-x scr-w then
    ctrl $20 and if 0 screen-y else screen-y scr-h then
    rot swap ( c x1 y1 x2 y2 ) fill-rect
  else
    ctrl 3 and screen-x screen-y plot  -1 to dirty
    auto 1 and if screen-x 1+ screen-x! then
    auto 2 and if screen-y 1+ screen-y! then
  then ;

create blending
  0 c, 0 c, 0 c, 0 c, 1 c, 0 c, 1 c, 1 c, 2 c, 2 c, 0 c, 2 c, 3 c, 3 c, 3 c, 0 c,
  0 c, 1 c, 2 c, 3 c, 0 c, 1 c, 2 c, 3 c, 0 c, 1 c, 2 c, 3 c, 0 c, 1 c, 2 c, 3 c,
  1 c, 2 c, 3 c, 1 c, 1 c, 2 c, 3 c, 1 c, 1 c, 2 c, 3 c, 1 c, 1 c, 2 c, 3 c, 1 c,
  2 c, 3 c, 1 c, 2 c, 2 c, 3 c, 1 c, 2 c, 2 c, 3 c, 1 c, 2 c, 2 c, 3 c, 1 c, 2 c,

0 value spr-color
0 value spr-flipx
0 value spr-flipy
0 value spr-2bpp
0 value spr-opaque
0 value spr-bits
: blit ( addr x y -- )
  { a x y }
  8 0 do
    a i + ram@  spr-2bpp if a i + 8 + ram@ 8 lshift or then  to spr-bits
    y  spr-flipy if 7 i - else i then  +  $ffff and
    8 0 do
      spr-bits 1 and  spr-bits 7 rshift 2 and  or
      dup spr-opaque or if
        16 * spr-color + blending + c@
        x  spr-flipx if i else 7 i - then  +  $ffff and
        2 pick plot
      else drop then
      spr-bits 1 rshift to spr-bits
    loop drop
  loop ;

: sprite ( -- )
  $2f dev@ { ctrl }  auto { move }  $2c dev2@ { a }
  ctrl $40 and 0<> to fg?
  ctrl $0f and to spr-color
  ctrl $10 and 0<> to spr-flipx
  ctrl $20 and 0<> to spr-flipy
  ctrl $80 and 0<> to spr-2bpp
  spr-color 5 mod 0<> spr-color 0= or to spr-opaque
  spr-flipx if -1 else 1 then { fx }
  spr-flipy if -1 else 1 then { fy }
  move 1 and 3 lshift { dx }  move 2 and 2 lshift { dy }
  move 4 and if spr-2bpp if 16 else 8 then else 0 then { da }
  move 4 rshift 1+ 0 do
    a  screen-x dy fx * i * +  screen-y dx fy * i * +  blit
    da +to a
  loop
  move 1 and if screen-x dx fx * + screen-x! then
  move 2 and if screen-y dy fy * + screen-y! then
  move 4 and if a $ffff and dup 8 rshift $2c dev! $2d dev! then
  -1 to dirty ;

: screen-deo ( p -- )
  $0f and case
    $03 of resize endof
    $05 of resize endof
    $0e of pixel endof
    $0f of sprite endof
  endcase ;

: varvara-system-deo ( p -- )
  dup system-deo  $0f and dup $08 >= swap $0d <= and if set-colors then ;

( ---- Controller and Mouse ---- )

: controller ( -- ) $80 dev2@ eval ;
: buttons ( b f -- )   ( set or clear button bits )
  $82 dev@ swap if or else swap invert and then $82 dev! controller ;
: send-key ( c -- ) $83 dev! controller 0 $83 dev! ;

: mouse ( -- ) $90 dev2@ eval ;
: mouse-xy ( x y -- ) $ffff and dup 8 rshift $94 dev! $95 dev!
                      $ffff and dup 8 rshift $92 dev! $93 dev! ;
: mouse-button ( n f -- )
  >r 1 swap 1- lshift  $96 dev@ r> if or else swap invert and then $96 dev! ;
: scroll ( x y -- )
  $ffff and dup 8 rshift $9c dev! $9d dev!
  $ffff and dup 8 rshift $9a dev! $9b dev!
  mouse  0 0 $9a dev! $9b dev!  0 0 $9c dev! $9d dev! ;

: key-button ( sym -- b )
  case
    $400000e0 of $01 endof  $400000e4 of $01 endof   ( Ctrl: A )
    $400000e2 of $02 endof  $400000e6 of $02 endof   ( Alt: B )
    $400000e1 of $04 endof  $400000e5 of $04 endof   ( Shift: Select )
    $4000004a of $08 endof                           ( Home: Start )
    $40000052 of $10 endof  $40000051 of $20 endof
    $40000050 of $40 endof  $4000004f of $80 endof
    0 swap
  endcase ;
: key-char ( sym mod -- c | 0 )   ( keys that send no text event )
  over $20 < 2 pick $7f = or if drop exit then
  $c0 and if dup [char] a [char] z 1+ within if exit then then
  drop 0 ;

0 value quit-gui
: key-down ( -- )
  ev 20 + sl@ dup key-button ?dup if -1 buttons then
  ev 24 + uw@ key-char ?dup if send-key then ;
: key-up ( -- ) ev 20 + sl@ key-button ?dup if 0 buttons then ;
: text-input ( -- ) ev 12 + begin dup c@ ?dup while send-key 1+ repeat drop ;
: uxn-event ( -- )
  ev ul@ case
    SDL_QUIT of -1 to quit-gui endof
    SDL_KEYDOWN of key-down endof
    SDL_KEYUP of key-up endof
    SDL_TEXTINPUT of text-input endof
    SDL_MOUSEMOTION of ev 20 + sl@ ev 24 + sl@ mouse-xy mouse endof
    SDL_MOUSEBUTTONDOWN of ev 20 + sl@ ev 24 + sl@ mouse-xy
                           ev 16 + c@ -1 mouse-button mouse endof
    SDL_MOUSEBUTTONUP of ev 20 + sl@ ev 24 + sl@ mouse-xy
                         ev 16 + c@ 0 mouse-button mouse endof
    SDL_MOUSEWHEEL of ev 16 + sl@ ev 20 + sl@ negate scroll endof
  endcase ;

( ---- Datetime ---- )

also posix
z" getenv" 1 sysfunc libc-getenv
z" time" 1 sysfunc libc-time
z" localtime" 1 sysfunc libc-localtime
previous
create now-t 8 allot
: tm ( -- a ) now-t libc-time drop now-t libc-localtime ;
: tm@ ( n -- v ) 4 * tm + sl@ ;
: datetime-dei ( p -- b )
  dup $0f and case
    $00 of drop 5 tm@ 1900 + 8 rshift endof
    $01 of drop 5 tm@ 1900 + $ff and endof
    $02 of drop 4 tm@ endof
    $03 of drop 3 tm@ endof
    $04 of drop 2 tm@ endof
    $05 of drop 1 tm@ endof
    $06 of drop 0 tm@ endof
    $07 of drop 6 tm@ endof
    $08 of drop 7 tm@ 8 rshift endof
    $09 of drop 7 tm@ $ff and endof
    $0a of drop 8 tm@ 0<> 1 and endof
    drop dev@ 0
  endcase ;

( ---- Main loop ---- )

: setup-devices ( -- )
  ['] system-dei ['] varvara-system-deo 0 device!
  ['] plain-dei ['] screen-deo 2 device!
  ['] datetime-dei ['] plain-deo $c device! ;

: frame ( -- )
  begin ev SDL_PollEvent $ffffffff and while uxn-event repeat
  $20 dev2@ eval
  dirty if present then ;

: varvara ( a n -- )   ( run the ROM named a n )
  load-rom setup-devices
  z" VARVARA_ZOOM" libc-getenv ?dup if c@ [char] 0 - 1 max 8 min to scale then
  scale to zoom  512 320 screen  s" Varvara" title
  512 320 open-surface  SDL_StartTextInput drop
  $100 eval  present
  0 to quit-gui  ticks { next }
  begin quit-gui halted? or 0= while
    frame
    17 +to next  next ticks - dup 0> if SDL_Delay drop else drop ticks to next then
  repeat
  flush-out close-screen ;

: varvara-main ( -- )
  argc 3 < if ." usage: lugforth varvara.fs program.rom" cr 1 terminate then
  2 argv varvara exit-code terminate ;
varvara-main
