# Lugforth for DOS (`ports/dos`)

A 32-bit protected mode DOS program, built with the DJGPP cross compiler:
the same C kernel and Forth boot as the other ports, cells of 32 bits. Needs
a 386 or better, about 5 MB of memory and a DPMI host. The prompt in text
mode, the files, and the `sdl2` words of the boards: graphics in a VESA
mode, the keyboard and the mouse, the sound of a Sound Blaster 16.

## Build

It needs the DJGPP cross compiler, `i586-pc-msdosdjgpp-gcc`. Debian and
Ubuntu have no package for it: `binutils-djgpp` is only the assembler
and the linker, without gcc or DJGPP's libc. `install-djgpp.sh` installs
the prebuilt one of <https://github.com/andrewwutw/build-djgpp> (v3.4,
gcc 12.2, Linux x86-64; the archive is checked against its SHA-256)
in `/opt/djgpp-x` if it can write there, else in `~/.local/djgpp-x`,
the two places the Makefile looks in; `install-djgpp.sh DIR` puts it in
DIR, then give `DJGPP=DIR/djgpp` to make. On another system, build it
with the `build-djgpp.sh` of the same project.

```sh
ports/dos/install-djgpp.sh           # or: make -C ports/dos djgpp
sudo apt install dosbox dosbox-x     # to run it and for make test
make -C ports/dos                    # out/dos/lugforth.exe (about 500 KB)
make -C ports/dos hdpmi              # out/dos/HDPMI32.EXE, the DPMI host (see below)
make -C ports/dos stage              # out/dos/disk: what to copy on a DOS disk
make -C ports/dos run                # DOSBox on out/dos/disk, in a window
make -C ports/dos test               # 38 checks in DOSBox (tools/dos_test.py)
DOSBOX=dosbox-x python3 tools/dos_test.py out/dos             # the same in DOSBox-X
DOSBOX=dosbox-x DOS_LFN=1 python3 tools/dos_test.py out/dos   # DOS 7.1 with long names
```

`out/dos/disk` (tools/dos_stage.py) holds LUGFORTH.EXE, HDPMI32.EXE,
the examples in GAMES, GRAPHICS, MUSIC and UTILS, `ed` and `writhdeck`
in LIB, the manual in DOC. Every name is cut to 8.3 the way DOS cuts it
(`gfx_breakout.fs` is GFX_BREA.FS): DOS without long names cuts the
names a program opens the same way, so `include games/gfx_breakout.fs`
and the `needs` of the editors find their files under their long names.
With long names (Windows 9x, DOSLFN, DOSBox-X as DOS 7) DOS cuts
nothing: then `files.h` tries a path that does not exist again cut to
8.3, so the same folder works, and real long names work too.

`make test` needs `dosbox` (apt). Its last eleven checks drive DOSBox in a
window of a virtual display with `xdotool` (`Xvfb`, `xdotool` and
ImageMagick installed; without them they are skipped): the text screen
read back by the word `screen-char`, `ed` and `writhdeck`, two beeps
recorded by DOSBox (Ctrl+F6) and measured, 2048 and breakout in VESA
from screenshots. DOSBox-X (apt) passes them too, but the recording of
the beeps (its shortcuts differ).

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
    When no 32-bit mode holds the screen even at zoom 1 (2048's 484x640
    in DOSBox, whose S3 has 1024x768 in 16 bits only), a 16-bit mode
    (5-6-5): the drawing goes to a copy in RAM, and each flip converts
    the rows that changed.
  - While a screen is open the keyboard is read by interrupt (IRQ 1,
    chained to the handler that was there, so Ctrl+Break still works):
    keys down and up for `pressed?`, characters for `key` (US, or
    French with `azerty`; in text mode DOS's KEYB decides); the mouse
    through the INT 33h driver.
  - Sound: a Sound Blaster 16 found from `BLASTER` (DOSBox sets it),
    44100 Hz stereo by auto-init DMA, filled at each pause: `beep`,
    `drum-hit`, `play`, the ABC tunes, the PCM queue. Without one the
    sound words do nothing.
  - The games of `examples/games` run (breakout and 2048 tried; the
    mouse not tried), from the folder of `make stage`: `include
    games/gfx_breakout.fs`, or `lugforth games/gfx_breakout.fs` from
    DOS (the game ends with `script-bye`, back to DOS).
- `ed` and `writhdeck`, read from LIB beside `lugforth.exe` the first
  time, as on the boards.
- `lugforth prog.fs` includes the file at start.

## Not yet

- `graphic` (drawing under the text at the prompt): the text mode has no
  pixels; VGA mode 13h for the cards without VESA 2.0; the PC speaker
  when there is no Sound Blaster.
- A disk or floppy image (`make stage` makes a folder).
- Tried in DOSBox 0.74 and DOSBox-X 2024.03 (with and without long
  names); not on a real PC, FreeDOS or Windows 9x.
