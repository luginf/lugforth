# SDL2 word reference

Every word of the `sdl2` vocabulary, for the POSIX build
(`out/posix/lugforth`). For a tour with examples read [SDL2.md](SDL2.md)
first. This page is the complete list.

The words come from five loaders, each loading its source the first time
it is typed. They all add to the same `sdl2` vocabulary:

| Loader | Library | Section |
|--------|---------|---------|
| `sdl2` | `libSDL2-2.0.so.0` | [Core](#core-sdl2) |
| `sdl2-image` | `libSDL2_image-2.0.so.0` | [Images](#images-sdl2-image) |
| `sdl2-ttf` | `libSDL2_ttf-2.0.so.0` | [Text](#text-sdl2-ttf) |
| `sdl2-mixer` | `libSDL2_mixer-2.0.so.0` | [Sound](#sound-sdl2-mixer) |
| `sdl2-abc` | (loads `sdl2-mixer`) | [Tunes](#tunes-sdl2-abc) |

Stack notation: `x y` are pixel coordinates, `w h` sizes, `a n` a Forth
string (address, length), `z` a zero terminated C string, `f` a flag,
`$rrggbb` a color. `img snd mus font` are handles returned by the
`load-` words. Values are read by name and changed with `to`:
`3 to zoom`.

Raw C bindings keep the C name and argument order. Each one pushes
exactly one result. When the table says `void` the C function returns
nothing but a value is still pushed: `drop` it. Int results that can be
negative (`-1` for an error) should go through `sign-extend` from the
`posix` vocabulary.

Word lists: [alphabetical index](#alphabetical-index).

## Core (sdl2)


### Screen

| Word | Stack | What it does |
|------|-------|--------------|
| `screen` | `( w h -- )` | Open a window of w*`zoom` by h*`zoom` pixels whose drawing area is w by h logical pixels (scaled, aspect kept). Closes a previous screen first. Uses an accelerated renderer with vsync, or the software one if that fails. Throws with the SDL message on failure. |
| `close-screen` | `( -- )` | Destroy the renderer and window, run `close-hook`, close the audio device and call `sdl-shutdown` (the C function `SDL_Quit`). |
| `screen-open?` | `( -- f )` | True while a screen is open. |
| `zoom` | `( -- n )` | Value, default 1. Window scale factor used by the next `screen`: `3 to zoom`. |
| `width` | `( -- n )` | Value. Logical width given to the last `screen`. |
| `height` | `( -- n )` | Value. Logical height given to the last `screen`. |
| `title` | `( a n -- )` | Set the window title. |
| `fullscreen` | `( f -- )` | Switch desktop fullscreen on (true) or off (false). |
| `cursor` | `( f -- )` | Show (true) or hide (false) the mouse cursor. |
| `window-handle` | `( -- a )` | Value. The `SDL_Window*`, 0 when no screen is open. |
| `renderer` | `( -- a )` | Value. The `SDL_Renderer*`, 0 when no screen is open. Pass it to raw calls. |
| `sdl-error` | `( -- a n )` | Text of the last SDL error. |

### Colors

| Word | Stack | What it does |
|------|-------|--------------|
| `color` | `( $rrggbb -- )` | Set the drawing color, opaque. `red color`, `$102030 color`. |
| `rgb` | `( r g b -- )` | Set the drawing color from components 0 to 255, opaque. |
| `rgba` | `( r g b a -- )` | Set the drawing color and alpha. Alpha below 255 blends with what is already drawn. |
| `alpha` | `( a -- )` | Change only the alpha of the current color. |
| `black` `white` `red` `green` `blue` `yellow` `cyan` `magenta` `orange` `purple` `pink` `brown` `gray` | `( -- $rrggbb )` | Named colors, constants: `$000000 $ffffff $ff0000 $00c000 $0000ff $ffff00 $00ffff $ff00ff $ff8000 $800080 $ff80c0 $804000 $808080`. Use as `red color`. |
| `pen-r` `pen-g` `pen-b` `pen-a` | `( -- n )` | Values. Components of the current color. Read them, set the color with `rgba`. |
| `ink` | `( n -- )` | Set the drawing color by number, as on an 8 bit micro: 0 black, 1 red, 2 green, 3 yellow, 4 blue, 5 magenta, 6 cyan, 7 white, 8 to 15 gray, orange, pink, brown, purple, light green, light blue, light gray. `1 ink` is `red color`. Opaque. |
| `pen` | `( -- n )` | Value. Current color packed as `r + g<<8 + b<<16 + a<<24`, the layout of an `SDL_Color` passed by value (used by `text`). |

### Drawing

| Word | Stack | What it does |
|------|-------|--------------|
| `cls` | `( -- )` | Fill the whole screen with the current color. |
| `dot` | `( x y -- )` | Draw one pixel. Also named `plot` (see [Aliases](#aliases)). |
| `line` | `( x1 y1 x2 y2 -- )` | Draw a line, both ends included. |
| `box` | `( x y w h -- )` | Filled rectangle. |
| `frame` | `( x y w h -- )` | Rectangle outline. |
| `hline` | `( x y w -- )` | Horizontal line of width w starting at x y. |
| `disc` | `( cx cy r -- )` | Filled circle of radius r. |
| `circle` | `( cx cy r -- )` | Circle outline of radius r. |
| `flip` | `( -- )` | Show what was drawn since the last `flip`. Waits for vsync when available, and updates `dt`. Also tops up the sound of `drum-hit`. |
| `pixel@` | `( x y -- $rrggbb )` | Read back one pixel of the screen. Useful for color based collisions. |

### Timing

| Word | Stack | What it does |
|------|-------|--------------|
| `ticks` | `( -- ms )` | Milliseconds since SDL started. |
| `delay` | `( ms -- )` | Sleep for ms milliseconds, topping up the sound of `drum-hit` meanwhile. |
| `dt` | `( -- ms )` | Value. Milliseconds between the last two `flip`. |

### Input

| Word | Stack | What it does |
|------|-------|--------------|
| `events` | `( -- )` | Handle every pending event: keys, mouse, window close. Call once per frame, before reading input. |
| `wait` | `( -- )` | Block until one event arrives and handle it. |
| `quit?` | `( -- f )` | True once the window was closed or `quit!` was called. |
| `quit!` | `( -- )` | Set the quit flag, as if the window was closed. |
| `pressed?` | `( key -- f )` | True while the key or mouse button is held, as of the last `events`. Key is a character (`[char] a`, case ignored), a `key-` constant, or a `-BUTTON` constant. A press released within the same frame is not seen. |
| `scan?` | `( scancode -- f )` | True while the key at that place of the keyboard is held (SDL scancode: 4 to 29 are A to Z of a QWERTY, 30 to 39 are 1 to 9 and 0, 54 55 56 are comma, period, slash), whatever the layout. |
| `last-key` | `( -- n )` | Value. Code of the last key pressed. |
| `typed` | `( -- c )` | The next character typed, or 0 when there is none. Printable characters come from the text input events (so shift, accents and AltGr are right), plus 13 for Enter, 8 for Backspace and 1 for the up arrow. Read it in a loop after `events` for a prompt inside the window (examples/graphics/gfx_turtle.fs). The oldest characters are dropped if the program does not read them. |
| `unpress` | `( key -- )` | Forget a key that is down, so that `pressed?` says no until it is pressed again (to take an Esc once). |
| `mouse-x` `mouse-y` | `( -- n )` | Values. Last mouse position seen in events, in logical pixels (the `screen` size), whatever the `zoom`. |
| `wheel` | `( -- n )` | Value. Mouse wheel movement handled by the last `events` (up is positive). |
| `key-left` `key-right` `key-up` `key-down` | `( -- n )` | Constants. Arrow keys, for `pressed?`. |
| `key-space` `key-enter` `key-esc` `key-tab` `key-backspace` | `( -- n )` | Constants. `32 13 27 9 8`, for `pressed?`. |
| `LEFT-BUTTON` `MIDDLE-BUTTON` `RIGHT-BUTTON` | `( -- n )` | Constants `255 254 253`, for `pressed?`. Same values as the generic graphics interface. |

### Beeps and hits

| Word | Stack | What it does |
|------|-------|--------------|
| `beep` | `( freq ms -- )` | Queue a tone of freq Hz lasting ms milliseconds. Tones play one after the other, so a series of `beep` is a melody. Silent if no audio device could be opened. |
| `drum-hit` | `( freq ms -- )` | Play a percussive sound now, over whatever else is playing: it fades out linearly and its pitch can fall (`pitch-drop`). Up to 8 at a time, a ninth cuts the oldest. Uses `wave` and `beep-volume`. The mix is queued about 50 ms ahead by `drum-hit`, `flip` and `delay`, so call `flip` or `delay` regularly. A `beep` waits for the queue while a hit does not, so do not expect them to line up. |
| `pitch-drop` | `( n -- )` | Setting (`pitch-drop@` reads it), default 0. Percent by which the pitch of the next hits falls while they play, 0 to 100. Noise has no pitch. |
| `silence` | `( -- )` | Drop all queued beeps and stop all hits. |
| `latency` | `( -- n )` | Value, default 2205. Samples kept queued by `flip`, `delay` and `drum-hit` (50 ms); lower it for a more reactive instrument. |
| `render-hook` `active-hook` | `( -- xt )` | Values, 0 by default. Extra sources for the mix of `pump`: `render-hook` is run as `( n -- )` and adds n samples to the cell array `mix`, `active-hook` as `( -- f )` tells that it has sound to give. `sdl2-fm` uses them. |
| `beeping?` | `( -- f )` | True while queued beeps or hits remain. |
| `wave` | `( n -- )` | Setting. Shape of the next beeps: 0 square, 1 triangle, 2 saw, 3 noise, 4 sine (`wave@` reads it; `to wave` works too). |
| `beep-volume` | `( n -- )` | Setting (`beep-volume@` reads it), default 3000. Beep amplitude, 0 to 32767. |
| `square` `triangle` `saw` `noise` `sine` | `( -- )` | Choose the wave of the next beeps by name: `0 wave`, `1 wave`, `2 wave`, `3 wave`, `4 wave`. |
| `play` | `( a n -- )` | Play a melody written as text (MML, as on the MSX), queued like `beep`: `s" t140 o4 l8 c d e c  e4 g4 r4 >c2" play`. Notes `c d e f g a b`, `#` or `+` sharp, `-` flat, then a length (4 quarter, 8 eighth, a dot lengthens by half); `r` or `p` a rest; `o4` octave, `<` one octave down, `>` one up; `l8` default length; `t120` tempo in quarters a minute; `w1` the wave; `v8` the volume 0 to 15; `n49` a note by number (`n0` a rest, 49 is the middle C); `c4&c8` ties two notes of the same pitch. Spaces are free; the settings stay from one `play` to the next. A comma starts another voice, played together with the others (4 voices, 512 notes each, each begins with the octave, length, tempo, wave and volume the `play` started with). Only in the short words (`sdl2`), not in the mixer: with `sdl2-mixer`, `play` is the word that plays a sound. |
| `adsr` | `( a d s r -- )` | Envelope of the beeps and of `play`: attack and decay in ms, sustain in percent of the peak, release in ms (the end of the note, not added to it). Hits and ABC tunes are not affected. Off by default. |
| `adsr@` | `( -- a d s r )` | The four values of `adsr`. |
| `adsr-off` | `( -- )` | Back to the plain note, without envelope. |
| `setting` | `( n "name" -- )` | Defines a setting, as `wave`, `beep-volume` and `pitch-drop` are: `3000 beep-volume` stores, `to beep-volume` too, and `beep-volume@` reads it. |
| `audio-open` | `( -- )` | Open the beep audio device now instead of at the first `beep`. |
| `audio-open?` | `( -- f )` | True when the beep audio device is open. |
| `sample-rate` | `( -- n )` | Constant 44100. Beep sample rate. |
| `sdl` | `( z n "name" -- )` | Handle of libSDL2 made by `shared-library`. `z" SDL_Init" 1 sdl SDL_Init` defines a word calling that C function with n arguments. |

### Constants (core)

| Constant | Value |
|----------|-------|
| `SDL_INIT_AUDIO` | `$00000010` |
| `SDL_INIT_VIDEO` | `$00000020` |
| `SDL_INIT_EVENTS` | `$00004000` |
| `SDL_WINDOW_SHOWN` | `$00000004` |
| `SDL_WINDOW_RESIZABLE` | `$00000020` |
| `SDL_WINDOW_FULLSCREEN_DESKTOP` | `$00001001` |
| `SDL_WINDOWPOS_CENTERED` | `$2FFF0000` |
| `SDL_RENDERER_SOFTWARE` | `$00000001` |
| `SDL_RENDERER_ACCELERATED` | `$00000002` |
| `SDL_RENDERER_PRESENTVSYNC` | `$00000004` |
| `SDL_BLENDMODE_NONE` | `0` |
| `SDL_BLENDMODE_BLEND` | `1` |
| `SDL_BLENDMODE_ADD` | `2` |
| `SDL_PIXELFORMAT_ARGB8888` | `$16362004` |
| `SDL_QUIT` | `$0100` |
| `SDL_KEYDOWN` | `$0300` |
| `SDL_KEYUP` | `$0301` |
| `SDL_MOUSEMOTION` | `$0400` |
| `SDL_MOUSEBUTTONDOWN` | `$0401` |
| `SDL_MOUSEBUTTONUP` | `$0402` |
| `SDL_MOUSEWHEEL` | `$0403` |
| `AUDIO_S16SYS` | `$8010` |
| `#voices` | `8` |

### Raw C bindings (core)

`sdl-shutdown` is the C function `SDL_Quit`: lugforth ignores case, so
it could not share its name with the `SDL_QUIT` event constant.

| Word | Stack | Args |
|------|-------|------|
| `SDL_Init` | `( n -- n )` | 1 |
| `SDL_InitSubSystem` | `( n -- n )` | 1 |
| `sdl-shutdown` | `( -- void )` | 0 |
| `SDL_GetError` | `( -- z )` | 0 |
| `SDL_SetHint` | `( z z -- n )` | 2 |
| `SDL_GetTicks` | `( -- n )` | 0 |
| `SDL_Delay` | `( n -- void )` | 1 |
| `SDL_CreateWindow` | `( z x y w h flags -- a )` | 6 |
| `SDL_DestroyWindow` | `( a -- void )` | 1 |
| `SDL_SetWindowTitle` | `( a z -- void )` | 2 |
| `SDL_SetWindowFullscreen` | `( a n -- n )` | 2 |
| `SDL_ShowCursor` | `( n -- n )` | 1 |
| `SDL_CreateRenderer` | `( a n n -- a )` | 3 |
| `SDL_DestroyRenderer` | `( a -- void )` | 1 |
| `SDL_RenderSetLogicalSize` | `( a n n -- n )` | 3 |
| `SDL_SetRenderDrawColor` | `( a r g b a -- n )` | 5 |
| `SDL_SetRenderDrawBlendMode` | `( a n -- n )` | 2 |
| `SDL_RenderClear` | `( a -- n )` | 1 |
| `SDL_RenderPresent` | `( a -- void )` | 1 |
| `SDL_RenderDrawPoint` | `( a x y -- n )` | 3 |
| `SDL_RenderDrawLine` | `( a x y x y -- n )` | 5 |
| `SDL_RenderDrawRect` | `( a a -- n )` | 2 |
| `SDL_RenderFillRect` | `( a a -- n )` | 2 |
| `SDL_RenderCopy` | `( a a a a -- n )` | 4 |
| `SDL_RWFromFile` | `( z z -- a )` | 2 |
| `SDL_RenderReadPixels` | `( a a n a n -- n )` | 5 |
| `SDL_CreateTexture` | `( a n n n n -- a )` | 5 |
| `SDL_CreateTextureFromSurface` | `( a a -- a )` | 2 |
| `SDL_UpdateTexture` | `( a a a n -- n )` | 4 |
| `SDL_QueryTexture` | `( a a a a a -- n )` | 5 |
| `SDL_SetTextureColorMod` | `( a r g b -- n )` | 4 |
| `SDL_SetTextureAlphaMod` | `( a n -- n )` | 2 |
| `SDL_SetTextureBlendMode` | `( a n -- n )` | 2 |
| `SDL_DestroyTexture` | `( a -- void )` | 1 |
| `SDL_FreeSurface` | `( a -- void )` | 1 |
| `SDL_PollEvent` | `( a -- n )` | 1 |
| `SDL_WaitEvent` | `( a -- n )` | 1 |
| `SDL_PushEvent` | `( a -- n )` | 1 |
| `SDL_GetMouseState` | `( a a -- n )` | 2 |
| `SDL_GetKeyboardState` | `( a -- a )` | 1 |
| `SDL_OpenAudioDevice` | `( z n a a n -- n )` | 5 |
| `SDL_CloseAudioDevice` | `( n -- void )` | 1 |
| `SDL_PauseAudioDevice` | `( n n -- void )` | 2 |
| `SDL_QueueAudio` | `( n a n -- n )` | 3 |
| `SDL_GetQueuedAudioSize` | `( n -- n )` | 1 |
| `SDL_ClearQueuedAudio` | `( n -- void )` | 1 |

### Internal words (core)

Used by the words above. Listed because they are in the vocabulary and
can hide or be hidden by your own names.

| Word | Stack | What it does |
|------|-------|--------------|
| `8bit` | `( n -- n )` | Keep the low 8 bits. |
| `?sdl` | `( n -- )` | Throw with the SDL error text when an int result is negative. |
| `ev` | `( -- a )` | Buffer holding the current `SDL_Event`, 64 bytes. |
| `ev-type` | `( -- n )` | Type of the event in `ev`. |
| `rect` | `( -- a )` | Shared `SDL_Rect` buffer (four int32: x y w h). |
| `rect-at!` | `( x y w h a -- )` | Fill an `SDL_Rect` at address a. |
| `rect!` | `( x y w h -- )` | Fill `rect`. |
| `px` | `( -- a )` | 4 byte buffer used by `pixel@`. |
| `span-x` | `( -- n )` | Value. Running half width used by `disc` and `circle`. |
| `span-step` | `( i r -- )` | Shrink `span-x` until it fits the circle of radius r at row i. |
| `dots4` | `( cx cy a b -- )` | Plot the four mirrored points cx+-a, cy+-b. |
| `key-index` | `( sym scancode -- n )` | Turn an SDL key into the 0 to 255 code used by `pressed?`: ASCII as is, special keys as 128 plus scancode. |
| `key-event` | `( f -- )` | Store a key press (1) or release (0) from `ev`. |
| `button-event` | `( f -- )` | Store a mouse button press or release from `ev`. |
| `handle-event` | `( -- )` | Dispatch the event in `ev` on its type. |
| `keys` | `( -- a )` | 256 byte table of held keys and buttons. |
| `fold-case` | `( k -- k )` | Lowercase an upper case letter code. |
| `quitting` | `( -- n )` | Value. Quit flag behind `quit?`. |
| `close-hook` | `( -- xt )` | Value. Optional xt run by `close-screen`; the mixer uses it. |
| `last-flip` | `( -- ms )` | Value. Time of the last `flip`. |
| `make-renderer` | `( -- a )` | Create the renderer, accelerated with vsync or else software. |
| `audio-dev` | `( -- n )` | Value. Beep audio device id, 0 when closed. |
| `audio-spec` | `( -- a )` | `SDL_AudioSpec` buffer, 32 bytes. |
| `seed` | `( -- n )` | Value. Noise generator state. |
| `noise-sample` | `( -- n )` | Next noise sample. |
| `wave-sample` | `( phase -- n )` | Sample of the current `wave` at phase 0 to 65535. |
| `fill-beep` | `( freq n buf -- )` | Write n samples of a tone into buf, with a short fade out. |
| `base-name` | `( a n -- a' n' )` | A file name without its directories. |
| `audio-hook` | `( -- xt )` | Value. Optional xt run by `flip` and `delay`; it is set to `pump`. |
| `run-audio-hook` | `( -- )` | Run `audio-hook` if set. |
| `#voices` | `( -- 8 )` | Constant. Number of hits that can play at once. |
| `voice-size` | `( -- n )` | Constant. Bytes of one voice: 7 cells (phase, frequency, samples left, samples in all, wave, pitch drop, volume). |
| `voices` | `( -- a )` | Table of the voices. |
| `voice` | `( i -- a )` | Address of voice i. |
| `clear-voices` | `( -- )` | Stop all hits without touching the audio queue. |
| `left@` | `( i -- n )` | Samples left to play in voice i, 0 when idle. |
| `voices-active` | `( -- n )` | Number of hits playing. |
| `voice-free` | `( -- i )` | An idle voice, or else the one closest to its end. |
| `start-voice` | `( freq ms -- )` | Start a hit in a voice without mixing it yet (`drum-hit` is this plus `pump`). |
| `pitch-at` | `( freq drop elapsed total -- hz )` | Frequency part way through a hit whose pitch falls by drop percent. |
| `mix` | `( -- a )` | 4096 cells where the voices are added up. |
| `mixed` | `( -- a )` | 4096 samples of 16 bits, the mix ready to queue. |
| `latency` | `( -- n )` | Constant 2205. Samples kept queued ahead, 50 ms. |
| `mix-voice` | `( v n -- )` | Add up to n samples of voice v into `mix`. |
| `render` | `( n buf -- )` | Mix the voices into n 16 bit samples of buf, clipped. |
| `pump` | `( -- )` | Top the audio queue up to `latency` samples with the mix of the voices, unless none is playing. |

## Images (sdl2-image)

Type `sdl2-image` once, then load images after `screen`.

| Word | Stack | What it does |
|------|-------|--------------|
| `load-image` | `( a n -- img )` | Load a PNG, JPG, BMP... file into a texture. A name with directories that is not found is tried again without them, so a program finds its files beside it. Throws with the SDL message on failure. Open the screen first. |
| `free-image` | `( img -- )` | Destroy the texture. |
| `image-size` | `( img -- w h )` | Size of the image in pixels. |
| `draw` | `( img x y -- )` | Draw the image at its natural size, top left at x y. |
| `draw-size` | `( img x y w h -- )` | Draw the image stretched to w by h. |
| `draw-part` | `( img sx sy sw sh x y w h -- )` | Draw the part sx sy sw sh of the image into x y w h. For sprite sheets and tiles. |
| `image-alpha` | `( img a -- )` | Set the image opacity, 0 to 255. |
| `image-tint` | `( img $rrggbb -- )` | Multiply the image colors by a color. |
| `sdlimg` | `( z n "name" -- )` | Handle of libSDL2_image made by `shared-library`. |

### Raw C bindings (image)

| Word | Stack | Args |
|------|-------|------|
| `IMG_LoadTexture` | `( a z -- a )` | 2 |

### Internal words (image)

| Word | Stack | What it does |
|------|-------|--------------|
| `try-image` | `( a n -- img, or 0 )` | Load a texture from a file name, 0 when it fails. |
| `srect` | `( -- a )` | Source `SDL_Rect` buffer used by `draw-part`. |
| `isize` | `( -- a )` | 8 byte buffer used by `image-size`. |

## Text (sdl2-ttf)

Type `sdl2-ttf` once. Text is drawn in the current color.

| Word | Stack | What it does |
|------|-------|--------------|
| `load-font` | `( a n pt -- font )` | Open a TrueType file at pt points, initializing SDL_ttf the first time. Throws on failure. |
| `font` | `( font -- )` | Choose the font used by `text` and `text-size`. |
| `free-font` | `( font -- )` | Close the font. |
| `text` | `( x y a n -- )` | Draw the string with the current font and color, top left at x y. Builds a texture at each call: fine for scores and menus. |
| `text-size` | `( a n -- w h )` | Size the string would take with the current font. |
| `sdlttf` | `( z n "name" -- )` | Handle of libSDL2_ttf made by `shared-library`. |

### Raw C bindings (text)

The `SDL_Color` argument of `TTF_RenderUTF8_Blended` is passed packed,
as `pen`.

| Word | Stack | Args |
|------|-------|------|
| `TTF_Init` | `( -- n )` | 0 |
| `TTF_Quit` | `( -- void )` | 0 |
| `TTF_OpenFont` | `( z n -- a )` | 2 |
| `TTF_CloseFont` | `( a -- void )` | 1 |
| `TTF_SizeUTF8` | `( a z a a -- n )` | 4 |
| `TTF_RenderUTF8_Blended` | `( a z n -- a )` | 3 |

### Internal words (text)

| Word | Stack | What it does |
|------|-------|--------------|
| `current-font` | `( -- font )` | Value. Font chosen by `font`. |
| `tsize` | `( -- a )` | 8 byte buffer used for sizes. |

## Sound (sdl2-mixer)

Type `sdl2-mixer` once. Not to be confused with `beep`, which is in the
core.

| Word | Stack | What it does |
|------|-------|--------------|
| `soundfont` | `( a n -- )` | Use a soundfont file (.sf2 or .sf3) for MIDI music. The name is tried as given, then without its directories in the current directory, then in the directory of the script, so a soundfont put beside the program is found from anywhere. Throws if not found. Without it FluidSynth uses the soundfont of the system. |
| `soundfont?` | `( a n -- f )` | Same, but returns a flag instead of throwing: true when the file was found and set. |
| `load-sound` | `( a n -- snd )` | Load a WAV, OGG, MP3 or FLAC effect. Opens the mixer the first time. Throws on failure. |
| `play` | `( snd -- )` | Play the sound on a free channel. Sounds overlap and are mixed. |
| `free-sound` | `( snd -- )` | Free the sound. |
| `sound-volume` | `( n -- )` | Volume of all channels, 0 to 128. |
| `stop-sounds` | `( -- )` | Stop every playing sound. |
| `sounds-playing` | `( -- n )` | Number of channels currently playing. |
| `load-music` | `( a n -- mus )` | Load a music file. Opens the mixer the first time. Throws on failure. |
| `music` | `( mus -- )` | Play the music, looping until `stop-music`. |
| `music-once` | `( mus -- )` | Play the music one time. |
| `stop-music` | `( -- )` | Stop the music. |
| `music-volume` | `( n -- )` | Music volume, 0 to 128. |
| `music?` | `( -- f )` | True while music plays. |
| `free-music` | `( mus -- )` | Free the music. |
| `sdlmix` | `( z n "name" -- )` | Handle of libSDL2_mixer made by `shared-library`. |

### Constants (sound)

| Constant | Value |
|----------|-------|
| `MIX_RATE` | `44100` |
| `MIX_CHANNELS` | `2` |

### Raw C bindings (sound)

| Word | Stack | Args |
|------|-------|------|
| `Mix_OpenAudio` | `( n n n n -- n )` | 4 |
| `Mix_CloseAudio` | `( -- void )` | 0 |
| `Mix_LoadWAV_RW` | `( a n -- a )` | 2 |
| `Mix_FreeChunk` | `( a -- void )` | 1 |
| `Mix_PlayChannelTimed` | `( n a n n -- n )` | 4 |
| `Mix_Volume` | `( n n -- n )` | 2 |
| `Mix_HaltChannel` | `( n -- n )` | 1 |
| `Mix_Playing` | `( n -- n )` | 1 |
| `Mix_LoadMUS` | `( z -- a )` | 1 |
| `Mix_FreeMusic` | `( a -- void )` | 1 |
| `Mix_PlayMusic` | `( a n -- n )` | 2 |
| `Mix_HaltMusic` | `( -- n )` | 0 |
| `Mix_VolumeMusic` | `( n -- n )` | 1 |
| `Mix_PlayingMusic` | `( -- n )` | 0 |
| `Mix_SetSoundFonts` | `( z -- n )` | 1 |

### Internal words (sound)

| Word | Stack | What it does |
|------|-------|--------------|
| `path-buf` | `( -- a )` | 512 byte buffer holding the name found beside the script. |
| `beside-script` | `( a n -- a' n' f )` | The file name in the directory of the running script; f is true when that file exists. |
| `find-file` | `( a n -- a' n' f )` | Look for a file as given, then by its name alone in the current directory, then beside the script. |
| `mixer-init` | `( -- )` | Open the mixer at 44100 Hz, 16 bit, stereo, if not already open. |
| `mixer-open?` | `( -- f )` | True when the mixer is open. |
| `mixer-open` | `( -- n )` | Value. Mixer open flag. |
| `mixer-close` | `( -- )` | Close the mixer. Installed in `close-hook`. |

## Tunes (sdl2-abc)

Type `sdl2-abc` once (it loads `sdl2-mixer` if needed). A tune written
in ABC notation (see abcnotation.com) is read, turned into a MIDI file
in a temporary file (deleted once loaded) and played by SDL2_mixer
through FluidSynth, with the soundfont given by `soundfont` or the one
of the system. The same words exist in the web build, where simple
oscillators play the notes.

| Word | Stack | What it does |
|------|-------|--------------|
| `abc` | `( a n -- )` | Read a tune written in ABC notation (a string, several lines). Understood: the fields K: (key and mode), L:, M:, Q:, the line `%%MIDI program n`; notes `A`-`G` and `a`-`g` with `^ _ =` and `, '`, lengths like `2`, `/2`, `3/2`; rests `z x`; chords `[CEG]`; ties `-`; broken rhythm `> <`; tuplets `(3`; bar lines. Words, decorations, grace notes, repeats, voices and other fields are skipped: one voice, a melody line. Sets `tune-bpm` and `tune-program` from the tune. Use `r~ ... ~` for a multi-line string, as ABC uses the bar character. |
| `tune-play` | `( -- )` | Play the tune read by `abc` once, as MIDI through the soundfont. |
| `tune-loop` | `( -- )` | Play it again and again until `tune-stop`. |
| `tune-stop` | `( -- )` | Stop the tune. |
| `tune-playing?` | `( -- f )` | True while the tune plays. |
| `abc-play` | `( a n -- )` | `abc` then `tune-play`. |
| `abc-loop` | `( a n -- )` | `abc` then `tune-loop`. |
| `tune-notes` | `( -- n )` | Number of notes of the tune (each note of a chord counts). At most 1024 are kept. |
| `tune-ms` | `( -- ms )` | Length of the tune in milliseconds at `tune-bpm`. |
| `tune-bpm` | `( -- n )` | Value. Quarter notes per minute, set by `Q:` (default 120). Change it after `abc` to play faster or slower. |
| `tune-program` | `( -- n )` | Value. General MIDI instrument 0 to 127, set by `%%MIDI program` (default 0). Change it after `abc` to choose another. |
| `tune-midi` | `( -- a n )` | The tune as the bytes of a standard MIDI file (format 0, 480 ticks per quarter note). |
| `tune-save` | `( a n -- )` | Write the tune as a `.mid` file with that name. |

### Internal words (tunes)

The reader itself lives in the vocabulary `abc-int`, which is not
searched by default.

| Word | Stack | What it does |
|------|-------|--------------|
| `abc-int` | `( -- )` | Vocabulary holding the reader of ABC notation: about 80 words (`token`, `note`, `chord`, `read-key`, the arrays `nt nd nk` of the notes...). Kept apart so they do not hide your own names; `also abc-int words` lists them. |
| `smf` | `( -- a )` | 32 KB buffer where the MIDI file is built. |
| `smf-n` | `( -- n )` | Value. Bytes written in `smf`. |
| `smf-t` | `( -- n )` | Value. Time of the last event written, in ticks. |
| `smf-len` | `( -- n )` | Value. Where the length of the track is written. |
| `sb,` | `( byte -- )` | Append a byte to the MIDI file. |
| `sbe32,` | `( n -- )` | Append a 32 bit number, most significant byte first. |
| `(vlq)` | `( n flag -- )` | Append a variable length number, high groups first. |
| `vlq,` | `( n -- )` | Append a MIDI variable length number. |
| `emit-group` | `( first -- next )` | Write the notes that start together: all on, then all off. |
| `build-smf` | `( -- )` | Build the MIDI file of the tune in `smf`. |
| `tune-music` | `( -- mus )` | Value. The music being played, 0 for none. |
| `tune-path` | `( -- a n )` | Make a new empty file with a name of its own in `/tmp` (with `mkstemps`, so it cannot clash with another user or run) and give its name. |
| `tune-load` | `( -- )` | Write the MIDI file to a temporary file, load it as music and delete the file. |
| `c-mkstemps` | `( z n -- n )` | C `mkstemps`: makes a unique file from a template ending in XXXXXX. |
| `c-close` | `( n -- n )` | C `close`. |
| `tmpl` | `( -- a )` | 64 byte buffer for the template and name of the temporary file. |
| `tune-code` | `( -- n )` | Value. Error code kept while the temporary file is deleted. |

## Drawing at the prompt, and the boards (rpi, efi, web)

`graphic` makes the drawing words work at the prompt of the bare-metal
console (Raspberry Pi, PC UEFI), of the console window (`--gui`) and of
the web terminal, without `screen`: a drawing area is shown under (web:
above) the text, which stays usable. `0 0 100 100 line`, `red color`,
`cls`. The words below exist in the short words of those builds; they
are listed here with the others.

### Drawing area

| Word | Stack | What it does |
|------|-------|--------------|
| `graphic` | `( -- )` | Puts `sdl2` in the search order and opens the drawing area (`gfx-on`), 640 by 480 logical pixels. |
| `gfx-on` | `( -- )` | Opens the drawing area under the text of the console (bare metal) and sets `width`, `height` and `gfx-mode`. |
| `graphic-size` | `( w h -- )` | Opens the drawing area with another size, up to 1920 by 1440. The console screen itself is 640 by 480 times `console-zoom`. Web: `graphic-on` does the same with 640 by 480. |
| `gfx-mode` | `( -- f )` | Value. True (-1) while the drawing words work under the console. |
| `gfx-cls` | `( -- )` | At the prompt: clears the drawing area to black, whatever the pen. `cls` calls it. |
| `graphic-on` | `( -- )` | Web only: `640 480 graphic-size`. |

### Keys on the boards (internal)

The serial line of the boards is read as a stream of characters, without
key releases (a USB keyboard is read by C code, see `pressed?`): a key
counts as held for `key-hold` milliseconds after its last press.

| Word | Stack | What it does |
|------|-------|--------------|
| `key-hold` | `( -- n )` | Value, default 120. How long a key counts as held after its last press, in ms. |
| `key-times` | `( -- a )` | Array of 256 cells: the time (`ticks`) of the last press of each key code. |
| `key-hit` | `( k -- )` | Records the press of key k: sets `last-key` and `key-times`. |
| `forget-keys` | `( -- )` | Marks every key as not pressed. |
| `key-within` | `( ms -- k\|-1 )` | Waits at most ms for a byte from the serial line: the byte, or -1 if none comes in time. |
| `one-key` | `( c -- )` | Records one character c received on the serial line as a key press (`key-hit`): letters become lower case, 10 is `key-enter`, 127 `key-backspace`, ESC starts `escape`. |
| `escape` | `( -- c\|-1 )` | After an ESC: reads what follows, an arrow key or the Esc key. |
| `arrow` | `( c -- )` | Records the arrow key named by the letter that ends an escape sequence: A `key-up`, B `key-down`, C `key-right`, D `key-left`. |
| `ticks>ms` | `( ticks -- ms )` | Converts the duration of a note in ticks to milliseconds, from `tune-bpm` (ABC tunes). |
| `tune-level` | `( -- n )` | Value, default 2500. Amplitude of the notes of a tune, 0 to 32767. |
| `tune-send` | `( loop -- )` | Sends the notes of the current tune to the mixer, once (0) or looping (-1). Used by `tune-play` and `tune-loop`. |

### Sound on the PC (efi)

| Word | Stack | What it does |
|------|-------|--------------|
| `sound-off` | `( -- )` | Stops the sound (the Intel HD Audio controller is reset) and writes `sound = off` in `lugforth.ini` at the root of the stick: read at boot. For a PC whose codec makes noise. |
| `sound` `sound-on` | `( -- )` | Allows the sound again, writes `sound = on`. The controller starts at the next sound word. |
| `snd-ok` | `( -- f )` | Starts the sound if needed and tells whether a controller answered. |

## Aliases

Other names for the same word, as other Forths and BASICs call them. An
alias is a plain colon definition that calls the word, so it works the
same everywhere `sdl2` does (Linux, web, rpi, efi). Add new ones here.

Where to change them: an alias is one line, `: plot ( x y -- ) dot ;`,
written right after the word it renames in each of the three sources of
the short words, which must stay equal:

| Build | Source |
|-------|--------|
| Linux (and the console window) | `ports/posix/sdl2.fs` |
| Raspberry Pi and PC (UEFI) | `ports/baremetal/sdl2.fs` |
| Web | `ports/web/sdl2.fs` (the web build also has a stub below, `: plot graphic s" plot" evaluate ;`, so that the word works at the prompt before `graphic`) |

Then add the row to the table above, and the word to the alphabetical
index at the end of this page.

Where the other sections of this page come from: this file is written by
hand, section by section, after the sources: Core, Images, Text, Sound
and Tunes (`ports/posix/sdl2.fs`, `sdl2_image.fs`, `sdl2_ttf.fs`,
`sdl2_mixer.fs`, `sdl2_abc.fs`); the words of the boards and the web
page (`ports/baremetal/sdl2.fs`, `ports/web/sdl2.fs`,
`ports/web/sdl2_abc.fs`); `sound-off` and `snd-ok`
(`ports/efi/sound_ini.fs`, `ports/efi/hda.h`). The generated list of the
other words is `docs/en/WORDS.md` (see `docs/en/dev/PITFALLS.md`,
"Adding words").

| Alias | Same as | Why |
|-------|---------|-----|
| `plot` | `dot` `( x y -- )` | Forth and BASIC programs say `plot` for one pixel. |

## Alphabetical index

`abc` `abc-loop` `abc-play` `active-hook` `alpha` `arrow` `audio-open`
`audio-open?` `beep` `beep-volume` `beeping?` `black` `blue` `box`
`brown` `c-close` `c-mkstemps` `circle` `close-screen` `cls` `color`
`cursor` `cyan` `delay` `disc` `dot` `draw` `draw-part` `draw-size`
`drum-hit` `dt` `escape` `events` `flip` `font` `forget-keys` `frame`
`free-font` `free-image` `free-music` `free-sound` `fullscreen`
`gfx-cls` `gfx-mode` `gfx-on` `graphic` `graphic-on` `graphic-size`
`gray` `green` `height` `hline` `image-alpha` `image-size` `image-tint`
`IMG_LoadTexture` `ink` `key-backspace` `key-down` `key-enter` `key-esc`
`key-hit` `key-hold` `key-left` `key-right` `key-space` `key-tab`
`key-times` `key-up` `key-within` `last-key` `latency` `LEFT-BUTTON`
`line` `load-font` `load-image` `load-music` `load-sound` `magenta`
`MIDDLE-BUTTON` `Mix_CloseAudio` `Mix_FreeChunk` `Mix_FreeMusic`
`Mix_HaltChannel` `Mix_HaltMusic` `Mix_LoadMUS` `Mix_LoadWAV_RW`
`Mix_OpenAudio` `Mix_PlayChannelTimed` `Mix_Playing` `Mix_PlayingMusic`
`Mix_PlayMusic` `Mix_SetSoundFonts` `Mix_Volume` `Mix_VolumeMusic`
`mouse-x` `mouse-y` `music` `music-once` `music-volume` `music?` `noise`
`one-key` `orange` `pen` `pen-a` `pen-b` `pen-g` `pen-r` `pink`
`pitch-drop` `pixel@` `play` `plot` `pressed?` `purple` `quit!` `quit?`
`red` `render-hook` `renderer` `rgb` `rgba` `RIGHT-BUTTON` `sample-rate`
`saw` `scan?` `screen` `screen-open?` `sdl` `sdl-error` `sdl-shutdown`
`SDL_ClearQueuedAudio` `SDL_CloseAudioDevice` `SDL_CreateRenderer`
`SDL_CreateTexture` `SDL_CreateTextureFromSurface` `SDL_CreateWindow`
`SDL_Delay` `SDL_DestroyRenderer` `SDL_DestroyTexture`
`SDL_DestroyWindow` `SDL_FreeSurface` `SDL_GetError`
`SDL_GetKeyboardState` `SDL_GetMouseState` `SDL_GetQueuedAudioSize`
`SDL_GetTicks` `SDL_Init` `SDL_InitSubSystem` `SDL_OpenAudioDevice`
`SDL_PauseAudioDevice` `SDL_PollEvent` `SDL_PushEvent`
`SDL_QueryTexture` `SDL_QueueAudio` `SDL_RenderClear` `SDL_RenderCopy`
`SDL_RenderDrawLine` `SDL_RenderDrawPoint` `SDL_RenderDrawRect`
`SDL_RenderFillRect` `SDL_RenderPresent` `SDL_RenderReadPixels`
`SDL_RenderSetLogicalSize` `SDL_RWFromFile` `SDL_SetHint`
`SDL_SetRenderDrawBlendMode` `SDL_SetRenderDrawColor`
`SDL_SetTextureAlphaMod` `SDL_SetTextureBlendMode`
`SDL_SetTextureColorMod` `SDL_SetWindowFullscreen` `SDL_SetWindowTitle`
`SDL_ShowCursor` `SDL_UpdateTexture` `SDL_WaitEvent` `sdlimg` `sdlmix`
`sdlttf` `setting` `silence` `sine` `snd-ok` `sound` `sound-off`
`sound-on` `sound-volume` `soundfont` `soundfont?` `sounds-playing`
`square` `stop-music` `stop-sounds` `text` `text-size` `ticks`
`ticks>ms` `title` `triangle` `TTF_CloseFont` `TTF_Init` `TTF_OpenFont`
`TTF_Quit` `TTF_RenderUTF8_Blended` `TTF_SizeUTF8` `tune-bpm`
`tune-level` `tune-loop` `tune-midi` `tune-ms` `tune-notes` `tune-play`
`tune-playing?` `tune-program` `tune-save` `tune-send` `tune-stop`
`typed` `unpress` `wait` `wave` `wheel` `white` `width` `window-handle` `yellow` `zoom`

