# Features and examples: what exists and how it was tested

Editors, SDL2 words, games and ports of examples, with what was and was not checked. Part of [README.md](../../README.md).

## SDL2 multimedia

Short game-oriented words on top of libSDL2, for the POSIX build and (same source, same words) the web build. Full docs: `docs/SDL2.md`, `docs/SDL2-words.md` (every word), `docs/SDL2-web.md`. Examples: `examples/graphics/sdl2_demo.fs`, `examples/games/sdl2_breakout.fs` and `examples/music/sdl2_drums.fs` (a 16 step drum machine); these two also run unchanged in the browser through `ports/web/sdl2_breakout.html` and `ports/web/sdl2_drums.html`. `examples/games/sdl2_roguelike.fs` uses the tile sheet `examples/games/sdl2_tiles.png` (drawn by `examples/games/make_sdl2_tiles.fs`, a lugforth program that writes the PNG by hand with CRC-32, Adler-32 and a stored zlib block: no outside assets; `examples/games/make_sdl2_tiles.py` is the same program in Python, kept to compare the two, same pixels) through `sdl2-image` (`draw-part`, `image-tint`); `ports/web/sdl2.fs` has the same image words on canvas and `ports/web/sdl2_roguelike.html` runs the same file. `load-image` retries a missing name without its directories on both platforms. `examples/music/fm/` is a two operator FM synthesizer with a keyboard on screen and MIDI input, on both platforms, deliberately kept out of the interpreter (see its README); the interpreter only has the hooks `render-hook`, `active-hook`, `latency` and `scan?`. `examples/music/sdl2_pianoroll.fs` is a piano-roll sequencer (4 tracks, chainable 16-step patterns, MIDI+soundfont playback, `.mid` and ABC export); POSIX only, not ported to the web build.

### Files


- `ports/posix/sdl2.fs` - core, lazy loaded: typing `sdl2` compiles the vocabulary. Extras loaded the same way: `sdl2-image` (`ports/posix/sdl2_image.fs`), `sdl2-ttf`, `sdl2-mixer`. All are pulled in by `ports/posix/posix_boot.fs`.
- `ports/web/sdl2.fs` - same vocabulary name and same words on a canvas (JS state in `context.sdl`, `JSWORD:` primitives). Only the short words exist there: no raw `SDL_` words, no image/ttf/mixer, no `wait sdl-error window-handle renderer pen sample-rate`. Pulled in by `ports/web/web_boot.fs`; `ports/web/BUILD` copies the example pages and files to `out/web`.
- Tests are NOT in the ninja build. Run by hand:
  `SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy out/posix/lugforth ports/posix/sdl2_tests.fs` (22 tests) `ports/posix/sdl2_media_tests.fs` (7 tests), `ports/posix/sdl2_abc_tests.fs` (18 tests) and `examples/music/fm/sdl2_fm_tests.fs` (24 tests: MIDI parser and ALSA events, voices, pitch). `common/forth_namespace_tests.fs` lists the new vocabularies.

### Music: soundfont and ABC tunes

- `ports/posix/sdl2_mixer.fs` has `soundfont ( a n -- )` and `soundfont?` (`Mix_SetSoundFonts`): a `.sf2` or `.sf3` file, looked for as given, then by base name in the current directory, then in the directory of the script (`1 argv`). FluidSynth reads only files, so a soundfont cannot come from memory. Loading a MIDI from memory (`SDL_RWFromConstMem`, `Mix_LoadMUS_RW`) failed with "Unrecognized audio format" and was not pursued.
- `sdl2-abc` (`ports/posix/sdl2_abc.fs`, `ports/web/sdl2_abc.fs`) reads tunes in ABC notation and plays them. The reader is shared: `common/abc.fs` defines `abc-parser` in `internals`, a word that returns the reader's source as a string, which each platform loader evaluates (words in vocabulary `abc-int`, public ones in `sdl2`). POSIX writes a standard MIDI file to a temporary file made by `mkstemps` (a fixed name in `/tmp` failed when another user had made it), deletes it once loaded and plays it with the mixer; the web loader schedules WebAudio oscillators. Tests: `ports/posix/sdl2_abc_tests.fs` (18). A tune is written with `r~ ... ~` because ABC uses `|`.

## Editors: `ed` and `writhdeck` (ports/posix/editor.fs, writhdeck.fs, *_tests.fs)

`ed` is a text editor in Forth; `writhdeck` is a port of WrithDeck (original in `~/src/writhdeck`, Tcl/C,
manual `writhdeck_MANUAL.md`) built on top of it. Both are strings compiled on first use (`ed-src`,
`wd-src`), so no `|` inside the source.

Done in writhdeck: wrapped lines in a centered column, focus mode, Hemingway, words and daily goal, stopwatch
and countdown, outline (Esc o), replace all, autosave, txt2tags/Markdown highlighting, themes, Esc menu
(`w f h t o v s r g a + - c x q ?`), word occurrences (Esc v), redo (Ctrl+Y, also in ed), file browser
(`writhdeck` alone or Ctrl+O: Enter, n new, / filter, r rename, d delete, b backup to `backups/`, f favorite, v cycles folder / favorites / recent files; both lists are kept in `writhdeck.rec` beside the ini, lines `F path` and `R path`, 20 favorites and 10 recent files, the files chosen in the browser are noted as recent; `*` marks a favorite in the folder view).
Line numbers (Ctrl+L, ini key `line_numbers`, wrapped view only): shown on the first row of each paragraph, in a 6-column margin; the count of the first line shown is kept from the last draw (`line-at`, `cl-pos`/`cl-line`/`cl-len`), so a key costs a few lines, not a scan of the text (the cache is dropped when the text changes before the cursor, on load and after replace all).
Daily statistics: the words written in each file each day (never lowered, today's earlier sessions included) are kept as lines `S day words path` in `writhdeck.rec` (day = days since 1970, shifted by the ini key `utc_offset` in hours; only today's entries and those of favorites are kept). Noted every 10 s while editing and on Ctrl+S, Ctrl+O, Ctrl+Q. `Esc s` adds "Today, this file / all files"; `s` in the browser lists the last 14 days. Tested in the unit tests (`dstr`, `st-put`) and the pty test, natively and under gforth (`python3 tools/editors_pty_test.py ports/gforth/writhdeck-gforth.fs`: the editors must keep loading there, run it after each change of writhdeck.fs or editor.fs).
PageUp/PageDown work (checked in a pty, ed and writhdeck).

Still to do, from the original (priority order):
1. Repetition detection (same word within a range) and spell check (Hunspell via `hunspell -a` pipe?).
2. Second workspace (F10) and vertical split view (F3/F4).
3. Ctrl+Q in an edited text returns to the browser (now it quits); modal commands `t s w q` of the original.
4. INI profiles/schemes, i18n.
Esc followed at once by a letter is read as Alt+letter: the pty tests wait 0.5 s between them.
Settings: `ed` keeps `editor.ini`, `writhdeck` `writhdeck.ini`, both in the ini format of WrithDeck (Tcl): `key = value`,
`%`/`#` comments, `[sections]` ignored, parser `ini-text` in editor.fs, `ini-kv` / `ini-body` / `ini-defname` / `ini-load` are
deferred and `wd-install` / `ed-plain` swap them. Never the Tcl program's own `~/Documents/writhdeck/writhdeck.ini`
(its `[keys]`, `[schemes]`, `[profiles]` would be lost). Status bar zones `status_left/center/right` with tokens (`session`,
`modes` are ours); `dry` makes `stype` only count, to measure a zone. Tests: `test-wd-ini` writes to out/tests, not to the user's home.
Not run on the boards (no aarch64 or gnu-efi toolchain here).
Both also run on the bare-metal board (ports/rpi, `ed` / `writhdeck` words, files in /lib): the POSIX-only
parts are under `[defined] posix [if] ... [else] ... [then]` inside the source strings, keep them that way.
The editors end with `only forth ...`: a file that is `include`d must save `current @` and put it back
(`current !`) at its end if the caller may have pushed a vocabulary (`sdl2` at the prompt pushes one), as
varvara_rpi.fs does: `definitions` after `previous` would otherwise leave the caller defining in `sdl2`.

## Ports from eForth Windows (Marc Petremann, GPL) - done 2026-10-13

Source: https://github.com/MPETREMANN11/eForth-Windows (clone it into the scratchpad to read). Its eForth is
a ueForth (7.0.7.x), so lugforth (7.0.11) loads most of it as is: `value/to`, `{ }`, `vocabulary`, `:noname`,
floats. Only the Windows parts (GDI, `Kernel32`, `structures` with `L,`) had to be replaced.
Sibling project `~/src/forks/uf-9` did the same for a 16-bit Forth (its `PORTS-EFORTH.txt`).

In `examples/` (GPL, listed in NOTICE; `make -C examples test-ports` runs the logic tests, `make smoke` the SDL2 one):
- `2048.fs` (+ `2048_tests.fs`): terminal game, `2048-play`. Tested in a pty; colors/layout not seen by eye.
- `3d.fs` (+ `3d_tests.fs`) and `sdl2_3d.fs`: math, objects, rotation, perspective; SDL2 drawing checked only with
  the dummy video driver (pixel counts), not on a real screen.
- `inference/` (inference.fs, weather.fs, lamp_diagnosis.fs, tests, Makefile, READMEs): facts, rules, `infer`.
- `sdl2_2048.fs`: SDL2 front end of 2048.fs (bitmap digits, arrows/wasd/mouse swipe, slide+pop animation, `--no-anim`); checked with the dummy driver
  (pixel colors of tiles and digits), not by eye. Could be added to the Android Examples list.
Ideas left: more inference examples from his docs
(`INFERENCE/docs/*.odt`), `3Dmodelisation`/`dumpTool` extras, other upstream projects (lotto, 6809, flog, BMP, HTTP).
Verified in a real X window (display :0) for sdl2_2048: see [PITFALLS.md](PITFALLS.md), last bullets. Gotchas met: `include` is relative to the including file; `if` at top level does not work (wrap in a word);
`<# #s #>` takes ONE number here; `.r`, `random` ( n -- r ) are in `ansi_compat.fs`, `ms-ticks 1 or rand-seed !` seeds it.

## Games after r3forth (examples/games/sdl2_tetris.fs, sdl2_snake.fs, sdl2_minesweeper.fs) - done 2026-10-19

r3forth (https://github.com/phreda4/r3, Pablo Reda, MIT, credited in NOTICE) has its own syntax (`cond? ( ... )`,
`:word`, fixed point 48.16): its code does not load here, the games are rewritten keeping its ideas. Digits from the
3x5 bitmap of breakout, no fonts. `games_tests.fs` (in `make test-ports`) loads them with `tetris-no-run` etc.
defined, which skips the game loop (`[defined] x [if] [else] ... [then]` at the end of each file).
tools/efi_qemu_test.py plays them from the stick (screendump checks). Not seen on the Pi under QEMU (same sdl2.fs).
`color` resets the alpha to 255: set `alpha` after `color`. Edges of keys: `just` keeps the last state per key.
`sdl2_mastermind.fs` (ours, not from r3) follows the same pattern: `mastermind-no-run`, tests in games_tests.fs, efi test.
Other r3 candidates: bdash (Boulder Dash, own sprites needed), sokoban (levels of unclear license), raycaster,
term demos (fire, matrix, torus), util/penner.r3 easing, lib/sdl2poly.r3 polygon fill.

## Forth is Fun (turtle)

Forth is Fun (https://gitlab.com/garvalf/forth-is-fun, Eric Forgeot, MIT/Apache): `examples/graphics/sdl2_turtle.fs` is written after its
forthtoise.fs (same word names, English and French; strokes kept in a list and redrawn each frame), tests in `turtle_tests.fs`
(`make -C examples test-ports`). Running it starts a shell (`logo`): lines typed in the terminal are kept as a program, `go` / `play` run it (compiled as a word `logo-prog`, ; `safe-eval` restores the source after a throw, `evaluate` does not). The shell loop was checked in a pty under Xvfb, not automated. Still to borrow: bresenham, the CYOA (choose your own adventure, also in French), glitch patterns.
Trap met: `fdup f>s s>f f< if` left a wrong value here; round with `fdup f0< if 0.5e f- else 0.5e f+ then f>s`.

## Drawing at the prompt (`graphic`) - done 2026-10-20

`graphic` (loads sdl2 too) makes `0 0 100 100 line`, `red color ... box`, `n ink`, `cls` work at the console
prompt, drawn under the text, as BASIC on a MO5. Baremetal (ports/baremetal, rpi + efi): `gfx_ensure` in screen.h makes a 640x480
layer in `fb_back` (`fb_gfx`), every drawing C function calls it; `con_sync` (console.h) shows it through cell pixels that are 0
(default background, scaled by `con_zoom`); `fb_flip` then only syncs; `fb_open` / `fb_close` end it. Forth: `gfx-on`, `graphic`,
`gfx-mode`, `graphic-size` (GFX-SIZE, `gfx_size` in screen.h; `con_compose` maps screen pixels to layer pixels) and a `cls` that clears to black in sdl2.fs. Posix console (sdl2_console.fs): a target texture `gfx-tex`, `con-draw`
renders it between the background and the cells (pen saved around the redraw), `cls` and `flip` are redefined, `con-key` marks the
window dirty so a drawing made between two keys shows. Tested: efi QEMU test (9 checks), the posix console by hand under Xvfb
(xdotool; `python3 tools/gui_graphic_test.py out/posix/lugforth` automates it), not rpi (no aarch64 toolchain), not the Android app. Web console (ports/web, 2026-10-22): `graphic` puts the drawing area above the text of terminal.html (fixed canvas + spacer, S.layer in sdl2.fs), checked in headless Chromium by CDP on desktop and an emulated phone.

## `play` and waves by name - done 2026-10-20

`play ( a n -- )` (MML, MSX octave numbering, with p n & v w; parser `p-step` and friends, output through the deferred `p-out`, default `beep`) is
written in the three sdl2.fs: ports/posix (tests in sdl2_tests.fs), ports/baremetal, ports/web: keep the copies equal.
`wave`, `beep-volume`, `pitch-drop` are settings (`: setting` = `create , does> !`: `1 wave` and `1 to wave` both store; read with `wave@` etc.), like `color` takes its value; `square triangle saw noise sine` set `wave`. Sine is wave 4 (table in sdl2.fs posix,
`snd_wave` in ports/rpi/sound.h, 'sine' oscillator on the web). A rest is a beep of frequency 0 (silent). Not heard on the
boards or in the browser, only the parsing is tested (posix).

## File manager (`filer`) and desktop (`desk`) - first versions 2026-10-24

`examples/utils/filer.fs` (SDL2 + SDL2_ttf, DejaVuSans path in `font-file`): list of the current folder (folders first, yellow), click or arrows to select,
double click or Enter to open (folder: chdir; `.fs`: `included` after the window closes; other file: `ed`), Backspace = parent, `D` then `Y` deletes a file, wheel scrolls.
The list draws in a rectangle (`fl-w` `fl-h`, origin 0 0) and takes events as calls (`fl-click`, `fl-key`, `fl-wheel`, `fl-open` is a defer), so a desktop can host it.

`examples/utils/desktop.fs` (`lugforth examples/utils/desktop.fs`, or `desk`): 800x600, icons in two columns (`icon`: kind 0 file manager window, 1 a command in a terminal window,
2 a Forth program), double click starts one; windows with title bar, drag, raise, x; taskbar with the windows and Quit. A text window is a small terminal emulator
(`t-*` words: grid of code points + attributes, UTF-8, CSI H J K A B C D G s u m with 16/256 colors, `?25l/h`): `ed` and `writhdeck` run unchanged inside it.
How: `type`, `key`, `key?` and `form-hook` are redirected while the editor runs (`run-in-terminal`), and `key`/`key?` run one frame of the desktop (events, draw, flip)
whenever the queue of keys is empty. So one terminal at a time (a second launch says so in the taskbar), the other windows stay alive. Keys: `on-key` for specials and
Ctrl+letter, SDL_TEXTINPUT for the rest (AZERTY, accents). The x of a terminal sends Ctrl+Q (an unsaved text asks twice). A Forth program (`.fs` from an icon or
from the file manager) closes the desktop, runs with `argc` set to 1 (so `script-bye` does not exit) and the desktop comes back; the program is started with the
desktop words out of the search order (all of them are in the vocabulary `fm`, `desk` and `filer` are in forth).
Tested under Xvfb with xdotool (mousedown/mouseup with a pause, keydown/keyup): icons, file manager window, drag, writhdeck in a window (new file, typing with accents,
Ctrl+S, x), ed, README.md opened from the file manager, Snake, 2048 and the demo launched and left with Esc, Quit. Idle CPU about 5 percent (a frame is drawn only when something changed).
The Forth icon (`repl`) runs the line editor of the console (`edit-accept` with `le-force`, history, arrows, Tab) and `evaluate` in a terminal window; the x types `bye`; `screen` or `graphic` typed there would replace the desktop window (not handled). Not tested: Windows, the web build, the Android app, a second terminal, ed/writhdeck mouse use, wide characters (a character is one cell).
Traps met: a word named `scan`, `top`, `step`, `frame` or `key-down` replaces the system or sdl2 word of the same name, even for the programs that are loaded later (the games use `key-down`);
`{ locals }` in a loop body; `!` writes 8 bytes (use `l!` and `ul@` for 32-bit cells of an array); `0 { x }` is the way to add locals after the arguments;
`catch` does not restore locals; a program that touches the real tty (editor-raw) is stopped when started in a pipeline: use `timeout --foreground` and `< /dev/null` in tests.
Settings (2026-10-24): icons are dragged and their places, the background color (8 swatches) and the size of the desktop (4 sizes, the screen is opened again) are saved as Forth lines in `$HOME/.config/lugforth/desktop.ini` (folders made when missing, room for other settings later) (`dk-bg!`, `dk-size!`, `icon@`, loaded with `included`). One line per icon: `kind s" label" s" command" x y icon@`; kinds 0 file manager, 1 command in a terminal window (`ed`, `writhdeck`, `repl`), 2 Forth program (path from the project folder, absolute if it starts with `/`), 3 settings, 4 SDL Forth console (the desktop closes, the console opens as with `--gui`, `desk` brings the desktop back); the file may be edited by hand. The project folder is found by looking for `examples/utils/desktop.fs` in `./`, `../`, `../../`, `../../../`, so `desk` starts from examples/utils as from the root. Tested under Xvfb (drag, swatch, size, restart from the root, Snake).
Next steps: rename, copy, new folder in the file manager (needs a text field), mouse selection in terminal windows.
