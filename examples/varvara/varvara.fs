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
needs file.fs
sdl2
also uxn definitions

z" SDL_CreateRGBSurfaceWithFormat" 5 sdl SDL_CreateRGBSurfaceWithFormat ( flags w h depth format -- a )
z" SDL_SetPaletteColors" 4 sdl SDL_SetPaletteColors ( palette colors first n -- n )
z" SDL_SetWindowSize" 3 sdl SDL_SetWindowSize ( window w h -- void )
z" SDL_StartTextInput" 0 sdl SDL_StartTextInput ( -- void )
z" SDL_SaveBMP_RW" 3 sdl SDL_SaveBMP_RW ( surface rw free -- n )
z" SDL_free" 1 sdl SDL_free ( a -- void )
$1000 constant SDL_DROPFILE
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
0 value zoom-set    ( the launcher chose the zoom )
512 value init-w   ( screen size at the start of a ROM )
320 value init-h
0 value no-mouse   ( ignore the mouse events )
0 value no-audio   ( no sound device )

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
0 value cv-ready
0 value keepm    ( bits of a pixel byte kept: the other layer )
create cv 4 cells allot   ( by pixel value 0 to 3: the new byte for the layer, shifted, or -1 )
: set-colors-table ( -- )
  4 0 do
    i spr-opaque or if
      i 16 * spr-color + blending + c@  fg? if 2 lshift then
    else -1 then
    cv i cells + !
  loop
  fg? if 3 else $0c then to keepm ;

$0101010101010101 constant ones
0 value row-p
0 value row-x
: draw-row ( x y -- )   ( the 8 pixels of spr-bits on one row )
  $ffff and dup scr-h u< 0= if 2drop exit then
  pitch * pixels + to row-p  to row-x
  8 0 do
    spr-bits 1 and spr-bits 7 rshift 2 and or cells cv + @
    dup 0< 0= if
      row-x  spr-flipx if i else 7 i - then +  $ffff and
      dup scr-w u< if row-p + dup c@ keepm and 2 pick or swap c! drop else 2drop then
    else drop then
    spr-bits 1 rshift to spr-bits
  loop ;

: zero-tile? ( a -- f )   ( all the bytes of the tile are 0 )
  spr-2bpp if 16 else 8 then 0
  ?do dup i + ram@ if drop 0 unloop exit then loop drop -1 ;

: fill-tile ( x y -- )   ( an opaque 8x8 tile of 0 bits: one 8 byte write for each row )
  { x y }
  cv @ ones * { vv }  keepm ones * { km }
  x $ffff and 8 + scr-w > if
    8 0 do 0 to spr-bits  x y i + draw-row loop exit
  then
  x $ffff and { xm }
  8 0 do
    y i + $ffff and dup scr-h u< if pitch * pixels + xm + dup @ km and vv or swap ! else drop then
  loop ;

: blit ( addr x y -- )
  { a x y }
  a zero-tile? if
    spr-opaque 0= if exit then
    cv-ready 0= if set-colors-table -1 to cv-ready then
    x y fill-tile exit
  then
  cv-ready 0= if set-colors-table -1 to cv-ready then
  8 0 do
    a i + ram@  spr-2bpp if a i + 8 + ram@ 8 lshift or then  to spr-bits
    spr-bits spr-opaque or if
      x  y spr-flipy if 7 i - else i then +  draw-row
    then
  loop ;

: sprite ( -- )
  $2f dev@ { ctrl }  auto { move }  $2c dev2@ { a }
  ctrl $40 and 0<> to fg?
  ctrl $0f and to spr-color
  ctrl $10 and 0<> to spr-flipx
  ctrl $20 and 0<> to spr-flipy
  ctrl $80 and 0<> to spr-2bpp
  spr-color 5 mod 0<> spr-color 0= or to spr-opaque
  0 to cv-ready
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
0 value reboot   ( -1 when F4 or a dropped file asks to load the ROM again )
create rom-path 512 allot
0 value rom-len
: zoom-step ( -- )
  scale 3 < if scale 1+ else 1 then to scale
  window-handle scr-w scale * scr-h scale * SDL_SetWindowSize drop ;
: screenshot ( -- )
  surface z" varvara.bmp" z" wb" SDL_RWFromFile 1 SDL_SaveBMP_RW drop ;
: rom! ( a n -- ) 511 min dup to rom-len rom-path swap cmove ;
: shortcut ( sym -- f )   ( F1 zoom, F2 stacks, F3 screenshot, F4 reboot )
  case
    $4000003a of zoom-step -1 endof
    $4000003b of flush-out debug-stacks -1 endof
    $4000003c of screenshot -1 endof
    $4000003d of -1 to reboot -1 endof
    0 swap
  endcase ;
: key-down ( -- )
  ev 20 + sl@ shortcut if exit then
  ev 20 + sl@ dup key-button ?dup if -1 buttons then
  ev 24 + uw@ key-char ?dup if send-key then ;
: key-up ( -- ) ev 20 + sl@ key-button ?dup if 0 buttons then ;
: text-input ( -- ) ev 12 + begin dup c@ ?dup while send-key 1+ repeat drop ;
: mouse-type? ( t -- f )
  dup SDL_MOUSEMOTION = over SDL_MOUSEBUTTONDOWN = or
  over SDL_MOUSEBUTTONUP = or swap SDL_MOUSEWHEEL = or ;
: uxn-event ( -- )
  no-mouse if ev ul@ mouse-type? if exit then then
  ev ul@ case
    SDL_QUIT of -1 to quit-gui endof
    SDL_DROPFILE of ev 8 + @ dup dup zlen rom! SDL_free -1 to reboot endof
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

( ---- Audio: four sample channels, mixed in software ---- )
\ Same algorithm as the C uxn: each channel steps through its sample at a pitch
\ (single cycle for samples up to 256 bytes, else a repeat at the note rate),
\ shaped by an ADSR envelope, left and right volume in one nibble each. The mix
\ of the four channels is queued ahead (about 60 ms) by `audio-pump` once a frame.

44100 constant rate
$4000 rate * 11025 / constant note-period
rate 15 / constant adsr-step
2646 constant lead   ( frames kept queued )

create advances
  $80000 , $879c8 , $8facd , $9837f , $a1451 , $aadc1 ,
  $b504f , $bfc88 , $cb2ff , $d7450 , $e411f , $f1a1c ,

: fld ( n "name" -- ) create cells , does> @ + ;   ( a field of a channel, in cells )
 0 fld a-adv    1 fld a-per    2 fld a-cnt    3 fld a-i      4 fld a-len
 5 fld a-addr   6 fld a-age    7 fld a-a      8 fld a-d      9 fld a-s
10 fld a-r     11 fld a-vl    12 fld a-vr    13 fld a-rep   14 fld a-done
create achan 4 16 cells * allot
: ch ( n -- a ) 16 cells * achan + ;

0 value audio-dev
create aspec 32 allot
create amix 2048 2* cells allot     ( 2048 frames, left and right )
create aout 2048 4 * allot          ( the same as signed 16 bits )

: envelope ( c age -- e )
  { c age }
  c a-r @ 0= if $888 exit then
  age c a-a @ < if $888 age * c a-a @ / exit then
  age c a-d @ < if $444 c a-d @ 2* c a-a @ - age - * c a-d @ c a-a @ - / exit then
  age c a-s @ < if $444 exit then
  age c a-r @ < if $444 c a-r @ age - * c a-r @ c a-s @ - / exit then
  0 c a-adv ! 0 ;

: render-chan ( c n -- )   ( adds n frames of a channel to amix )
  { c n }
  c a-adv @ 0= c a-per @ 0= or if exit then
  n 0 ?do
    c a-adv @ 0= if leave then
    c a-adv @ c a-cnt +!
    c a-cnt @ c a-per @ / c a-i +!
    c a-cnt @ c a-per @ mod c a-cnt !
    c a-i @ c a-len @ >= if
      c a-rep @ if c a-i @ c a-len @ mod c a-i ! else 0 c a-adv ! leave then
    then
    c a-addr @ c a-i @ + ram@ $80 -
    c a-age @ dup 1+ c a-age !  c swap envelope *
    dup c a-vl @ * $180 / amix i 2* cells + +!
        c a-vr @ * $180 / amix i 2* 1+ cells + +!
  loop
  c a-adv @ 0= if 1 c a-done ! then ;

: audio-start ( n -- )   ( the pitch port of channel n was written )
  { n }
  n $10 * $30 + { base }
  n ch { c }
  base $f + dev@ $7f and { pitch }
  base 8 + dev2@ { adsr }
  base $a + dev2@ c a-len !
  base $c + dev2@ c a-addr !
  base $e + dev@ dup 4 rshift c a-vl !  $f and c a-vr !
  base $f + dev@ $80 and 0= c a-rep !
  pitch 108 < c a-len @ 0<> and 0= if 0 c a-adv ! exit then
  advances pitch 12 mod cells + @  8 pitch 12 / -  rshift c a-adv !
  adsr-step adsr 12 rshift * c a-a !
  adsr-step adsr 8 rshift $f and * c a-a @ + c a-d !
  adsr-step adsr 4 rshift $f and * c a-d @ + c a-s !
  adsr-step adsr $f and * c a-s @ + c a-r !
  0 c a-age !  0 c a-i !
  c a-len @ $100 <= if note-period 337 * 2/ c a-len @ / else note-period then c a-per ! ;

: audio-deo ( p -- )
  dup $f and $f = if 4 rshift 3 - audio-start else drop then ;

: vu ( c i -- n )
  { c i }  c a-adv @ 0= c a-per @ 0= or if 0 exit then
  i if c a-vr @ else c a-vl @ then 0= if 0 exit then
  c c a-age @ envelope  i if c a-vr @ else c a-vl @ then * $800 / 1+ $f min ;

: audio-dei ( p -- b )
  { p }
  p $f and { port }  p $f0 and { base }  p 4 rshift 3 - ch { c }
  audio-dev 0= if base port + dev@ exit then
  port 4 = if c 0 vu 4 lshift c 1 vu or exit then
  port 2 = if c a-i @ dup 8 rshift base 2 + dev! base 3 + dev! then
  base port + dev@ ;

: audio-open ( -- )
  SDL_INIT_AUDIO SDL_InitSubSystem if exit then
  aspec 32 erase
  rate aspec l!  AUDIO_S16SYS aspec 4 + w!  2 aspec 6 + c!  1024 aspec 8 + w!
  0 0 aspec 0 0 SDL_OpenAudioDevice $ffffffff and to audio-dev
  audio-dev if audio-dev 0 SDL_PauseAudioDevice drop then ;

: audio-close ( -- )
  audio-dev if audio-dev SDL_CloseAudioDevice drop 0 to audio-dev then ;

: audio-active? ( -- f ) 0 4 0 do i ch a-adv @ 0<> or loop ;

: audio-pump ( -- )
  audio-dev 0= if exit then
  audio-active? 0= if exit then
  lead audio-dev SDL_GetQueuedAudioSize $ffffffff and 2 rshift - 2048 min { n }
  n 1 < if exit then
  amix n 2* cells erase
  4 0 do i ch n render-chan loop
  n 2* 0 do amix i cells + @ 32767 min -32768 max aout i 2* + w! loop
  audio-dev aout n 4 * SDL_QueueAudio drop
  4 0 do
    i ch a-done @ if
      0 i ch a-done !
      i $10 * $30 + dev2@ ?dup if eval then
    then
  loop ;

( ---- Main loop ---- )

: setup-devices ( -- )
  ['] system-dei ['] varvara-system-deo 0 device!
  ['] plain-dei ['] screen-deo 2 device!
  4 0 do ['] audio-dei ['] audio-deo i 3 + device! loop
  ['] datetime-dei ['] plain-deo $c device! ;

: frame ( -- )
  begin ev SDL_PollEvent $ffffffff and while uxn-event repeat
  $20 dev2@ eval  audio-pump  flush-out
  dirty if present then ;

: basename ( a n -- a' n' )   ( the part after the last slash )
  begin
    2dup [char] / scan dup while
    1 /string 2swap 2drop
  repeat 2drop ;
create title-buf 256 allot
: window-title ( a n -- )   ( forth/uxn/varvara : name.rom )
  basename 200 min { a n }
  s" forth/uxn/varvara : " title-buf swap cmove
  a title-buf 20 + n cmove
  title-buf n 20 + title ;

: reset-screen ( -- )   ( back to the initial size after a reboot )
  init-w init-h open-surface
  window-handle init-w scale * init-h scale * SDL_SetWindowSize drop
  renderer init-w init-h SDL_RenderSetLogicalSize drop ;

( ---- Console input: the arguments of the command line, then stdin ---- )
also posix
z" poll" 3 sysfunc libc-poll
previous
create pfd 8 allot
0 value stdin-eof
: stdin-ready? ( -- f ) 0 pfd l!  1 pfd 4 + w!  0 pfd 6 + w!  pfd 1 0 libc-poll 0> ;
: stdin-pump ( -- )   ( what was typed in the terminal goes to the Console vector )
  stdin-eof if exit then
  64 0 do
    stdin-ready? 0= if leave then
    stdin-byte dup 0< if drop -1 to stdin-eof leave then
    console-input
  loop ;

: boot ( -- )
  rom-path rom-len load-rom  setup-devices  setup-files
  achan 4 16 cells * erase
  rom-path rom-len window-title
  reset-screen
  n-args 0> if 1 $17 dev! then
  $100 eval  run-args  present ;

: varvara ( a n -- )   ( run the ROM named a n )
  rom!
  zoom-set 0= if z" VARVARA_ZOOM" libc-getenv ?dup if c@ [char] 0 - 1 max 8 min to scale then then
  scale to zoom  init-w init-h screen
  init-w init-h open-surface  SDL_StartTextInput drop
  no-audio 0= if audio-open then
  boot
  0 to quit-gui  ticks { next }
  begin quit-gui halted? or 0= while
    reboot if 0 to reboot boot then
    stdin-pump  frame
    17 +to next  next ticks - dup 0> if SDL_Delay drop else drop ticks to next then
  repeat
  flush-out audio-close close-screen ;

: script-dir ( -- a n )   ( the directory of this file, with its slash )
  1 argv
  begin dup while 2dup + 1- c@ [char] / = if exit then 1- repeat
  2drop s" " ;
create boot-path 512 allot
: boot-rom ( -- a n )   ( boot.rom, next to varvara.fs )
  script-dir 400 min { a n }
  a boot-path n cmove  s" boot.rom" boot-path n + swap cmove
  boot-path n 8 + ;

: varvara-main ( -- )
  3 args-from-argv
  argc 3 < if boot-rom else 2 argv then varvara exit-code terminate ;
[undefined] launching [if] varvara-main [then]
