# Lugforth: notes for Claude

Lugforth is a fork of ueForth (Brad Nelson), 64-bit, ignores case. Read `docs/SKILLS.md`
(pitfalls learned the hard way) and `README.md` before touching anything.

## Build and test

    python3 configure.py && ninja posix     # out/posix/lugforth (and lugforth-mini); also runs all .fs tests
    python3 tools/editors_pty_test.py out/posix/lugforth   # pty test of ed, writhdeck (save, browser, idle CPU)
    make -C examples smoke                  # headless load of the SDL2 examples (SDL_VIDEODRIVER=dummy)

No Xvfb here, only `xdotool`; SDL graphics can only be checked headless (dummy driver, `SDL_RenderReadPixels`).
The Android toolchain is NOT on this PC: anything in `ports/android` or `ports/web` is written blind and
compiled later on another machine. Write the code carefully and say so in the commit.

Forth pitfalls hit again and again: `b@` in the editor takes a *position in the text*, not an address;
`{ locals }` inside a conditional or loop body break (declare at the top of the word); a `|` inside an `r|`
block ends it; `[char]` only inside definitions; `?do` with start > limit runs away. More in `docs/SKILLS.md`.
Silent "Error including: file" = a throw somewhere: bisect with a scratch file using `.` prints.

## Editors: `ed` and `writhdeck` (ports/posix/editor.fs, writhdeck.fs, *_tests.fs)

`ed` is a text editor in Forth; `writhdeck` is a port of WrithDeck (original in `~/src/writhdeck`, Tcl/C,
manual `writhdeck_MANUAL.md`) built on top of it. Both are strings compiled on first use (`ed-src`,
`wd-src`), so no `|` inside the source.

Done in writhdeck: wrapped lines in a centered column, focus mode, Hemingway, words and daily goal, stopwatch
and countdown, outline (Esc o), replace all, autosave, txt2tags/Markdown highlighting, themes, Esc menu
(`w f h t o v s r g a + - c x q ?`), word occurrences (Esc v), redo (Ctrl+Y, also in ed), file browser
(`writhdeck` alone or Ctrl+O: Enter, n new, / filter, r rename, d delete, b backup to `backups/`).
PageUp/PageDown work (checked in a pty, ed and writhdeck).

Still to do, from the original (priority order):
1. Favorites and recent files in the browser (kept in `editor.ini` or a `recent.txt` beside it).
2. Line numbers (Ctrl+L), Ctrl+Left/Right already move by word.
3. Daily statistics kept on disk (per file, high-water mark) and shown by `s` in the browser too.
4. Repetition detection (same word within a range) and spell check (Hunspell via `hunspell -a` pipe?).
5. Second workspace (F10) and vertical split view (F3/F4).
6. Ctrl+Q in an edited text returns to the browser (now it quits); modal commands `t s w q` of the original.
7. INI profiles/schemes, i18n.
Esc followed at once by a letter is read as Alt+letter: the pty tests wait 0.5 s between them.

## Android (ports/android, not buildable here)

`MenuBar.java` is the bar of both consoles: Tab, arrows, Ctrl (sticky: next letter is a control code; sends F9,
which `ports/posix/sdl2_console.fs` turns into `ctrl-next`; the web page does the same through `lugkey('Ctrl')`
in `ports/web/platform.fs`), a "⋮" button for Examples/A-/A+/Help, and home. To check once compiled:
the Ctrl button on a real soft keyboard (letters may arrive as key events rather than text input), and that
nothing shows that Ctrl is armed (an indicator would help).

## Ports from eForth Windows (Marc Petremann, GPL)

Source: https://github.com/MPETREMANN11/eForth-Windows (clone it into the scratchpad to read). Its eForth is
a ueForth, so most code loads as is. Sibling project `~/src/forks/uf-9` did the same for a 16-bit Forth
(see its `PORTS-EFORTH.txt`). Ported here: see `examples/` (2048, 3d, inference) and the notes in
`examples/README.md`. These files are GPL (derived from his code), unlike the rest of the repository.
