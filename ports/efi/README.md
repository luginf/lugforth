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
make -C ports/efi usb      # out/efi/usb.img: MBR + FAT32, EFI/BOOT/BOOTX64.EFI and the examples
make -C ports/efi run      # QEMU (q35, OVMF) on that image: a window, the serial line here
make -C ports/efi test     # boots it under QEMU and checks 54 things
```

## Booting it from a USB stick (a Dell Latitude, for example)

Recent Latitudes boot UEFI only. Their firmware looks for
`\EFI\BOOT\BOOTX64.EFI` on a FAT32 partition, which is what the stick holds.

1. Make the stick. Either:
   * write the image to the whole stick (this erases it; check the device
     name with `lsblk` first):
     `sudo dd if=out/efi/usb.img of=/dev/sdX bs=4M conv=fsync`
   * or, on a stick already formatted FAT32, copy `out/efi/BOOTX64.EFI` to
     `EFI/BOOT/BOOTX64.EFI`. Add whatever else you want at the root: the
     `.fs` files of `examples/`, `ports/rpi/disk/autoexec.fs`, and
     `ports/posix/{editor,writhdeck,eval_copy}.fs` in `lib/` for `ed` and
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
   refuse it. Press F2 at power-on to open the BIOS setup, then go to Boot
   Configuration (or Secure Boot) and set Secure Boot to off. Turn it back
   on when you are done with lugforth.
3. Plug in the stick, power on, press F12 for the one-time boot menu, and
   choose the USB stick under UEFI Boot. If it is not listed, check in the
   setup that USB boot is enabled. On some models it is "Enable USB Boot
   Support" or "Thunderbolt Boot Support", for a stick plugged into a USB-C
   dock.

`bye` turns the PC off. Ctrl+Alt+Del usually restarts it: the firmware handles that.

The firmware's keyboard layout is QWERTY US. On a French keyboard, type
`azerty` (`autoexec.fs` from `ports/rpi/disk` does it at boot). The keys are
read by position, as on the Pi.

## Status

| | QEMU (q35, OVMF) | a real PC |
|---|---|---|
| Boot from a USB stick (MBR + FAT32), prompt, floats (x87), timer | tested | to try |
| Faults (#GP, #PF, divide...) become `THROW` | tested | to try |
| Idle CPU: sleeps in `WaitForEvent` | not measured | to try |
| Console on the screen (GOP, at least 1280x960, zoom 1 to 3 by size) | tested | to try |
| Screen words, lines, text, images, breakout, roguelike | tested | to try |
| Keyboard (firmware console input), typing and held keys | USB keyboard, tested | to try |
| Files on the stick: `include`, `ls`, long names, write, rename, delete | tested, checked with mtools and fsck.fat | to try |
| `ed`, `writhdeck` | tested | to try |
| Serial line (COM1, when there is one) | tested | most laptops have none |
| Date and time (`unix-time`, from the battery clock) | tested | to try |
| Mouse (firmware simple and absolute pointers) | written, not testable under OVMF | to try (a laptop's I2C trackpad may not be exposed) |
| Sound | not done (silent) | not done |

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
  after its last press. The firmware's key repeat keeps a held key held, so
  games work, but there is a short gap at first: the repeat delay.
* The screen is the firmware's mode. At start, if that mode is smaller than
  1280x960, the smallest one at least that big is chosen. A program's
  `screen` is centered in it, and the rest is black.
* No sound: the `snd-*` words do nothing.
* Mouse: `main.c` connects all the firmware's drivers at start (as the shell's
  `connect -r`) and polls every simple and absolute pointer, relative motion
  doubled. OVMF only shows its ConSplitter pointers, with no event behind them,
  so this is not checked by `make test`.
* The console and `fb_flip` draw in RAM and write only the changed pixels to
  video memory, which is never read (slow on a real GPU). Written after a real
  test showed the cursor crawling in writhdeck and breakout jerky; the gain
  has not been measured on the PC yet.
* The x86 exceptions go to a copy of the firmware's IDT. A fault in the
  firmware itself (rather than in Forth) is not caught, and 50 faults in one
  second stop the machine.
