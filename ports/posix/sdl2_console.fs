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
\ Usage:
\   include sdl2_console.fs
\   console            \ type, key and key? now go to the SDL window
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
z" getenv" 1 sysfunc c-getenv ( z -- z )
$0303 constant SDL_TEXTINPUT

( ---- Font: 95 glyphs, from space to tilde, 16 rows of 8 bits each ---- )
create font-data
  $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c,   \  
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
  $00 c, $18 c, $18 c, $18 c, $18 c, $18 c, $18 c, $18 c, $18 c, $18 c, $18 c, $18 c, $18 c, $18 c, $00 c, $00 c,   \ |
  $00 c, $70 c, $10 c, $18 c, $18 c, $18 c, $0e c, $18 c, $18 c, $18 c, $18 c, $10 c, $70 c, $00 c, $00 c, $00 c,   \ }
  $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $72 c, $0e c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c, $00 c,   \ ~

( ---- The text grid: 1000 lines of up to 300 columns ---- )
1000 constant con-lines
300 constant max-cols
create con-buf max-cols con-lines * allot
0 value con-cols   0 value con-rows   0 value con-cw   0 value con-ch
0 value cx   0 value cy
0 value dirty
0 value menu-on     ( a desktop: menu bar, mouse, TrueType font )
0 value top-px      ( height of the menu bar )
0 value back        ( lines scrolled back )
0 value sel-on

: grid ( x y -- a ) max-cols * + con-buf + ;
: con-clear ( -- )
  con-buf max-cols con-lines * 32 fill  0 to cx 0 to cy  0 to back  0 to sel-on  -1 to dirty ;
: con-scroll ( -- )
  con-buf max-cols + con-buf max-cols con-lines 1- * cmove
  0 con-lines 1- grid max-cols 32 fill  0 to sel-on ;
: con-nl ( -- )
  0 to cx  1 +to cy  -1 to dirty
  back if 1 +to back then
  cy con-lines >= if con-scroll con-lines 1- to cy then ;
: put-char ( c -- )
  cx con-cols >= if con-nl then
  cx cy grid c!  1 +to cx  -1 to dirty ;

: kbd-shown ( -- f ) window-handle SDL_IsScreenKeyboardShown $ffffffff and 0<> ;
: vis ( -- n ) kbd-shown if con-rows 2/ 3 max else con-rows then ;
: win-top ( -- n ) cy vis - 1+ 0 max ;
: view-top ( -- n ) win-top back - 0 max ;
: scroll-by ( n -- ) back + 0 max win-top min to back  -1 to dirty ;

( Just enough ANSI for page, at-xy, clear-to-eol and cursor moves; the rest is ignored )
0 value esc-state   0 value p1   0 value p2   0 value pn
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
    drop cx cy grid con-cols cx - 0 max 32 fill  -1 to dirty exit then
  drop ;
: csi-char ( c -- )
  dup [char] 0 >= over [char] 9 <= and if
    [char] 0 - pn if p2 10 * + to p2 else p1 10 * + to p1 then exit then
  dup [char] ; = if drop 1 to pn exit then
  dup 64 >= over 126 <= and if 0 to esc-state csi-done exit then
  drop ;
: con-putc ( c -- )
  esc-state 2 = if csi-char exit then
  esc-state 3 = if 7 = if 0 to esc-state then exit then   ( title, ends with bell )
  esc-state 1 = if
    dup [char] [ = if drop 2 to esc-state 0 to p1 0 to p2 0 to pn exit then
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
  menu-on if con-ch 3 * 2/ else 0 then to top-px
  hsize last-built <> if atlas-build hsize to last-built then
  ow con-cw / max-cols min to cols
  oh top-px - con-ch / 1 max to con-rows
  cols to con-cols
  -1 to dirty ;
: relayout ( -- )   renderer con-renderer = if con-layout then ;
: font-size ( n -- )   ( 1 small, 2, 3, 4... ; 0 is automatic )
  2* to user-size relayout ;
: font-step ( d -- )   ( one step bigger or smaller, as a pinch or the wheel does )
  hrel if dup hsize hrel * 100 / 1 max * else hstep * then
  hsize + hmin max hmax min to user-size relayout ;
: font-auto ( -- )   0 to user-size relayout ;

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
  0 sel-put  selbuf SDL_SetClipboardText drop ;
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
        bc1 con-cw *  top-px i con-ch * +  bc2 bc1 - 1+ con-cw *  con-ch box
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
: paste ( -- )
  SDL_GetClipboardText dup 0= if drop exit then
  dup begin dup c@ dup while paste-char 1+ repeat 2drop
  SDL_free drop ;

0 value kmod
: ctrl? ( -- f ) kmod $c0 and 0<> ;
: shift? ( -- f ) kmod 3 and 0<> ;

( ---- The menu bar of a desktop ---- )
-1 value menu-open   -1 value hover   0 value help-open   0 value hit
3 constant menus
: bar-h ( -- n ) top-px ;
: m-name ( i -- a n )
  dup 0= if drop s" Examples" exit then
  1 = if s" View" exit then
  s" Help" ;
: m-w ( i -- px ) m-name nip 2 + con-cw * ;
: m-x ( i -- px ) con-cw swap 0 ?do i m-w + loop ;
: m-at ( x -- i )   \ the menu under x, or -1
  -1 swap
  menus 0 ?do
    dup i m-x >= over i m-x i m-w + < and if nip i swap then
  loop drop ;

64 constant ent-max
create ent-a ent-max cells allot
create ent-n ent-max cells allot
create ent-id ent-max cells allot
4096 constant pool-size
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

: menu-fill ( i -- )
  0 to ent-count   0 to ent-used
  dup 0= if
    drop ex-list ent-sort
    ent-count 0= if s" (no example here)" 0 ent-add then
    exit then
  1 = if
    s" Bigger        Ctrl +" 1 ent-add
    s" Smaller       Ctrl -" 2 ent-add
    s" Default size  Ctrl 0" 3 ent-add
    s" Clear screen  Ctrl L" 6 ent-add
    exit then
  s" Keys and tips" 4 ent-add
  s" List the words" 5 ent-add ;

: close-menu ( -- )  -1 to menu-open  -1 to hover  -1 to dirty ;
: open-menu ( i -- ) dup to menu-open menu-fill  -1 to hover  -1 to dirty ;
: drop-x ( -- px ) menu-open m-x ;
: drop-w ( -- px )
  0 ent-count 0 ?do i ent-str nip max loop 2 + con-cw * ;
: drop-rows ( -- n ) ent-count con-rows 2 - min ;
: drop-hit ( x y -- k )   \ the entry under the pointer, or -1
  bar-h - dup 0< if 2drop -1 exit then   con-ch /
  dup drop-rows >= if 2drop -1 exit then
  swap drop-x - dup 0< if 2drop -1 exit then
  drop-w >= if drop -1 exit then ;

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
  drop ;

: help-line ( k -- a n )
  dup 0 = if drop s" Lugforth" exit then
  dup 1 = if drop s" " exit then
  dup 2 = if drop s" Examples: run a file of your examples folder." exit then
  dup 3 = if drop s" View: text size (Ctrl+wheel, Ctrl + and Ctrl - too)." exit then
  dup 4 = if drop s" Mouse: drag to select and copy, middle click pastes." exit then
  dup 5 = if drop s" Wheel, PageUp and PageDown scroll back." exit then
  dup 6 = if drop s" Tab completes words and file names, Up and Down" exit then
  dup 7 = if drop s" give back the history. Type  words  for the list" exit then
  dup 8 = if drop s" of words and  bye  to quit." exit then
  dup 9 = if drop s" " exit then
  drop s" Click or press a key to close." ;
11 constant help-lines

: fbox ( x y w h r g b -- ) rgb box ;
: draw-bar ( -- )
  0 0 ow bar-h 32 33 36 fbox
  230 230 230 glyph-tint
  menus 0 ?do
    i menu-open = if i m-x 0 i m-w bar-h 60 64 67 fbox then
    i m-name  i m-x con-cw +  bar-h con-ch - 2/  draw-text
  loop ;
: draw-drop ( -- )
  menu-open 0< if exit then
  drop-x bar-h drop-w drop-rows con-ch * 45 46 50 fbox
  230 230 230 glyph-tint
  drop-rows 0 ?do
    i hover = if drop-x bar-h i con-ch * +  drop-w con-ch  63 81 181 fbox then
    i ent-str  drop-x con-cw +  bar-h i con-ch * +  draw-text
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

( ---- Drawing ---- )
0 value blink
: con-draw ( -- )
  0 0 0 rgb cls
  210 210 210 glyph-tint
  vis view-top { v top }
  top v draw-sel
  v 0 ?do
    con-cols 0 ?do
      i j top + grid c@
      dup 32 > over 127 < and if
        32 - i con-cw * j con-ch * top-px + glyph-at
      else drop then
    loop
  loop
  blink back 0= and if
    210 210 210 150 rgba
    cx con-cw *  cy top - con-ch * top-px + con-ch 3 / 2* +  con-cw  con-ch 3 /  box
  then
  menu-on if draw-bar draw-drop draw-help then
  renderer SDL_RenderPresent drop
  0 to dirty ;

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
: key-event ( sym -- )
  help-open if drop 0 to help-open -1 to dirty exit then
  ctrl? if dup ctrl-key if drop exit then then
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
  dup $40000044 = if drop -1 font-step exit then   ( F11 )
  dup $40000045 = if drop 1 font-step exit then    ( F12 )
  drop ;

: ev-x ( -- n ) ev 20 + sl@ ;
: ev-y ( -- n ) ev 24 + sl@ ;
: mouse>cell ( x y -- idx )
  top-px - con-ch / 0 max view-top +
  swap con-cw / 0 max con-cols 1- min
  swap max-cols * + ;
: mouse-down ( -- )
  help-open if 0 to help-open -1 to dirty exit then
  ev-y bar-h < if
    ev-x m-at to hit
    hit menu-open = if close-menu exit then
    hit 0< 0= if hit open-menu else close-menu then exit
  then
  menu-open 0< 0= if
    ev-x ev-y drop-hit dup 0< 0= if run-entry close-menu exit then drop
    close-menu exit
  then
  ev 16 + c@ 2 = if paste exit then
  ev 16 + c@ 1 <> if exit then
  ev-x ev-y mouse>cell dup to sel-a to sel-b  0 to sel-on  -1 to dragging  -1 to dirty ;
: mouse-up ( -- )
  dragging if 0 to dragging  sel-on if sel-copy then  -1 to dirty then ;
: mouse-move ( -- )
  menu-open 0< 0= if
    ev-x ev-y drop-hit to hover  -1 to dirty
    ev-y bar-h < if
      ev-x m-at dup 0< 0= over menu-open <> and if open-menu else drop then
    then
    exit then
  dragging if ev-x ev-y mouse>cell to sel-b  sel-a sel-b <> to sel-on  -1 to dirty then ;
: wheel-event ( -- )
  ev 20 + sl@ dup 0= if drop exit then
  SDL_GetModState to kmod
  ctrl? if font-step exit then
  3 * scroll-by ;
: mouse-event ( type -- )
  dup $401 = if drop mouse-down exit then
  dup $402 = if drop mouse-up exit then
  dup $400 = if drop mouse-move exit then
  drop wheel-event ;

: con-event ( -- )
  ev-type
  dup SDL_QUIT = if drop bye exit then
  dup SDL_TEXTINPUT = if
    drop help-open if 0 to help-open -1 to dirty then
    ev 12 + begin dup c@ dup while q-push 1+ repeat 2drop exit then
  dup SDL_KEYDOWN = if
    drop ev 24 + sl@ to kmod  ev 20 + sl@ key-event exit then
  dup $400 >= over $403 <= and if
    menu-on if mouse-event exit then
    $401 = if SDL_StartTextInput drop -1 to dirty then exit then
  dup $200 = if   ( window resized or turned )
    drop ev 12 + c@ dup 5 = swap 6 = or if relayout then exit then
  drop ;
: pump ( -- ) begin ev SDL_PollEvent sign-extend while con-event repeat ;

: con-open ( -- )
  menu-on if 1024 640 else 960 540 then screen
  menu-on if s" Lugforth" title then
  renderer 0 0 SDL_RenderSetLogicalSize drop
  renderer osize osize 4 + SDL_GetRendererOutputSize drop
  con-cols 0= if con-clear then
  1 +to con-gen   0 to last-built
  con-layout
  renderer to con-renderer
  SDL_StartTextInput drop
  -1 to dirty ;
: con-ensure ( -- )
  renderer con-renderer = renderer 0<> and if exit then
  con-open ;

0 value last-blink
: con-key ( -- c )
  con-ensure
  begin
    pump
    q-any? if 0 to back q-pop exit then
    ticks 500 / 1 and blink <> if blink 1 xor to blink -1 to dirty then
    dirty if con-draw then
    10 SDL_Delay drop
  again ;
: con-key? ( -- f )
  renderer con-renderer = renderer 0<> and if pump q-any? else 0 then ;

also internals
: le-on ( -- )   -1 le-force ! ;   ( line editing even though stdin is no terminal )
previous

: console ( -- )
  con-ensure   le-on
  ['] con-type is type   ['] con-key is key   ['] con-key? is key? ;

( A program opening its own screen does not want the keyboard )
: screen ( w h -- ) SDL_StopTextInput drop screen ;

( ---- The desktop: menu bar, and a TrueType font when there is one ---- )
( The font code is a string, run at once: it needs SDL_ttf, which a machine may lack. )
: ttf-code r|
sdl2-ttf
sdl2 definitions also posix
0 value tfont   0 value font-file   0 value tex-gen
create tex-t 100 cells allot
create gz 4 allot
: font-try ( z -- f )
  dup 0= if drop 0 exit then
  dup 12 TTF_OpenFont ?dup if TTF_CloseFont drop to font-file -1 else drop 0 then ;
: font-find ( -- f )
  z" LUGFORTH_FONT" c-getenv font-try if -1 exit then
  z" /usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf" font-try if -1 exit then
  z" /usr/share/fonts/dejavu/DejaVuSansMono.ttf" font-try if -1 exit then
  z" /usr/share/fonts/TTF/DejaVuSansMono.ttf" font-try if -1 exit then
  z" /usr/share/fonts/truetype/liberation/LiberationMono-Regular.ttf" font-try if -1 exit then
  z" /usr/share/fonts/liberation-mono/LiberationMono-Regular.ttf" font-try if -1 exit then
  0 ;
: ttf-cell ( pt -- cw ch )
  tfont ?dup if TTF_CloseFont drop then
  font-file swap TTF_OpenFont to tfont
  tfont z" M" tsize tsize 4 + TTF_SizeUTF8 drop
  tsize sl@ tsize 4 + sl@ ;
: ttf-free ( -- )
  tex-gen con-gen = if
    95 0 do tex-t i cells + @ ?dup if SDL_DestroyTexture drop then loop
  then
  tex-t 100 cells 0 fill ;
: ttf-one ( i -- )
  dup 32 + gz c!  0 gz 1+ c!
  tfont gz $ffffffff TTF_RenderUTF8_Blended
  dup 0= if drop drop exit then
  dup renderer swap SDL_CreateTextureFromSurface
  swap SDL_FreeSurface drop
  swap cells tex-t + ! ;
: ttf-build ( -- )
  ttf-free
  95 0 do i ttf-one loop
  con-gen to tex-gen ;
: ttf-glyph ( g x y -- )
  { g x y }
  g 0< g 94 > or if exit then
  g cells tex-t + @ ?dup if
    x y con-cw con-ch gdst rect-at!
    renderer swap 0 gdst SDL_RenderCopy drop
  then ;
: ttf-tint ( r g b -- )
  { r g b }
  95 0 do tex-t i cells + @ ?dup if r g b SDL_SetTextureColorMod drop then loop ;
: ttf-install ( -- )
  TTF_Init drop
  font-find if
    ['] ttf-cell is font-cell   ['] ttf-build is atlas-build
    ['] ttf-glyph is glyph-at   ['] ttf-tint is glyph-tint
    8 to hmin   72 to hmax   2 to hstep   28 to hdiv   8 to hrel
  then ;
ttf-install
previous
| evaluate ;

: smooth-font ( -- )   ( TrueType if SDL_ttf and a font are there, else the bitmap stays )
  ['] ttf-code catch if sdl-error 2drop then ;
: desktop ( -- )   -1 to menu-on  smooth-font ;

previous
