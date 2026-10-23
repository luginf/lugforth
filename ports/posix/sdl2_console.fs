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

( A text console drawn in an SDL window, with its own bitmap font )
\
\ Usage (the code is part of the lugforth binary, `lugforth --gui` does all this):
\   gui-load           \ loads sdl2 and this console
\   desktop console    \ type, key and key? now go to the SDL window (with sdl2 on top)
\
\ Needs only libSDL2. Keys come from SDL text input, so the native keyboard of
\ Android shows up. When a program opens its own screen (screen), the console
\ window is replaced; it comes back at the next key.
\ The font is a bitmap of 8x16, derived from DejaVu Sans Mono (Bitstream Vera
\ license), drawn at a whole multiple.
\
\ `smooth-font` before `console` uses a TrueType font if SDL_ttf and a font file are
\ found (LUGFORTH_FONT, or DejaVu Sans Mono of the system).
\ On a desktop, `desktop` before `console` adds a menu bar (examples, text size,
\ help), mouse selection that is copied, paste, scrolling back, and a smooth
\ TrueType font when SDL_ttf and a font are found (LUGFORTH_FONT, or DejaVu Sans
\ Mono of the system). Text size: Ctrl+wheel, Ctrl +, Ctrl -, Ctrl 0.

( The code is a string, evaluated when asked: SDL2 is only loaded then. )
needs eval_copy.fs
needs sdl2.fs

\ The source of the console window (lugforth --gui), a string compiled when the window is first opened.
: gui-src r|
sdl2
sdl2 definitions also posix

z" SDL_GetRendererOutputSize" 3 sdl SDL_GetRendererOutputSize ( a a a -- n )
z" SDL_StartTextInput" 0 sdl SDL_StartTextInput ( -- void )
z" SDL_StopTextInput" 0 sdl SDL_StopTextInput ( -- void )
z" SDL_IsScreenKeyboardShown" 1 sdl SDL_IsScreenKeyboardShown ( a -- n )
z" SDL_GetModState" 0 sdl SDL_GetModState ( -- n )
z" SDL_SetClipboardText" 1 sdl SDL_SetClipboardText ( z -- n )
z" SDL_GetClipboardText" 0 sdl SDL_GetClipboardText ( -- z )
z" SDL_free" 1 sdl SDL_free ( a -- void )
\ The X11 primary selection (the text just selected, pasted with the middle click) came with SDL 2.26:
\ looked up by hand, so that an older SDL2 still gives the console, without the primary selection.
' sdl >body @ constant sdl-lib
: primary-get-fn ( -- a )   z" SDL_GetPrimarySelectionText" sdl-lib swap dlsym ;
: primary-set-fn ( -- a )   z" SDL_SetPrimarySelectionText" sdl-lib swap dlsym ;
also internals ' call0 constant ccall0   ' call1 constant ccall1 previous
: primary-get ( -- z or 0 ) primary-get-fn dup 0= if exit then ccall0 execute ;
: primary-set ( z -- ) primary-set-fn dup 0= if 2drop exit then ccall1 execute drop ;
z" SDL_SetRenderTarget" 2 sdl SDL_SetRenderTarget ( a a -- n )
z" getenv" 1 sysfunc c-getenv ( z -- z )
$0303 constant SDL_TEXTINPUT

( ---- Font: 95 glyphs, from space to tilde, 16 rows of 8 bits each ---- )
create font-data
  $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c,   \ space
  $00 c, $00 c, $00 c, $18 c, $18 c, $18 c, $18 c, $18 c, $18 c, $00 c, $18 c, $18 c, $00 c, $00 c, $00 c, $00 c,   \ !
  $00 c, $00 c, $00 c, $2c c, $2c c, $2c c, $2c c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c,   \ "
  $00 c, $00 c, $12 c, $16 c, $16 c, $7f c, $24 c, $2c c, $fe c, $68 c, $48 c, $48 c, $00 c, $00 c, $00 c, $00 c,   \ #
  $00 c, $00 c, $00 c, $08 c, $3c c, $6e c, $68 c, $38 c, $1e c, $0a c, $4e c, $3c c, $08 c, $08 c, $00 c, $00 c,   \ $
  $00 c, $00 c, $00 c, $70 c, $90 c, $90 c, $76 c, $18 c, $6e c, $0b c, $0b c, $0e c, $00 c, $00 c, $00 c, $00 c,   \ %
  $00 c, $00 c, $00 c, $3c c, $60 c, $20 c, $30 c, $5b c, $cb c, $c6 c, $66 c, $3f c, $00 c, $00 c, $00 c, $00 c,   \ &
  $00 c, $00 c, $00 c, $18 c, $18 c, $18 c, $18 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c,   \ '
  $00 c, $0c c, $08 c, $18 c, $10 c, $10 c, $10 c, $10 c, $10 c, $10 c, $18 c, $08 c, $0c c, $00 c, $00 c, $00 c,   \ (
  $00 c, $30 c, $10 c, $18 c, $18 c, $08 c, $08 c, $08 c, $08 c, $18 c, $18 c, $10 c, $30 c, $00 c, $00 c, $00 c,   \ )
  $00 c, $00 c, $00 c, $10 c, $56 c, $3c c, $3c c, $56 c, $10 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c,   \ *
  $00 c, $00 c, $00 c, $00 c, $18 c, $18 c, $18 c, $fe c, $18 c, $18 c, $18 c, $00 c, $00 c, $00 c, $00 c, $00 c,   \ +
  $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $18 c, $18 c, $10 c, $10 c, $00 c, $00 c,   \ ,
  $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $3c c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c,   \ -
  $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $18 c, $18 c, $00 c, $00 c, $00 c, $00 c,   \ .
  $00 c, $00 c, $00 c, $06 c, $04 c, $0c c, $08 c, $18 c, $10 c, $10 c, $30 c, $20 c, $60 c, $40 c, $00 c, $00 c,   \ /
  $00 c, $00 c, $00 c, $3c c, $66 c, $66 c, $46 c, $5a c, $46 c, $66 c, $66 c, $3c c, $00 c, $00 c, $00 c, $00 c,   \ 0
  $00 c, $00 c, $00 c, $78 c, $08 c, $08 c, $08 c, $08 c, $08 c, $08 c, $08 c, $3e c, $00 c, $00 c, $00 c, $00 c,   \ 1
  $00 c, $00 c, $00 c, $3c c, $46 c, $06 c, $06 c, $0c c, $18 c, $30 c, $60 c, $7e c, $00 c, $00 c, $00 c, $00 c,   \ 2
  $00 c, $00 c, $00 c, $3c c, $44 c, $06 c, $04 c, $3c c, $06 c, $06 c, $46 c, $7c c, $00 c, $00 c, $00 c, $00 c,   \ 3
  $00 c, $00 c, $00 c, $0c c, $1c c, $14 c, $24 c, $64 c, $44 c, $fe c, $04 c, $04 c, $00 c, $00 c, $00 c, $00 c,   \ 4
  $00 c, $00 c, $00 c, $7c c, $60 c, $60 c, $7c c, $06 c, $06 c, $06 c, $46 c, $7c c, $00 c, $00 c, $00 c, $00 c,   \ 5
  $00 c, $00 c, $00 c, $3c c, $64 c, $40 c, $7c c, $66 c, $42 c, $42 c, $66 c, $3c c, $00 c, $00 c, $00 c, $00 c,   \ 6
  $00 c, $00 c, $00 c, $7e c, $06 c, $04 c, $0c c, $08 c, $18 c, $18 c, $10 c, $30 c, $00 c, $00 c, $00 c, $00 c,   \ 7
  $00 c, $00 c, $00 c, $3c c, $66 c, $66 c, $66 c, $3c c, $66 c, $42 c, $66 c, $3c c, $00 c, $00 c, $00 c, $00 c,   \ 8
  $00 c, $00 c, $00 c, $3c c, $66 c, $46 c, $46 c, $66 c, $3e c, $06 c, $4c c, $38 c, $00 c, $00 c, $00 c, $00 c,   \ 9
  $00 c, $00 c, $00 c, $00 c, $00 c, $18 c, $18 c, $00 c, $00 c, $00 c, $18 c, $18 c, $00 c, $00 c, $00 c, $00 c,   \ :
  $00 c, $00 c, $00 c, $00 c, $00 c, $18 c, $18 c, $00 c, $00 c, $00 c, $18 c, $18 c, $10 c, $10 c, $00 c, $00 c,   \ ;
  $00 c, $00 c, $00 c, $00 c, $00 c, $02 c, $1e c, $70 c, $70 c, $1e c, $02 c, $00 c, $00 c, $00 c, $00 c, $00 c,   \ <
  $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $fe c, $00 c, $00 c, $fe c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c,   \ =
  $00 c, $00 c, $00 c, $00 c, $00 c, $c0 c, $78 c, $0e c, $0e c, $78 c, $c0 c, $00 c, $00 c, $00 c, $00 c, $00 c,   \ >
  $00 c, $00 c, $00 c, $3c c, $66 c, $06 c, $0c c, $18 c, $10 c, $00 c, $18 c, $18 c, $00 c, $00 c, $00 c, $00 c,   \ ?
  $00 c, $00 c, $00 c, $3c c, $62 c, $42 c, $df c, $93 c, $93 c, $93 c, $df c, $40 c, $60 c, $1e c, $00 c, $00 c,   \ @
  $00 c, $00 c, $00 c, $18 c, $18 c, $3c c, $2c c, $24 c, $66 c, $7e c, $42 c, $c3 c, $00 c, $00 c, $00 c, $00 c,   \ A
  $00 c, $00 c, $00 c, $7c c, $46 c, $46 c, $46 c, $7c c, $46 c, $42 c, $46 c, $7c c, $00 c, $00 c, $00 c, $00 c,   \ B
  $00 c, $00 c, $00 c, $1c c, $32 c, $60 c, $40 c, $40 c, $40 c, $60 c, $32 c, $1c c, $00 c, $00 c, $00 c, $00 c,   \ C
  $00 c, $00 c, $00 c, $78 c, $4c c, $46 c, $46 c, $42 c, $46 c, $46 c, $4c c, $78 c, $00 c, $00 c, $00 c, $00 c,   \ D
  $00 c, $00 c, $00 c, $7e c, $60 c, $60 c, $60 c, $7e c, $60 c, $60 c, $60 c, $7e c, $00 c, $00 c, $00 c, $00 c,   \ E
  $00 c, $00 c, $00 c, $7e c, $60 c, $60 c, $60 c, $7e c, $60 c, $60 c, $60 c, $60 c, $00 c, $00 c, $00 c, $00 c,   \ F
  $00 c, $00 c, $00 c, $3c c, $62 c, $40 c, $40 c, $4e c, $42 c, $42 c, $66 c, $3e c, $00 c, $00 c, $00 c, $00 c,   \ G
  $00 c, $00 c, $00 c, $42 c, $42 c, $42 c, $42 c, $7e c, $42 c, $42 c, $42 c, $42 c, $00 c, $00 c, $00 c, $00 c,   \ H
  $00 c, $00 c, $00 c, $7e c, $18 c, $18 c, $18 c, $18 c, $18 c, $18 c, $18 c, $7e c, $00 c, $00 c, $00 c, $00 c,   \ I
  $00 c, $00 c, $00 c, $3c c, $04 c, $04 c, $04 c, $04 c, $04 c, $04 c, $4c c, $78 c, $00 c, $00 c, $00 c, $00 c,   \ J
  $00 c, $00 c, $00 c, $42 c, $44 c, $48 c, $70 c, $78 c, $48 c, $4c c, $46 c, $43 c, $00 c, $00 c, $00 c, $00 c,   \ K
  $00 c, $00 c, $00 c, $60 c, $60 c, $60 c, $60 c, $60 c, $60 c, $60 c, $60 c, $7e c, $00 c, $00 c, $00 c, $00 c,   \ L
  $00 c, $00 c, $00 c, $e6 c, $e6 c, $e6 c, $fa c, $da c, $da c, $c2 c, $c2 c, $c2 c, $00 c, $00 c, $00 c, $00 c,   \ M
  $00 c, $00 c, $00 c, $62 c, $62 c, $72 c, $52 c, $5a c, $4a c, $4e c, $46 c, $46 c, $00 c, $00 c, $00 c, $00 c,   \ N
  $00 c, $00 c, $00 c, $3c c, $66 c, $46 c, $42 c, $42 c, $42 c, $46 c, $66 c, $3c c, $00 c, $00 c, $00 c, $00 c,   \ O
  $00 c, $00 c, $00 c, $7c c, $66 c, $62 c, $62 c, $66 c, $7c c, $60 c, $60 c, $60 c, $00 c, $00 c, $00 c, $00 c,   \ P
  $00 c, $00 c, $00 c, $3c c, $66 c, $46 c, $42 c, $42 c, $42 c, $46 c, $66 c, $3c c, $0c c, $04 c, $00 c, $00 c,   \ Q
  $00 c, $00 c, $00 c, $7c c, $46 c, $46 c, $46 c, $7c c, $4c c, $46 c, $42 c, $43 c, $00 c, $00 c, $00 c, $00 c,   \ R
  $00 c, $00 c, $00 c, $3c c, $66 c, $40 c, $60 c, $3c c, $06 c, $02 c, $46 c, $3c c, $00 c, $00 c, $00 c, $00 c,   \ S
  $00 c, $00 c, $00 c, $ff c, $18 c, $18 c, $18 c, $18 c, $18 c, $18 c, $18 c, $18 c, $00 c, $00 c, $00 c, $00 c,   \ T
  $00 c, $00 c, $00 c, $46 c, $46 c, $46 c, $46 c, $46 c, $46 c, $46 c, $66 c, $3c c, $00 c, $00 c, $00 c, $00 c,   \ U
  $00 c, $00 c, $00 c, $c2 c, $42 c, $66 c, $66 c, $24 c, $2c c, $3c c, $18 c, $18 c, $00 c, $00 c, $00 c, $00 c,   \ V
  $00 c, $00 c, $00 c, $83 c, $c3 c, $c3 c, $da c, $5a c, $7a c, $6e c, $66 c, $66 c, $00 c, $00 c, $00 c, $00 c,   \ W
  $00 c, $00 c, $00 c, $42 c, $66 c, $3c c, $18 c, $18 c, $3c c, $24 c, $66 c, $c3 c, $00 c, $00 c, $00 c, $00 c,   \ X
  $00 c, $00 c, $00 c, $c2 c, $66 c, $24 c, $3c c, $18 c, $18 c, $18 c, $18 c, $18 c, $00 c, $00 c, $00 c, $00 c,   \ Y
  $00 c, $00 c, $00 c, $7e c, $06 c, $04 c, $0c c, $18 c, $10 c, $30 c, $60 c, $7f c, $00 c, $00 c, $00 c, $00 c,   \ Z
  $00 c, $1c c, $10 c, $10 c, $10 c, $10 c, $10 c, $10 c, $10 c, $10 c, $10 c, $10 c, $1c c, $00 c, $00 c, $00 c,   \ [
  $00 c, $00 c, $00 c, $40 c, $60 c, $20 c, $30 c, $10 c, $10 c, $18 c, $08 c, $0c c, $04 c, $06 c, $00 c, $00 c,   \ backslash
  $00 c, $38 c, $08 c, $08 c, $08 c, $08 c, $08 c, $08 c, $08 c, $08 c, $08 c, $08 c, $38 c, $00 c, $00 c, $00 c,   \ ]
  $00 c, $00 c, $00 c, $18 c, $3c c, $64 c, $42 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c,   \ ^
  $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $ff c, $00 c,   \ _
  $00 c, $00 c, $30 c, $18 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c,   \ `
  $00 c, $00 c, $00 c, $00 c, $00 c, $3c c, $46 c, $06 c, $3e c, $66 c, $46 c, $7e c, $00 c, $00 c, $00 c, $00 c,   \ a
  $00 c, $60 c, $60 c, $60 c, $60 c, $7c c, $66 c, $62 c, $62 c, $62 c, $66 c, $7c c, $00 c, $00 c, $00 c, $00 c,   \ b
  $00 c, $00 c, $00 c, $00 c, $00 c, $1c c, $32 c, $60 c, $60 c, $60 c, $32 c, $1c c, $00 c, $00 c, $00 c, $00 c,   \ c
  $00 c, $06 c, $06 c, $06 c, $06 c, $3e c, $66 c, $46 c, $46 c, $46 c, $66 c, $3e c, $00 c, $00 c, $00 c, $00 c,   \ d
  $00 c, $00 c, $00 c, $00 c, $00 c, $3c c, $66 c, $42 c, $7e c, $40 c, $62 c, $3c c, $00 c, $00 c, $00 c, $00 c,   \ e
  $00 c, $0e c, $18 c, $10 c, $10 c, $7e c, $10 c, $10 c, $10 c, $10 c, $10 c, $10 c, $00 c, $00 c, $00 c, $00 c,   \ f
  $00 c, $00 c, $00 c, $00 c, $00 c, $3e c, $66 c, $46 c, $46 c, $46 c, $66 c, $3e c, $06 c, $24 c, $38 c, $00 c,   \ g
  $00 c, $60 c, $60 c, $60 c, $60 c, $7c c, $66 c, $66 c, $66 c, $66 c, $66 c, $66 c, $00 c, $00 c, $00 c, $00 c,   \ h
  $00 c, $18 c, $00 c, $00 c, $00 c, $78 c, $18 c, $18 c, $18 c, $18 c, $18 c, $7e c, $00 c, $00 c, $00 c, $00 c,   \ i
  $00 c, $08 c, $00 c, $00 c, $00 c, $38 c, $08 c, $08 c, $08 c, $08 c, $08 c, $08 c, $08 c, $18 c, $70 c, $00 c,   \ j
  $00 c, $60 c, $60 c, $60 c, $60 c, $66 c, $6c c, $78 c, $78 c, $6c c, $66 c, $62 c, $00 c, $00 c, $00 c, $00 c,   \ k
  $00 c, $70 c, $10 c, $10 c, $10 c, $10 c, $10 c, $10 c, $10 c, $10 c, $18 c, $0e c, $00 c, $00 c, $00 c, $00 c,   \ l
  $00 c, $00 c, $00 c, $00 c, $00 c, $7e c, $5a c, $5a c, $5a c, $5a c, $5a c, $5a c, $00 c, $00 c, $00 c, $00 c,   \ m
  $00 c, $00 c, $00 c, $00 c, $00 c, $7c c, $66 c, $66 c, $66 c, $66 c, $66 c, $66 c, $00 c, $00 c, $00 c, $00 c,   \ n
  $00 c, $00 c, $00 c, $00 c, $00 c, $3c c, $66 c, $46 c, $42 c, $46 c, $66 c, $3c c, $00 c, $00 c, $00 c, $00 c,   \ o
  $00 c, $00 c, $00 c, $00 c, $00 c, $7c c, $66 c, $62 c, $62 c, $62 c, $66 c, $7c c, $60 c, $60 c, $60 c, $00 c,   \ p
  $00 c, $00 c, $00 c, $00 c, $00 c, $3e c, $66 c, $46 c, $46 c, $46 c, $66 c, $3e c, $06 c, $06 c, $06 c, $00 c,   \ q
  $00 c, $00 c, $00 c, $00 c, $00 c, $3e c, $30 c, $30 c, $30 c, $30 c, $30 c, $30 c, $00 c, $00 c, $00 c, $00 c,   \ r
  $00 c, $00 c, $00 c, $00 c, $00 c, $3c c, $64 c, $60 c, $3c c, $06 c, $46 c, $3c c, $00 c, $00 c, $00 c, $00 c,   \ s
  $00 c, $00 c, $00 c, $10 c, $10 c, $7e c, $10 c, $10 c, $10 c, $10 c, $10 c, $1e c, $00 c, $00 c, $00 c, $00 c,   \ t
  $00 c, $00 c, $00 c, $00 c, $00 c, $66 c, $66 c, $66 c, $66 c, $66 c, $66 c, $3e c, $00 c, $00 c, $00 c, $00 c,   \ u
  $00 c, $00 c, $00 c, $00 c, $00 c, $42 c, $66 c, $64 c, $24 c, $3c c, $38 c, $18 c, $00 c, $00 c, $00 c, $00 c,   \ v
  $00 c, $00 c, $00 c, $00 c, $00 c, $83 c, $c3 c, $da c, $5a c, $7e c, $6e c, $64 c, $00 c, $00 c, $00 c, $00 c,   \ w
  $00 c, $00 c, $00 c, $00 c, $00 c, $66 c, $24 c, $38 c, $18 c, $3c c, $64 c, $46 c, $00 c, $00 c, $00 c, $00 c,   \ x
  $00 c, $00 c, $00 c, $00 c, $00 c, $42 c, $66 c, $66 c, $24 c, $3c c, $18 c, $18 c, $18 c, $10 c, $70 c, $00 c,   \ y
  $00 c, $00 c, $00 c, $00 c, $00 c, $7e c, $04 c, $0c c, $18 c, $30 c, $20 c, $7e c, $00 c, $00 c, $00 c, $00 c,   \ z
  $00 c, $0e c, $18 c, $18 c, $18 c, $18 c, $70 c, $18 c, $18 c, $18 c, $18 c, $18 c, $0e c, $00 c, $00 c, $00 c,   \ {
  $00 c, $18 c, $18 c, $18 c, $18 c, $18 c, $18 c, $18 c, $18 c, $18 c, $18 c, $18 c, $18 c, $18 c, $00 c, $00 c,   \ vbar
  $00 c, $70 c, $10 c, $18 c, $18 c, $18 c, $0e c, $18 c, $18 c, $18 c, $18 c, $10 c, $70 c, $00 c, $00 c, $00 c,   \ }
  $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $72 c, $0e c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c,   \ ~

( ---- The text grid: 1000 lines of up to 300 columns ---- )
1000 constant con-lines
300 constant max-cols
create con-buf max-cols con-lines * allot
create con-attr max-cols con-lines * 4 * allot   ( the colors of each cell, see sgr below )
0 value cur-sgr   ( the attributes new characters get: 9 bits of foreground, 9 of background, flags )
0 value con-cols   0 value con-rows   0 value con-cw   0 value con-ch
0 value cx   0 value cy
0 value dirty
0 value menu-on     ( a desktop: menu bar, mouse, TrueType font )
0 value bar-open    ( the menu bar shows when the button at the top right is clicked )
$d2d2d2 value fg-col   $000000 value bg-col
: rgb-split ( $rrggbb -- r g b ) dup 16 rshift 255 and swap dup 8 rshift 255 and swap 255 and ;
defer save-ini      ( keeps the settings of a desktop in lugforth.ini )
: no-save ( -- ) ;   ' no-save is save-ini
defer pick-open ( n -- )   ( the color picker, further down )
0 value top-px      ( height of the menu bar )
0 value margin      ( blank pixels between the text and the edge of the window )
0 value back        ( lines scrolled back )
0 value sel-on

: grid ( x y -- a ) max-cols * + con-buf + ;
: attr-addr ( x y -- a ) max-cols * + 4 * con-attr + ;
: con-clear ( -- )
  con-buf max-cols con-lines * 32 fill  con-attr max-cols con-lines * 4 * erase
  0 to cx 0 to cy  0 to back  0 to sel-on  -1 to dirty ;
: con-scroll ( -- )
  con-buf max-cols + con-buf max-cols con-lines 1- * cmove
  0 con-lines 1- grid max-cols 32 fill  0 to sel-on
  con-attr max-cols 4 * + con-attr max-cols 4 * con-lines 1- * cmove
  con-attr max-cols 4 * con-lines 1- * + max-cols 4 * erase ;
: con-nl ( -- )
  0 to cx  1 +to cy  -1 to dirty
  back if 1 +to back then
  cy con-lines >= if con-scroll con-lines 1- to cy then ;
: put-char ( c -- )
  cx con-cols >= if con-nl then
  cx cy grid c!  cur-sgr cx cy attr-addr l!  1 +to cx  -1 to dirty ;

: kbd-shown ( -- f ) window-handle SDL_IsScreenKeyboardShown $ffffffff and 0<> ;
: vis ( -- n ) kbd-shown if con-rows 2/ 3 max else con-rows then ;
: win-top ( -- n ) cy vis - 1+ 0 max ;
: view-top ( -- n ) win-top back - 0 max ;
: scroll-by ( n -- ) back + 0 max win-top min to back  -1 to dirty ;

( ANSI: page, at-xy, clear-to-eol, cursor moves, and colors: the sequences of m. Others are ignored )
0 value esc-state   0 value p1   0 value p2
0 value acc   0 value np   0 value pdig   0 value si   0 value sv
create prm 8 cells allot
: prm-reset ( -- )   0 to acc  0 to np  0 to pdig  0 to p1  0 to p2  prm 8 cells erase ;
: prm-push ( -- )   np 8 < if acc prm np cells + !  1 +to np then  0 to acc ;

( Colors as in xterm: 0 to 15 the ANSI ones, 16 to 231 a cube, 232 to 255 grays )
: ansi16 ( i -- $rrggbb )
  { i }
  i 7 = if $e5e5e5 exit then   i 8 = if $7f7f7f exit then
  i 12 = if $5c5cff exit then   i 4 = if $0000ee exit then
  i 8 < if 205 else 255 then { v }
  i 1 and if v 16 lshift else 0 then
  i 2 and if v 8 lshift or then
  i 4 and if v or then ;
: cube-comp ( c -- v ) dup 0= if exit then 40 * 55 + ;
: xterm>rgb ( i -- $rrggbb )
  dup 16 < if ansi16 exit then
  dup 232 >= if 232 - 10 * 8 + dup 16 lshift over 8 lshift or or exit then
  16 - { n }
  n 36 / cube-comp 16 lshift  n 6 / 6 mod cube-comp 8 lshift or  n 6 mod cube-comp or ;
: mix1 ( a b pct -- v ) { a b pc } a 100 pc - * b pc * + 100 / ;
: mix ( c1 c2 pct -- c )
  { c1 c2 pc }
  c1 16 rshift 255 and  c2 16 rshift 255 and  pc mix1 16 lshift
  c1 8 rshift 255 and  c2 8 rshift 255 and  pc mix1 8 lshift or
  c1 255 and  c2 255 and  pc mix1 or ;

: sgr-fg ( n -- )   cur-sgr $1ff invert and  swap 1+ or to cur-sgr ;      ( n is 0 to 255, or -1 )
: sgr-bg ( n -- )   cur-sgr $3fe00 invert and  swap 1+ 9 lshift or to cur-sgr ;
: sgr-flag ( mask f -- )   if cur-sgr or else invert cur-sgr and then to cur-sgr ;
: sgr-one ( v -- )
  dup 0 = if drop 0 to cur-sgr exit then
  dup 1 = if drop $80000 -1 sgr-flag exit then
  dup 2 = if drop $100000 -1 sgr-flag exit then
  dup 4 = if drop $200000 -1 sgr-flag exit then
  dup 7 = if drop $40000 -1 sgr-flag exit then
  dup 22 = if drop $180000 0 sgr-flag exit then
  dup 24 = if drop $200000 0 sgr-flag exit then
  dup 27 = if drop $40000 0 sgr-flag exit then
  dup 39 = if drop -1 sgr-fg exit then
  dup 49 = if drop -1 sgr-bg exit then
  dup 30 >= over 37 <= and if 30 - sgr-fg exit then
  dup 40 >= over 47 <= and if 40 - sgr-bg exit then
  dup 90 >= over 97 <= and if 82 - sgr-fg exit then
  dup 100 >= over 107 <= and if 92 - sgr-bg exit then
  drop ;
: sgr-apply ( -- )
  np 0= if 0 to cur-sgr exit then
  0 to si
  begin si np < while
    prm si cells + @ to sv
    sv 38 = sv 48 = or if
      prm si 1+ cells + @ 5 = if
        prm si 2 + cells + @ 255 and  sv 38 = if sgr-fg else sgr-bg then
        2 +to si
      else prm si 1+ cells + @ 2 = if 4 +to si then then
    else sv sgr-one then
    1 +to si
  repeat ;

: csi-done ( c -- )
  dup [char] H = if
    drop  p2 1- 0 max con-cols 1- min to cx
    win-top p1 1- 0 max + con-lines 1- min to cy  -1 to dirty exit then
  dup [char] J = if
    drop p1 2 = if con-clear then exit then
  dup [char] C = if
    drop p1 1 max cx + con-cols 1- min to cx  -1 to dirty exit then
  dup [char] D = if
    drop cx p1 1 max - 0 max to cx  -1 to dirty exit then
  dup [char] K = if
    drop cx cy grid con-cols cx - 0 max 32 fill
    cx cy attr-addr con-cols cx - 0 max 4 * erase  -1 to dirty exit then
  dup [char] m = if drop sgr-apply -1 to dirty exit then
  drop ;
: csi-char ( c -- )
  dup [char] 0 >= over [char] 9 <= and if
    [char] 0 - acc 10 * + to acc  -1 to pdig exit then
  dup [char] ; = if drop prm-push -1 to pdig exit then
  dup 64 >= over 126 <= and if
    0 to esc-state
    pdig np 0> or if prm-push then
    prm @ to p1   prm cell+ @ to p2
    csi-done exit then
  drop ;
: con-putc ( c -- )
  esc-state 2 = if csi-char exit then
  esc-state 3 = if 7 = if 0 to esc-state then exit then   ( title, ends with bell )
  esc-state 1 = if
    dup [char] [ = if drop 2 to esc-state prm-reset exit then
    [char] ] = if 3 to esc-state else 0 to esc-state then
    exit then
  dup 27 = if drop 1 to esc-state exit then
  dup 10 = if drop con-nl exit then
  dup 13 = if drop 0 to cx exit then
  dup 8 = if drop cx 1- 0 max to cx -1 to dirty exit then
  dup 9 = if drop begin 32 put-char cx 7 and 0= until exit then
  dup 128 >= if 192 >= if 63 put-char then exit then
  dup 32 < if drop exit then
  put-char ;
: con-type ( a n -- ) 0 ?do dup i + c@ con-putc loop drop ;

( ---- The font: a bitmap, or TrueType when a desktop finds one ---- )
( The glyphs as a texture: white where the bit is set )
760 constant atlas-w
create atlas-px atlas-w 16 * 4 * allot
0 value atlas

: atlas-row! ( g y bits -- )
  { g y bits }
  8 0 do
    bits 7 i - rshift 1 and if $ffffffff else 0 then
    y atlas-w * g 8 * + i + 4 * atlas-px + l!
  loop
;
: build-atlas ( -- )
  95 0 do  16 0 do  j i  font-data j 16 * + i + c@  atlas-row!  loop  loop ;
: make-atlas ( -- )
  build-atlas
  renderer SDL_PIXELFORMAT_ARGB8888 0 atlas-w 16 SDL_CreateTexture to atlas
  atlas 0 atlas-px atlas-w 4 * SDL_UpdateTexture drop
  atlas SDL_BLENDMODE_BLEND SDL_SetTextureBlendMode drop
;

create gsrc 16 allot
create gdst 16 allot
: bm-cell ( size -- cw ch ) dup 4 * swap 8 * ;
: bm-build ( -- ) make-atlas ;
: bm-glyph ( g x y -- )
  { g x y }
  g 8 * 0 8 16 gsrc rect-at!
  x y con-cw con-ch gdst rect-at!
  renderer atlas gsrc gdst SDL_RenderCopy drop ;
: bm-tint ( r g b -- ) { r g b } atlas r g b SDL_SetTextureColorMod drop ;

defer font-cell ( size -- cw ch )   ' bm-cell is font-cell
defer atlas-build ( -- )            ' bm-build is atlas-build
defer glyph-at ( g x y -- )         ' bm-glyph is glyph-at
defer glyph-tint ( r g b -- )       ' bm-tint is glyph-tint
2 value hmin   24 value hmax   1 value hstep   180 value hdiv   0 value hrel
( the size is counted in half scales of the bitmap, in points with TrueType, where a step is hrel percent )

0 value con-gen   ( counts the windows, a TrueType atlas belongs to one )

: draw-text ( a n x y -- )
  { a n x y }
  n 0 ?do
    a i + c@ dup 32 > over 127 < and if
      32 - x i con-cw * + y glyph-at
    else drop then
  loop ;

( ---- The window ---- )
0 value con-renderer
0 value user-size   0 value ow   0 value oh   0 value hsize   0 value cols
0 value last-built
create osize 8 allot
: con-layout ( -- )
  renderer osize osize 4 + SDL_GetRendererOutputSize drop
  osize sl@ to ow   osize 4 + sl@ to oh
  user-size dup 0= if drop ow oh min hdiv / then hmin max hmax min to hsize
  hsize font-cell to con-ch to con-cw
  menu-on bar-open and if con-ch 3 * 2/ else 0 then to top-px
  hsize last-built <> if atlas-build hsize to last-built then
  ow margin 2* - con-cw / 1 max max-cols min to cols
  oh top-px - margin 2* - con-ch / 1 max to con-rows
  cols to con-cols
  -1 to dirty ;
: relayout ( -- )   renderer con-renderer = if con-layout then ;
: font-size ( n -- )   ( 1 small, 2, 3, 4... ; 0 is automatic )
  2* to user-size relayout ;
: font-step ( d -- )   ( one step bigger or smaller, as a pinch or the wheel does )
  hrel if dup hsize hrel * 100 / 1 max * else hstep * then
  hsize + hmin max hmax min to user-size relayout save-ini ;
: font-auto ( -- )   0 to user-size relayout save-ini ;

( ---- Selection, with the mouse ---- )
0 value sel-a   0 value sel-b   0 value dragging
: sel-lo ( -- n ) sel-a sel-b min ;
: sel-hi ( -- n ) sel-a sel-b max ;
create selbuf 20000 allot
0 value sl
0 value sr   0 value sc1   0 value sc2   0 value lastc
0 value r1   0 value c1   0 value r2   0 value c2
: sel-put ( c -- ) sl 19990 < if selbuf sl + c! 1 +to sl else drop then ;
: sel-row ( r c1 c2 -- )   \ the cells c1 to c2 of a row, trailing spaces dropped
  to sc2 to sc1 to sr
  sc2 begin dup sc1 >= if dup sr grid c@ 32 = else 0 then while 1- repeat to lastc
  lastc 1+ sc1 ?do  i sr grid c@ dup 32 < if drop 32 then sel-put  loop ;
: sel-copy ( -- )
  sel-on 0= if exit then
  0 to sl
  sel-lo max-cols /mod to r1 to c1   sel-hi max-cols /mod to r2 to c2
  r2 1+ r1 ?do
    i  i r1 = if c1 else 0 then  i r2 = if c2 else con-cols 1- then  sel-row
    i r2 < if 10 sel-put then
  loop
  0 sel-put  selbuf SDL_SetClipboardText drop  selbuf primary-set ;
0 value dr   0 value rb   0 value bc1   0 value bc2
: draw-sel ( top v -- )
  sel-on 0= if 2drop exit then
  70 110 190 130 rgba
  0 ?do
    dup i + to dr   dr max-cols * to rb
    sel-lo rb max   sel-hi rb max-cols + 1- min
    2dup <= if
      rb - con-cols 1- min to bc2   rb - to bc1
      bc1 bc2 <= if
        bc1 con-cw * margin +  top-px margin + i con-ch * +  bc2 bc1 - 1+ con-cw *  con-ch box
      then
    else 2drop then
  loop drop ;

( ---- Input ---- )
4096 constant q-size
create text-queue q-size allot
0 value qh   0 value qt
: q-push ( c -- ) qt qh - q-size < if text-queue qt q-size 1- and + c! 1 +to qt else drop then ;
: q-pop ( -- c ) text-queue qh q-size 1- and + c@ 1 +to qh ;
: q-any? ( -- f ) qh qt <> ;
: q-str ( a n -- ) 0 ?do dup i + c@ q-push loop drop ;

: paste-char ( c -- )
  dup 10 = if drop 13 q-push exit then
  dup 13 = if drop exit then
  dup 9 = over 31 > or if q-push else drop then ;
: paste-text ( z -- )   ( a string of SDL, freed here )
  dup begin dup c@ dup while paste-char 1+ repeat 2drop
  SDL_free drop ;
: paste ( -- )   SDL_GetClipboardText dup 0= if drop exit then paste-text ;
: paste-primary ( -- )   ( middle click: the X11 selection, made in this window or in another program, not the clipboard )
  primary-get-fn 0= if paste exit then   ( an SDL2 without it: the clipboard )
  primary-get dup 0= if drop exit then
  paste-text ;

0 value kmod
: ctrl? ( -- f ) kmod $c0 and 0<> ;
: shift? ( -- f ) kmod 3 and 0<> ;

( ---- Settings, kept in lugforth.ini ---- )
z" mkdir" 2 sysfunc c-mkdir
create fpath 300 allot      ( the TrueType font file, empty for the bitmap font )
create inipath 400 allot   0 value inin
create ibuf 1200 allot   0 value ibn
create obuf 1200 allot   0 value obn
0 value ini-size
0 value ka   0 value kn   0 value va   0 value vn   0 value lstart
: ini+ ( a n -- ) dup >r inipath inin + swap cmove r> +to inin ;
: ini-dir ( -- )   \ LUGFORTH_HOME, or ~/.local/share/lugforth
  0 to inin
  z" LUGFORTH_HOME" c-getenv ?dup if z>s ini+ exit then
  z" HOME" c-getenv ?dup if z>s ini+ s" /.local/share/lugforth" ini+ exit then
  s" ." ini+ ;
: mkdirs ( -- )   \ the directory of inipath, and its parents
  0 inipath inin + c!
  inin 1 ?do
    inipath i + c@ [char] / = if
      0 inipath i + c!   inipath 493 c-mkdir drop   [char] / inipath i + c!
    then
  loop
  inipath 493 c-mkdir drop ;
: ini-file ( -- a n ) ini-dir s" /lugforth.ini" ini+ inipath inin ;
: ini-read ( -- )
  0 to ibn
  ini-file r/o open-file if drop exit then
  >r ibuf 1190 r@ read-file if drop 0 then to ibn
  r> close-file drop ;
: key= ( a n -- f ) ka kn 2swap compare 0= ;
: num ( a n base -- u )
  base @ >r base !  0 0 2swap >number 2drop drop  r> base ! ;
: hexnum ( a n -- u ) 16 num ;
: decnum ( a n -- u ) 10 num ;

( Full screen: the key is a word of lugforth.ini, like Alt+Enter or F11 )
1 value fs-mod   $0d value fs-sym   0 value fs-on   0 value ts   0 value kl
create fsname 32 allot
create kbuf 32 allot
create lbuf 60 allot
: lower ( c -- c ) dup 65 >= over 90 <= and if 32 + then ;
: key-token ( a n -- )
  2dup s" alt" compare 0= if 2drop fs-mod 1 or to fs-mod exit then
  2dup s" ctrl" compare 0= if 2drop fs-mod 2 or to fs-mod exit then
  2dup s" shift" compare 0= if 2drop fs-mod 4 or to fs-mod exit then
  2dup s" enter" compare 0= if 2drop $0d to fs-sym exit then
  2dup s" return" compare 0= if 2drop $0d to fs-sym exit then
  2dup s" esc" compare 0= if 2drop 27 to fs-sym exit then
  2dup s" space" compare 0= if 2drop 32 to fs-sym exit then
  2dup s" tab" compare 0= if 2drop 9 to fs-sym exit then
  dup 1 = if drop c@ to fs-sym exit then
  over c@ 102 = over 1 > and if   ( f1 to f12 )
    over 1+ over 1- decnum dup 1 13 within if $40000039 + to fs-sym 2drop exit then drop
  then
  2drop ;
: parse-key ( a n -- )
  0 to fs-mod   0 to fs-sym
  31 min dup to kl   kbuf swap cmove
  kl 0 ?do kbuf i + dup c@ lower swap c! loop
  0 to ts
  kl 0 ?do kbuf i + c@ [char] + = if kbuf ts + i ts - key-token  i 1+ to ts then loop
  kbuf ts + kl ts - key-token ;
: fs-set ( a n -- )
  31 min { a n }
  a fsname n cmove   0 fsname n + c!
  fsname n parse-key ;
: mods ( -- n )   kmod $300 and 0<> 1 and   kmod $c0 and 0<> 2 and or   kmod 3 and 0<> 4 and or ;
: fs-key? ( sym -- f )   fs-sym = mods fs-mod = and ;
: fs-toggle ( -- )   fs-on 0= to fs-on   fs-on fullscreen   relayout ;
: fs-label ( -- a n )
  s" Full screen   " lbuf swap cmove   fsname z>s 40 min lbuf 14 + swap cmove
  lbuf  14 fsname z>s nip 40 min + ;
s" Alt+Enter" fs-set
: ini-kv ( -- )
  s" size" key= if va vn decnum to ini-size exit then
  s" margin" key= if va vn decnum 64 min to margin exit then
  s" fg" key= if va vn hexnum to fg-col exit then
  s" bg" key= if va vn hexnum to bg-col exit then
  s" fullscreen" key= if va vn fs-set exit then
  s" font" key= if
    va fpath vn 299 min dup >r cmove   0 fpath r> + c!  exit then ;
: eq-pos ( a n -- k )   0 ?do dup i + c@ [char] = = if drop i unloop exit then loop drop -1 ;
: ini-line ( a n -- )
  2dup eq-pos dup 0< if drop 2drop exit then
  to kn   over to ka   kn - 1- to vn   kn + 1+ to va
  ini-kv ;
: ini-parse ( -- )
  0 to lstart
  ibn 0 ?do
    ibuf i + c@ 10 = if ibuf lstart + i lstart - ini-line  i 1+ to lstart then
  loop
  ibn lstart > if ibuf lstart + ibn lstart - ini-line then ;
: ini-load ( -- )   ini-read ini-parse ;
: ob+ ( a n -- ) dup >r obuf obn + swap cmove r> +to obn ;
: ob-nl ( -- ) 10 obuf obn + c!  1 +to obn ;
: hex6 ( u -- a n )   base @ >r 16 base !  <# # # # # # # #> r> base ! ;
: env-font? ( -- f )   \ the font comes from LUGFORTH_FONT (the AppImage), whose path changes at each run
  z" LUGFORTH_FONT" c-getenv ?dup if z>s fpath z>s compare 0= else 0 then ;
: ini-save ( -- )
  menu-on 0= if exit then
  0 to obn
  s" # Lugforth window. Colors are hexadecimal RRGGBB, font is a TrueType file." ob+ ob-nl
  hrel if user-size to ini-size then
  s" size=" ob+  ini-size str ob+ ob-nl
  s" fg=" ob+  fg-col hex6 ob+ ob-nl
  s" bg=" ob+  bg-col hex6 ob+ ob-nl
  s" margin=" ob+  margin str ob+ ob-nl
  s" fullscreen=" ob+  fsname z>s ob+ ob-nl
  fpath c@ env-font? 0= and if s" font=" ob+  fpath z>s ob+ ob-nl then
  ini-dir mkdirs ini-file w/o create-file if drop exit then
  >r obuf obn r@ write-file drop r> close-file drop ;
' ini-save is save-ini

( Colors )
create theme-fg $d2d2d2 , $202020 , $33ff66 , $ffb000 , $93a1a1 , $e0e8ff ,
create theme-bg $000000 , $f4f4f0 , $001a08 , $1a1000 , $002b36 , $10205a ,
: theme-set ( i -- )
  dup cells theme-fg + @ to fg-col   cells theme-bg + @ to bg-col   -1 to dirty save-ini ;

( System fonts to choose from: their names are kept here, TrueType loads them )
create fcand 20 cells allot
: c+ ( a z -- a' ) over ! cell+ ;
: cands ( -- )
  fcand
  z" /usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf" c+
  z" /usr/share/fonts/dejavu/DejaVuSansMono.ttf" c+
  z" /usr/share/fonts/TTF/DejaVuSansMono.ttf" c+
  z" /usr/share/fonts/truetype/liberation/LiberationMono-Regular.ttf" c+
  z" /usr/share/fonts/liberation-mono/LiberationMono-Regular.ttf" c+
  z" /usr/share/fonts/liberation/LiberationMono-Regular.ttf" c+
  z" /usr/share/fonts/truetype/ubuntu/UbuntuMono-R.ttf" c+
  z" /usr/share/fonts/truetype/noto/NotoSansMono-Regular.ttf" c+
  z" /usr/share/fonts/noto/NotoSansMono-Regular.ttf" c+
  z" /usr/share/fonts/truetype/freefont/FreeMono.ttf" c+
  z" /usr/share/fonts/truetype/hack/Hack-Regular.ttf" c+
  z" /usr/share/fonts/TTF/Hack-Regular.ttf" c+
  z" /usr/share/fonts/truetype/firacode/FiraCode-Regular.ttf" c+
  z" /usr/share/fonts/truetype/jetbrains-mono/JetBrainsMono-Regular.ttf" c+
  z" /usr/share/fonts/truetype/source-code-pro/SourceCodePro-Regular.ttf" c+
  z" /usr/share/fonts/truetype/cascadia-code/CascadiaMono-Regular.ttf" c+
  0 swap ! ;
cands
( Fixed width fonts found on the system, by the Font menu )
120 constant fl-max   24000 constant fpool-size
create flist fl-max cells allot
create fpool fpool-size allot
create spath 520 allot
0 value fcount   0 value fused   0 value fscanned   0 value splen   0 value sdepth

( ---- The menu bar of a desktop ---- )
-1 value menu-open   -1 value hover   0 value help-open   0 value menu-clicked
0 value drop-off    ( first entry shown, the wheel scrolls a long menu )
5 constant menus
: bar-h ( -- n ) top-px ;
: m-name ( i -- a n )
  dup 0= if drop s" Examples" exit then
  dup 1 = if drop s" View" exit then
  dup 2 = if drop s" Colors" exit then
  3 = if s" Font" exit then
  s" Help" ;
0 value ex-have   \ a .fs file in the examples folder: else the Examples menu is not shown
: m-w ( i -- px )
  dup 0= ex-have 0= and if drop 0 exit then
  m-name nip 2 + con-cw * ;
: m-x ( i -- px ) con-cw swap 0 ?do i m-w + loop ;
: m-at ( x -- i )   \ the menu under x, or -1
  -1 swap
  menus 0 ?do
    dup i m-x >= over i m-x i m-w + < and if nip i swap then
  loop drop ;

128 constant ent-max
create ent-a ent-max cells allot
create ent-n ent-max cells allot
create ent-id ent-max cells allot
8192 constant pool-size
create ent-pool pool-size allot
0 value ent-count   0 value ent-used
0 value xa   0 value xn   0 value xid
: ent-add ( a n id -- )
  to xid to xn to xa
  ent-count ent-max >= xn 60 > or ent-used xn 1+ + pool-size > or if exit then
  ent-pool ent-used + >r
  xa r@ xn cmove   0 r@ xn + c!
  r@ ent-count cells ent-a + !
  xn ent-count cells ent-n + !
  xid ent-count cells ent-id + !
  rdrop xn 1+ +to ent-used  1 +to ent-count ;
: ent-str ( k -- a n ) dup cells ent-a + @ swap cells ent-n + @ ;
: ent< ( i j -- f )   \ name i comes before name j
  swap ent-str rot ent-str compare 0< ;
: cell-swap ( arr i j -- )
  { arr i j }
  arr i cells + @  arr j cells + @  arr i cells + !  arr j cells + ! ;
: ent-xchg ( i j -- )
  2dup ent-a -rot cell-swap   2dup ent-n -rot cell-swap   ent-id -rot cell-swap ;
: ent-sort ( -- )
  ent-count 2 < if exit then   \ ?do with start > limit runs away
  ent-count 1 ?do
    i begin dup 0> if dup dup 1- ent< else 0 then while
      dup dup 1- ent-xchg  1-
    repeat drop
  loop ;

: ex-dir ( -- a n ) z" LUGFORTH_EXAMPLES" c-getenv ?dup if z>s exit then s" ." ;
: ex-ent ( ent -- )
  .d_name z>s  dup 3 <= if 2drop exit then
  2dup + 3 - 3 s" .fs" compare if 2drop exit then
  -1 ent-add ;
: ex-list ( -- )
  ex-dir open-dir if drop exit then
  begin dup readdir dup while ex-ent repeat drop closedir drop ;
: ex-check ( -- )   \ looked at again each time the bar opens: the folder may have changed
  0 to ent-count   0 to ent-used   ex-list   ent-count 0<> to ex-have   0 to ent-count   0 to ent-used ;

: no-fonts ( -- ) s" (no system font found)" 0 ent-add ;
defer font-fill ( -- )   ' no-fonts is font-fill
defer font-pick ( k -- )   ' drop is font-pick
: menu-fill ( i -- )
  0 to ent-count   0 to ent-used
  dup 0= if
    drop ex-list ent-sort
    ent-count 0= if s" (no example here)" 0 ent-add then
    exit then
  dup 1 = if
    drop
    s" Bigger        Ctrl +" 1 ent-add
    s" Smaller       Ctrl -" 2 ent-add
    s" Default size  Ctrl 0" 3 ent-add
    s" Clear screen  Ctrl L" 6 ent-add
    s" Hide menu     F10" 7 ent-add
    fs-label 8 ent-add
    s" More margin" 9 ent-add
    s" Less margin" 10 ent-add
    exit then
  dup 2 = if
    drop
    s" Foreground    " 40 ent-add   s" Background    " 41 ent-add
    s" Dark" 20 ent-add   s" Light" 21 ent-add   s" Green" 22 ent-add
    s" Amber" 23 ent-add   s" Solarized" 24 ent-add   s" Blue" 25 ent-add
    exit then
  3 = if font-fill exit then
  s" Keys and tips" 4 ent-add
  s" List the words" 5 ent-add ;

: close-menu ( -- )  -1 to menu-open  -1 to hover  -1 to dirty ;
: margin-step ( n -- )   margin + 0 max 64 min to margin  relayout save-ini ;
: bar-toggle ( -- )   bar-open 0= to bar-open  close-menu  bar-open if ex-check then  relayout ;
: open-menu ( i -- ) dup to menu-open menu-fill  0 to drop-off  -1 to hover  -1 to dirty ;
: drop-x ( -- px ) menu-open m-x ;
: drop-w ( -- px )
  0 ent-count 0 ?do i ent-str nip max loop 2 + con-cw * ;
: drop-rows ( -- n ) ent-count con-rows 2 - min ;
: drop-scroll ( n -- )   drop-off + ent-count drop-rows - min 0 max to drop-off  -1 to dirty ;
: drop-hit ( x y -- k )   \ the entry under the pointer, or -1
  bar-h - dup 0< if 2drop -1 exit then   con-ch /
  dup drop-rows >= if 2drop -1 exit then
  swap drop-x - dup 0< if 2drop -1 exit then
  drop-w >= if drop -1 exit then
  drop-off + ;

: run-entry ( k -- )
  dup cells ent-id + @
  dup -1 = if drop ent-str s" include " q-str q-str 13 q-push exit then
  nip
  dup 1 = if drop 1 font-step exit then
  dup 2 = if drop -1 font-step exit then
  dup 3 = if drop font-auto exit then
  dup 4 = if drop -1 to help-open -1 to dirty exit then
  dup 5 = if drop s" words" q-str 13 q-push exit then
  dup 6 = if drop 12 q-push exit then
  dup 7 = if drop bar-toggle exit then
  dup 20 >= over 25 <= and if 20 - theme-set exit then
  dup 40 = if drop 1 pick-open exit then
  dup 41 = if drop 2 pick-open exit then
  dup 8 = if drop fs-toggle exit then
  dup 9 = if drop 4 margin-step exit then
  dup 10 = if drop -4 margin-step exit then
  dup 100 >= if 100 - font-pick exit then
  drop ;

: help-line ( k -- a n )
  dup 0 = if drop s" Lugforth" exit then
  dup 1 = if drop s" " exit then
  dup 2 = if drop s" Menu: the button at the top right, or F10. Right click on the button hides it, and on its place shows it again." exit then
  dup 3 = if drop s" Examples: run a file of your examples folder." exit then
  dup 4 = if drop s" View: text size (Ctrl+wheel, Ctrl + and Ctrl - too)." exit then
  dup 5 = if drop s" Colors (with a picker) and Font: kept with the size in" exit then
  dup 6 = if drop s" ~/.local/share/lugforth/lugforth.ini" exit then
  dup 7 = if drop s" Mouse: drag to select and copy, middle click pastes the selection (of this window or of another program)." exit then
  dup 8 = if drop s" Wheel, PageUp and PageDown scroll back." exit then
  dup 9 = if drop s" Tab completes words and file names, Up and Down" exit then
  dup 10 = if drop s" give back the history. Type  words  for the list" exit then
  dup 11 = if drop s" of words and  bye  to quit." exit then
  dup 12 = if drop s" Full screen: Alt+Enter, or the View menu." exit then
  dup 13 = if drop s" " exit then
  drop s" Click or press a key to close." ;
15 constant help-lines

: fbox ( x y w h r g b -- ) rgb box ;
: hb-size ( -- n ) con-ch 3 * 2/ ;
: hb-x ( -- n ) ow hb-size - ;
: hb-hit ( x y -- f ) hb-size < swap hb-x >= and ;
0 value ht
0 value hb-hidden   \ right click on the button hides it, right click on its place shows it again
: hb-toggle ( -- )
  hb-hidden 0= to hb-hidden
  hb-hidden if bar-open if bar-toggle then then
  close-menu  -1 to dirty ;
: draw-hb ( -- )
  bar-open if 60 64 67 rgb else 30 32 36 200 rgba then
  hb-x 0 hb-size hb-size box
  230 230 230 rgb
  hb-size 12 / 2 max to ht
  3 0 do
    hb-x hb-size 4 / +   hb-size i 1+ * 4 / ht 2/ -   hb-size 2/ ht box
  loop ;
: draw-bar ( -- )
  0 0 ow bar-h 32 33 36 fbox
  230 230 230 glyph-tint
  menus 0 ?do
    i m-w if
      i menu-open = if i m-x 0 i m-w bar-h 60 64 67 fbox then
      i m-name  i m-x con-cw +  bar-h con-ch - 2/  draw-text
    then
  loop ;
: draw-drop ( -- )
  menu-open 0< if exit then
  drop-x bar-h drop-w drop-rows con-ch * 45 46 50 fbox
  230 230 230 glyph-tint
  drop-rows 0 ?do
    i drop-off + hover = if drop-x bar-h i con-ch * +  drop-w con-ch  63 81 181 fbox then
    i drop-off + ent-str  drop-x con-cw +  bar-h i con-ch * +  draw-text
    i drop-off + cells ent-id + @ dup 40 = swap 41 = or if
      i drop-off + cells ent-id + @ 40 = if fg-col else bg-col then  color
      drop-x drop-w + con-cw 3 * -  bar-h i con-ch * + 3 +  con-cw 2*  con-ch 6 -  box
    then
  loop ;
0 value hw   0 value hx   0 value hy
: draw-help ( -- )
  help-open 0= if exit then
  con-cols 2 - 58 min to hw
  ow hw con-cw * - 2/ to hx
  bar-h con-ch + to hy
  hx hy hw con-cw *  help-lines 2 + con-ch *  40 42 46 fbox
  230 230 230 glyph-tint
  help-lines 0 ?do
    i help-line  hx con-cw +  hy i 1+ con-ch * +  draw-text
  loop ;

: ev-x ( -- n ) ev 20 + sl@ ;
: ev-y ( -- n ) ev 24 + sl@ ;
( ---- Color picker: a square of saturation and brightness, a strip of hues ---- )
0 value pick-on   0 value pick-drag   0 value ph   0 value ps   0 value pv
0 value qx   0 value qy   0 value qs   0 value qc   0 value qg
: hsv>rgb ( h s v -- r g b )
  { h s v }
  h 60 / { reg }
  h 60 mod 255 * 60 / { f }
  v 255 s - * 255 / { p }
  v 255 s f * 255 / - * 255 / { q }
  v 255 s 255 f - * 255 / - * 255 / { t }
  reg 0 = if v t p exit then
  reg 1 = if q v p exit then
  reg 2 = if p v t exit then
  reg 3 = if p q v exit then
  reg 4 = if t p v exit then
  v p q ;
: rgb>hsv ( r g b -- h s v )
  { r g b }
  r g max b max { mx }
  r g min b min { mn }
  mx mn - { d }
  0 { h }
  d 0<> if
    mx r = if g b - 60 * d / to h
    else mx g = if b r - 60 * d / 120 + to h
    else r g - 60 * d / 240 + to h then then
    h 0< if h 360 + to h then
  then
  h   mx 0= if 0 else d 255 * mx / then   mx ;
: rgb-pack ( r g b -- $rrggbb ) { r g b } r 16 lshift g 8 lshift or b or ;
: pick-cur ( -- $rrggbb ) pick-on 1 = if fg-col else bg-col then ;
: pick-apply ( -- )
  ph ps pv hsv>rgb rgb-pack
  pick-on 1 = if to fg-col else to bg-col then   -1 to dirty ;
: pick-start ( n -- )   ( 1 text color, 2 background )
  to pick-on   pick-cur rgb-split rgb>hsv to pv to ps to ph   -1 to dirty ;
' pick-start is pick-open
: pick-close ( -- )   0 to pick-on  0 to pick-drag  -1 to dirty  save-ini ;
( geometry, in pixels: qs side of the square, qc size of one of its 24 cells )
: pick-geom ( -- )
  con-ch 9 * 24 / 2 max to qc   qc 24 * to qs   con-ch 2/ to qg
  2 m-x  ow qs qg 2* + - min  0 max to qx   bar-h to qy ;
: pick-w ( -- n ) qs qg 2* + ;
: pick-h ( -- n ) qg 5 * con-ch 3 * + qs + ;
: sq-x ( -- n ) qx qg + ;
: sq-y ( -- n ) qy qg 2* + con-ch + ;
: hue-y ( -- n ) sq-y qs + qg + ;
: pick-inside ( x y -- f )   qy - swap qx - swap   pick-h u< swap pick-w u< and ;
: pick-sv ( x y -- )
  sq-y - 0 max qs 1- min   255 qs 1- */ 255 swap - to pv
  sq-x - 0 max qs 1- min   255 qs 1- */ to ps   pick-apply ;
: pick-hue ( x -- )   sq-x - 0 max qs 1- min   359 qs 1- */ to ph   pick-apply ;
: pick-click ( -- )
  pick-geom
  ev-x ev-y pick-inside 0= if pick-close exit then
  ev-x sq-x >= ev-x sq-x qs + < and ev-y sq-y >= and ev-y sq-y qs + < and if
    1 to pick-drag ev-x ev-y pick-sv exit then
  ev-x sq-x >= ev-x sq-x qs + < and ev-y hue-y >= and ev-y hue-y con-ch + < and if
    2 to pick-drag ev-x pick-hue then ;
: pick-move ( -- )
  pick-geom
  pick-drag 1 = if ev-x ev-y pick-sv exit then
  ev-x pick-hue ;
: draw-picker ( -- )
  pick-on 0= if exit then
  pick-geom
  qx qy pick-w pick-h 40 42 46 fbox
  230 230 230 glyph-tint
  pick-on 1 = if s" Foreground" else s" Background" then  sq-x qy qg + draw-text
  24 0 do
    24 0 do
      ph  i 255 * 23 /  255 j 255 * 23 / -  hsv>rgb rgb
      sq-x i qc * +  sq-y j qc * +  qc qc box
    loop
  loop
  48 0 do
    i 359 * 47 /  255 255 hsv>rgb rgb
    sq-x i qs * 48 / +  hue-y  qs 48 / 1+  con-ch box
  loop
  255 255 255 rgb   sq-x ps qs 1- * 255 / + 4 -   sq-y 255 pv - qs 1- * 255 / + 4 -   9 9 frame
  0 0 0 rgb   sq-x ps qs 1- * 255 / + 3 -   sq-y 255 pv - qs 1- * 255 / + 3 -   7 7 frame
  255 255 255 rgb   sq-x ph qs 1- * 359 / + 1-   hue-y 1-   3 con-ch 2 + box
  pick-cur color   sq-x  hue-y con-ch + qg +  con-cw 3 *  con-ch box
  230 230 230 glyph-tint
  [char] # lbuf c!  pick-cur hex6 lbuf 1+ swap cmove   lbuf 7
  sq-x con-cw 4 * +  hue-y con-ch + qg +  draw-text ;

( ---- Drawing ---- )
0 value blink
: attr@ ( x y -- a )   attr-addr sl@ ;
: idx-color ( field default -- $rrggbb )   over 0= if nip exit then  drop 1- xterm>rgb ;
: attr-colors ( a -- fg bg )   \ the colors of a cell: foreground and background
  { a }
  a $1ff and fg-col idx-color { f }
  a 9 rshift $1ff and bg-col idx-color { b }
  a $80000 and if
    a $1ff and
    dup 0= if drop $ffffff to f else dup 8 <= if 1- 8 + xterm>rgb to f else drop then then
  then
  a $100000 and if f b 50 mix to f then
  a $40000 and if f b to f to b then
  f b ;
0 value view-t   0 value cc   0 value ca   0 value cell-f   0 value cell-b   -1 value tint-a
: draw-cell ( col row -- )
  { col row }
  col row view-t + grid c@ to cc
  col row view-t + attr@ to ca
  col con-cw * margin +  row con-ch * top-px + margin + { px py }
  ca tint-a <> if
    ca attr-colors to cell-b to cell-f
    ca to tint-a  cell-f rgb-split glyph-tint
  then
  cell-b bg-col <> if cell-b color px py con-cw con-ch box then
  ca $200000 and if cell-f color px py con-ch + 2 - con-cw 1 box then
  cc 32 > cc 127 < and if cc 32 - px py glyph-at then ;
( ---- Graphics under the text: after `graphic`, the drawing words work at the prompt ---- )
\ A texture of 640x480 receives the drawing (the renderer targets it while the program runs);
\ the console draws it first, stretched to the window, then the text over it: the cells
\ with the default background let it show. cls clears it, flip draws the window at once.
0 value gfx-tex
create gp 4 cells allot
: pen-save ( -- ) pen-r gp !  pen-g gp 8 + !  pen-b gp 16 + !  pen-a gp 24 + ! ;
: pen-back ( -- ) gp @ gp 8 + @ gp 16 + @ gp 24 + @ rgba ;
: gfx-bind ( -- ) renderer gfx-tex SDL_SetRenderTarget drop ;
: gfx-clear ( -- )
  pen-save  0 0 0 0 rgba  renderer SDL_RenderClear drop  pen-back ;
: gfx-off ( -- )
  gfx-tex if
    renderer 0 SDL_SetRenderTarget drop
    gfx-tex SDL_DestroyTexture drop  0 to gfx-tex
  then ;
0 value gw   0 value gh   0 value av   640 value gtw   480 value gth
: gfx-show ( -- )   ( the layer, under the text: its 0 0 is the top left of the text area )
  gfx-tex if
    oh top-px - margin 2* - to av   ow margin 2* - to gw   gw gth * gtw / to gh
    gh av > if av to gh  av gtw * gth / to gw then   ( kept in proportion )
    margin top-px margin +  gw gh rect rect-at!
    renderer gfx-tex 0 rect SDL_RenderCopy drop
  then ;

: con-draw0 ( -- )
  bg-col color cls  gfx-show
  fg-col rgb-split glyph-tint
  vis view-top { v top }
  top to view-t   -1 to tint-a
  top v draw-sel
  v 0 ?do
    con-cols 0 ?do  i j draw-cell  loop
  loop
  blink back 0= and if
    fg-col rgb-split 150 rgba
    cx con-cw * margin +  cy top - con-ch * top-px + margin + con-ch 3 / 2* +  con-cw  con-ch 3 /  box
  then
  menu-on if bar-open if draw-bar then hb-hidden 0= if draw-hb then draw-drop draw-picker draw-help then
  renderer SDL_RenderPresent drop
  0 to dirty ;

: con-draw ( -- )
  gfx-tex if
    pen-save  renderer 0 SDL_SetRenderTarget drop
    con-draw0
    gfx-bind  pen-back
  else con-draw0 then ;

: graphic-on ( -- )   ( 0 0 100 100 line, drawn at once in the console window )
  con-renderer 0= renderer con-renderer <> or if
    s" graphic needs the console window: lugforth --gui" type cr exit then
  gfx-tex 0= if
    renderer SDL_PIXELFORMAT_ARGB8888 2 gtw gth SDL_CreateTexture to gfx-tex
    gfx-tex 0= if sdl-error type cr exit then
    gfx-tex SDL_BLENDMODE_BLEND SDL_SetTextureBlendMode drop
    gfx-bind  gfx-clear
  then
  gtw to width  gth to height  fg-col color  -1 to dirty ;
: graphic-size ( w h -- )   ( the drawing area, in pixels: 640 480 by default, up to 4096 )
  4096 min 16 max to gth  4096 min 16 max to gtw
  gfx-off  graphic-on ;
: cls ( -- ) gfx-tex if gfx-clear else cls then ;
: flip ( -- ) gfx-tex if con-draw else flip then ;

( ---- Keys and mouse ---- )
: q-csi ( c -- )   27 q-push [char] [ q-push q-push ;
: ctrl-key ( sym -- f )   \ Ctrl and a key: text size, paste, and the control codes
  dup [char] + = over [char] = = or over $40000057 = or if drop 1 font-step -1 exit then
  dup [char] - = over $40000056 = or if drop -1 font-step -1 exit then
  dup [char] 0 = over $40000062 = or if drop font-auto -1 exit then
  dup [char] v = if drop paste -1 exit then
  dup [char] c = shift? and if drop sel-copy -1 exit then
  dup [char] a >= over [char] z <= and shift? 0= and if 96 - q-push -1 exit then
  drop 0 ;
0 value ctrl-cmd   \ a Ctrl key was taken as a command: the text event that follows is dropped
: con-key-event ( sym -- )
  help-open if drop 0 to help-open -1 to dirty exit then
  pick-on if drop pick-close exit then
  dup fs-key? if drop fs-toggle exit then
  ctrl? if dup ctrl-key if drop -1 to ctrl-cmd exit then then
  dup $0d = over $40000058 = or if drop 13 q-push exit then
  dup 8 = over 9 = or over 27 = or if q-push exit then
  dup $7f = if drop 27 q-push [char] [ q-push [char] 3 q-push [char] ~ q-push exit then
  dup $40000052 = if drop [char] A q-csi exit then
  dup $40000051 = if drop [char] B q-csi exit then
  dup $4000004f = if drop [char] C q-csi exit then
  dup $40000050 = if drop [char] D q-csi exit then
  dup $4000004a = if drop [char] H q-csi exit then
  dup $4000004d = if drop [char] F q-csi exit then
  dup $4000004b = if drop con-rows 1- scroll-by exit then   ( PageUp )
  dup $4000004e = if drop con-rows 1- negate scroll-by exit then   ( PageDown )
  dup $40000043 = if drop menu-on if bar-toggle then exit then   ( F10 )
  dup $40000044 = if drop -1 font-step exit then   ( F11 )
  dup $40000045 = if drop 1 font-step exit then    ( F12 )
  drop ;

: mouse>cell ( x y -- idx )
  top-px - margin - con-ch / 0 max view-top +
  swap margin - con-cw / 0 max con-cols 1- min
  swap max-cols * + ;
: mouse-down ( -- )
  help-open if 0 to help-open -1 to dirty exit then
  pick-on if pick-click exit then
  ev 16 + c@ 3 = if   ( right click: on the button, hide it or show it again; on the open bar, close it )
    ev-x ev-y hb-hit if hb-toggle exit then
    bar-open if ev-y bar-h < if bar-toggle then then exit then
  ev-x ev-y hb-hit hb-hidden 0= and if bar-toggle exit then
  ev-y bar-h < if
    ev-x m-at to menu-clicked
    menu-clicked menu-open = if close-menu exit then
    menu-clicked 0< 0= if menu-clicked open-menu else close-menu then exit
  then
  menu-open 0< 0= if
    ev-x ev-y drop-hit dup 0< 0= if run-entry close-menu exit then drop
    close-menu exit
  then
  ev 16 + c@ 2 = if paste-primary exit then
  ev 16 + c@ 1 <> if exit then
  ev-x ev-y mouse>cell dup to sel-a to sel-b  0 to sel-on  -1 to dragging  -1 to dirty ;
: mouse-up ( -- )
  pick-drag if 0 to pick-drag save-ini exit then
  dragging if 0 to dragging  sel-on if sel-copy then  -1 to dirty then ;
: mouse-move ( -- )
  pick-drag if pick-move exit then
  menu-open 0< 0= if
    ev-x ev-y drop-hit to hover  -1 to dirty
    ev-y bar-h < if
      ev-x m-at dup 0< 0= over menu-open <> and if open-menu else drop then
    then
    exit then
  dragging if ev-x ev-y mouse>cell to sel-b  sel-a sel-b <> to sel-on  -1 to dirty then ;
: wheel-event ( -- )
  ev 20 + sl@ dup 0= if drop exit then
  menu-open 0< 0= if negate 3 * drop-scroll exit then
  SDL_GetModState to kmod
  ctrl? if font-step exit then
  3 * scroll-by ;
: mouse-event ( type -- )
  dup $401 = if drop mouse-down exit then
  dup $402 = if drop mouse-up exit then
  dup $400 = if drop mouse-move exit then
  drop wheel-event ;

0 value ctrl-next   \ set by the Ctrl button of the Android app (F9): the next letter is a control code
: con-event ( -- )
  ev-type
  dup SDL_QUIT = if drop bye exit then
  dup SDL_TEXTINPUT = if
    drop help-open if 0 to help-open -1 to dirty then
    pick-on if pick-close exit then
    ctrl-next if
      ev 12 + c@ 32 or dup [char] a >= over [char] z <= and
      if 96 - q-push 0 to ctrl-next exit then drop 0 to ctrl-next then
    ctrl-cmd if exit then   ( Ctrl and a key that was a command, such as + or -: no text )
    ev 12 + begin dup c@ dup while q-push 1+ repeat 2drop exit then
  dup SDL_KEYDOWN = if
    drop ev 24 + sl@ to kmod  ev 20 + sl@
    dup $40000042 = if drop ctrl-next 0= to ctrl-next exit then
    ctrl-next if 0 to ctrl-next dup [char] a >= over [char] z <= and
      if 96 - q-push exit then then
    con-key-event exit then
  dup $400 >= over $403 <= and if
    menu-on if mouse-event exit then
    $401 = if SDL_StartTextInput drop -1 to dirty then exit then
  dup $200 = if   ( window resized or turned )
    drop ev 12 + c@ dup 5 = swap 6 = or if relayout then exit then
  drop ;
: con-pump ( -- ) begin ev SDL_PollEvent sign-extend while con-event repeat  0 to ctrl-cmd ;

: con-form ( -- rows cols )   con-rows con-cols ;
also termios
: set-form-hook ( -- )   ['] con-form to form-hook ;
previous
: con-open ( -- )
  menu-on if
    z" DISPLAY" c-getenv 0=  z" WAYLAND_DISPLAY" c-getenv 0=  and  z" SDL_VIDEODRIVER" c-getenv 0=  and
    if -1 throw then
  then
  menu-on if 1024 640 else 960 540 then screen
  renderer 0= if -1 throw then
  menu-on if s" Lugforth" title then
  renderer 0 0 SDL_RenderSetLogicalSize drop
  renderer osize osize 4 + SDL_GetRendererOutputSize drop
  con-cols 0= if con-clear then
  1 +to con-gen   0 to last-built
  con-layout
  renderer to con-renderer
  set-form-hook
  SDL_StartTextInput drop
  -1 to dirty ;
: con-ensure ( -- )
  renderer con-renderer = renderer 0<> and if exit then
  con-open ;

0 value last-blink
: con-key ( -- c )
  con-ensure
  gfx-tex if -1 to dirty then
  begin
    con-pump
    q-any? if 0 to back q-pop exit then
    ticks 500 / 1 and blink <> if blink 1 xor to blink -1 to dirty then
    dirty if con-draw then
    10 SDL_Delay drop
  again ;
: con-key? ( -- f )   ( a program that polls, as writhdeck does, still sees its output )
  renderer con-renderer = renderer 0<> and if con-pump dirty if con-draw then q-any? else 0 then ;

also internals
: le-on ( -- )   -1 le-force ! ;   ( line editing even though stdin is no terminal )
previous

: console ( -- )
  con-ensure   le-on
  ['] con-type is type   ['] con-key is key   ['] con-key? is key? ;

( A program opening its own screen does not want the keyboard )
: screen ( w h -- ) gfx-off SDL_StopTextInput drop screen ;

( ---- The desktop: menu bar, and a TrueType font when there is one ---- )
( The font code is loaded when asked: it needs SDL_ttf, which a machine may lack. )
: ttf-code ( -- ) gui-ttf-src eval-copy ;

: smooth-font ( -- )   ( TrueType if SDL_ttf and a font are there, else the bitmap stays )
  ['] ttf-code catch if sdl-error 2drop then ;
: ini-apply ( -- )   hrel if ini-size to user-size then ;
: desktop ( -- )   -1 to menu-on  0 to bar-open  ini-load  smooth-font  ini-apply ;

also forth definitions
: graphic ( -- )   \ the drawing words at the prompt, in the console window (sdl2 is loaded too)
  sdl2  s" graphic-on" evaluate ;
: sound ( -- )   \ the sound words at the prompt: 440 300 beep, s" c d e" play (sdl2 is loaded too)
  sdl2  s" audio-open" evaluate ;
previous sdl2 definitions

previous
| ;

\ Compiles the console window code (gui-src) if it is not yet.
: gui-load ( -- )   sdl2 gui-src eval-copy ;

also posix
\ True when standard input is a terminal (then the console window is not needed).
: gui-tty? ( -- f )   stdin isatty ;
previous
