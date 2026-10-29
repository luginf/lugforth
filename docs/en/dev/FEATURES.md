# Features and examples: what exists and how it was tested

Editors, SDL2 words, games and ports of examples, with what was and was
not checked. Part of [README.md](../../../README.md).

## SDL2 multimedia

Short game-oriented words on top of libSDL2, for the POSIX build and
(same source, same words) the web build. Full docs: `docs/en/SDL2.md`,
`docs/en/SDL2-words.md` (every word), `docs/en/SDL2-web.md`. Examples:
`examples/graphics/sdl2_demo.fs`, `examples/games/sdl2_breakout.fs` and
`examples/music/sdl2_drums.fs` (a 16 step drum machine); these two also
run unchanged in the browser through `ports/web/sdl2_breakout.html` and
`ports/web/sdl2_drums.html`. `examples/games/sdl2_roguelike.fs` uses the
tile sheet `examples/games/sdl2_tiles.png` (drawn by
`examples/games/make_sdl2_tiles.fs`, a lugforth program that writes the
PNG by hand with CRC-32, Adler-32 and a stored zlib block: no outside
assets; `examples/games/make_sdl2_tiles.py` is the same program in
Python, kept to compare the two, same pixels) through `sdl2-image`
(`draw-part`, `image-tint`); `ports/web/sdl2.fs` has the same image
words on canvas and `ports/web/sdl2_roguelike.html` runs the same file.
`load-image` retries a missing name without its directories on both
platforms. `examples/music/fm/` is a two operator FM synthesizer with a
keyboard on screen and MIDI input, on both platforms, deliberately kept
out of the interpreter (see its README); the interpreter only has the
hooks `render-hook`, `active-hook`, `latency` and `scan?`.
`examples/music/sdl2_pianoroll.fs` is a piano-roll sequencer (4 tracks,
chainable 16-step patterns, MIDI+soundfont playback, `.mid` and ABC
export); POSIX only, not ported to the web build.

### Files


- `ports/posix/sdl2.fs` - core, lazy loaded: typing `sdl2` compiles the
  vocabulary. Extras loaded the same way: `sdl2-image`
  (`ports/posix/sdl2_image.fs`), `sdl2-ttf`, `sdl2-mixer`. All are
  pulled in by `ports/posix/posix_boot.fs`.
- `ports/web/sdl2.fs` - same vocabulary name and same words on a canvas
  (JS state in `context.sdl`, `JSWORD:` primitives). Only the short
  words exist there: no raw `SDL_` words, no image/ttf/mixer, no
  `wait sdl-error window-handle renderer pen sample-rate`. Pulled in by
  `ports/web/web_boot.fs`; `ports/web/BUILD` copies the example pages
  and files to `out/web`.
- Tests are NOT in the ninja build. Run by hand:
  `SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy out/posix/lugforth ports/posix/sdl2_tests.fs`
  (22 tests) `ports/posix/sdl2_media_tests.fs` (7 tests),
  `ports/posix/sdl2_abc_tests.fs` (18 tests) and
  `examples/music/fm/sdl2_fm_tests.fs` (24 tests: MIDI parser and ALSA
  events, voices, pitch). `common/forth_namespace_tests.fs` lists the
  new vocabularies.

### Music: soundfont and ABC tunes

- `ports/posix/sdl2_mixer.fs` has `soundfont ( a n -- )` and
  `soundfont?` (`Mix_SetSoundFonts`): a `.sf2` or `.sf3` file, looked
  for as given, then by base name in the current directory, then in the
  directory of the script (`1 argv`). FluidSynth reads only files, so a
  soundfont cannot come from memory. Loading a MIDI from memory
  (`SDL_RWFromConstMem`, `Mix_LoadMUS_RW`) failed with "Unrecognized
  audio format" and was not pursued.
- `sdl2-abc` (`ports/posix/sdl2_abc.fs`, `ports/web/sdl2_abc.fs`) reads
  tunes in ABC notation and plays them. The reader is shared:
  `common/abc.fs` defines `abc-parser` in `internals`, a word that
  returns the reader's source as a string, which each platform loader
  evaluates (words in vocabulary `abc-int`, public ones in `sdl2`).
  POSIX writes a standard MIDI file to a temporary file made by
  `mkstemps` (a fixed name in `/tmp` failed when another user had made
  it), deletes it once loaded and plays it with the mixer; the web
  loader schedules WebAudio oscillators. Tests:
  `ports/posix/sdl2_abc_tests.fs` (18). A tune is written with
  `r~ ... ~` because ABC uses `|`.

## Editors: `ed` and `writhdeck` (editors/editor.fs, writhdeck.fs, *_tests.fs)

The editors moved from `ports/posix/` to `editors/` (2026-10-25): the
same two files serve the POSIX boot (`ports/posix/posix_boot.fs`),
Windows, the EFI and Raspberry Pi disks (`lib/`), the release packages
and the gforth loader. `editor.fs` loads `eval_copy.fs` (kept in
`ports/posix/`) only when `eval-copy` is not defined yet: the PC boot
has it already, a board has it in `lib/`. Under gforth `type` and `emit`
are defers read with `defer@`, and `emit` does not go through `type`
(the row cache redirects both there).

`ed` is a text editor in Forth; `writhdeck` is a port of WrithDeck
(original in `~/src/writhdeck`, Tcl/C, manual `writhdeck_MANUAL.md`)
built on top of it. Both are strings compiled on first use (`ed-src`,
`wd-src`), so no `|` inside the source.

Done in writhdeck: wrapped lines in a centered column, focus mode,
Hemingway, words and daily goal, stopwatch and countdown, outline (Esc
o), replace all, autosave, txt2tags/Markdown highlighting, themes, Esc
menu (`w f h t o v s r g a + - c x q ?`), word occurrences (Esc v), redo
(Ctrl+Y, also in ed), file browser (`writhdeck` alone or Ctrl+O: Enter,
n new, / filter, r rename, d delete, b backup to `backups/`, f favorite,
v cycles folder / favorites / recent files; both lists are kept in
`writhdeck.rec` beside the ini, lines `F path` and `R path`, 20
favorites and 10 recent files, the files chosen in the browser are noted
as recent; `*` marks a favorite in the folder view). Speed on a big text
(670 KB): Markdown colors are computed for the lines on the screen only
(`md-window`; a mark never leaves its line; `wd-scroll` marks the colors
stale when `top` or the number of rows changes), not for the whole text
at each key (that was 0.56 s); the word count of the status bar is
recounted at most every 20 times its own cost (`word-count`, the exact
`word-count!` serves the statistics). Ini key `highlight` (yes/no) turns
the colors off.

Only changed rows are sent (wrapped view): `wrap-row` draws a row into a
buffer by redirecting `type` (`rc-type`) and sends it only if it differs
from the last row sent to that line (`rc-cache`, `rc-send`); one key
costs about 170 bytes instead of 1400. The cache is emptied (`rc-reset`)
before and after the Esc menu (every overlay is reached from it), when
the size changes and at each load. Plain (not wrapped) view still
redraws everything. A jump far below the top counts the rows up from the
cursor (`wrap-scroll`), not down from the old top. Ini key `open_last`
(yes/no): with no file given, the first start opens the most recent file
of `writhdeck.rec` if it still exists (the Tcl WrithDeck opens its
browser).

No flicker: `tick` (once a second) redraws only when the timer runs, a
message was set, the word count is stale or the size changed (idle = 0
bytes sent to the terminal; it used to redraw the whole screen each
second), and `wrap-row` writes the text first and clears the rest of the
row after (it cleared the row before writing).

Line numbers (Ctrl+L, ini key `line_numbers`, wrapped view only): shown
on the first row of each paragraph, in a 6-column margin; the count of
the first line shown is kept from the last draw (`line-at`,
`cl-pos`/`cl-line`/`cl-len`), so a key costs a few lines, not a scan of
the text (the cache is dropped when the text changes before the cursor,
on load and after replace all). Cursor position: `writhdeck.rec` also
keeps lines `P position path` (byte position, 50 files, the oldest
dropped), so a file opens where it was left (`cp-start` at load,
`cur-mem` follows the cursor, saved by `stats-hook` and by `cp-flush` at
the end of each editing session). The Tcl WrithDeck keeps its own
`.writhdeck.json` (cursors as line/column, favorites, recents); Lugforth
does not read or write it, on purpose, so as not to risk corrupting it.

Daily statistics: the words written in each file each day (never
lowered, today's earlier sessions included) are kept as lines
`S day words path` in `writhdeck.rec` (day = days since 1970, shifted by
the ini key `utc_offset` in hours; only today's entries and those of
favorites are kept). Noted every 10 s while editing and on Ctrl+S,
Ctrl+O, Ctrl+Q. `Esc s` adds "Today, this file / all files"; `s` in the
browser lists the last 14 days. Tested in the unit tests (`dstr`,
`st-put`) and the pty test, natively and under gforth
(`python3 tools/editors_pty_test.py ports/gforth/writhdeck-gforth.fs`:
the editors must keep loading there, run it after each change of
writhdeck.fs or editor.fs). PageUp/PageDown work (checked in a pty, ed
and writhdeck).

Still to do, from the original (priority order):
1. Repetition detection (same word within a range) and spell check
   (Hunspell via `hunspell -a` pipe?).
2. Second workspace (F10) and vertical split view (F3/F4).
3. Ctrl+Q in an edited text returns to the browser (now it quits); modal
   commands `t s w q` of the original.
4. INI profiles/schemes, i18n. Esc followed at once by a letter is read
   as Alt+letter: the pty tests wait 0.5 s between them. Settings: `ed`
   keeps `editor.ini`, `writhdeck` `writhdeck.ini`, both in the ini
   format of WrithDeck (Tcl): `key = value`, `%`/`#` comments,
   `[sections]` ignored, parser `ini-text` in editor.fs, `ini-kv` /
   `ini-body` / `ini-defname` / `ini-load` are deferred and `wd-install`
   / `ed-plain` swap them. Never the Tcl program's own
   `~/Documents/writhdeck/writhdeck.ini` (its `[keys]`, `[schemes]`,
   `[profiles]` would be lost). Status bar zones
   `status_left/center/right` with tokens (`session`, `modes` are ours);
   `dry` makes `stype` only count, to measure a zone. Tests:
   `test-wd-ini` writes to out/tests, not to the user's home. Not run on
   the boards (no aarch64 or gnu-efi toolchain here). Both also run on
   the bare-metal board (ports/rpi, `ed` / `writhdeck` words, files in
   /lib): the POSIX-only parts are under
   `[defined] posix [if] ... [else] ... [then]` inside the source
   strings, keep them that way. The editors end with `only forth ...`: a
   file that is `include`d must save `current @` and put it back
   (`current !`) at its end if the caller may have pushed a vocabulary
   (`sdl2` at the prompt pushes one), as varvara_rpi.fs does:
   `definitions` after `previous` would otherwise leave the caller
   defining in `sdl2`.

## Ports from eForth Windows (Marc Petremann, GPL) - done 2026-10-13

Source: https://github.com/MPETREMANN11/eForth-Windows (clone it into
the scratchpad to read). Its eForth is a ueForth (7.0.7.x), so lugforth
(7.0.11) loads most of it as is: `value/to`, `{ }`, `vocabulary`,
`:noname`, floats. Only the Windows parts (GDI, `Kernel32`, `structures`
with `L,`) had to be replaced. Sibling project `~/src/forks/uf-9` did
the same for a 16-bit Forth (its `PORTS-EFORTH.txt`).

In `examples/` (GPL, listed in NOTICE; `make -C examples test-ports`
runs the logic tests, `make smoke` the SDL2 one):
- `zmachine.fs` (+ `zmachine_tests.fs`, stories in `zmachine/`:
  `test.inf` bare v3/v5, `cave.inf` a small game with the Inform 6
  library, v5, to play:
  `lugforth examples/games/zmachine.fs examples/games/zmachine/cave.z5`;
  `cave_fr.inf` the same game in French, written for the French library
  of Lionel Ange (gitlab.com/auraes/inform6lib_fr, CC BY-SA, on top of
  the English 6.12.x library; NOT in this repository), compiled with
  Inform 6.45 `-Cu`: see the header of the file; the story brings its
  own alphabet and Unicode table, which `zmachine.fs` reads): Z-machine
  interpreter written from the standard 1.1, one file, versions 3, 4, 5
  and 8, `zplay story.z5`. Text only (no upper window: a v5 status line
  is dropped; the v3 one with `-1 zm-status !`), own save format,
  accents in UTF-8. Checked against `dfrotz` on `test.inf` (v3, v5, v8)
  and on a small Inform 6 library game (v5, v8: same text, save,
  restore, restart). About 2 million instructions a second. Also runs
  under gforth 0.7.9 (`gforth ports/gforth/zmachine-gforth.fs story.z5`,
  tests: `gforth examples/games/zmachine_tests.fs`): three
  `[defined] gforth` spots in the file (accept on a terminal or
  read-line, file-size double, ms-ticks); output identical to Lugforth's
  on test.z3, test.z5 and the library game z5/z8. Not done: version 6
  and 7, undo, colours, sound, timed input, Quetzal. Fortz (GPL) was
  only the inspiration, no code of it is used.
- `2048.fs` (+ `2048_tests.fs`): terminal game, `2048-play`. Tested in a
  pty; colors/layout not seen by eye.
- `3d.fs` (+ `3d_tests.fs`) and `sdl2_3d.fs`: math, objects, rotation,
  perspective; SDL2 drawing checked only with the dummy video driver
  (pixel counts), not on a real screen.
- `inference/` (inference.fs, weather.fs, lamp_diagnosis.fs, tests,
  Makefile, READMEs): facts, rules, `infer`.
- `sdl2_2048.fs`: SDL2 front end of 2048.fs (bitmap digits,
  arrows/wasd/mouse swipe, slide+pop animation, `--no-anim`); checked
  with the dummy driver (pixel colors of tiles and digits), not by eye.
  Could be added to the Android Examples list. Ideas left: more
  inference examples from his docs (`INFERENCE/docs/*.odt`),
  `3Dmodelisation`/`dumpTool` extras, other upstream projects (lotto,
  6809, flog, BMP, HTTP). Verified in a real X window (display :0) for
  sdl2_2048: see [PITFALLS.md](PITFALLS.md), last bullets. Gotchas met:
  `include` is relative to the including file; `if` at top level does
  not work (wrap in a word); `<# #s #>` takes a double, as in the
  standard (since 2026.10.27.2; before, one cell as in eForth):
  `n 0 <# #s #>`; `.r`, `random` ( n -- r ) are in `ansi_compat.fs`,
  `ms-ticks 1 or rand-seed !` seeds it.

## ANS words added to common/ansi_compat.fs - 2026-10-27

The words of Forth 2012 still missing, checked word set by word set
against the lists of the standard with `[defined]`: CORE EXT, DOUBLE,
LOCALS, SEARCH, STRING, MEMORY and EXCEPTION are now complete; FACILITY
lacks only TIME&DATE (a clock per port), FLOATING only REPRESENT and the
double precision words (the floats here are single, F@ is SF@), TOOLS
only CODE ;CODE ASSEMBLER, FILE five words that belong with the files
(READ-LINE WRITE-LINE INCLUDE-FILE FILE-STATUS REQUIRE). Choices: `TO`
is redefined last, to store in a 2VALUE and an FVALUE too (it recognizes
them by their DOES> code); `C"` is compiled as an `s"` whose text starts
with the count byte, so `see` reads it; SEARCH-WORDLIST puts the
wordlist alone in the search order around FIND; TRAVERSE-WORDLIST skips
the builtins of C (forks) and smudged words; `{: a b | c -- d :}` is
built on `(local)`, `|` locals start at 0. Tests in
`ansi_compat_tests.fs` (posix); on the web build they were checked by
hand (the test framework does not run there). Gerry Jackson's
forth2012-test-suite was first run on 2026-10-28, see the section below.
The pictured numeric output takes a double since 2026.10.27.2 (`#`
divides by the base with three u/mod on half cells, `pad` is 160 bytes
above `here` for the 128 digits of a binary double).

## Forth 2012 test suite (Gerry Jackson's forth2012-test-suite) - 2026-10-28

First run, and what it found. All fixed the same day:

- `FIND` was ueForth's `( a n -- xt | 0 )`. The primitive is now
  `(FIND)`, and `FIND` is the standard
  `( c-addr -- c-addr 0 | xt 1 | xt -1 )` (`ansi_compat.fs`).
- Number prefixes `#` `$` `%` with a sign after them (`$-ff`), and
  character literals `'a'` (`convert` in `common/core.h` and in
  `web.template.js`). `-$ff` still works.
- `+LOOP` ended half a turn early with a large step: the test is now the
  signed overflow of `index - limit + MIN_INT + step`
  (`tier2_opcodes.h`, and the Forth versions in `boot.fs`).
- `2@` and `2!` had their cells swapped: the standard puts x2 at the
  address, x1 in the next cell. This also fixes the initial value of a
  `2VALUE`. Callers that read two cells written one by one got a `swap`
  (`esp32/optional/interrupts/timers.fs`, `sdl2_tests.fs`).
- A file was evaluated as one single line: `SOURCE` was the whole file,
  so `SOURCE >IN !` (skip the rest of the line, as `TESTING` of the
  suite does) jumped to the end of the file and the rest was silently
  ignored. `include-file` now reads it line by line (`evaluate-lines` in
  `including.fs`), `REFILL` gives the next line, and the words that read
  up to a delimiter (`(`, `.(`, `s"`, `r"`, `r|`, `r~`, `{`, `{:`,
  `locals|`, `e:`) go on to the next lines when they do not find it
  (`parse-more`, `parse-name-more` in `io.fs`). `evaluate` puts the
  input back in place after an error. Not done: the web build has its
  own `include-file` (`ports/web/utils.fs`), still one single line.

Second round (2026-10-28, version .2), all the word sets:

- Files: `READ-LINE`, `WRITE-LINE`, `FILE-STATUS`, `REQUIRE`, public
  `INCLUDE-FILE` (the old internal one is `include-fid`), `SOURCE-ID`
  gives the file id while a file is included. `FILE-SIZE`,
  `FILE-POSITION`, `REPOSITION-FILE` and `RESIZE-FILE` take and give a
  double cell number as the standard has it (`common/fileops.fs`, loaded
  before the first user of them; the ports keep their single cell
  primitives). The callers in the editors, the examples, `blocks.fs` and
  the gforth port were changed (`d>s`, `s>d`).
- The file words no longer move `PAD`: `s>zt` is `s>z` that gives `here`
  back (the suite fills `PAD`, opens a file and compares). `rename-file`
  keeps its first name with `s>z`.
- The POSIX file words give the error of `close`, `fsync`, `unlink`,
  `rename` and `ftruncate` (an `int` is sign extended first: `-1` was
  not negative on 64 bits).
- `ABORT"` was unconditional: it now takes a flag. An undefined word
  throws -13 (it was -1).
- `CMOVE` copies from the low end and `CMOVE>` from the high end, one
  character at a time, so that overlapping regions give the standard
  result (`cmove_up` and `cmove_down` in `core.h`, and in
  `web.template.js`). `RESIZE` leaves the old block as it was when it
  fails.
- `1.` `-$ff.`: a number with a point at the end is a double
  (`REC-DNUM`, ansi_compat.fs). `M*/` rounds as `/` does, down.
- `SUBSTITUTE`: names are compared without the case, as the words are,
  and a source that overlaps the destination gives a negative count.
  `REPLACES` keeps a zero length text.
- Blocks: `BLK`; `LOAD` sets it, `EVALUATE` clears it; `\` in a block
  goes to the end of the 64 character line; `REFILL` goes on to the next
  block; `SAVE-INPUT` gives 4 values and `RESTORE-INPUT` reads the block
  again; `EMPTY-BUFFERS` forgets the update.
- `[DEFINED]` and `[UNDEFINED]` leave the flag in any state, `[IF]`
  works inside a definition, `DOES>` ends the locals of the defining
  part, `#LOCALS` gives 16, the locals area holds 19.
- A regression found by running the web build under node: the Forth
  `LOOP` and `+LOOP` of the web build used `-1 1 rshift`, and the web
  `rshift` is arithmetic, so no loop ended. MIN_INT is now
  `1 cells 8 * 1- 1 swap lshift`. Check `ninja web` and a `do loop`
  after a change there.

`ninja forth2012` runs it (`tools/forth2012_test.py`: the suite is taken
from `$F2012_DIR` or cloned into `out/forth2012-suite`; it fails when a
word set has an error or a file is aborted). By hand, in a checkout of
the suite (the files `include` each other by relative name, and
`core.fr` asks for a line of text: give it one on stdin):

    printf 'hello world\n' | lugforth driver.fth

with a driver that includes, by absolute path and in the order of the
suite's `runtests.fth`, `prelimtest.fth`, `tester.fr`, `core.fr`,
`coreplustest.fth`, `utilities.fth`, `errorreport.fth`, then the
`...test.fth` of the word sets (`coreexttest.fth` before `filetest.fth`:
it defines `SI_INC`), then calls `REPORT-ERRORS`.

Result: all the word sets run by the driver pass (Core, Core extension,
Block, Double, Exception, Facility, File-access, Locals,
Memory-allocation, Programming-tools, Search-order, String): 0 errors.
The floating point sets (`fp/`) were not run.

For the comparison: pforth (upstream, 64 bit) passes Core and Facility,
stops on `BUFFER:`, `{:`, `UNESCAPE`, `CS-PICK`, `BLOCK` and `REQUIRED`,
and crashes on Double, Exception and Search-order. gforth 0.7.9 passes
everything but 2 tests of String (a zero length substitution: the names
of `REPLACES` are case sensitive there), and Search-order needs
`FORTH-WORDLIST SET-CURRENT` first.

## DO LOOP in C - 2026-10-27

`DO ?DO LOOP +LOOP` were Forth words (`common/boot.fs`, about twenty
primitives a turn for `LOOP`). They are opcodes of tier 2 now
(`(DO) (?DO) (LOOP) (+LOOP)` in `internals`, `common/tier2_opcodes.h`);
`opcode-as (DO) DO` in boot.fs gives the Forth `DO` the code field of
the opcode, as `BEGIN` and `IF` are made, so `see` and the marks of
utils.fs still find one word. The return stack is laid out as before
(limit, then index: `I` is `R@`, `J`, `K`, `LEAVE`, `UNLOOP` unchanged).
The web build has no tier 2 and keeps the Forth versions. `see`
(2026.10.27.3): a word whose code field is a builtin's, as `DO` or `I`,
shows `C code: DO runs the opcode (DO)` instead of `Unsupported`
(`opcode-of` in utils.fs), and the literal xt that `postpone` compiles
before `,` shows as `POSTPONE DO` (`known-xt?` walks the vocabularies
and the builtins). `see DO` finds the compiling word `do` (case is
ignored); the runtime `DO` is in the vocabulary `internalized`:
`internals internalized see DO forth`. 1e7 turns of
`0 10000000 0 do i + loop` on posix: 554 ms before, 71 ms after. Same
results as gforth on `+LOOP` with negative steps and `LEAVE`.

## Games after r3forth (examples/games/sdl2_tetris.fs, sdl2_snake.fs, sdl2_minesweeper.fs) - done 2026-10-19

r3forth (https://github.com/phreda4/r3, Pablo Reda, MIT, credited in
NOTICE) has its own syntax (`cond? ( ... )`, `:word`, fixed point
48.16): its code does not load here, the games are rewritten keeping its
ideas. Digits from the 3x5 bitmap of breakout, no fonts.
`games_tests.fs` (in `make test-ports`) loads them with `tetris-no-run`
etc. defined, which skips the game loop
(`[defined] x [if] [else] ... [then]` at the end of each file).
tools/efi_qemu_test.py plays them from the stick (screendump checks).
Not seen on the Pi under QEMU (same sdl2.fs). `color` resets the alpha
to 255: set `alpha` after `color`. Edges of keys: `just` keeps the last
state per key. `sdl2_mastermind.fs` (ours, not from r3) follows the same
pattern: `mastermind-no-run`, tests in games_tests.fs, efi test. Other
r3 candidates: bdash (Boulder Dash, own sprites needed), sokoban (levels
of unclear license), raycaster, term demos (fire, matrix, torus),
util/penner.r3 easing, lib/sdl2poly.r3 polygon fill.

## Forth is Fun (turtle)

Forth is Fun (https://gitlab.com/garvalf/forth-is-fun, Eric Forgeot,
MIT/Apache): `examples/graphics/sdl2_turtle.fs` is written after its
forthtoise.fs (same word names, English and French; strokes kept in a
list and redrawn each frame), tests in `turtle_tests.fs`
(`make -C examples test-ports`). Running it starts a shell (`logo`):
lines typed in the terminal are kept as a program, `go` / `play` run it
(compiled as a word `logo-prog`, ; `safe-eval` restores the source after
a throw, `evaluate` does not). The shell loop was checked in a pty under
Xvfb, not automated. Still to borrow: bresenham, the CYOA (choose your
own adventure, also in French), glitch patterns. Trap met:
`fdup f>s s>f f< if` left a wrong value here; round with
`fdup f0< if 0.5e f- else 0.5e f+ then f>s`.

## Drawing at the prompt (`graphic`) - done 2026-10-20

`graphic` (loads sdl2 too) makes `0 0 100 100 line`,
`red color ... box`, `n ink`, `cls` work at the console prompt, drawn
under the text, as BASIC on a MO5. Baremetal (ports/baremetal, rpi +
efi): `gfx_ensure` in screen.h makes a 640x480 layer in `fb_back`
(`fb_gfx`), every drawing C function calls it; `con_sync` (console.h)
shows it through cell pixels that are 0 (default background, scaled by
`con_zoom`); `fb_flip` then only syncs; `fb_open` / `fb_close` end it.
Forth: `gfx-on`, `graphic`, `gfx-mode`, `graphic-size` (GFX-SIZE,
`gfx_size` in screen.h; `con_compose` maps screen pixels to layer
pixels) and a `cls` that clears to black in sdl2.fs. Posix console
(sdl2_console.fs): a target texture `gfx-tex`, `con-draw` renders it
between the background and the cells (pen saved around the redraw),
`cls` and `flip` are redefined, `con-key` marks the window dirty so a
drawing made between two keys shows. Tested: efi QEMU test (9 checks),
the posix console by hand under Xvfb (xdotool;
`python3 tools/gui_graphic_test.py out/posix/lugforth` automates it),
not rpi (no aarch64 toolchain), not the Android app. Web console
(ports/web, 2026-10-22): `graphic` puts the drawing area above the text
of terminal.html (fixed canvas + spacer, S.layer in sdl2.fs), checked in
headless Chromium by CDP on desktop and an emulated phone.

## `play` and waves by name - done 2026-10-20

`play ( a n -- )` (MML, MSX octave numbering, with p n & v w; parser
`p-step` and friends, output through the deferred `p-out`, default
`beep`) is written in the three sdl2.fs: ports/posix (tests in
sdl2_tests.fs), ports/baremetal, ports/web: keep the copies equal.
`wave`, `beep-volume`, `pitch-drop` are settings (`: setting` =
`create , does> !`: `1 wave` and `1 to wave` both store; read with
`wave@` etc.), like `color` takes its value;
`square triangle saw noise sine` set `wave`. Sine is wave 4 (table in
sdl2.fs posix, `snd_wave` in ports/rpi/sound.h, 'sine' oscillator on the
web). A rest is a beep of frequency 0 (silent). Not heard on the boards
or in the browser, only the parsing is tested (posix).

## File manager (`filer`) and desktop (`desk`) - first versions 2026-10-24

`examples/utils/filer.fs` (SDL2 + SDL2_ttf, DejaVuSans path in
`font-file`): list of the current folder (folders first, yellow), click
or arrows to select, double click or Enter to open (folder: chdir;
`.fs`: `included` after the window closes; other file: `ed`), Backspace
= parent, `D` then `Y` deletes a file, wheel scrolls. The list draws in
a rectangle (`fl-w` `fl-h`, origin 0 0) and takes events as calls
(`fl-click`, `fl-key`, `fl-wheel`, `fl-open` is a defer), so a desktop
can host it.

`examples/utils/desktop.fs` (`lugforth examples/utils/desktop.fs`, or
`desk`): 800x600, icons in two columns (`icon`: kind 0 file manager
window, 1 a command in a terminal window, 2 a Forth program), double
click starts one; windows with title bar, drag, raise, x; taskbar with
the windows and Quit. A text window is a small terminal emulator (`t-*`
words: grid of code points + attributes, UTF-8, CSI H J K A B C D G s u
m with 16/256 colors, `?25l/h`): `ed` and `writhdeck` run unchanged
inside it. How: `type`, `key`, `key?` and `form-hook` are redirected
while the editor runs (`run-in-terminal`), and `key`/`key?` run one
frame of the desktop (events, draw, flip) whenever the queue of keys is
empty. So one terminal at a time (a second launch says so in the
taskbar), the other windows stay alive. Keys: `on-key` for specials and
Ctrl+letter, SDL_TEXTINPUT for the rest (AZERTY, accents). The x of a
terminal sends Ctrl+Q (an unsaved text asks twice). A Forth program
(`.fs` from an icon or from the file manager) closes the desktop, runs
with `argc` set to 1 (so `script-bye` does not exit) and the desktop
comes back; the program is started with the desktop words out of the
search order (all of them are in the vocabulary `fm`, `desk` and `filer`
are in forth). Tested under Xvfb with xdotool (mousedown/mouseup with a
pause, keydown/keyup): icons, file manager window, drag, writhdeck in a
window (new file, typing with accents, Ctrl+S, x), ed, README.md opened
from the file manager, Snake, 2048 and the demo launched and left with
Esc, Quit. Idle CPU about 5 percent (a frame is drawn only when
something changed). The Forth icon (`repl`) runs the line editor of the
console (`edit-accept` with `le-force`, history, arrows, Tab) and
`evaluate` in a terminal window; the x types `bye`; `screen` or
`graphic` typed there would replace the desktop window (not handled).
Not tested: Windows, the web build, the Android app, a second terminal,
ed/writhdeck mouse use, wide characters (a character is one cell). Traps
met: a word named `scan`, `top`, `step`, `frame` or `key-down` replaces
the system or sdl2 word of the same name, even for the programs that are
loaded later (the games use `key-down`); `{ locals }` in a loop body;
`!` writes 8 bytes (use `l!` and `ul@` for 32-bit cells of an array);
`0 { x }` is the way to add locals after the arguments; `catch` does not
restore locals; a program that touches the real tty (editor-raw) is
stopped when started in a pipeline: use `timeout --foreground` and
`< /dev/null` in tests. Settings (2026-10-24): icons are dragged and
their places, the background color (8 swatches) and the size of the
desktop (4 sizes, the screen is opened again) are saved as Forth lines
in `$HOME/.config/lugforth/desktop.ini` (folders made when missing, room
for other settings later) (`dk-bg!`, `dk-size!`, `icon@`, loaded with
`included`). One line per icon: `kind s" label" s" command" x y icon@`;
kinds 0 file manager, 1 command in a terminal window (`ed`, `writhdeck`,
`repl`), 2 Forth program (path from the project folder, absolute if it
starts with `/`), 3 settings, 4 SDL Forth console (the desktop closes,
the console opens as with `--gui`, `desk` brings the desktop back); the
file may be edited by hand. The project folder is found by looking for
`examples/utils/desktop.fs` in `./`, `../`, `../../`, `../../../`, so
`desk` starts from examples/utils as from the root. Tested under Xvfb
(drag, swatch, size, restart from the root, Snake). Next steps: rename,
copy, new folder in the file manager (needs a text field), mouse
selection in terminal windows.

`examples/utils/desktop_bm.fs`: the same idea for the bare-metal PC and
Pi (no libSDL2 there): icons, draggable windows (file manager, settings
with the mouse speed saved in lugforth.ini) with the short sdl2 words,
runs `.fs`, Z-machine stories, `writhdeck` and `ed` (they take the
screen, a key brings the desktop back). Tested under QEMU with the
keyboard (2026-10-27); the mouse is the one of the sdl2 words (software
arrow), never felt under OVMF.

## Unicode in the console window (`lugforth --gui`)

A grid cell holds a Unicode code point (4 bytes: `grid` has a stride of
4, `gfill` blanks cells, read with `sl@`, written with `l!`). `con-putc`
decodes the UTF-8 that programs `type` (`u-byte`; a bad sequence shows
`?`). The TrueType font (`sdl2_console_ttf.fs`) makes the glyphs 33 to
126 at once and any other letter when it is first shown (`ttf-other`: a
table of 1024 textures keyed by code point mod 1021, a clash replaces);
the color of the text is given to a glyph when it is drawn (`ttf-tint`
only stores it). A letter the font lacks shows the font's empty box
(DejaVu Sans Mono has no CJK or emoji); wide letters still take one
cell. The bitmap font has only ASCII and draws the plain letter for
Latin-1 and 20 common signs (`bm-base`, `base-map`, `extra-cp`), else
`?`. A selection is copied as UTF-8 (`sel-cell`). Typed letters arrive
as UTF-8 bytes (SDL text input). Tested in `sdl2_console_tests.fs`
(headless) and by screenshot under Xvfb (Latin, Greek, Cyrillic,
symbols; with the bitmap font through `gui-load sdl2 console`).

Line editor at the prompt (`common/lineedit.fs`): moves, backspace and
delete work on a letter, not a byte (`le-prev`, `le-next`), and the
cursor sequences count columns, that is letters (`le-cols`); this was
also wrong on a real terminal. Tested in `lineedit_tests.fs`.

The console cursor blinks from `blink-tick`, called by `con-key` and by
`con-key?`: a program that polls with `key?` (writhdeck) had a frozen
phase, often the invisible one, hence no cursor.

## Inform 6 stories

Stories live in `examples/games/zmachine/` (`.inf` source, `.z3`/`.z5`
compiled, both committed). The compilers and libraries are NOT in the
repository.

### What is installed here (checked 2026-10-26)

- `/usr/bin/inform` = Inform 6.31 (2006), library 6/11 in
  `/usr/share/inform/6.31/module`. Latin-1 sources only, no `-Cu`. OK
  for a quick English test
  (`inform -v5 +/usr/share/inform/6.31/module x.inf x.z5`), but 6/11 has
  no automatic "(first opening the chest)": `cave.inf` was built with
  6.12 and `zmachine_tests.fs` expects its messages.
- Nothing else. Get what you need in a scratch directory (never in the
  repo):
  - compiler:
    `git clone --depth 1 https://github.com/DavidKinder/Inform6.git && make`
    (6.45, has `-Cu`)
  - English 6.12.x library + French files:
    `git clone https://gitlab.com/auraes/inform6lib_fr` and unzip its
    `libI6_Windows.zip` (gives `inform6lib/` = English 6.12.6,
    `inform6lib_fr/` = `french.h`, `frenchg.h`, `translation.h`, and the
    example `cqvdle.inf`, a good second test of the interpreter).

### Compile

    inform6 cave.inf cave.z5 -v5 +/path/inform6lib/ # English, version 5
    (the library does not fit in v3)
    inform6 cave_fr.inf cave_fr.z5 +/path/inform6lib/
    ++/path/inform6lib_fr/ # French (needs 6.35+ for the !% -Cu header)

The French source starts with `!% -Cu`, `!% -~S`,
`!% +Language_name=french` and is UTF-8. French library conventions:
verbs in the infinitive (`prendre`, `ouvrir`, `allumer`, `regarder`, not
`regarde`), accents must be typed, genders by `has male` / `has female`
(the library does elisions and agreements itself, no `articles` needed),
`Include "parser"; Include "verblib";` then `Include "frenchg";` last.
The French library is CC BY-SA 4.0: do not copy it into the repository
(if ever: NOTICE with its text).

### Test

    out/posix/lugforth examples/games/zmachine_tests.fs        # ends with PASS (49 checks)
    gforth examples/games/zmachine_tests.fs -e bye # same, under gforth
    printf 'regarder\ninventaire\n' | out/posix/lugforth
    examples/games/zmachine.fs story.z5 # by hand; stdin EOF prints
    garbage at the end, ignore

`zmachine_tests.fs`: `s" cmd" iline` queues a command, `play` runs,
`has?` searches the capture (`ocap`, 16 KB), the input buffer `icap` is
600 bytes. A check on text that the interpreter wraps at 78 columns can
miss (the line break replaces a space): test a short fragment. The turn
count excludes meta commands such as `score`.

### Traps

- Inform's `-Cu` stories bring their own alphabet table (header word
  $34) and Unicode table (header extension word 3, via $36);
  `zmachine.fs` reads both in `zsetup` (`zuni`, `zun`, `zalpha`,
  `afind`, `ez-char`). Wrong accents ("Apr7s") = that code broke.
- Print-only messages in a `LibraryMessages` `before` routine must end
  with `rtrue`, or the English message is printed after.
- Library constants such as `AND__TX` cannot be redefined without a
  language file: use a real language library instead of patching the
  English one (the first `cave_fr.inf` did, with `BeforeParsing` mapping
  French words to English ones: abandoned).
- Rebuild the EFI/Pi disks after adding a `.z5`: `ports/efi/Makefile`
  takes `examples/games/zmachine/*.z3|*.z5` by wildcard.
