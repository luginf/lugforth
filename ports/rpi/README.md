# lugforth on bare-metal AArch64: QEMU and Raspberry Pi 3/4

No operating system: the image is loaded by QEMU or by the Pi firmware and
runs on its own, with the console on the PL011 UART. It uses the same kernel
(`common/*.h`) and the same boot chain as `ports/pico`. The C library is
picolibc, used for `malloc`, `math.h` and `setjmp`.

| Target | Image | Tested |
|--------|-------|--------|
| QEMU `virt` (Cortex-A53) | `out/rpi/virt/lugforth.elf` | yes, `make test` |
| QEMU `raspi3b` | `out/rpi/pi3/kernel8.img` | yes, `make test` |
| Raspberry Pi 3 | same `pi3/kernel8.img` | not yet on a real board |
| Raspberry Pi 4 | `out/rpi/pi4/kernel8.img` | written blind: QEMU 8.2 has no `raspi4b` |

## Building and running (Linux x86_64 is fine, QEMU emulates the ARM)

```sh
sudo apt install gcc-aarch64-linux-gnu picolibc-aarch64-linux-gnu qemu-system-arm
make -C ports/rpi            # the three images
make -C ports/rpi run        # QEMU virt, console here; Ctrl-A then X quits
make -C ports/rpi run-pi3    # QEMU raspi3b
make -C ports/rpi test       # boots both, checks answers over the serial line
```

## Graphics: the sdl2 words

The vocabulary `sdl2` has the same short words as Linux
(`ports/posix/SDL2-words.md`) and the web (`ports/web/SDL2.md`). So
`examples/sdl2_breakout.fs` runs unchanged, sent over the serial line:

```sh
make -C ports/rpi breakout             # QEMU raspi3b window; play from this terminal
make -C ports/rpi screen               # the same window, prompt only
make -C ports/rpi breakout BOARD=virt  # QEMU virt with -device ramfb
```

`tools/rpi_qemu_run.py` (what both targets run) sends the file one line at a
time, waiting for the prompt each time. The line that does not give the
prompt back (`run`) starts the game. From then on, the terminal is the
keyboard: arrows, space, Esc. Ctrl-] quits.

```forth
sdl2  2 to zoom  160 120 screen
$101830 color cls  red color 10 10 50 30 box  white color 120 20 15 circle  flip
```

The words that exist:

* Screen: `screen close-screen screen-open? zoom width height`. `title`,
  `fullscreen` and `cursor` do nothing.
* Colors: `color rgb rgba alpha pen pen-r pen-g pen-b pen-a` and the named
  colors.
* Drawing: `cls box frame dot hline line disc circle pixel@ flip`.
* Text: `load-font font free-font text text-size` (and `sdl2-ttf`, which
  does nothing). There is no TrueType: one built-in 8x8 font (font8x8,
  public domain), scaled. `s" any.ttf" 16 load-font font` gives scale 2. The
  text is UTF-8, with the Latin-1 accents (é à ç ö...); other characters are
  drawn as `?`.
* Time: `ticks delay dt`.
* Input: `events pressed? last-key quit? quit!`, the `key-` constants.
  `mouse-x`, `mouse-y` and `wheel` are always 0.
* Sound: `beep` and `drum-hit` are silent.

How it works:

* `screen` asks the firmware for a 32-bit framebuffer of `width*zoom` by
  `height*zoom` through the mailbox. QEMU's raspi3b emulates it; on HDMI the
  firmware scales it to the monitor.
* Drawing goes to a back buffer in C (`screen.h`, the `FB-` words of `rpi`).
* `line` is the same Bresenham loop as the web port, clipped to the screen
  without changing its pixels. A closed form gives the position after k
  steps, and a binary search finds the visible steps, so a line 10^12 pixels
  long takes under 1 ms. Horizontal and vertical lines are boxes.
* `flip` copies the back buffer to the screen, each pixel as a zoom x zoom
  square, then cleans the data cache. It takes about 5 ms under QEMU at
  zoom 3.
* On `virt`, the screen is QEMU's `ramfb` device (`-device ramfb`). The
  framebuffer is RAM of ours, and its address, size and XRGB8888 format
  are written through `fw_cfg` (the file `etc/ramfb`, by DMA). Without
  `-device ramfb` there is no such file, so `screen` throws -21.

Keys come from the serial terminal, which sends a byte when a key goes down
(and again for each auto-repeat) but nothing when it goes up. So a key counts
as held for `key-hold` ms (120) after its last byte. A held arrow moves,
pauses for the terminal's repeat delay, then moves on. `xset r rate 200 40`
shortens that pause. A USB keyboard would need a USB host stack (DWC2 on the
Pi 3).

`make test` does this on raspi3b and on virt with ramfb. It loads breakout
this way, reads the screen through QEMU's
`screendump` (bricks, paddle and ball colors), launches the ball with space,
and leaves with Esc.

## Leaving

`bye` makes QEMU exit: PSCI `SYSTEM_OFF` on `virt`, a watchdog reset on the Pi.
On a real Pi that reset restarts the board instead.

To send a file, run `make -C ports/rpi run-pty`. QEMU prints the pty it uses;
pass it to `tools/pico_send.py -p /dev/pts/N file.fs`. The same script works
on a real Pi through a USB serial adapter (`-p /dev/ttyUSB0`).

## On a real Raspberry Pi 3 or 4

On the SD card's FAT partition, keep the firmware files (`bootcode.bin`
(Pi 3 only), `start*.elf`, `fixup*.dat`), then add the right `kernel8.img`
and a `config.txt`:

```
arm_64bit=1
kernel=kernel8.img
enable_uart=1
```

Use a 3.3 V USB serial adapter: GND to pin 6, its RX to GPIO14 (pin 8), its
TX to GPIO15 (pin 10). Settings: 115200 8N1 (`picocom -b 115200 /dev/ttyUSB0`).
`main.c` muxes GPIO14/15 to the PL011 itself, so the firmware's Bluetooth
setup does not matter. It assumes the PL011 clock is the firmware's default
48 MHz.

## Words

The vocabulary `rpi` holds:

| Word | Meaning |
|------|---------|
| `board` | the board name, e.g. `rpi board type` |
| `us-ticks` | microseconds from the generic timer |
| `cntfrq` | the timer frequency |
| `uart-base` | address of the PL011 registers |
| `gpio-base` | address of the GPIO registers |
| `gpio-mode ( mode pin -- )` | set a pin's mode: 0 input, 1 output, 4 alt0... |
| `gpio-on ( pin -- )` | drive a pin high |
| `gpio-off ( pin -- )` | drive a pin low |
| `gpio@ ( pin -- f )` | read a pin's level |

Peripherals are memory-mapped, so `ul@` and `l!` reach any other register.
On `virt`, the GPIO words throw -21 (no Pi GPIO there). The Pi 4's green LED
is GPIO 42 (`also rpi 1 42 gpio-mode 42 gpio-on`); the Pi 3's LED sits
behind the firmware mailbox, which is not done yet.

## How it works

* `start.S` runs these steps in order:
  * It parks the other cores. On the Pi they are sent through the spin table
    at 0xd8 to a `wfi` loop; otherwise QEMU spins three host cores.
  * It drops from EL3/EL2 to EL1 and enables the FPU.
  * It sets the stack and zeroes `.tbss`/`.bss`.
  * It installs the exception vectors.
* `main.c` then does the rest, in this order:
  * It turns on the MMU and caches with an identity map of the first 4G in
    2M blocks: Normal memory for RAM, Device memory for peripherals,
    nothing elsewhere. The MMU is needed, not just faster: with it off,
    every access is a Device access and any unaligned one faults
    (picolibc's `memcpy` makes such accesses, and Forth's `@` can).
  * It sets picolibc's TLS block (errno lives there).
  * It starts the UART.
* Faults become a `THROW`, as signals do in `ports/posix/faults.h`. The vector
  saves the registers, prints `lugforth: fault ESR=... ELR=... FAR=...`, then
  `eret`s into a function that `longjmp`s into `forth_run`. A data abort
  becomes -9, an alignment fault -23. Anything else (an IRQ, SError, a fault
  inside the handler) prints and halts.
* While waiting for a key, the core sleeps in `wfi`, woken by the virtual
  timer at most 1 ms later. Interrupts stay masked, but a pending one still
  wakes `wfi`. The timer line reaches the core through the GIC (`virt`,
  Pi 4) or the Pi 3's local interrupt controller. QEMU at the prompt
  drops from 100% of a host core to about 10%.
* Memory: the image loads at 0x80000 (Pi) or 0x40080000 (`virt`). The C heap
  runs from the end of the image up to 512M on the Pi, or to the end of the
  default 128M on `virt`. The Forth dictionary is 16M taken from it.

## Not done yet

* No filesystem (no SD card driver): programs come in over the serial line.
* No images or sound in the sdl2 words; no USB keyboard or mouse.
* Real hardware: no data cache invalidation before the MMU goes on. The
  firmware runs with caches off, so this should not matter, but it is
  unchecked. The Pi 4 GIC addresses (0xFF841000/0xFF842000) are from the
  documentation, not from a board.
* A 32-bit build for the Pi Zero/1 (ARMv6).
