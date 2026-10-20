# ports/baremetal

Code shared by the two bare-metal ports, [ports/rpi](../rpi/README.md) (64-bit ARM: QEMU, Raspberry
Pi 3/4) and [ports/efi](../efi/README.md) (x86-64 PC, UEFI). It is not a port by itself and does not
build alone: each port includes these files from its own `main.c` and `platform.fs`.

| File | Role |
|---|---|
| `screen.h` | the framebuffer and the drawing behind the `sdl2` words (written in C) |
| `console.h` | the text console on the screen (8x8 font, 80x30 cells) |
| `input.h` | key and mouse state, keys turned into characters (AZERTY too) |
| `font8x8.h` | the font |
| `builtins.h` | the screen, input, sound and file opcodes (`BM_VOC` is `rpi` or `pc`) |
| `sdl2.fs` | the `sdl2` words, so the SDL2 programs of `examples/` run unchanged |
| `files.fs` | the file words (`ls`, `cat`, `include`) over the port's FAT code |
| `allocation.fs` | `allocate`, `free`, `resize` |
| `autoboot.fs` | runs `autoexec.fs` from the disk at boot |

What stays in each port: the hardware (`start.S`, USB, virtio, sound, SD card and `fat.h` on the Pi;
GOP screen, firmware keyboard, `files.h` on the PC), and each port's `main.c` (it defines
`input_flush`, among others).

The tools `tools/baremetal_*.py` (disk images, assets, QEMU runner and test) serve both ports too.
