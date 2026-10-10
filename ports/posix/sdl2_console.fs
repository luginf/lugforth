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
\ Needs only libSDL2 (no SDL_ttf). Keys come from SDL text input, so the
\ native keyboard of Android shows up. When a program opens its own screen
\ (screen), the console window is replaced; it comes back at the next key.
\ The font is 8x16, derived from DejaVu Sans Mono (Bitstream Vera license).

sdl2
sdl2 definitions also posix

z" SDL_GetRendererOutputSize" 3 sdl SDL_GetRendererOutputSize ( a a a -- n )
z" SDL_StartTextInput" 0 sdl SDL_StartTextInput ( -- void )
z" SDL_StopTextInput" 0 sdl SDL_StopTextInput ( -- void )
z" SDL_IsScreenKeyboardShown" 1 sdl SDL_IsScreenKeyboardShown ( a -- n )
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

( ---- The text grid: 100 lines of up to 300 columns ---- )
100 constant con-lines
300 constant max-cols
create con-buf max-cols con-lines * allot
0 value con-cols   0 value con-rows   0 value con-cw   0 value con-ch
0 value cx   0 value cy
0 value dirty

: grid ( x y -- a ) con-cols * + con-buf + ;
: con-clear ( -- )
  con-buf max-cols con-lines * 32 fill  0 to cx 0 to cy  -1 to dirty ;
: con-scroll ( -- )
  con-buf con-cols + con-buf con-cols con-lines 1- * cmove
  0 con-lines 1- grid con-cols 32 fill ;
: con-nl ( -- )
  0 to cx  1 +to cy  -1 to dirty
  cy con-lines >= if con-scroll con-lines 1- to cy then ;
: put-char ( c -- )
  cx con-cols >= if con-nl then
  cx cy grid c!  1 +to cx  -1 to dirty ;

: kbd-shown ( -- f ) window-handle SDL_IsScreenKeyboardShown $ffffffff and 0<> ;
: vis ( -- n ) kbd-shown if con-rows 2/ 3 max else con-rows then ;
: win-top ( -- n ) cy vis - 1+ 0 max ;

( Just enough ANSI for page, at-xy and clear-to-eol; the rest is ignored )
0 value esc-state   0 value p1   0 value p2   0 value pn
: csi-done ( c -- )
  dup [char] H = if
    drop  p2 1- 0 max con-cols 1- min to cx
    win-top p1 1- 0 max + con-lines 1- min to cy  -1 to dirty exit then
  dup [char] J = if
    drop p1 2 = if con-clear then exit then
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

( ---- Drawing ---- )
create gsrc 16 allot
create gdst 16 allot
: draw-glyph ( g x y -- )
  { g x y }
  g 8 * 0 8 16 gsrc rect-at!
  x y con-cw con-ch gdst rect-at!
  renderer atlas gsrc gdst SDL_RenderCopy drop ;
0 value blink
: con-draw ( -- )
  0 0 0 rgb cls
  atlas 210 210 210 SDL_SetTextureColorMod drop
  vis win-top { v top }
  v 0 ?do
    con-cols 0 ?do
      i j top + grid c@
      dup 32 > over 127 < and if
        32 - i con-cw * j con-ch * draw-glyph
      else drop then
    loop
  loop
  blink if
    210 210 210 150 rgba
    cx con-cw *  cy top - con-ch * con-ch 3 / 2* +  con-cw  con-ch 3 /  box
  then
  renderer SDL_RenderPresent drop
  0 to dirty ;

( ---- Input ---- )
create text-queue 128 allot
0 value qh   0 value qt
: q-push ( c -- ) qt qh - 128 < if text-queue qt 127 and + c! 1 +to qt else drop then ;
: q-pop ( -- c ) text-queue qh 127 and + c@ 1 +to qh ;
: q-any? ( -- f ) qh qt <> ;

: key-translate ( sym -- c )
  dup $0d = if drop 13 exit then
  dup $40000058 = if drop 13 exit then
  dup 8 = over 9 = or over 27 = or if exit then
  drop 0 ;
: con-event ( -- )
  ev-type
  dup SDL_QUIT = if drop bye exit then
  dup SDL_TEXTINPUT = if
    drop ev 12 + begin dup c@ dup while q-push 1+ repeat 2drop exit then
  dup SDL_KEYDOWN = if
    drop ev 20 + sl@ key-translate dup if q-push else drop then exit then
  dup SDL_MOUSEBUTTONDOWN = if drop SDL_StartTextInput drop -1 to dirty exit then
  drop ;
: pump ( -- ) begin ev SDL_PollEvent sign-extend while con-event repeat ;

( ---- The window ---- )
0 value con-renderer
0 value user-scale   0 value ow   0 value oh   0 value scale   0 value cols
create osize 8 allot
: con-layout ( -- )
  renderer osize osize 4 + SDL_GetRendererOutputSize drop
  osize sl@ to ow   osize 4 + sl@ to oh
  user-scale dup 0= if drop ow oh min 360 / 1 max then to scale
  scale 8 * to con-cw   scale 16 * to con-ch
  ow con-cw / max-cols min to cols
  oh con-ch / 1 max to con-rows
  cols con-cols <> if cols to con-cols con-clear then
  -1 to dirty ;
: font-size ( n -- )   ( 1 small, 2, 3, 4... ; 0 is automatic )
  to user-scale  renderer con-renderer = if con-clear con-layout then ;
: con-open ( -- )
  960 540 screen
  renderer 0 0 SDL_RenderSetLogicalSize drop
  renderer osize osize 4 + SDL_GetRendererOutputSize drop
  con-layout
  renderer to con-renderer
  make-atlas
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
    q-any? if q-pop exit then
    ticks 500 / 1 and blink <> if blink 1 xor to blink -1 to dirty then
    dirty if con-draw then
    10 SDL_Delay drop
  again ;
: con-key? ( -- f )
  renderer con-renderer = renderer 0<> and if pump q-any? else 0 then ;

: console ( -- )
  con-ensure
  ['] con-type is type   ['] con-key is key   ['] con-key? is key? ;


( A program opening its own screen does not want the keyboard )
: screen ( w h -- ) SDL_StopTextInput drop screen ;

previous
