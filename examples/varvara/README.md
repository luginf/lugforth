# Varvara in Forth

A [Uxn/Varvara](https://100r.co/site/uxn.html) virtual machine written in
lugforth, so that Uxn ROMs run inside lugforth.

| file | what it is |
|---|---|
| `uxn.fs` | the Uxn CPU (32 opcodes, modes 2/r/k, 16 banks of 64 KB), the System device (stack pointers, memory fill and copy, debug, halt) and Console output |
| `file.fs` | the two File devices ($a0, $b0): read, write, append, stat, delete, directory listing, sandbox in the working directory |
| `boot.rom`, `boot.tal` | the ROM `varvara.fs` shows when started without a ROM: the usage line and a drawing of a file dropped on a square (source in uxntal, built with `uxnasm boot.tal boot.rom`) |
| `launch.fs` | a launcher with options (window or terminal, zoom, screen size, mouse, sound, tokens for writhdeck-uxn), like the `writhdeck-uxn` script |
| `uxncli.fs` | runs a ROM in the terminal, like `uxncli` |
| `screen.fs` | the Screen device (two layers, sprites, fills), the Controller and Mouse devices: shared by `varvara.fs` and `varvara_rpi.fs`, which draw and read the keys their own way |
| `audio.fs` | the Audio devices (four channels mixed in software), shared like `screen.fs` |
| `devtest.tal`, `tone.tal` (+ `.rom`) | test ROMs of the board port: File, Datetime and Console; a one second tone |
| `xo5.rom` | a 5x5 tic-tac-toe (4 in a row) from <https://github.com/luginf/uxneries>, kept here as a test ROM |
| `varvara_rpi.fs` | runs a ROM on the bare-metal board of `ports/rpi` (see below) |
| `varvara.fs` | runs a ROM in an SDL2 window, like `uxnemu`: Screen, Controller, Mouse, Datetime, Audio, File, Console input |
| `uxn_tests.fs` | 102 tests: random programs, jumps and calls, System device |

```
lugforth examples/varvara/uxncli.fs program.rom [arguments]
lugforth examples/varvara/varvara.fs program.rom
lugforth examples/varvara/varvara.fs                # boot.rom: usage and drop a ROM on the window
lugforth examples/varvara/uxn_tests.fs
```

`uxncli.fs` passes the arguments then stdin to the Console vector (type 2 for
an argument byte, 3 between arguments, 1 for stdin, 4 at the end) and exits
with the code written to System/state.

## Launcher

```
lugforth examples/varvara/launch.fs [options] [rom [file or arguments...]]
```

| option | |
|---|---|
| `-c`, `--console` | run in the terminal (like `uxncli.fs`), the terminal is put in raw mode and restored at the end |
| `-z`, `--zoom N` | zoom of the window, 1 to 8 |
| `-s`, `--size WxH` | screen size at the start (default 512x320); the ROM can resize it |
| `--no-mouse`, `-q` | ignore the mouse events, no sound |
| `-w`, `--writhdeck` | writhdeck-uxn conventions: without a ROM, `$WRITHDECK_UXN/bin/writhdeck.rom` (`writhdeck-cli.rom` with `-c`; default directory `~/src/writerdeck/writhdeck-uxn`); the file is opened from its own directory (the File device refuses other paths); the size becomes a token |
| `-l`, `-f NAME`, `-m`, `-a WORD` | tokens for the second argument of the ROM: `light`, the font (`vga`, `cream`, `prop`, `creamprop` give `f1` `f2` `f3`), `mouse`, any word |

Without a ROM it shows `boot.rom`.

The options go BEFORE the ROM: the options are read up to the first argument that is not
one, then that argument is the ROM if it ends with `.rom`, else the file; everything after
the ROM is given to the ROM as it is. So with `varvara.fs`, which has no options,
`varvara.fs writhdeck.rom README.md -s 640x400 -m` gives `-s` and the others to the ROM, which
does not know them. To open a file with options, go through `launch.fs`:

```
lugforth examples/varvara/launch.fs -w -s 640x400 -m README.md
lugforth examples/varvara/launch.fs -w -s 640x400 -m /path/to/writhdeck.rom README.md
lugforth examples/varvara/launch.fs -w -s 1280x800 -m -f cream notes.txt
lugforth examples/varvara/launch.fs -w -c notes.txt
```

With `-w` and no ROM given, the ROM is `$WRITHDECK_UXN/bin/writhdeck.rom` (default directory
`~/src/writerdeck/writhdeck-uxn`); give the ROM just before the file to choose another one.
`varvara.fs` and `uxncli.fs` still run by themselves (they read `program.rom
[arguments]` from the command line); the launcher loads both without starting them.

## Window

`varvara.fs` opens a 512x320 screen (the ROM can resize it), zoomed twice
(`VARVARA_ZOOM=1` to `8` changes it). The two layers of the Screen are kept
in one 8-bit SDL surface whose 16 colors show the foreground over the
background, so changing the System colors redraws nothing. The Screen vector
runs 60 times a second.

Keys: Ctrl is button A, Alt B, Shift Select, Home Start, and the arrows;
letters come from SDL text input, Enter, Backspace, Tab, Esc and Delete from
the key itself, Ctrl+letter sends the letter. Mouse: position, the three
buttons and the wheel. Console output goes to the terminal; the arguments after the
ROM name, then what is typed in the terminal (stdin, polled once a frame), come in
through the Console vector.

Shortcuts of uxnemu: F1 zoom 1 to 3, F2 prints the two stacks in the terminal (no
overlay), F3 saves `varvara.bmp`, F4 reboots the ROM, dropping a ROM file on the
window loads it.

## Audio

Four channels, mixed in software with the algorithm of the C uxn (single cycle
samples up to 256 bytes, sample repeat above, ADSR envelope, one volume nibble per
side, the vector called when a note ends, VU and position ports). The mix is queued
about 60 ms ahead once per frame through SDL_QueueAudio. Checked by rendering a
note with the SDL `disk` audio driver and comparing it with uxnemu: same length,
peak and period (the samples differ by one unit here and there, rounding of the
envelope). `SDL_AUDIODRIVER=dummy` or no sound card: the ROM runs silent.

## Tests

The expected results of `uxn_tests.fs` come from
[uxn.wasm](https://github.com/remko/uxn.wasm), which passes the upstream
opcode test suite: 100 random programs without jumps or devices (the two
stacks and a hash of the memory are compared), one program with jumps, calls,
`LDR`/`STR` and keep mode (its Console output), and one with the System
device, checked by hand. The upstream `opctest.rom` (assembled with uxnasm from uxn2's `etc/tests`) passes, with a
output identical to uxncli's, and so does uxn2's `file.tal` (File device).

## Speed

Each of the 256 opcodes is its own Forth word, compiled at load time from
one source for each of the 8 modes (`build` in `uxn.fs`): the macros `pop`,
`push`, `jump`... pick the stack, the width and the keep pointer of the mode
being built, so nothing tests the mode while a ROM runs, and the stacks are
reached inline. One pass of `bench_sieve.tal` takes 0.43 s, against 7 ms in
the C uxn: about 60 times slower, enough for ROMs that mostly wait for
events, not for heavy ones.

## Not done yet

The debugger overlay of F2. File: the sandbox check is a little looser than the C one
(any realpath failure counts as a missing file) and the code reads the directory entry
name at offset 19 of `struct dirent` (Linux).

## On the bare-metal board (ports/rpi)

`varvara_rpi.fs` runs a ROM in the QEMU window (or on a Pi's HDMI screen), through the
`sdl2` words of the board. `make -C ports/rpi disk` puts `examples/varvara/` on the SD
image as `varvara/`, then `make -C ports/rpi gui DISK=../../out/rpi/disk.img` and, at the prompt:

```
include varvara/varvara.fs      \ or varvara_rpi.fs: on the board varvara.fs loads it
s" varvara/boot.rom" varvara
s" notes.txt" s" varvara/writhdeck.rom" varvara-open     \ a ROM given a file
```

| device | on the board |
|---|---|
| Screen, Controller, Mouse | the sdl2 words: the pixels are drawn by `FB-BLIT8`; the keyboard and pointer of the window |
| Console | output to the console and the UART; the UART's keys are its input; `s" text" varvara-arg` gives arguments (`varvara-args-clear`) |
| Audio | the board's PCM queue (`SND-QUEUE`): virtio-sound on QEMU virt, the headphone jack's PWM on a Pi 3 (written blind, as the rest of the Pi's sound) |
| Datetime | the clock of the board: UTC. It starts from the RTC of QEMU virt or, on a Pi (no battery), the day of the build; `unix-time!` (vocabulary `rpi`) sets it and `60 tz!` gives UTC+1 |
| File (x2) | the FAT of the SD card, same behavior as on Linux: the root is the working directory, a name with `..` is refused, |

Keys: Ctrl is A, Alt is B, Shift is Select, Home is Start, the arrows, and the text typed
(`azerty` works). F1 changes the zoom, F2 prints the stacks (on the UART), F3 writes
`varvara.bmp`, F4 loads the ROM again and F12 quits, back to the Forth console.
`1 to varvara-zoom` before `varvara` gives a window of the ROM's own size (default zoom 2).

`make -C ports/rpi test` runs `boot.rom` and `xo5.rom` (a click changes the board), `devtest.rom`
(files, date, arguments, serial input) and, on virt, `tone.rom` recorded by QEMU (262 Hz).
It is slow under QEMU's emulation of a Pi 3 (about half a second to redraw writhdeck's
960x576 screen after a key); a real Pi should be faster. Not done: dropping a file on the
window, and the sound of a Pi 3 is untested (QEMU has no PWM sound).
