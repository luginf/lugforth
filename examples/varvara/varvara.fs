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

\ On the bare-metal boards (Pi, PC with UEFI) there is no SDL2: the same name loads varvara_rpi.fs.
[defined] posix 0= [if]  include varvara_rpi.fs  [else]

needs uxn.fs
needs file.fs
needs screen.fs
needs audio.fs
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
2 value scale
0 value zoom-set    ( the launcher chose the zoom )
512 value init-w   ( screen size at the start of a ROM )
320 value init-h
0 value no-mouse   ( ignore the mouse events )
0 value no-audio   ( no sound device )

create colors 16 4 * allot   ( SDL_Color: r g b a )
: palette ( -- a ) surface 8 + @ 8 + @ ;
: sdl-set-colors ( -- )
  16 0 do
    i 2 rshift ?dup 0= if i 3 and then system-color
    dup 8 rshift nibble>byte  i 4 * colors + c!
    dup 4 rshift nibble>byte  i 4 * colors + 1+ c!
    nibble>byte i 4 * colors + 2 + c!
    255 i 4 * colors + 3 + c!
  loop
  palette colors 0 16 SDL_SetPaletteColors drop  -1 to dirty ;
' sdl-set-colors is set-colors

: open-surface ( w h -- )
  surface if surface SDL_FreeSurface drop then
  2dup to scr-h to scr-w
  0 -rot 8 SDL_PIXELFORMAT_INDEX8 SDL_CreateRGBSurfaceWithFormat
  dup 0= if sdl-error type cr -1 throw then to surface
  surface 24 + sl@ to pitch  surface 32 + @ to pixels
  pixels pitch scr-h * erase
  scr-w 8 rshift $22 dev!  scr-w $23 dev!  scr-h 8 rshift $24 dev!  scr-h $25 dev!
  set-colors ;

: sdl-resize ( -- )
  $22 dev2@ $24 dev2@ 2dup scr-h = swap scr-w = and if 2drop exit then
  2dup open-surface
  window-handle -rot swap scale * swap scale * SDL_SetWindowSize drop
  renderer scr-w scr-h SDL_RenderSetLogicalSize drop ;
' sdl-resize is resize

: present ( -- )
  renderer surface SDL_CreateTextureFromSurface ?dup if
    renderer SDL_RenderClear drop
    renderer over 0 0 SDL_RenderCopy drop
    renderer SDL_RenderPresent drop
    SDL_DestroyTexture drop
  then 0 to dirty ;

( ---- Controller and Mouse ---- )

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
: posix-tm@ ( n -- v ) 4 * tm + sl@ ;
' posix-tm@ is tm@
( ---- Audio: see audio.fs ---- )
create aspec 32 allot
: audio-open ( -- )
  SDL_INIT_AUDIO SDL_InitSubSystem if exit then
  aspec 32 erase
  rate aspec l!  AUDIO_S16SYS aspec 4 + w!  2 aspec 6 + c!  1024 aspec 8 + w!
  0 0 aspec 0 0 SDL_OpenAudioDevice $ffffffff and to audio-dev
  audio-dev if audio-dev 0 SDL_PauseAudioDevice drop then ;

: audio-close ( -- )
  audio-dev if audio-dev SDL_CloseAudioDevice drop 0 to audio-dev then ;
: sdl-audio-queued ( -- n ) audio-dev SDL_GetQueuedAudioSize $ffffffff and 2 rshift ;
: sdl-audio-queue ( n -- ) >r audio-dev aout r> 4 * SDL_QueueAudio drop ;
' sdl-audio-queued is audio-queued
' sdl-audio-queue is audio-queue

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

[then]
