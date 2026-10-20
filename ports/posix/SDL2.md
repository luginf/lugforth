# SDL2 for the POSIX build

Windows, drawing, keyboard, mouse, images, text and sound for games,
through the shared libraries of SDL2. Nothing is linked into `lugforth`:
the libraries are opened at run time (`dlopen`) the first time you use them.

```forth
sdl2                          \ load, sdl2 becomes your vocabulary
3 to zoom  320 240 screen     \ 320x240 logical pixels, shown 3 times bigger
: frame  events  black color cls  red color 10 10 50 50 box  flip ;
: run  begin frame quit? until close-screen ;
run
```

Examples: `sdl2_demo.fs` (tour of the words), `sdl2_breakout.fs` (a game, about
200 lines), `sdl2_drums.fs` (16-step drum machine), `fm/sdl2_fm.fs` (two operator FM synthesizer, MIDI in, kept out of the interpreter: see `fm/README.md`), `sdl2_pianoroll.fs` (piano
roll with soundfont playback and export to `.mid` and ABC, POSIX only),
`sdl2_roguelike.fs` (tile sheet, with music), `sdl2_3d.fs` (turning wireframe cube, pyramid and octahedron), `sdl2_2048.fs` (2048 with a slide and pop animation, `--no-anim` turns it off; digits drawn from a 3x5 bitmap), `pinball/pinball.fs` (a pinball in the
style of Night Mission, fixed point physics). Breakout, drums, the FM synthesizer and roguelike also
run in the browser.

All words: [SDL2-words.md](SDL2-words.md). The short words also exist in the web
build, see [../web/SDL2.md](../web/SDL2.md).

## Packages

| Loader        | Library needed       | Debian/Ubuntu package  | Adds                        |
|---------------|----------------------|------------------------|-----------------------------|
| `sdl2`        | libSDL2-2.0.so.0     | `libsdl2-2.0-0`        | window, drawing, input, beeps |
| `sdl2-image`  | libSDL2_image-2.0.so.0 | `libsdl2-image-2.0-0` | sprites (PNG, JPG, BMP...)  |
| `sdl2-ttf`    | libSDL2_ttf-2.0.so.0 | `libsdl2-ttf-2.0-0`    | text with TrueType fonts    |
| `sdl2-mixer`  | libSDL2_mixer-2.0.so.0 | `libsdl2-mixer-2.0-0` | sound files, music, mixing, soundfont |
| `sdl2-abc`    | (`sdl2-mixer`, and a soundfont for MIDI) | `fluid-soundfont-gm` or your own `.sf2` | tunes in ABC notation |

Typing a loader again puts `sdl2` back on top. All words below live in the
`sdl2` vocabulary, with the raw C names (`SDL_Init`, ...) for anything without
a short word. `sdl2` is also where your definitions go.

## Short words (`sdl2`)

Screen: `screen ( w h -- )`, `close-screen`, `zoom` (value, set before `screen`),
`title ( a n -- )`, `fullscreen ( f -- )`, `cursor ( f -- )`, `width`, `height`.

Colors: `color ( $rrggbb -- )`, `rgb ( r g b -- )`, `rgba ( r g b a -- )`,
`alpha ( a -- )`, and named colors `black white red green blue yellow cyan
magenta orange purple pink brown gray` (`red color`), and `ink ( n -- )`, a color by
number from 0 to 15 (0 black, 1 red, 2 green, 3 yellow, 4 blue, 5 magenta, 6 cyan, 7 white...).

Drawing: `cls`, `dot ( x y -- )`, `line ( x1 y1 x2 y2 -- )`, `box ( x y w h -- )`,
`frame ( x y w h -- )`, `hline ( x y w -- )`, `disc ( cx cy r -- )`,
`circle ( cx cy r -- )`, `flip`, `pixel@ ( x y -- $rrggbb )`.

Frame loop: `events` (call once per frame), `quit?`, `quit!`, `wait`,
`ticks ( -- ms )`, `delay ( ms -- )`, `dt` (ms taken by the last frame).

Input: `pressed? ( key -- f )` with `[char] a`, `key-left key-right key-up
key-down key-space key-enter key-esc key-tab key-backspace`,
`LEFT-BUTTON MIDDLE-BUTTON RIGHT-BUTTON`, `mouse-x`, `mouse-y`, `last-key`,
`wheel`. Same names and meaning as the generic graphics interface.
`scan? ( scancode -- f )` tells whether a key is held by its place on the keyboard
(SDL scancode: 4 is the key A of a QWERTY, 20 is Q...), whatever the layout: use it
for a piano or WASD style row of keys that must not move on an AZERTY keyboard.

Beeps (no library beyond libSDL2): `beep ( freq ms -- )` queues a tone,
so several `beep` in a row play as a melody. `wave` sets the shape
(0 square, 1 triangle, 2 saw, 3 noise, 4 sine), as `color` sets the color: `1 wave`, or the words
`square triangle saw noise sine`. `beep-volume ( n -- )` and `pitch-drop ( n -- )` work the same way; read
a setting back with `wave@`, `beep-volume@`, `pitch-drop@`. `to wave` still works. Also `silence`, `beeping?`.

`play ( a n -- )` queues notes written in MML (Music Macro Language, the `PLAY` of the BASICs of MSX, GW-BASIC
and QBasic): `s" t140 o4 l8 c d e c  e4 g4 r4 >c2" play`.

| Command | Meaning |
|---|---|
| `c d e f g a b` | a note; `#` or `+` after it sharpens, `-` flattens |
| a number after a note | its length: 4 quarter, 8 eighth, 1 whole; a dot adds half (`c4.`) |
| `r` or `p` | a rest (same length syntax) |
| `o4` | the octave, 0 to 8; `o4 c` is the middle C (262 Hz) |
| `<` `>` | one octave down, up |
| `l8` | the default length |
| `t120` | the tempo, in quarters a minute |
| `n49` | a note by number: n1 is the lowest C (octave 0), n49 is the middle C; n0 is a rest |
| `&` | ties a note to the next one of the same pitch: `c4&c8` |
| `v8` | the volume, 0 to 15 (it sets `beep-volume`) |
| `w1` | the wave, as `1 to wave` (not part of the usual MML) |

Spaces are free; the settings stay from one `play` to the next. Differences from the dialects: the octave
numbering is the MSX one (`o4 c` is the middle C; check a tune written for another dialect, they differ); no chords, no several voices, no
`m` modes (legato, staccato) and no `x` (play a string variable). It works at the prompt after `sdl2` (the console
window, the boards, the web page).

Hits: `drum-hit ( freq ms -- )` starts at once and plays over other sounds (8 at a
time), fading out; `pitch-drop` (percent) makes the pitch fall, enough for a
kick drum. Call `flip` or `delay` regularly, as a game loop does. Use `beep` or
`drum-hit` in a program, not both.

## Images (`sdl2-image`)

`load-image ( a n -- img )`, `free-image`, `image-size ( img -- w h )`,
`draw ( img x y -- )`, `draw-size ( img x y w h -- )`,
`draw-part ( img sx sy sw sh x y w h -- )` for sprite sheets,
`image-alpha`, `image-tint`. Load images after `screen`. A name with
directories that is not found is tried again without them, so
`s" examples/tiles.png" load-image` also works from inside `examples/`.

## Text (`sdl2-ttf`)

`load-font ( a n pt -- font )`, `font ( font -- )`, `free-font`,
`text ( x y a n -- )` in the current color, `text-size ( a n -- w h )`.

## Sound (`sdl2-mixer`)

`load-sound ( a n -- snd )`, `play ( snd -- )`, `free-sound`, `sound-volume`,
`stop-sounds`, `sounds-playing`, `load-music ( a n -- mus )`,
`music ( mus -- )` (loops), `music-once`, `stop-music`, `music-volume`,
`music?`, `free-music`. Overlapping sounds are mixed.

Music files: OGG, MP3, FLAC, Opus, WAV, MOD and MIDI, as far as your
SDL2_mixer has the decoders. MIDI needs a soundfont: `soundfont ( a n -- )` sets
a `.sf2` or `.sf3` (`soundfont?` returns a flag instead of throwing). The name
is tried as given, then in the current directory, then in the script's
directory, so `s" mygame.sf2" soundfont` finds a file beside the program.
Without one, FluidSynth uses the system soundfont if there is one.

## Tunes (`sdl2-abc`)

Tunes written in [ABC notation](https://abcnotation.com/), a text format for
melodies, can be played with the soundfont. Type `sdl2-abc` once (it loads
`sdl2-mixer` if needed). Since ABC uses the bar character, write the tune with
`r~ ... ~`, a multi-line string that ends at a tilde:

```forth
sdl2-abc
s" gm.sf2" soundfont?  drop        \ optional, a soundfont beside the program
: tune r~ X:1
T:Scale
M:4/4
L:1/4
Q:1/4=120
K:C
%%MIDI program 0
C D E F | [CEG]2 z2 |
~ ;
tune abc-play                      \ once; abc-loop repeats; tune-stop stops
```

`abc ( a n -- )` reads the tune; `tune-play`, `tune-loop`, `tune-stop` and
`tune-playing?` play it; `abc-play` and `abc-loop` do both. After `abc` you can
change `tune-bpm` and `tune-program` (General MIDI instrument), read `tune-ms`
and `tune-notes`, or write a MIDI file with `tune-save`.

Understood: `K:` `L:` `M:` `Q:` and `%%MIDI program n`; notes, accidentals,
octave marks, lengths, rests, chords, ties, broken rhythm, tuplets, bar lines.
Skipped: chord symbols, decorations, grace notes, comments, lyrics. Not
supported: repeats, several voices, tempo changes. At most 1024 notes. The web
build has the same words with a simple synthesizer.

## Console window (`sdl2_console.fs`)

In the console window, `graphic` makes the drawing words work at the prompt (`0 0 100 100 line`):
a 640x480 texture receives them, drawn under the text, centered, in proportion; `cls` clears it, `flip`
redraws the window. `w h graphic-size` sets the size of the texture (16 to 4096), kept in proportion in the window. A program that calls `screen` ends it. The pen is saved and restored around each redraw.

`lugforth --gui [file.fs]` opens a console window (`type` and `key` go to an SDL window). The
code is part of the binary: `gui-load` loads it, then `desktop console` starts it (Android
uses `gui-load smooth-font console`). Without SDL2 or a display, `--gui` says so and keeps
the terminal.

The menu is hidden: the button at the top right, or F10, shows it (a right click on the menu or on the button toggles it too) (Examples, View, Colors,
Font, Help). Examples lists the `.fs` files of `LUGFORTH_EXAMPLES` (default: the current
directory). Colors has a color picker for the text and for the background (a square of saturation and brightness, a strip of hues, the hex value) and a few themes. Alt+Enter, or View, toggles full screen. Font lists the fixed width
TrueType and OpenType fonts found in `/usr/share/fonts`, `~/.local/share/fonts` and `~/.fonts` (the wheel scrolls a long menu). View also has
More margin and Less margin (`margin=` in the `.ini`, in pixels). Also: mouse selection (copied), middle click or Ctrl+V to paste, wheel
or PageUp/PageDown to scroll back, Ctrl+wheel, Ctrl +/-, Ctrl 0 for the text size. ASCII only.

Settings are kept in `lugforth.ini` in `~/.local/share/lugforth` (or `LUGFORTH_HOME`):

```
size=15
fg=D2D2D2
bg=000000
font=/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf
margin=12
fullscreen=Alt+Enter
```

`fullscreen=` is the key of full screen: `Alt+Enter`, `F11`, `Ctrl+Shift+F`...

`font=` wins over `LUGFORTH_FONT`, then DejaVu Sans Mono and other common fonts of the
system. `ports/posix/sdl2_console_tests.fs` tests the settings (dummy drivers, like the
other SDL tests).

## Limits

* Calls pass integers and pointers only: no floats or structs by value
  (`SDL_RenderCopyEx`, rotation...).
* No callbacks from C into Forth: audio uses queues (`beep`, `drum-hit`) or the mixer.
* Mouse positions are in logical pixels, whatever the `zoom`.
* `pressed?` reads the held state once per `events`; a press shorter than one
  frame is missed.
* Names ignore case, so the function `SDL_Quit` is bound as `sdl-shutdown`;
  `SDL_QUIT` is the event constant.

## Tests

They need the libraries, so `ninja posix` does not run them:

```sh
SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy out/posix/lugforth ports/posix/sdl2_tests.fs
```

Same for `sdl2_media_tests.fs` and `sdl2_abc_tests.fs`.
