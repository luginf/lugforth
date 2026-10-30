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
examples/games/sdl2_breakout.fs in a QEMU raspi3b window (`BOARD=virt`:
virt with `-device ramfb`, set up through fw_cfg), `make test` checks
both by screendump. Text (font8x8), images (PNGs built in by
tools/baremetal_assets.py), keyboard/mouse (virtio-input on virt with
`-global virtio-mmio.force-legacy=false`, DWC2 USB HID on pi3; tested
through QMP input-send-event), sound (sound.h mixer, virtio-sound on
virt checked from a -audiodev wav file; Pi 3 PWM+DMA written blind),
`paste` (a whole file over serial, then Ctrl-D: multi-line strings
work). `make roguelike` runs examples/games/sdl2_roguelike.fs. QEMU runs
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
tools/baremetal_mkdisk.py), 124 checks (tools/efi_qemu_test.py, which
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
stick (prompt, sdl2_3d, roguelike work); ports/efi/README.md has the
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
  product must go through `*/` (see `examples/games/sdl2_raycaster.fs`).
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
point cloud of `sdl2_magnoquill.fs`, its maths in C in fixed point
(`GBA-MAGNO`), at about 20 pictures a second for 400 points under mgba
(checked there, not on a console). The linker script now has an IWRAM
section and aligns the ROM images that `crt0.s` copies; the roguelike
and the tile demo still boot after that (screenshots under mgba).

## The bare-metal disk and the USB stick

The folders of `examples/` are kept (`include games/sdl2_snake.fs`,
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

- `sdl2_magnoquill.fs`, `sdl2_mouse_test.fs`, `sdl2_demo.fs` and
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
  `0 cursor` (as `sdl2_mouse_test.fs`, which draws its own cross). The
  desktop also shows its frame five times a second there. Not drawn by
  the SDL console (`sdl2_console.fs`) nor by `varvara.fs`, which
  present the frame themselves.
- `Error including: ...sdl2_demo.fs` after Esc is a caught `-9`
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
- `make -C examples smoke`: `sdl2_magnoquill.fs` did not stop under
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
  font), the raycaster, `sdl2_roguelike.fs` (image sheet loaded, mixer
  opened) under Xvfb, a C program linked the same way. Not tested: a
  machine without X libraries (it should not need any), Wayland-only
  desktops (XWayland needed), audible sound, other keyboards than
  xdotool's.
- Not in the release workflow (the Debian 10 container has no musl-gcc):
  decision of the maintainer.
