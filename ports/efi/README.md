# lugforth on a bare x86-64 PC (UEFI)

`BOOTX64.EFI` is lugforth as a UEFI application: the PC's firmware starts it
from a USB stick, with no operating system. It stays in the firmware's boot
services and uses them for the screen (GOP framebuffer), the keyboard
(console input), the clock and the files (the FAT of the stick it was
started from). The kernel (`common/*.h`), the boot chain and most of the
Forth side are those of `ports/baremetal`. That includes `sdl2.fs`, the text
console drawn on the screen, `autoexec.fs` and `paste`, so the programs
written for the Pi run here unchanged. The vocabulary is `pc`, also named
`rpi`.

Build with gcc and gnu-efi. You need `qemu-system-x86` and `ovmf` to try it
in QEMU, and `dosfstools` and `mtools` for the stick image:

```
sudo apt install gnu-efi ovmf qemu-system-x86 dosfstools mtools
make -C ports/efi          # out/efi/BOOTX64.EFI
make -C ports/efi usb      # out/efi/usb.img: MBR + FAT32, EFI/BOOT/BOOTX64.EFI, the examples and the docs (docs/*.md: manual, words, SDL2)
make -C ports/efi stage    # the same files as a tree in /tmp/lugforth, or in DEST=/media/me/STICK, for a stick you already have
make -C ports/efi run      # QEMU (q35, OVMF) on that image: a window, the serial line here, sound (AUDIODEV=pa)
make -C ports/efi test     # boots it under QEMU and checks 129 things (136 with EFIFS=dir of the EfiFs drivers)
```

## Booting it from a USB stick

Any recent x86-64 PC that boots in UEFI mode can do it (legacy BIOS only machines cannot). The
firmware looks for `\EFI\BOOT\BOOTX64.EFI` on a FAT32 partition, which is what the stick holds.

1. Make the stick. Either:
   * write the image to the whole stick (this erases it; check the device
     name with `lsblk` first):
     `sudo dd if=out/efi/usb.img of=/dev/sdX bs=4M conv=fsync`
   * or, on a stick already formatted FAT32, copy `out/efi/BOOTX64.EFI` to
     `EFI/BOOT/BOOTX64.EFI`. Add whatever else you want at the root: the
     `.fs` files of `examples/`, `ports/rpi/disk/autoexec.fs`, and
     `editors/{editor,writhdeck}.fs` and `ports/posix/eval_copy.fs` in `lib/` for `ed` and
     `writhdeck`.
   * or make a GPT stick by hand. One FAT32 partition of type "EFI System" is
     enough, there is no need for a second one (this erases the stick; replace
     `/dev/sdX` after checking with `lsblk`):
     ```
     sudo sgdisk --zap-all /dev/sdX
     sudo sgdisk -n1:1M:0 -t1:ef00 -c1:LUGFORTH /dev/sdX
     sudo mkfs.fat -F32 -n LUGFORTH /dev/sdX1
     sudo mount /dev/sdX1 /mnt
     sudo mkdir -p /mnt/EFI/BOOT
     sudo cp out/efi/BOOTX64.EFI /mnt/EFI/BOOT/
     sudo umount /mnt
     ```
     If the stick is already GPT with a FAT32 partition that is not of the EFI
     System type, some firmwares will not boot from it. Change the type
     without touching the data: `sudo sgdisk -t1:ef00 /dev/sdX` (1 is the
     partition number). The files lugforth reads (`autoexec.fs`, examples,
     `lib/`) go on that same partition.
2. Turn off Secure Boot. `BOOTX64.EFI` is not signed, so the firmware would
   refuse it. Open the firmware setup at power-on (the key depends on the maker:
   F2, Del, F10 or Esc), find Secure Boot (under Boot or Security) and set it to off. Turn it back
   on when you are done with lugforth.
3. Plug in the stick, power on, press the key of the one-time boot menu (often F12, also F11,
   F9 or Esc) and choose the USB stick, the entry marked UEFI. If it is not listed, check in the
   setup that USB boot is enabled. On some models it is "Enable USB Boot
   Support" or "Thunderbolt Boot Support", for a stick plugged into a USB-C
   dock.

`bye` turns the PC off. Ctrl+Alt+Del usually restarts it: the firmware handles that.

The firmware's keyboard layout is QWERTY US. On a French keyboard, type
`azerty` (`autoexec.fs` from `ports/rpi/disk` does it at boot). The keys are
read by position, as on the Pi.

## Changing stick during the session

The program is in memory once started, so the boot stick is not needed to keep running, but the
file words use the stick it was started from until told otherwise:

```
volumes        \ 0  LUGFORTH  14 MB   (the * marks the one in use)
1 mount        \ include, ls, cd, cat... now read the second one, in its root
0 mount        \ back to the boot stick
```

`volumes` looks again each time (it asks the firmware to connect the drivers), so a stick plugged
after the start shows up: tested under QEMU with a hot-plugged USB disk, not yet on real hardware. `ed` and
`writhdeck` are read from `/lib` of the disk in use the first time: run them before mounting another
stick, or have `/lib/editor.fs` and `/lib/writhdeck.fs` on it too. Do not pull the stick in use
without `mount`ing another one first. `mount` refuses ("close the open files first") while a file
is open, and flushes the volume it leaves.

## Other file systems: exFAT, ext4 (drivers to load)

The firmware itself only reads FAT (the only file system UEFI requires), so a disk in exFAT, ext2/3/4,
NTFS... is not in `volumes` at first. UEFI file system drivers fill the gap: lugforth loads them from
the stick with `load-driver` (vocabulary `pc`), and `drivers.fs`, on the stick made by `make usb`,
loads every `.efi` file of `EFI/drivers/`. It is not run at boot.

1. Take the x64 drivers of [EfiFs](https://github.com/pbatard/efifs/releases) (GPL, not part of
   lugforth): `exfat_x64.efi` for exFAT, `ext2_x64.efi` for ext2, ext3 and ext4 (`ntfs_x64.efi`,
   `btrfs_x64.efi`, `iso9660_x64.efi`... work the same way).
2. Copy them to `EFI/drivers/` on the stick (beside `EFI/BOOT/`).
3. At the prompt:

   ```
   include drivers.fs   \ /EFI/drivers/exfat_x64.efi : loaded ... then the volumes
   2 mount              \ the ext4 partition: ls, cd, include, cat
   0 mount              \ back to the stick
   ```

   To have them at each boot, add the line `include drivers.fs` to `autoexec.fs`.

These drivers only read: on such a volume `include`, `cat`, `ls` and `open-file` work, but creating or
writing a file fails (`create-file` gives 22). Keep the files you write on a FAT volume. The stick
itself stays FAT32, or the firmware would not find `EFI/BOOT/BOOTX64.EFI`.
`s" /EFI/drivers/x.efi" pc load-driver forth` loads one driver by hand and gives 0, or -1 (the file
cannot be read), -2 (the firmware refuses the image: not an x64 UEFI one, or Secure Boot is on) or
-3 (its start failed). Tested under QEMU with an exFAT disk and an ext4 one, not yet on a real PC.

## Status

| | QEMU (q35, OVMF) | a real PC |
|---|---|---|
| Boot from a USB stick (MBR + FAT32), prompt, floats (x87), timer | tested | to try |
| Faults (#GP, #PF, divide...) become `THROW` | tested | to try |
| Idle CPU: `hlt` between two checks (the firmware's `WaitForEvent` polls), `ms` and `delay` sleep; `include examples/system/cpu_load.fs` prints the load | tested (2% waiting, 100% in a loop that never waits) | to try |
| Console on the screen (GOP, at least 1280x960, zoom 1 to 3 by size) | tested | to try |
| Screen words, lines, text, images, breakout, roguelike, tetris, snake, minesweeper, mastermind | tested | to try |
| Keyboard (firmware console input), typing and held keys | USB keyboard, tested | to try |
| Files on the stick: `include`, `ls`, long names, write, rename, delete | tested, checked with mtools and fsck.fat | to try |
| `ed`, `writhdeck` | tested | to try |
| Serial line (COM1, when there is one) | tested | most laptops have none |
| Date and time (`unix-time`, from the battery clock) | tested | to try |
| Mouse (firmware simple and absolute pointers) | written, not testable under OVMF | to try (a laptop's I2C trackpad may not be exposed) |
| Line editor: history, Tab (words, file names) | tested | to try |
| Sound (`hda.h`: Intel HD Audio, `beep`, `play`, `drum-hit`, ABC tunes) | tested, recorded by QEMU (`intel-hda`) | to try (codecs differ) |

"Tested" means that `make test` (`tools/efi_qemu_test.py`) checks it on
every run. The test reuses the checks of `tools/baremetal_qemu_test.py`, with a
USB stick image made like the real one. QEMU gets a USB keyboard driven
through QMP, the screen is read with `screendump`, and the serial line
gives the answers.

Limits, by design:

* Only boot services are used (no `ExitBootServices`), so there are no
  drivers of our own. The firmware handles USB, the disk and the screen,
  and lugforth runs wherever UEFI runs.
* UEFI sends key presses but no releases. A key counts as held for 120 ms
  after its last press. The firmware's key repeat keeps a held key held, but
  it starts 500 ms after the first press (measured under OVMF): a held key is
  seen held for 120 ms, then not for about 380 ms, then continuously. Games
  that move with a held key stutter at first; a mouse avoids it. Reading the
  PS/2 controller directly would give real releases (not done).
* The screen is the firmware's mode. At start, if that mode is smaller than
  1280x960, the smallest one at least that big is chosen. A program's
  `screen` is centered in it, and the rest is black.
* The key `<` `>` of an ISO keyboard (left of W) gives nothing at all under OVMF (the firmware drops it):
  in `azerty` mode, `AltGr+;` types `<` and `AltGr+:` types `>`.
* Terminals: `Alt+F1` to `Alt+F4` or `n term` (see the manual), tested here (10 checks, with a game keeping the
  screen); the firmware reports Alt+Fn as it should under OVMF, a real PC not tried.
* Slow on a PC: `include speed.fs` (examples/bench/speed.fs, copied to the stick) prints the clock, the time of 100 `key?`
  polls, of 2000 characters and of 30 lines, and whether the sound started; the 3 s wait is to be timed by the watch.
* Sound: a minimal Intel HD Audio driver (`hda.h`) over the mixer shared with the Pi
  (`ports/baremetal/mixer.h`). It takes the first controller on the PCI bus and the first
  codec, follows every connected output pin (line out, speaker, headphones) down to a DAC,
  and plays 44100 Hz stereo from a ring that `RAW-YIELD` and the wait for a key keep filled.
  The controller is started at the first sound word (or `snd-ok`), not at boot, and gives up after 0.4 s if the
  codec does not answer. Under QEMU: `make run` adds `-device intel-hda -device hda-duplex` (host side with
  `AUDIODEV`, default `pa`: `make run AUDIODEV=alsa`). On a real PC it is not tried: codecs
  differ (pin choice, amplifier ranges), and some controllers need quirks (snoop bits).
  `snd-ok` tells whether a controller answered. Without one the words stay silent.
  `sound-off` stops the controller (a PC whose codec makes white noise or crackles) and writes `sound = off`
  in `lugforth.ini` at the root of the stick, read at boot before `autoexec.fs`; `sound` (or `sound-on`)
  starts it again and writes `sound = on`. `ports/efi/sound_ini.fs`.
* Mouse: `main.c` connects all the firmware's drivers at start (as the shell's
  `connect -r`) and polls every simple and absolute pointer. A relative device
  moves 0.10 screen pixel per count of a device of 8 counts per mm (the movement is scaled by the resolution the firmware reports: one machine gave dx = -16384); `n mouse-speed` sets it in percent (put `15 mouse-speed` in `autoexec.fs` to keep it; the default was lowered from 40 to 20 on 2026-10-24, a real mouse was too fast, the right value depends on the dpi of the mouse and is not measured). OVMF
  never polls the USB mouse or tablet (QEMU keeps the PS/2 mouse as the active
  one), so there is no mouse under QEMU and `make test` does not check it.
  `mouse-info` prints what the firmware reports (pointers, last raw dx dy, resolution);
  `examples/graphics/gfx_mouse_test.fs` shows position, buttons, wheel and a speed figure.
  The sdl2 words draw a small arrow (in `fb_flip`, put back after the copy) unless the
  program says `0 cursor`; `n mouse-cursor` toggles it.
* A small desktop: `include utils/desktop_bm.fs` (it starts at once; `desk` brings it back). Icons, a file
  manager and a settings window (draggable windows, taskbar; mouse speed with - and +, Save writes it to
  `lugforth.ini` as `mouse = 55`, read at boot like `sound = off`), drawn with the sdl2 words (`examples/utils/desktop_bm.fs`; `desktop.fs` needs the real libSDL2 and cannot
  run here). Mouse or keyboard (arrows, Enter, Esc): double click to open a folder, run a `.fs`, play a `.z3 .z5 .z8`
  story, or edit any other file in writhdeck; the editors and programs take the whole screen, a key brings the
  desktop back. The icons are the table in `dk-defaults`. Checked under QEMU with the keyboard, not with a real mouse.
* The console shows its pixels when it waits for a key, or every 100 ms during
  a stream of output, so an editor's row by row redraw does not flash.
* The line editor of the desktop (`common/lineedit.fs`) is loaded: history of
  64 lines (not kept), cursor keys, Tab completes words and file names.
* `common/pager.fs` stops the output of a typed line at each screenful of the
  console (space: page, Enter: line, `q`: stop; `pager-off` / `pager-on`).
  Not while a file is included, and the count restarts at each key read.
* A UTF-8 character written one byte at a time (typed accents) is kept by
  `con_write` until complete.
* Video memory is usually write-combining: `dcache_clean` (screen.h) issues an
  `sfence` on x86, else small updates stay unseen until the next key press.
* The console and `fb_flip` draw in RAM and write only the changed pixels to
  video memory, which is never read (slow on a real GPU). Written after a real
  test showed the cursor crawling in writhdeck and breakout jerky; the gain
  has not been measured on the PC yet.
* The x86 exceptions go to a copy of the firmware's IDT. A fault in the
  firmware itself (rather than in Forth) is not caught, and 50 faults in one
  second stop the machine.
