# Lugforth for DOS (`ports/dos`)

A 32-bit protected mode DOS program, built with the DJGPP cross compiler:
the same C kernel and Forth boot as the other ports, cells of 32 bits. Needs
a 386 or better, about 5 MB of memory and a DPMI host. First step: the
prompt in text mode and the files. No graphics or sound yet.

## Build

The DJGPP cross compiler for Linux:
<https://github.com/andrewwutw/build-djgpp/releases> (v3.4,
`djgpp-linux64-gcc1220.tar.bz2`), unpacked in `/opt/djgpp-x` or anywhere
else given as `DJGPP=`:

```sh
make -C ports/dos                    # out/dos/lugforth.exe (about 430 KB)
make -C ports/dos hdpmi              # out/dos/HDPMI32.EXE, the DPMI host (see below)
make -C ports/dos run                # DOSBox on out/dos, in a window
make -C ports/dos test               # 27 checks in DOSBox (tools/dos_test.py)
```

`make test` needs `dosbox` (apt). Its last four checks drive DOSBox in a
window of a virtual display with `xdotool` (`Xvfb` and `xdotool`
installed); without them they are skipped.

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

## Not yet

- Graphics (VGA mode 13h or VESA, through `ports/baremetal/screen.h`),
  the keyboard by interrupt (key releases, for games), sound (PC speaker,
  Sound Blaster).
- `ed` and `writhdeck`: their files have long names (`writhdeck.fs`,
  `eval_copy.fs`) that 8.3 DOS cannot open.
- A program given on the command line (`lugforth prog.fs`).
- Tried in DOSBox 0.74 only; not on a real PC, FreeDOS or Windows 9x.
