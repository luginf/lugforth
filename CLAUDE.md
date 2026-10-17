# Lugforth: notes for Claude

Lugforth is a fork of ueForth (Brad Nelson), 64-bit, ignores case. Read `docs/SKILLS.md`
(pitfalls learned the hard way) and `README.md` before touching anything.

## Start from the current main

Several sessions work on this repository at once, and `main` moves while a session runs
(a cloud session clones it once, at its start). Before the first commit of a change, and
again before opening a pull request: `git fetch origin main`, then start from (or merge in)
`origin/main`, and say so if `main` has moved meanwhile. Look for a file (`CLAUDE.md`
included) in `origin/main` too before saying it does not exist.

## Build and test

    python3 configure.py && ninja posix     # out/posix/lugforth (and lugforth-mini); also runs all .fs tests
    python3 tools/editors_pty_test.py out/posix/lugforth   # pty test of ed, writhdeck (save, browser, idle CPU)
    make -C examples smoke                  # headless load of the SDL2 examples (SDL_VIDEODRIVER=dummy)

Xvfb and `xdotool` are installed: run `Xvfb :99 &`, then the SDL examples with `DISPLAY=:99` and screenshot with `import -window root`
(hold a key with `xdotool keydown`, sleep 0.25, `keyup`: a quick `xdotool key` is missed by the game loop). Headless alone: dummy driver, `SDL_RenderReadPixels`.
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
Both also run on the bare-metal board (ports/rpi, `ed` / `writhdeck` words, files in /lib): the POSIX-only
parts are under `[defined] posix [if] ... [else] ... [then]` inside the source strings, keep them that way.
The editors end with `only forth ...`: a file that is `include`d must save `current @` and put it back
(`current !`) at its end if the caller may have pushed a vocabulary (`sdl2` at the prompt pushes one), as
varvara_rpi.fs does: `definitions` after `previous` would otherwise leave the caller defining in `sdl2`.

## Android (ports/android, not buildable here)

`MenuBar.java` is the bar of both consoles: Tab, arrows, Ctrl (sticky: next letter is a control code; sends F9,
which `ports/posix/sdl2_console.fs` turns into `ctrl-next`; the web page does the same through `lugkey('Ctrl')`
in `ports/web/platform.fs`), a "⋮" button for Examples/A-/A+/Help, and home. To check once compiled:
the Ctrl button on a real soft keyboard (letters may arrive as key events rather than text input), and that
nothing shows that Ctrl is armed (an indicator would help).

## Bare-metal AArch64 (ports/rpi)

`make -C ports/rpi test` builds out/rpi/{virt/lugforth.elf,pi3/kernel8.img,pi4/kernel8.img} and boots the first two
under QEMU (`virt`, `raspi3b`), checking answers over the serial line (tools/rpi_qemu_test.py). Needs
`gcc-aarch64-linux-gnu picolibc-aarch64-linux-gnu qemu-system-arm` (apt, installable in the cloud container).
No OS, picolibc, PL011 console, MMU identity map (needed: unaligned accesses fault with it off), faults become
THROW. Not tried on a real Pi; Pi 4 written blind (QEMU 8.2 has no raspi4b). See ports/rpi/README.md.
Graphics: `sdl2` short words on the mailbox framebuffer (ports/rpi/sdl2.fs, screen.h), keys from the UART;
`make -C ports/rpi breakout` plays examples/sdl2_breakout.fs in a QEMU raspi3b window (`BOARD=virt`: virt with
`-device ramfb`, set up through fw_cfg), `make test` checks both by screendump.
Text (font8x8), images (PNGs built in by tools/rpi_assets.py), keyboard/mouse (virtio-input on virt with
`-global virtio-mmio.force-legacy=false`, DWC2 USB HID on pi3; tested through QMP input-send-event), sound
(sound.h mixer, virtio-sound on virt checked from a -audiodev wav file; Pi 3 PWM+DMA written blind), `paste`
(a whole file over serial, then Ctrl-D: multi-line strings work). `make roguelike` runs examples/sdl2_roguelike.fs.
QEMU runs a whole bcm2835 DMA chain at once: a looping chain hangs it (even SIGTERM), so never start one blindly.
Files: storage.h (Pi SDHCI/EMMC polled, virt virtio-blk) + fat.h (FAT12/16/32, MBR or not, long names read and written, mkdir/rmdir/chdir, dates from the clock),
the standard file words, so include/ls/cat work; autoexec.fs runs at boot. tools/rpi_mkdisk.py makes images (needs
dosfstools mtools); make test checks them afterwards with mtools and fsck.fat.

## Bare x86-64 PC, UEFI (ports/efi)

`make -C ports/efi test` builds out/efi/BOOTX64.EFI (gcc + gnu-efi) and boots it under QEMU q35 + OVMF from a USB stick
image (MBR + FAT32, tools/rpi_mkdisk.py), 54 checks (tools/efi_qemu_test.py, which reuses rpi_qemu_test.py). Needs
`gnu-efi ovmf qemu-system-x86 dosfstools mtools`. Stays in boot services: GOP screen, ConIn keyboard (no releases: a key
is held 120 ms after its last press; the test uses `-device usb-kbd`, PS/2 does not repeat), the stick's FAT through
Simple File System (ports/efi/files.h, same API as fat.h). Reuses ports/rpi screen.h, console.h, input.h, sdl2.fs;
vocabulary `pc` aliased `rpi`. efi_main is called SysV by gnu-efi's crt0 (NOT EFIAPI: it crashed); own libc in libc.h
(x87 math), setjmp and fault stubs in start.S (patched copy of the firmware IDT). No sound, no mouse. Not tried on a real
PC yet; ports/efi/README.md has the Dell Latitude steps (Secure Boot off, F12).

## Ports from eForth Windows (Marc Petremann, GPL) - done 2026-10-13

Source: https://github.com/MPETREMANN11/eForth-Windows (clone it into the scratchpad to read). Its eForth is
a ueForth (7.0.7.x), so lugforth (7.0.10) loads most of it as is: `value/to`, `{ }`, `vocabulary`, `:noname`,
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
Verified in a real X window (display :0) for sdl2_2048: see docs/SKILLS.md, last bullets. Gotchas met: `include` is relative to the including file; `if` at top level does not work (wrap in a word);
`<# #s #>` takes ONE number here; `.r`, `random` ( n -- r ) are in `ansi_compat.fs`, `ms-ticks 1 or rand-seed !` seeds it.

## Open question: control structures outside a definition

Decision pending (the maintainer is thinking about it), nothing is implemented yet.

Today `do`, `?do`, `if`, `begin`... typed outside a definition (at the prompt,
at the top level of a file, in an `evaluate`d string) give no error and a wrong
result: `3 0 do 42 . loop` prints 42 once and leaves `3 0` on the stack,
`1 if 7 . then` runs its body whatever the flag and leaves the `1`. Wrap such
code in a definition (`: t 3 0 do ... loop ; t`), tests included.

The standard leaves this case undefined. What other Forths do:

1. An error, the word being compile-only (gforth, pForth): `-14 throw`.
2. Interpreted loops in brackets that re-read the source, like the standard
   `[IF] [ELSE] [THEN]`: gforth has `[DO] [?DO] [LOOP] [+LOOP] [BEGIN] [UNTIL]
   [AGAIN] [WHILE] [REPEAT] [FOR] [NEXT]`.
3. A temporary anonymous definition: met in interpret state, `do`/`if`/`begin`
   open a `:noname`, compiled up to the end of the structure, then run and
   forgotten (Win32Forth, for example).
4. Quotations, always compiled (RetroForth, Konilo, Factor): `#10 [ ... ] times`.

Lugforth has only the conditionals in brackets (`common/conditionals.fs`:
`[IF] [ELSE] [THEN] [DEFINED] [UNDEFINED] DEFINED?`); none of the bracket loops
of point 2 exist.

Options considered:

- Point 1 only: the control words throw -14 in interpret state, a clear
  message instead of a silent wrong result. Smallest change.
- Point 3: what is typed at the prompt works as in a definition (and runs
  compiled). Nesting has to be handled, and the boot, ESP32 and Pico checked.
- Point 2: `[DO] [?DO] [LOOP] [+LOOP] [I] [BEGIN] [UNTIL] [AGAIN] [WHILE]
  [REPEAT]` beside `[IF]`, with tests in `common/conditionals_tests.fs`. Easy
  within one line (reset `>in`); across lines it needs `save-input` /
  `restore-input` (not checked whether lugforth has them), and lines typed at
  the prompt are gone. Mostly useful to generate code or data while a file loads.
