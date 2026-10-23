# Ports and platforms: state, limits, what was tested

Android, Raspberry Pi, UEFI PC, the web console and the Game Boy Advance. Part of [README.md](../../README.md).

## Android (ports/android, not buildable here)

`MenuBar.java` is the bar of both consoles: Tab, arrows, Ctrl (sticky: next letter is a control code; sends F9,
which `ports/posix/sdl2_console.fs` turns into `ctrl-next`; the web page does the same through `lugkey('Ctrl')`
in `ports/web/platform.fs`), a "⋮" button for Examples/A-/A+/Help, and home. To check once compiled:
the Ctrl button on a real soft keyboard (letters may arrive as key events rather than text input), and that
nothing shows that Ctrl is armed (an indicator would help).

## Bare-metal AArch64 (ports/rpi) and shared code (ports/baremetal)

`ports/baremetal` holds what the Pi and the PC (ports/efi) share: screen.h (framebuffer, `sdl2` words in C), console.h,
input.h, font8x8.h, sdl2.fs, files.fs, allocation.fs, autoboot.fs; tools/baremetal_*.py (disk images, assets, QEMU runner/test)
serve both. Hardware specific files stay in ports/rpi (start.S, usb.h, virtio.h, sound.h, storage.h, fat.h) and ports/efi.

`make -C ports/rpi test` builds out/rpi/{virt/lugforth.elf,pi3/kernel8.img,pi4/kernel8.img} and boots the first two
under QEMU (`virt`, `raspi3b`), checking answers over the serial line (tools/baremetal_qemu_test.py). Needs
`gcc-aarch64-linux-gnu picolibc-aarch64-linux-gnu qemu-system-arm` (apt, installable in the cloud container).
No OS, picolibc, PL011 console, MMU identity map (needed: unaligned accesses fault with it off), faults become
THROW. Not tried on a real Pi; Pi 4 written blind (QEMU 8.2 has no raspi4b). See ports/rpi/README.md.
Graphics: `sdl2` short words on the mailbox framebuffer (ports/baremetal/sdl2.fs, screen.h), keys from the UART;
`make -C ports/rpi breakout` plays examples/games/sdl2_breakout.fs in a QEMU raspi3b window (`BOARD=virt`: virt with
`-device ramfb`, set up through fw_cfg), `make test` checks both by screendump.
Text (font8x8), images (PNGs built in by tools/baremetal_assets.py), keyboard/mouse (virtio-input on virt with
`-global virtio-mmio.force-legacy=false`, DWC2 USB HID on pi3; tested through QMP input-send-event), sound
(sound.h mixer, virtio-sound on virt checked from a -audiodev wav file; Pi 3 PWM+DMA written blind), `paste`
(a whole file over serial, then Ctrl-D: multi-line strings work). `make roguelike` runs examples/games/sdl2_roguelike.fs.
QEMU runs a whole bcm2835 DMA chain at once: a looping chain hangs it (even SIGTERM), so never start one blindly.
Files: storage.h (Pi SDHCI/EMMC polled, virt virtio-blk) + fat.h (FAT12/16/32, MBR or not, long names read and written, mkdir/rmdir/chdir, dates from the clock),
the standard file words, so include/ls/cat work; autoexec.fs runs at boot. tools/baremetal_mkdisk.py makes images (needs
dosfstools mtools); make test checks them afterwards with mtools and fsck.fat.

## Bare x86-64 PC, UEFI (ports/efi)

Decision (2026-10-18): no x86 port without UEFI (own boot, VBE/Multiboot2 framebuffer, PS/2, AHCI/NVMe drivers and
FAT on them): too much work for a narrow target. If ever wanted, Multiboot2 through GRUB is the shortcut.
Known limits: OVMF never polls USB mice (no mouse under QEMU, only on hardware: `n mouse-speed`, default 40 = 0.40
pixel per count); no key releases from UEFI, the firmware repeat starts 500 ms after the press (breakout stutters on the
keyboard at first). Video memory is write-combining: keep the `sfence` in `dcache_clean` (screen.h). The console syncs
at most every 100 ms during output (console.h), else when waiting for a key: shorter shows editors' redraws half done.
`common/lineedit.fs` (history, Tab on words and files) and `common/pager.fs` (stops output at each screenful, space/Enter/q,
`pager-off`) are loaded by both bare-metal boots and the posix ones. The pager wraps `type`, `key`, `key?`, `prompt`; it acts only
while `'tib` is the console buffer (not in an included file), on a tty (posix) or when `con-rows` is not 0; `q` is `-28 throw`,
silent in `quit` (`ls` and `cat` close their handle on a throw). tools/pager_pty_test.py checks the posix side. The screen, input, sound
and file opcodes are shared in ports/baremetal/builtins.h (`BM_VOC` = rpi or pc, `input_flush` defined by each main.c).
Duplication left: the common part of the two platform.fs (azerty, console-zoom, keys).

`make -C ports/efi test` builds out/efi/BOOTX64.EFI (gcc + gnu-efi) and boots it under QEMU q35 + OVMF from a USB stick
image (MBR + FAT32, tools/baremetal_mkdisk.py), 124 checks (tools/efi_qemu_test.py, which reuses baremetal_qemu_test.py). Needs
`gnu-efi ovmf qemu-system-x86 dosfstools mtools`. Stays in boot services: GOP screen, ConIn keyboard (no releases: a key
is held 120 ms after its last press; the test uses `-device usb-kbd`, PS/2 does not repeat), the stick's FAT through
Simple File System (ports/efi/files.h, same API as fat.h). Reuses ports/baremetal (screen.h, console.h, input.h, font8x8.h, sdl2.fs, files.fs...), shared with ports/rpi;
vocabulary `pc` aliased `rpi`. Console and `fb_flip` (ports/baremetal) draw in RAM and write only changed pixels, never read video memory; mouse polled from the firmware pointers (untestable under OVMF, trackpad may not be exposed). Speed on real hardware not measured yet. efi_main is called SysV by gnu-efi's crt0 (NOT EFIAPI: it crashed); own libc in libc.h
(x87 math), setjmp and fault stubs in start.S (patched copy of the firmware IDT). Sound: minimal Intel HD Audio driver (ports/efi/hda.h, mixer shared in ports/baremetal/mixer.h with rpi/sound.h), checked by the test through `-audiodev wav` with QEMU intel-hda + hda-duplex (`make run` has them too, AUDIODEV=pa); `sound-off` / `sound` keep the choice in lugforth.ini (ports/efi/sound_ini.fs); not tried on a real PC. Booted from a real USB stick
(prompt, sdl2_3d, roguelike work); ports/efi/README.md has the steps (Secure Boot off, one-time boot menu).
Other file systems: `load-driver` (pc, files.h: LoadImage from a buffer, StartImage, connect_all) and
ports/efi/disk/drivers.fs (on the stick, NOT run at boot) load EfiFs drivers from EFI/drivers/ (exfat_x64.efi,
ext2_x64.efi: read only, GPL, not in the repo). EFIFS=dir make test adds 7 checks (exFAT and ext4 disks made with
mkfs.exfat / mkfs.ext4 -d; exfatprogs installs with apt).
`volumes` / `n mount` (files.h, platform.fs) switch the FAT volume of the file words (refused while a file is open, the old volume flushed): tested with a second stick and a hot-plugged
one under QEMU, not on hardware. The efi test builds here with gnu-efi and ovmf unpacked from `apt download` (no root):
`make EFIINC=<root>/usr/include/efi EFILIB=<root>/usr/lib OVMF=<root>/usr/share/OVMF/OVMF_CODE_4M.fd test`. The dead key test is flaky.

## Terminals (Alt+F1..F4) on rpi and efi - done 2026-10-20

`ports/baremetal/term.h` (early: `term_cur`, `term_foc`, `TERM_FOCUSED()`) and `term_run.h` (late: the scheduler). Each terminal is another
`forth_init` on its own heap (32 MB after the first; all the interpreter state is in `g_sys` and that heap) and has its own screen
state, swapped by `term_save`/`term_load` (the `TERM_FIELDS` list: console cursor/colors/`con_shadow`, `fb_back`, `fb_w`..., `images`).
`forth_run` returns to the main loop at each `pause`, where `term_next()` picks the next terminal (cooperative: a loop that never pauses
holds the others). Shared and written by the focused terminal only: `con_front`, `fb_front`, `fb_mem` (`con_sync`, `fb_flip`,
`fb_hw_open` return when not focused), the keyboard (`getchar_timeout_us`, `console_extra_char`, `input_snapshot`...), the serial output
(`RAW-TYPE`). A focus switch redraws the whole screen (`term_present`). Alt+Fn is caught in `input_key` (input.h, shared);
`term_poll` (in `RAW-YIELD`) reads the keyboard at each pause so a program that never reads keys can still be left. Words: `term`,
`terminal`, `terminals` (ports/baremetal/terms.fs, 1-based). `bye` outside the first closes the terminal (`term_bye`). `autoexec.fs`
runs in the first only. Traps: the opcode macro `X` clashes with an X-macro list (use `F`); `also rpi` inside a colon definition
compiles, it does not search (use a helper defined after a top level `also rpi`); a word the interpreter does not find does not abort
the rest of the line (`v @` with an unknown `v` faults on the garbage left). Tested: efi test `terminals()` (10 checks) and by hand with a
game left and found again; rpi not built (no aarch64 toolchain). Not done: a status indicator of the terminal number on the screen,
sharing `cwd`, closing the first while others live, F5+ terminals.

## Web (ports/web)

- The cells are 32 bits (the other builds have 64): a 16.16 fixed point product must go through `*/`
  (see `examples/games/sdl2_raycaster.fs`). Pages are copied to `out/web` by `ports/web/BUILD` and must be served from there
  (`ninja web && cd out/web && python3 -m http.server`); `tools/make_pages.py` copies them to `demo/` on the site.
- Web console, 2026-10-22: `graphic` and the drawing words typed at the prompt open a drawing area above the text
  (`S.layer`, `S.need` in `ports/web/sdl2.fs`; a fixed canvas and a spacer); a key press or a tap unlocks the sound;
  on a phone a hidden textarea takes the characters (`ports/web/platform.fs`), the prompt is kept above the keyboard
  (`context.KeepInView`), and the cursor is redrawn after every escape code. Checked in headless Chromium over CDP,
  on desktop and with an emulated phone; not with a real keyboard.
- Traps are in [PITFALLS.md](PITFALLS.md) (the stack pointer `sp`, `[defined]` in a definition, encoding, keypress).

## Game Boy Advance (ports/gba)

A prototype with a roguelike. See `ports/gba/README.md`; the boot-chain traps met are in [PITFALLS.md](PITFALLS.md).

## The bare-metal disk and the USB stick

The folders of `examples/` are kept (`include games/sdl2_snake.fs`, `system/varvara/varvara_rpi.fs`), with `lib/` (editors),
`MANUAL.md` and `WORDS.md` (docs/), `autoexec.fs`. Built by the Makefiles of ports/rpi (`disk`) and ports/efi (`usb`, `stage`).

## SDL2 on Windows and macOS

The bindings of `ports/posix/sdl2*.fs` ask for their library by a word (`sdl2-lib`, `sdl2-ttf-lib`... in `ports/posix/posix.fs`:
Linux `.so.0` names, macOS `.dylib` looked up in the system then in the Homebrew folders). On Windows `ports/windows/windows_sdl2.fs`
makes a small `posix` vocabulary (`shared-library` = `dll`, `sign-extend`, `SDL2.dll`...) and `sdl2` / `sdl2-ttf` include `lib/sdl2.fs`,
`lib/sdl2_ttf.fs` (copies of the posix files, put in the zip by the `windows` job: the sources are too big for the MSVC string of the
boot). That job downloads the official SDL2 and SDL2_ttf DLLs and runs `ports/windows/sdl2_test.fs` (dummy drivers). Tested: Windows
(window, pixel, TTF) and macOS (`sdl2_tests.fs`, 38) in CI on 2026-10-23, version 2026.10.23.2. Not bundled: SDL2_image, SDL2_mixer.

## macOS (ports/posix)

Fixed in 7.0.11.16 with no Mac at hand, by running the `macos` job of `.github/workflows/release.yml` (branch `macos-fix` had a
job `macos-test.yml` with tracing steps: `fs_usage`, `sample`, a pty session). The start looped because of Apple arm64:
- a call through a `(...)` pointer puts every argument on the stack, so `common/calls.h` has, for `__APPLE__` + `__aarch64__`, one
  cast per argument count; `open`, `fcntl` and `ioctl` are variadic, so `DLSYM` (ports/posix/main.c) hands out fixed-argument
  wrappers for them;
- arm64 does not trap an integer division by zero: `DIV0_CHECK` (common/tier0_opcodes.h, defined for aarch64 in main.c) raises SIGFPE;
- the Linux numbers that Darwin changes (open flags, `EAGAIN`, `MAP_ANONYMOUS`, `SIGBUS`, `FNDELAY`, the `termios` layout and flags,
  `TIOCGWINSZ`, the `dirent` offsets) are chosen with `os ( linux darwin -- n )` (`host-mac?` opcode).
Checked in CI on macos-latest (arm64): all the tests pass (229), and a pty session (prompt, `1 2 + .`, Tab completion, `bye`). Not
checked: the SDL2 console, the editors, sockets (sockets.fs has the Linux constants; `sockaddr` has a length byte on Darwin), x86_64
(only the arm64 half of the universal binary ran), a real terminal size (`form` gave 0 0 in the CI pty). The binary is not signed:
`xattr -d com.apple.quarantine lugforth` or right click, Open. A `( ... )` comment must hold no inner `)`: it ended a comment
early and the boot faulted without a message.
