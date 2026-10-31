# Lugforth for DOS (`ports/dos`)

A 32-bit protected mode DOS program, built with the DJGPP cross compiler:
the same C kernel and Forth boot as the other ports, cells of 32 bits. Needs
a 386 or better, about 5 MB of memory and a DPMI host. The prompt in text
mode, the files, and the `sdl2` words of the boards: graphics in a VESA
mode, the keyboard and the mouse, the sound of a Sound Blaster 16.

## Build

The DJGPP cross compiler for Linux:
<https://github.com/andrewwutw/build-djgpp/releases> (v3.4,
`djgpp-linux64-gcc1220.tar.bz2`), unpacked in `/opt/djgpp-x` or anywhere
else given as `DJGPP=`:

```sh
make -C ports/dos                    # out/dos/lugforth.exe (about 500 KB)
make -C ports/dos hdpmi              # out/dos/HDPMI32.EXE, the DPMI host (see below)
make -C ports/dos run                # DOSBox on out/dos, in a window
make -C ports/dos test               # 30 checks in DOSBox (tools/dos_test.py)
```

`make test` needs `dosbox` (apt). Its last seven checks drive DOSBox in
a window of a virtual display with `xdotool` (`Xvfb`, `xdotool` and
ImageMagick installed; without them they are skipped): the text screen
read back by the word `screen-char`, two beeps recorded by DOSBox
(Ctrl+F6) and measured, breakout in VESA from screenshots.

## Run

DJGPP programs need a DPMI host. DOSBox has none: `make hdpmi` downloads
HDPMI32 of the HX DOS extender
(<https://github.com/Baron-von-Riedesel/HX>, freeware, not kept in the
repository). Start it once, resident, then Lugforth:

```
C:\> hdpmi32 -r
C:\> lugforth
```

On a real DOS, `CWSDPMI.EXE` (from DJGPP's `csdpmi7b.zip`) beside
`lugforth.exe` works too, without starting it first. Windows 9x and
FreeDOS with HDPMI have their own host.

## What works

- The prompt in text mode: the console is the screen through DJGPP's
  conio, and the ANSI codes of the line editor, the pager and the editors
  are interpreted in `main.c` (cursor moves, clearing, colors, the 256
  colors of xterm mapped to the 16 of the PC), so ANSI.SYS is not needed.
- The keyboard from the BIOS: arrows, Home, End, PageUp, PageDown, Insert,
  Delete, Ctrl+arrows, F1 to F4 become the escape sequences of a terminal.
  Ctrl+C is a key; Ctrl+Break stops a running word (`-28 throw`).
- History and Tab completion of the line editor, the pager (25 lines), the
  blocks and `blocks-editor`, `autoexec.fs` of the current directory at
  start.
- The file words, `ls`, `cd`, `pwd`, `md`, `rd`, through DJGPP's libc
  (`files.h`, the API of the boards' `fat.h`). Names are 8.3 unless DOS
  has long names (DOSBox has not).
- Floats (x87, or the emulator of DJGPP without one), faults (`-8 @`) caught.
- Redirected: `lugforth < in.txt > out.txt` reads the lines of the file
  and ends at its end; the output is written as it is (CR LF).
- The `sdl2` words (`ports/baremetal/sdl2.fs`, `screen.h`, `input.h`,
  `mixer.h`, shared with the Pi and the UEFI PC; `video.h`, `sb16.h`):
  - `screen` sets the smallest VESA mode of 32 bits per pixel with a
    linear framebuffer that holds the screen times its zoom (lowered if
    none does), centered, the rest black; `close-screen` gives the text
    mode back. Drawing, text, the built-in images (the tile sheets).
  - While a screen is open the keyboard is read by interrupt (IRQ 1,
    chained to the handler that was there, so Ctrl+Break still works):
    keys down and up for `pressed?`, characters for `key` (US, or
    French with `azerty`; in text mode DOS's KEYB decides); the mouse
    through the INT 33h driver.
  - Sound: a Sound Blaster 16 found from `BLASTER` (DOSBox sets it),
    44100 Hz stereo by auto-init DMA, filled at each pause: `beep`,
    `drum-hit`, `play`, the ABC tunes, the PCM queue. Without one the
    sound words do nothing.
  - The games of `examples/games` run (breakout tried; the mouse not
    tried); their names are long: copy them under 8.3 names
    (`gfx_breakout.fs` as `breakout.fs`) where DOS has no long names.

## Not yet

- `graphic` (drawing under the text at the prompt): the text mode has no
  pixels; VGA mode 13h for the cards without VESA 2.0; the PC speaker
  when there is no Sound Blaster.
- `ed` and `writhdeck`: their files have long names (`writhdeck.fs`,
  `eval_copy.fs`) that 8.3 DOS cannot open.
- A program given on the command line (`lugforth prog.fs`).
- Tried in DOSBox 0.74 only; not on a real PC, FreeDOS or Windows 9x.
