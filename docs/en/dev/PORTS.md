# Ports and platforms: state, limits, what was tested

Android, Raspberry Pi, UEFI PC, the web console and the Game Boy
Advance. Part of [README.md](../../../README.md).

## Android (ports/android, not buildable here)

`MenuBar.java` is the bar of both consoles: Tab, arrows, Ctrl (sticky:
next letter is a control code; sends F9, which
`ports/posix/sdl2_console.fs` turns into `ctrl-next`; the web page does
the same through `lugkey('Ctrl')` in `ports/web/platform.fs`), a "⋮"
button for Examples/A-/A+/Help, and home. To check once compiled: the
Ctrl button on a real soft keyboard (letters may arrive as key events
rather than text input), and that nothing shows that Ctrl is armed (an
indicator would help).

## Bare-metal AArch64 (ports/rpi) and shared code (ports/baremetal)

`ports/baremetal` holds what the Pi and the PC (ports/efi) share:
screen.h (framebuffer, `sdl2` words in C), console.h, input.h,
font8x8.h, sdl2.fs, files.fs, allocation.fs, autoboot.fs;
tools/baremetal_*.py (disk images, assets, QEMU runner/test) serve both.
Hardware specific files stay in ports/rpi (start.S, usb.h, virtio.h,
sound.h, storage.h, fat.h) and ports/efi.

`make -C ports/rpi test` builds
out/rpi/{virt/lugforth.elf,pi3/kernel8.img,pi4/kernel8.img} and boots
the first two under QEMU (`virt`, `raspi3b`), checking answers over the
serial line (tools/baremetal_qemu_test.py). Needs
`gcc-aarch64-linux-gnu picolibc-aarch64-linux-gnu qemu-system-arm` (apt,
installable in the cloud container). No OS, picolibc, PL011 console, MMU
identity map (needed: unaligned accesses fault with it off), faults
become THROW. Not tried on a real Pi; Pi 4 written blind (QEMU 8.2 has
no raspi4b). See ports/rpi/README.md. Graphics: `sdl2` short words on
the mailbox framebuffer (ports/baremetal/sdl2.fs, screen.h), keys from
the UART; `make -C ports/rpi breakout` plays
examples/games/gfx_breakout.fs in a QEMU raspi3b window (`BOARD=virt`:
virt with `-device ramfb`, set up through fw_cfg), `make test` checks
both by screendump. Text (font8x8), images (PNGs built in by
tools/baremetal_assets.py), keyboard/mouse (virtio-input on virt with
`-global virtio-mmio.force-legacy=false`, DWC2 USB HID on pi3; tested
through QMP input-send-event), sound (sound.h mixer, virtio-sound on
virt checked from a -audiodev wav file; Pi 3 PWM+DMA written blind),
`paste` (a whole file over serial, then Ctrl-D: multi-line strings
work). `make roguelike` runs examples/games/gfx_roguelike.fs. QEMU runs
a whole bcm2835 DMA chain at once: a looping chain hangs it (even
SIGTERM), so never start one blindly. Files: storage.h (Pi SDHCI/EMMC
polled, virt virtio-blk) + fat.h (FAT12/16/32, MBR or not, long names
read and written, mkdir/rmdir/chdir, dates from the clock), the standard
file words, so include/ls/cat work; autoexec.fs runs at boot.
tools/baremetal_mkdisk.py makes images (needs dosfstools mtools); make
test checks them afterwards with mtools and fsck.fat.

## Bare x86-64 PC, UEFI (ports/efi)

Decision (2026-10-18): no x86 port without UEFI (own boot,
VBE/Multiboot2 framebuffer, PS/2, AHCI/NVMe drivers and FAT on them):
too much work for a narrow target. If ever wanted, Multiboot2 through
GRUB is the shortcut. The pointer is kept in real screen pixels
(`in_mouse_rx/ry`) and drawn in `fb_cursor_flip` straight into the
framebuffer after the zoomed flip, so under `3 to zoom` it moves by one
pixel, not three (`mouse-x` still gives logical pixels). Not seen on a
real mouse yet. Known limits: OVMF never polls USB mice (no mouse under
QEMU, only on hardware: `n mouse-speed`, default 20 = 0.20 pixel per
count); no key releases from UEFI, the firmware repeat starts 500 ms
after the press (breakout stutters on the keyboard at first). Video
memory is write-combining: keep the `sfence` in `dcache_clean`
(screen.h). The console syncs at most every 100 ms during output
(console.h), else when waiting for a key: shorter shows editors' redraws
half done. `common/lineedit.fs` (history, Tab on words and files) and
`common/pager.fs` (stops output at each screenful, space/Enter/q,
`pager-off`) are loaded by both bare-metal boots and the posix ones. The
pager wraps `type`, `key`, `key?`, `prompt`; it acts only while `'tib`
is the console buffer (not in an included file), on a tty (posix) or
when `con-rows` is not 0; `q` is `-28 throw`, silent in `quit` (`ls` and
`cat` close their handle on a throw). tools/pager_pty_test.py checks the
posix side. The screen, input, sound and file opcodes are shared in
ports/baremetal/builtins.h (`BM_VOC` = rpi or pc, `input_flush` defined
by each main.c). Duplication left: the common part of the two
platform.fs (azerty, console-zoom, keys).

`make -C ports/efi test` builds out/efi/BOOTX64.EFI (gcc + gnu-efi) and
boots it under QEMU q35 + OVMF from a USB stick image (MBR + FAT32,
tools/baremetal_mkdisk.py), 129 checks (tools/efi_qemu_test.py, which
reuses baremetal_qemu_test.py). Needs
`gnu-efi ovmf qemu-system-x86 dosfstools mtools`. Stays in boot
services: GOP screen, ConIn keyboard (no releases: a key is held 120 ms
after its last press; the test uses `-device usb-kbd`, PS/2 does not
repeat), the stick's FAT through Simple File System (ports/efi/files.h,
same API as fat.h). Reuses ports/baremetal (screen.h, console.h,
input.h, font8x8.h, sdl2.fs, files.fs...), shared with ports/rpi;
vocabulary `pc` aliased `rpi`. Console and `fb_flip` (ports/baremetal)
draw in RAM and write only changed pixels, never read video memory;
mouse polled from the firmware pointers (untestable under OVMF, trackpad
may not be exposed). Speed on real hardware not measured yet. efi_main
is called SysV by gnu-efi's crt0 (NOT EFIAPI: it crashed); own libc in
libc.h (x87 math), setjmp and fault stubs in start.S (patched copy of
the firmware IDT). Sound: minimal Intel HD Audio driver
(ports/efi/hda.h, mixer shared in ports/baremetal/mixer.h with
rpi/sound.h), checked by the test through `-audiodev wav` with QEMU
intel-hda + hda-duplex (`make run` has them too, AUDIODEV=pa);
`sound-off` / `sound` keep the choice in lugforth.ini
(ports/efi/sound_ini.fs); not tried on a real PC. Booted from a real USB
stick (prompt, gfx_3d, roguelike work); ports/efi/README.md has the
steps (Secure Boot off, one-time boot menu). Other file systems:
`load-driver` (pc, files.h: LoadImage from a buffer, StartImage,
connect_all) and ports/efi/disk/drivers.fs (on the stick, NOT run at
boot) load EfiFs drivers from EFI/drivers/ (exfat_x64.efi, ext2_x64.efi:
read only, GPL, not in the repo). EFIFS=dir make test adds 7 checks
(exFAT and ext4 disks made with mkfs.exfat / mkfs.ext4 -d; exfatprogs
installs with apt). `volumes` / `n mount` (files.h, platform.fs) switch
the FAT volume of the file words (refused while a file is open, the old
volume flushed): tested with a second stick and a hot-plugged one under
QEMU, not on hardware. The efi test builds here with gnu-efi and ovmf
unpacked from `apt download` (no root):
`make EFIINC=<root>/usr/include/efi EFILIB=<root>/usr/lib OVMF=<root>/usr/share/OVMF/OVMF_CODE_4M.fd test`.
The dead key test is flaky.

## Terminals (Alt+F1..F4) on rpi and efi - done 2026-10-20

`ports/baremetal/term.h` (early: `term_cur`, `term_foc`,
`TERM_FOCUSED()`) and `term_run.h` (late: the scheduler). Each terminal
is another `forth_init` on its own heap (32 MB after the first; all the
interpreter state is in `g_sys` and that heap) and has its own screen
state, swapped by `term_save`/`term_load` (the `TERM_FIELDS` list:
console cursor/colors/`con_shadow`, `fb_back`, `fb_w`..., `images`).
`forth_run` returns to the main loop at each `pause`, where
`term_next()` picks the next terminal (cooperative: a loop that never
pauses holds the others). Shared and written by the focused terminal
only: `con_front`, `fb_front`, `fb_mem` (`con_sync`, `fb_flip`,
`fb_hw_open` return when not focused), the keyboard
(`getchar_timeout_us`, `console_extra_char`, `input_snapshot`...), the
serial output (`RAW-TYPE`). A focus switch redraws the whole screen
(`term_present`). Alt+Fn is caught in `input_key` (input.h, shared);
`term_poll` (in `RAW-YIELD`) reads the keyboard at each pause so a
program that never reads keys can still be left. Words: `term`,
`terminal`, `terminals` (ports/baremetal/terms.fs, 1-based). `bye`
outside the first closes the terminal (`term_bye`). `autoexec.fs` runs
in the first only. Traps: the opcode macro `X` clashes with an X-macro
list (use `F`); `also rpi` inside a colon definition compiles, it does
not search (use a helper defined after a top level `also rpi`); a word
the interpreter does not find does not abort the rest of the line (`v @`
with an unknown `v` faults on the garbage left). Tested: efi test
`terminals()` (10 checks) and by hand with a game left and found again;
rpi not built (no aarch64 toolchain). Not done: a status indicator of
the terminal number on the screen, sharing `cwd`, closing the first
while others live, F5+ terminals.

## Web (ports/web)

- The cells are 32 bits (the other builds have 64): a 16.16 fixed point
  product must go through `*/` (see `examples/games/gfx_raycaster.fs`).
  Pages are copied to `out/web` by `ports/web/BUILD` and must be served
  from there (`ninja web && cd out/web && python3 -m http.server`);
  `tools/make_pages.py` copies them to `demo/` on the site.
- Web console, 2026-10-22: `graphic` and the drawing words typed at the
  prompt open a drawing area above the text (`S.layer`, `S.need` in
  `ports/web/sdl2.fs`; a fixed canvas and a spacer); a key press or a
  tap unlocks the sound; on a phone a hidden textarea takes the
  characters (`ports/web/platform.fs`), the prompt is kept above the
  keyboard (`context.KeepInView`), and the cursor is redrawn after every
  escape code. Checked in headless Chromium over CDP, on desktop and
  with an emulated phone; not with a real keyboard.
- Traps are in [PITFALLS.md](PITFALLS.md) (the stack pointer `sp`,
  `[defined]` in a definition, encoding, keypress).

## TIC-80 (ports/tic80) - started 2026-10-27

A wasm32 module for the TIC-80 fantasy console. TIC-80 dropped its
pForth binding from its releases (nesbox/TIC-80#3133) because pForth
reads and writes the host's memory; here every address is an offset in
the module's linear memory, an access past it traps, and the only ways
out are the imports of the module `tic80` (`api`, `type`, `ticks`, the
math functions), whose addresses the host checks. No CALL, DLSYM, file
or task words.

- Build: `make -C ports/tic80` (clang with the wasm32 target and
  wasm-ld, no sysroot: `include/` and `libc.h` are the bits of a C
  library the kernel needs, with a first-fit `malloc` over
  `memory.grow`). Gives `out/tic80/lugforth-tic80.wasm` (about 75 KiB
  with the boot text). `-mcpu=mvp`; the memory may grow to 16 MiB
  (`--max-memory`), a limit in the module. The boot header is plain C
  (`importation.py --header c`).
- TIC-80 does not interpret the module: `make -C ports/tic80 c`
  translates it to C with wasm2c (package wabt), and TIC-80 compiles
  that with the wasm2c runtime in bounds-check mode (its
  `vendor/lugforth/`). First tried with wasm3, the interpreter TIC-80
  has for its wasm language: an interpreter running an interpreter, 25
  times slower than native Lugforth (1e6 DO LOOP: 1.3 s; boot 90 ms).
  With wasm2c: 13.5 ms (Lua in TIC-80: 10.5 ms), boot 11 ms.
- The inner interpreter switches on the opcode (`ports/tic80/interp.h`,
  as the MSVC build): wasm has no computed goto. A division by zero
  throws -10 (`DIV0_CHECK`); a forged xt or a stack pointer set anywhere
  stays in the module.
- Exports: `lf_init`, `lf_alloc`/`lf_free`, `lf_eval`, `lf_find`,
  `lf_call`, `lf_call1` (SCN, BDR, MENU take the row), `lf_depth`. The
  commands go through `'host`, read by `host-loop`
  (`ports/tic80/platform.fs`), which yields back to the host after each;
  the stack is emptied before each command, so a TIC that leaves cells
  shows in `lf_depth`. After a trap the instance is half way through a
  word: the host drops it.
- `api.h` is the list of the 56 API words, with the names and stack
  effects of TIC-80's pForth binding (`src/api/forth.c` there): EXITGAME
  and KEYPRESSED, not EXIT and KEY. TIC-80 keeps a copy of it.
- Test: `make -C ports/tic80 test` (node: boot, commands, API stack
  effects, traps). With a cartridge as second argument,
  `node ports/tic80/test_host.js out/tic80/lugforth-tic80.wasm cart.fth`
  loads it and calls BOOT and TIC. Checked 2026-10-27 with TIC-80's
  `demos/forthdemo.fth`, `demos/bunny/forthmark.fth` and
  `tests/conformance/probes/forth.fth` (334 API calls, no probe threw;
  in TIC-80, no divergence from Lua).
- Cells are 32 bits, as on the web.

## Game Boy Advance (ports/gba)

A prototype with a roguelike. See `ports/gba/README.md`; the boot-chain
traps met are in [PITFALLS.md](PITFALLS.md).

Since 2026.10.30.7 a second cartridge, `gba_magnoquill_boot.fs`: the
point cloud of `gfx_magnoquill.fs`, its maths in C in fixed point
(`GBA-MAGNO`), at about 20 pictures a second for 400 points under mgba
(checked there, not on a console). The linker script now has an IWRAM
section and aligns the ROM images that `crt0.s` copies; the roguelike
and the tile demo still boot after that (screenshots under mgba).

## ESP32: ABC tunes on a buzzer

`ports/esp32/abc.fs` (loaded after common/abc.fs): `esp32-abc` evaluates
the reader on demand (its words go into a hidden vocabulary `sdl2` in
internals, which common/abc.fs expects; only `buzzer`, `tone`,
`abc-play`, `abc-arp` and `tune-play` are put in forth). `N buzzer` sets
a passive piezo or a speaker on GPIO N (Arduino core 3: pin and Hz; core
2: channel 0 and milli-Hz, chosen by `DEFINED? ledcAttach`). A key on
the serial line stops a tune.

Voices: common/abc.fs reads `V:n` (and `[V:n]`) when `voices?` is -1
(the ESP32 loader and POSIX sdl2_abc.fs set it; the web build skips
them): each voice has its own time, events carry `nv@`, `n-voices`
counts them, and the events are then not in time order. `gather` lists
the notes sounding at a tick (voices and chord members, voice 1 with
`melody-weight`) and when the list changes.

`abc-play` mixes up to 4 of them (voice n has slot n-1, so its wave goes
on from note to note; the volume of a voice is fixed for the tune,
`vol-unit` times its weight, and shared by the notes of its chord; voice
1 is a half-period square wave, the others a quarter-period pulse):
`mix-sample` adds 4 phase accumulators (24 bits, square waves) and
writes 128 + the sum as the duty of an 8 bit LEDC at 78 kHz
(`ledcChangeFrequency`, `ledcSetup` on core 2). The loop has no clock:
`mix-rate!` counts the samples made in 50 ms and the increments are made
from that. It runs blocks of 128 samples, reads `ms-ticks` between them,
and calls `pause` every 16 blocks (the watchdog of the idle task is a
risk on a C3, not tried). `abc-arp` is the first version: one LEDC tone,
the next sounding note every `slice-ms`. Tested in out/esp32-sim with
the `ledc` words stubbed (the simulator has no LEDC opcodes): 439 Hz for
440, four levels for two voices; not on a board. Traps: the namespace
tests of common/forth_namespace_tests.fs list the words of the esp32
boot, so a new word there means a new `out:` line; `abc-parser` is in
internals and its text does `also sdl2`, which has to be reachable when
it is evaluated; a `)` inside a ( comment ) of the text ends it and the
rest is run as code.

### ESP32: files, the editor, sending files

`/spiffs` is mounted by `platform.fs` (`SPIFFS.begin`, format if it
fails); `autoboot.fs` includes `/spiffs/autoexec.fs` (a key within 100
ms skips it) and `remember` keeps `/spiffs/myforth`. The `SPIFFS.`
words live in the vocabulary `SPIFFS` (`also SPIFFS` first). `ls` and
`cat` come from common/filetools.fs. An upload from the IDE keeps the
partition, a change of partition scheme or "Erase All Flash" does not.

`tools/board_put.py PORT file...` (called by the short names
`tools/esp32_put.py`, names under `/spiffs/`, and `tools/pico_put.py`,
names without it; or `--esp32` / `--pico`) sends files: DTR high and RTS
low when opening (a native USB port sends nothing without DTR, RTS high
resets a board with auto-reset: both gave a dead or vanishing port), a
wake-up of empty lines until " ok", then Forth lines: `hx` (parses a
hexadecimal token, writes its bytes with `write-file`), 90 bytes a line,
each line waited for until " ok" and the prompt "> " (a fixed wait after
each line made it 4 times slower). The prompt takes 200 characters a
line (`input-limit`, common/io.fs), which also cuts a long `s" ..."
abc-play` typed by hand; `r~ ... ~` does not continue on the next line
at the prompt, only inside a file. Tested against a pseudo-terminal that
answers like the board and in lugforth POSIX; the transfers of the
user's board work (a 30 KB file in a few seconds).

`ports/esp32/editors.fs` (in the boot): `ed` includes
`/spiffs/miniedit.fs`, which redefines `ed`, then evaluates `ed name`
(`lz-run`); `terminal-size`, `form`. The ed of the other ports
(editors/editor.fs, writhdeck.fs) was tried first: it compiles in the
simulator (it needs a `rpi` vocabulary with `mkdir`) but takes about 80
KB compiled and 140 KB while loading (the file read in memory, its r|
string copied, `eval-copy`'s copy, the code), and an ESP32-C3 had 71 KB
of `unused` left: "Error including" on the board. So
`ports/esp32/miniedit.fs`, a small ed of its own (about 6 KB compiled,
no ANSI vocabulary: it writes the escape codes itself, redraws only
when no key waits, reads the sequences ESC [ A B C D H F and ESC [ n ~).
Its editing is tested in out/esp32-sim by replacing `key` and `key?`
(only inside the word that runs it: the prompt reads with `key`
too); files words are not in the simulator, and were tried in lugforth
POSIX. Running on the board: the user's. Traps: `?do` needs (limit
start): `2dup < if swap ?do` after `a b`; `{ }` locals not in loops;
sending the 200-character limit to the simulator through a pipe cuts the
lines like the board does.

POSIX tunes (`ports/posix/sdl2_abc.fs`) now set `voices?` too: the
MIDI file is made from the events sorted by time (an end before a start
at the same time) instead of groups of notes that start together, and
`tune-ms` is the end of the longest voice (`tune-end` in common/abc.fs).
`test-voices` is in sdl2_abc_tests.fs. The web build now sets `voices?` too (WebAudio plays each note with its
own oscillator, so the voices sound together).

## Pico: ABC tunes on a pin

`ports/pico/builtins.h` now has the PWM words of the pico-sdk in the
vocabulary `pico` (`pwm_gpio_to_slice_num`, `pwm_set_wrap`,
`pwm_set_clkdiv_int_frac`, `pwm_set_gpio_level`, `pwm_set_enabled`;
`hardware_pwm` in CMakeLists.txt), and `ports/pico/abc.fs` is
`ports/esp32/abc.fs` with that hardware layer: `pico-abc` loads the
reader on demand, `18 buzzer` sets the pin, then `abc-play`, `abc-arp`,
`tone`. The mix is the same loop (4 square waves, 24 bit phases, a
calibrated sample rate from `ms-ticks`) written as the level of an 8 bit
PWM (wrap 255, divider 1, about 488 kHz); a plain tone uses a counter at
1 MHz (divider 125) and wrap 1e6/hz - 1. The board of the first tests is
a Raspberry Pi Pico of 2020 (RP2040) on a Cytron Maker Pi Pico V1, the
buzzer and the jack being on GP18. It was built here (the pico-sdk is
the submodule of ports/pico-ice; `ninja pico`) and the Forth part ran in
out/pico-sim with the `pwm_` words stubbed, and the sound was heard on
the board. Trap: a `)` inside a ( comment ) of the text evaluated by
`pico-abc` ends it, as for the ESP32.

`paste` (ports/pico/platform.fs, as in ports/rpi): the text up to Ctrl-D
is kept and evaluated at once, so a string over several lines or a line
of more than 200 characters works; tools/pico_evaluate.py uses it by itself
for such a file (a line by line send gave NOT FOUND on the second line
of an r~ tune). Tested against a fake board and in out/pico-sim, not on
a board.

### Pico: files in the flash (ports/pico/flashfs.h)

The file words of the Pico build (`REQUIRED_FILES_SUPPORT` in
`ports/pico/builtins.h`) are a small file system in the last 1 MiB of
the flash (offset from `PICO_FLASH_SIZE_BYTES`; a guard refuses it if
`__flash_binary_end` goes over). 256 sectors of 4 KB: sectors 0 and 1
hold two copies of the directory (magic `LFFS`, a sequence number, a
CRC32), the others hold data, chained like a FAT by a table `next[256]`
(0 free, 255 end of chain). 119 entries of 32 bytes (name 23 characters,
first sector, size). The files 0, 1 and 2 stay the console (`write` and
`read` of newlib: the Forth `type` is `write-file` on the fid 2); the
ones of the flash are numbered 10 to 13.

Writing goes through one 4 KB RAM buffer into sectors the live directory
marks free; `close-file` writes the directory into the unused copy with
a higher sequence number, so replacing a file is atomic and a power cut
leaves the old state (the mount takes the newest copy whose CRC is
good). Reading uses the XIP memory directly. Each sector is erased and
programmed with `flash_range_erase` / `flash_range_program` with the
interrupts off (about 45 ms and 7 ms), which stops the USB for a moment.
`ls`, `cat`, `rm` come from `common/filetools.fs` through `OPEN-DIR` and
`READDIR` (`ports/pico/bindings.fs` gives `read-dir`); `resize-file`
only accepts a size not larger and `flush-file` does nothing (the data
is written at close), so the blocks do not work. The heap of the chip is
180 KB (the boot with `phase_filetools.fs` takes about 17 KB more than
before).

In `out/pico-sim` the flash is an array of 1 MiB with the semantics of a
NOR flash (a write can only clear bits, an erase sets them to 1), kept
in the file named by `LUGFORTH_PICO_FLASH` between runs. Tried there:
create, replace, read, `ls`, `cat`, `rename-file`, `delete-file`,
`include`, the free sector count, 48000 bytes in one file, a name of 24
characters (error 22), a missing file (error 2), a file left open at the
exit (the old one stays), the files after a restart, and 10000 random
bytes sent by `tools/pico_put.py` (compared with the flash
image). Not tried on a board: the real erase and program calls, the USB
stalls, and whether a UF2 reflash keeps the files (it should: it only
erases the sectors it writes, whereas `flash_nuke` and `picotool erase`
wipe everything). A trap met on the way: the first version sent the
console `write-file` to the flash and the simulator crashed at start,
before the banner.
### Random tunes (examples/music/abc_random.fs)

A small generator of ABC text for the PC, the Pico and the ESP32. It
uses only `random` and `rand-seed` of common/ansi_compat.fs, whose low
bits give the same sequence on 32 and 64 bit cells: the seed 1234 makes
the same tune on the PC and in out/pico-sim. A tune is 12 bars in 4/4 of
the notes of one of ten keys (no accidentals to write: the notes are
steps of the scale of C and the K: line puts the sharps and flats),
three phrases of four bars from five progressions of chords (major or
natural minor), the last phrase ending V then I. Voice 1 is an arpeggio
of the chord in eighths and quarters, going up and down the root, third,
fifth and octave; voice 2 is, bar by bar and by a mode chosen for the
tune, a whole or two half chords, a bass of root and fifth, or four
quarters of the chord moving against the melody. The text goes in two
buffers (1600 and 1000 bytes) and the second is appended to the first.

The player is the one of the board. ports/pico/abc.fs and
ports/esp32/abc.fs call `halt?` every 16 blocks of the mix (and in the
arpeggio): the word given to `abc-halt-with` or, by default, a key on
the serial line. `random-music` gives its own word, which looks at the
button (`music-button`: GPIO with the pull-up, pressed when 0) and at
the keys. Written for the Pico and tried in out/pico-sim, where gpio and
pwm words are stubs: the button word returns true and sets the request,
a key ends the loop. On the PC and in the browser the player loops the
tune itself (`abc-loop`) and the keys are polled, any key asking for
another tune; the file loads `sdl2-abc` by itself when there is no
player. The browser page is
`terminal.html?run=abc_random.fs,abc_random_web.fs` (`abc_random_web.fs`
only calls `random-music`); tried in headless Chromium over the DevTools
protocol (the tune is announced, a key gives another one), the sound
itself not heard. `save-tune` is defined only where `w/o` exists, so not
in the browser. examples/music/abc_random_tests.fs reads 200 tunes with
the reader of common/abc.fs (two voices, 12 bars each) and checks that a
seed gives the same text (`make -C examples test-ports`). Not tried on a
real button, nor on an ESP32 board. Traps met: `defined?` cannot be used
inside a definition (the conditional parts are compiled at the top
level); `?do` with a negative count runs away (`t+note` tests the octave
first); the Pico prompt cuts a line at 200 characters, which also cut a
long test stub.


## DOS (ports/dos) - started 2026-10-31

`lugforth.exe` built with the DJGPP cross compiler (gcc 12.2, from
github.com/andrewwutw/build-djgpp releases; Debian and Ubuntu have only
`binutils-djgpp`, no gcc nor libc: `ports/dos/install-djgpp.sh`, or
`make -C ports/dos djgpp`, downloads it, checks its SHA-256 and unpacks
it in `/opt/djgpp-x` or `~/.local/djgpp-x`, where the Makefile looks): 32-bit protected mode, cells of 32
bits, 4 MB heap. First step done: the prompt in text mode and the files.
`main.c` interprets the ANSI codes into conio calls (no ANSI.SYS), maps
the BIOS's special keys to terminal sequences, uses `uclock` for
MS-TICKS, signals for faults (`setjmp` in FAULT_ENTRY), stdout in binary
when redirected. `files.h` gives the `fat_*` API of the boards over
DJGPP's libc (errno mapped to the Unix numbers, directories read with
findfirst/findnext), so BM_FILES_SUPPORT of ports/baremetal/builtins.h
and ports/baremetal/files.fs serve unchanged; the vocabulary `dos` is also
named `rpi` for common/pager.fs. DJGPP's libm has no `fmin` / `fmax`
(defined in main.c).

Second step done the same day: the `sdl2` words. `video.h` sets the
smallest VESA mode of 32 bits with a linear framebuffer that holds the
screen (mapped by `__dpmi_physical_address_mapping`, written through a
near pointer: `_CRT0_FLAG_NONMOVE_SBRK` keeps it valid) and lets
ports/baremetal/screen.h draw in it as on the boards; while it is open
the keyboard is read by interrupt (IRQ 1, chained with
`_go32_dpmi_chain_protected_mode_interrupt_vector`, so DJGPP's Ctrl+Break
and the BIOS still run; the BIOS's copy is emptied) and set-1 scan codes
go to input.h as Linux key codes; the mouse through INT 33h.
`close-screen` sets text mode 3 back (and `atexit`). `sb16.h` is a
Sound Blaster 16 output for ports/baremetal/mixer.h, on the model of
hda.h: 44100 Hz 16-bit stereo, auto-init DMA on the high channel of
`BLASTER`, a ring of 8192 frames in conventional memory filled behind
the DMA position at each pause. screen.h now builds on a 32-bit i386:
`line_wide` (int64_t without `__int128`) and no `sfence` without SSE.
In DOSBox 0.74 breakout runs at zoom 2: no 32-bit mode with a linear
framebuffer held its 960x720 at zoom 3, and `screen` lowers the zoom.

Third step: `make stage` (tools/dos_stage.py) makes out/dos/disk with
every name cut to 8.3 as DOS cuts it (base 8, extension 3): DOS without
long names (DOSBox included, checked) cuts the names a program opens the
same way, so the long names in the sources (`include
games/gfx_breakout.fs`, `needs eval_copy.fs` in editor.fs) find the
files; two names that would collide are reported. `ed` and `writhdeck`
are read from LIB beside the program (`home`: the folder of argv 0).
`lugforth prog.fs` (autoboot.fs, as on Linux). The ANSI interpreter of
main.c has a terminal's pending wrap: a character in the last column
does not move to the next line until the next character (ed's status
line on the last row scrolled the screen and hid the text).
editors/editor.fs keeps its theme colors in a byte table now: the 48-bit
literal of before did not fit 32-bit cells (wrong colors of numbers and
names with 32-bit cells).

Fourth step: DOSBox-X (apt) as a second emulator: `DOSBOX=dosbox-x`
runs the same test (all but the recording of the beeps: its shortcuts
are F12 + ..., not Ctrl+F6), `DOS_LFN=1` with DOS 7.1 and long names.
With long names DOS no longer cuts the names, so files.h tries a path
that does not exist again cut to 8.3 (`path_short`, the rule of
dos_stage.py). A 16-bit VESA mode (5-6-5) when no 32-bit mode holds the
screen even at zoom 1 (2048's 484x640: DOSBox's S3 has 1024x768 in 16
bits only): screen.h draws in a copy in RAM, `dos_fb_flip` converts the
rows that changed (memcmp with the copy of the last flip);
`dos_fb_open` refuses 16 bits while a 32-bit mode exists at zoom 1, so
sdl2.fs lowers the zoom instead (breakout stays at zoom 2 in 32 bits:
converting 960x720 at each flip was too slow in DOSBox, the ball did
not move). Both are macros over the functions of screen.h (`#define
fb_flip dos_fb_flip` after screen.h, before builtins.h). Traps met: a
run that never ends with stdin redirected (a game) filled the disk with
its output, run DOSBox under `ulimit -f`; Xvfb killed with SIGKILL
leaves /tmp/.X<n>-lock and the next one on that display does not start
(the test uses `-displayfd` and SIGTERM).

Turnkey: `lugforth --turnkey prog.fs [prog.exe [data...]]` (autoboot.fs
for the list and the writing, files.h `bundle_*` for the reading): the
.exe is lugforth.exe + the files + an index + a 16-byte trailer ("LFTK",
index offset, count, main file); at start main.c looks for it in argv[0]
(DJGPP gives the full path); `ok` (platform.fs) then skips the banner
and autoexec includes the main file and says bye. The files are found by
the name `open-file` gets (lower case, `/`, no `./`), the list is
built by a text scan of the sources, not by running them.

Test: `make -C ports/dos test` (tools/dos_test.py, 40 checks): DOSBox
0.74 without a window, `lugforth < in.txt > out.txt`, then DOSBox in a
window of Xvfb typed into with xdotool, the screen read back by the word
`screen-char`, two beeps recorded by DOSBox itself (Ctrl+F6 writes a wav
in the `captures` folder of its configuration, measured with the
functions of baremetal_qemu_test.py), breakout from screenshots (`import
-window root rgb:`). DOSBox needs `SDL_AUDIODRIVER=dummy` here, or it
stops without a sound device. DOSBox has no DPMI host: `make hdpmi` downloads HDPMI32
(HX DOS extender, freeware) from its GitHub release (delorie.com, the
home of CWSDPMI, is not reachable from the cloud sessions). The files
DOSBox creates have capital names on the Linux side.

Next: a disk or floppy image of out/dos/disk; VGA 13h and the PC
speaker for older machines; `graphic`. The mouse is not tested (DOSBox's moves
with the X pointer, not tried). Not tried on a real PC, FreeDOS or
Windows 9x.

## The bare-metal disk and the USB stick

The folders of `examples/` are kept (`include games/gfx_snake.fs`,
`system/varvara/varvara_rpi.fs`), with `lib/` (editors), `MANUAL.md` and
`WORDS.md` (docs/), `autoexec.fs`. Built by the Makefiles of ports/rpi
(`disk`) and ports/efi (`usb`, `stage`).

## SDL2 on a Linux text console (KMSDRM, no X)

Reported 2026-10-29, tested by the maintainer on a tty (Ubuntu 24.04,
SDL 2.30.0, proprietary NVIDIA driver 595): `out/posix/lugforth`
started from a tty, with no X server and no Wayland, opens its `sdl2`
windows. The binary is linked to the system libSDL2, which on
Debian/Ubuntu has the KMSDRM video driver (libdrm, libgbm): with no
`DISPLAY` and no `WAYLAND_DISPLAY`, SDL picks it by itself and draws
through DRM/KMS. Keyboard and mouse then come from evdev, so the user
must be in the `input` group (or root). Force it with
`SDL_VIDEODRIVER=kmsdrm`. `lugforth-static-sdl` cannot do this: its SDL
is built for X11 only. Since 2026.10.29.5 `con-open`
(`sdl2_console.fs`) no longer refuses to open the console window when
`DISPLAY`, `WAYLAND_DISPLAY` and `SDL_VIDEODRIVER` are all unset.
Not testable from a graphical session (`kmsdrm not available` while
another client owns the DRM master).

What was found on that tty (2026-10-30):

- `gfx_magnoquill.fs`, `gfx_mouse_test.fs`, `gfx_demo.fs` and
  `desktop.fs` run, with sound, with no `SDL_AUDIODRIVER` and no
  `AUDIODEV` (`alsa`, `pulseaudio` and `pipewire` all work). The first
  runs of the demo and the desktop froze; with `SDL_AUDIODRIVER=dummy`
  they did not, and later runs did not freeze either: the cause was not
  found. Opening and playing sound alone never hung, with any driver.
- SDL shows **no mouse cursor** under KMSDRM (the clicks work). Since
  2026.10.30.6 `flip` (`ports/posix/sdl2.fs`) draws a small arrow at
  `mouse-x mouse-y` when `screen` found the `KMSDRM` driver
  (`soft-cursor`; SDL names the driver in upper case, the first version
  compared with `kmsdrm` and never matched), unless the program asked
  `0 cursor` (as `gfx_mouse_test.fs`, which draws its own cross). The
  desktop also shows its frame five times a second there. Not drawn by
  the SDL console (`sdl2_console.fs`) nor by `varvara.fs`, which
  present the frame themselves.
- `Error including: ...gfx_demo.fs` after Esc is a caught `-9`
  (segfault) in `SDL_DestroyWindow`, and in `SDL_Quit` too (it destroys
  the windows), once frames were shown (a window opened and closed
  without drawing did not crash). Probably a KMSDRM/GBM bug of SDL
  2.30.0 with the NVIDIA driver (not proved). Closing the audio device
  first, waiting 300 ms, or destroying the window before the renderer
  did not help. The message is harmless: the tty stays usable.
- **Do not skip `SDL_DestroyWindow` and `SDL_Quit` on KMSDRM** to avoid
  that message (tried in 2026.10.30.2, reverted): the demo displayed,
  then the tty was dead (no keyboard; the maintainer had to kill
  lightdm), most likely because `SDL_Quit` gives the console and the
  keyboard back. Same after killing a program that holds the screen.
  If the keyboard is dead, from another console `sudo kbd_mode -u -C
  /dev/tty1` may help (not tested).
- Audio card: `AUDIODEV=plughw:0,0`, which `main.c` forces for the
  static build, would be wrong on that PC (card 0 is the NVIDIA HDMI,
  with no device 0; the sound card is card 1, `plughw:1,0`).
- `make -C examples smoke`: `gfx_magnoquill.fs` did not stop under
  `timeout 5` on this PC (dummy drivers, also with the previous
  `sdl2.fs`), where it runs fine by hand. Not understood.

## SDL2 on Windows and macOS

The bindings of `ports/posix/sdl2*.fs` ask for their library by a word
(`sdl2-lib`, `sdl2-ttf-lib`... in `ports/posix/posix.fs`: Linux `.so.0`
names, macOS `.dylib` looked up in the system then in the Homebrew
folders). On Windows `ports/windows/windows_sdl2.fs` makes a small
`posix` vocabulary (`shared-library` = `dll`, `sign-extend`,
`SDL2.dll`...) and `sdl2` / `sdl2-ttf` include `lib/sdl2.fs`,
`lib/sdl2_ttf.fs` (copies of the posix files, put in the zip by the
`windows` job: the sources are too big for the MSVC string of the boot).
That job downloads the official SDL2 and SDL2_ttf DLLs and runs
`ports/windows/sdl2_test.fs` (dummy drivers). Tested: Windows (window,
pixel, TTF) and macOS (`sdl2_tests.fs`, 38) in CI on 2026-10-23, version
2026.10.23.2. Not bundled: SDL2_image, SDL2_mixer. The console window
(`--gui`, `gui`) is the posix `sdl2_console.fs` and
`sdl2_console_ttf.fs`, copied in `lib/`, with more words in the `posix`
shim (`sysfunc` through msvcrt.dll, `dlsym`, `readdir`, `.d_name`,
`.d_type`, `isatty`, a `termios` with `form-hook`) and `win?` in the
console (OS=Windows_NT: no DISPLAY test, `%APPDATA%\lugforth`,
`C:/Windows/Fonts`). The Examples menu walks three levels of folders on
every platform. Windows directories: `ports/windows/windows_dirs.fs`
(FindFirstFile); paths: `/` becomes a backslash in `open-file`, `ends/`
knows both. Traps met testing under wine (`wine lugforth.exe t.fs` with
output written to a file: the console output is not on stdout): `[']`
does not work at the prompt (use `'`), and (before 2026.10.27.2)
`<# #s #>` took a single cell.

## macOS (ports/posix)

Fixed in 7.0.11.16 with no Mac at hand, by running the `macos` job of
`.github/workflows/release.yml` (branch `macos-fix` had a job
`macos-test.yml` with tracing steps: `fs_usage`, `sample`, a pty
session). The start looped because of Apple arm64:
- a call through a `(...)` pointer puts every argument on the stack, so
  `common/calls.h` has, for `__APPLE__` + `__aarch64__`, one cast per
  argument count; `open`, `fcntl` and `ioctl` are variadic, so `DLSYM`
  (ports/posix/main.c) hands out fixed-argument wrappers for them;
- arm64 does not trap an integer division by zero: `DIV0_CHECK`
  (common/tier0_opcodes.h, defined for aarch64 in main.c) raises SIGFPE;
- the Linux numbers that Darwin changes (open flags, `EAGAIN`,
  `MAP_ANONYMOUS`, `SIGBUS`, `FNDELAY`, the `termios` layout and flags,
  `TIOCGWINSZ`, the `dirent` offsets) are chosen with
  `os ( linux darwin -- n )` (`host-mac?` opcode). Checked in CI on
  macos-latest (arm64): all the tests pass (229), and a pty session
  (prompt, `1 2 + .`, Tab completion, `bye`). Not checked: the SDL2
  console, the editors, sockets (sockets.fs has the Linux constants;
  `sockaddr` has a length byte on Darwin), x86_64 (only the arm64 half
  of the universal binary ran), a real terminal size (`form` gave 0 0 in
  the CI pty). The binary is not signed:
  `xattr -d com.apple.quarantine lugforth` or right click, Open. A
  `( ... )` comment must hold no inner `)`: it ended a comment early and
  the boot faulted without a message.

## ed and writhdeck under gforth

`gforth ports/gforth/writhdeck-gforth.fs` (then `ed file`,
`writhdeck file` at the prompt; tested with gforth 0.7.9). The files of
`ports/posix` are not changed: the loader cuts the Forth text between
`r|` and the closing bar, writes it to `/tmp/lugforth-gforth-body.fs`
and includes it. `ports/gforth/compat.fs` supplies what gforth lacks:
the vocabularies `ansi internals posix termios`, `z"` and `z>s`,
`bytes`, `sysfunc` (mapped to `sf-*` words: getenv, mkdir, chdir,
getcwd, remove, rename, time), `readdir` / `.d_name` / `.d_type` over
`read-dir`, the ANSI words of `common/ansi.fs`, `ms-ticks`, and `<#`
taking one cell as in ueForth (no longer needed since 2026.10.27.2: `<#`
takes a double, as in gforth). Raw mode is `stty raw -echo`. Traps:
gforth compiles into a wordlist outside `forth` at start
(`forth-wordlist set-current` first, or `only forth` loses the
vocabularies); `{ locals }` are refused outside a definition; `included`
paths are relative to the including file.
`python3 tools/editors_pty_test.py ports/gforth/writhdeck-gforth.fs`
runs the same pty test as for the native build (all pass).

## Windows exe built with MinGW (no Visual Studio)

`ninja windows-mingw` (needs `x86_64-w64-mingw32-gcc` and `windres`)
builds `out/windows-mingw/lugforth.exe` on Linux from the same
`ports/windows/main.c`, then runs `ports/windows/mingw_test.fs` under
wine. Differences from the MSVC build, all in `main.c` / `interp.h`:
`*/` uses `__int128` instead of `_mul128/_div128`; MinGW has no `__try`,
so a vectored exception handler `longjmp`s back into `forth_run`
(division by zero gives -10, an invalid address -9, as with MSVC); the
exe is a *console* program (`-DLUGFORTH_CONSOLE`, `main` calls
`WinMain`), so stdout, pipes and wine output work. The CI still builds
with MSVC. With this exe the whole `ports/windows/sdl2_test.fs` of the
CI passes under wine (SDL2.dll and SDL2_ttf.dll of the CI package, any
TTF copied to `drive_c/windows/Fonts/arial.ttf` and `consola.ttf`). The
MSVC exe prints nothing under wine (GUI subsystem).

## Static executable, musl (ports/posix)

`ninja posix-static` -> `out/posix/lugforth-static` (about 400 KB, no
library at all, runs on any Linux whatever its libc). Needs the package
`musl-dev` (`x86_64-linux-musl-gcc`; configure.py says when it is
missing). Same `main.c` and boot as `lugforth`, compiled with
`-DLUGFORTH_STATIC`: the terminal Forth, `ed`, `writhdeck`, files,
sockets, `-e`.

- A static executable has no dynamic symbol table, so
  `dlsym(RTLD_DEFAULT, "open")` finds nothing and the first `sysfunc`
  made the start loop. `main.c` holds a table (`lug_syms`) of the C
  functions that the `.fs` sources look up by name, searched before
  `dlsym`. **A new `z" name" n sysfunc` needs its name in that table**
  (the tests of `posix-static` then fail at load, error -38).
- `lugforth-static` has no SDL2, sound or window: those `dlopen` shared
  libraries, which a static musl executable cannot do. For them see
  `lugforth-static-sdl` below.
- The table of names is generated, not written by hand:
  `tools/static_syms.py` scans the `.fs` of `common/` and `ports/posix/`
  for `z" name" n sysfunc|sdl|sofunc` and `... dlsym` and writes
  `out/gen/static_syms*.h` (extern declarations by asm label, so no
  header clash). A new binding needs nothing more than being written in
  a `.fs`.
- Tested: `common/all_tests.fs`, the editor and writhdeck tests,
  `tools/editors_pty_test.py out/posix/lugforth-static`. Not in the
  release workflow yet (the Debian 10 container has no
  `x86_64-linux-musl-gcc`).

### lugforth-static-sdl (SDL2 inside the static executable)

`ninja posix-static-sdl` -> `out/posix/lugforth-static-sdl` (about 5.2
MB): the same, with SDL2 2.30.0, SDL2_image 2.8.2, SDL2_ttf 2.22.0,
SDL2_mixer 2.8.0, freetype 2.13.2, alsa-lib 1.2.11, libX11, libXext,
libxcb and libXau linked in. `--gui` (SDL console, TrueType font), the
`sdl2` games and graphics examples work on any Linux with an X server,
whatever its libc.

- `tools/build_musl_sdl.sh` (run by ninja, idempotent) downloads the
  sources (sha256 checked) and builds them against musl into
  `out/musl-deps/` (gitignored): needs `curl`, `cmake`, `make`,
  `pkg-config`, the network the first time, a few minutes. X11, freetype
  and alsa-lib are built with autotools (`--enable-static`), the SDL
  libraries with cmake.
- SDL is built for X11 only (linked, no `dlopen`), software renderer
  (`sdl2.fs` already falls back from ACCELERATED), dummy and offscreen
  video drivers (so `SDL_VIDEODRIVER=dummy` and the smoke tests work).
  SDL2_image decodes with stb_image (PNG, JPEG, BMP, GIF...; no SVG,
  TIFF, WEBP). SDL2_mixer: WAV, MP3, FLAC, OGG (stb_vorbis) and MIDI
  with its built-in timidity (needs instrument patches on the host, else
  silent). freetype has no harfbuzz, png, bzip2, brotli or zlib. **Left
  out:** joystick, Wayland, OpenGL, Vulkan, X11 extensions (xcursor,
  xrandr, xinput...), PulseAudio, PipeWire.
- **Sound (alsa-lib):** cost is about 1 MB. alsa-lib is LGPL 2.1 (see
  NOTICE: whoever distributes the binary must allow relinking, the
  object files and the build script are enough). A static alsa-lib
  cannot load plugins (`--with-libdl=no`). On a PipeWire/Pulse desktop
  (Ubuntu 24.04) the `default` device is the pipewire plugin, a shared
  library: it fails ("Cannot open shared library
  libasound_module_pcm_pipewire.so") and SDL finds no audio device.
  `main.c` therefore sets `AUDIODEV=plughw:0,0` (card 0, with format
  conversion) unless the variable is already set; **tested by the
  maintainer on a PipeWire desktop: sound works**. Another card:
  `AUDIODEV=plughw:1,0 ...`. The ALSA lines about the missing plugin are
  silenced; SDL's own "Couldn't open audio device" stays. If PipeWire
  ever holds the card ("busy"), the way out would be SDL's `disk` driver
  piped to `pw-play`. Not heard by me (the test PC session has no access
  to the sound card).
- cmake's own check of Xlib cannot link a static libX11, so two results
  are given by hand (`HAVE_XGENERICEVENT`,
  `SDL_VIDEO_DRIVER_X11_HAS_XKBKEYCODETOKEYSYM`); without them the build
  stops in `SDL_x11events.c` (conflicting `XGenericEventCookie`). The
  `sdl2.pc` of a static build lists the libs as `-l/path/libX11.a`,
  which does not link: the link line is written in `configure.py`.
- `main.c` (`LUGFORTH_STATIC_SDL`): `dlopen("libSDL2...")` (SDL2, image,
  ttf, mixer) returns a handle meaning "this executable", any other
  library returns 0; `dlsym` with such a handle looks in the table only.
  `tools/static_syms.py` writes the `SDL_`, `IMG_`, `TTF_`, `Mix_` names
  of the `sdl`, `sdlimg`, `sdlttf`, `sdlmix` words into the table (only
  for this build).
- Tested here: `all_tests`, `sdl2_console_tests`, `editors_pty_test.py`,
  `--gui` under Xvfb (accent typed with xdotool, drawn with the TrueType
  font), the raycaster, `gfx_roguelike.fs` (image sheet loaded, mixer
  opened) under Xvfb, a C program linked the same way. Not tested: a
  machine without X libraries (it should not need any), Wayland-only
  desktops (XWayland needed), audible sound, other keyboards than
  xdotool's.
- Not in the release workflow (the Debian 10 container has no musl-gcc):
  decision of the maintainer.
