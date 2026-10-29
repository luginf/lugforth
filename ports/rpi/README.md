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

## Status

| | QEMU `virt` | QEMU `raspi3b` | real Pi 3 | Pi 4 |
|---|---|---|---|---|
| Boot, prompt, floats, timer | tested | tested | to try | written blind |
| Faults become `THROW` | tested | tested | to try | written blind |
| Idle CPU (sleeps in `wfi`) | ~10% of a host core | ~12% | to try | written blind |
| Screen, drawing, lines, flip | `-device ramfb`, tested | mailbox, tested | to try | written blind |
| Text (8x8 font) | tested | tested | to try | written blind |
| Images (built-in PNGs) | tested | tested | to try | written blind |
| Keyboard and mouse | virtio-input, tested | USB (DWC2), tested | Pi 3 A+ only: no split transactions | not done (xHCI) |
| Keys from the serial line | tested | tested | to try | written blind |
| Sound, ABC tunes | virtio-sound, tested | silent (QEMU has no PWM sound) | written blind (PWM + DMA) | not done |
| GPIO words | none on virt | tested | to try | written blind |
| Files: SD card or disk, FAT12/16/32 | virtio-blk, FAT16 and FAT12, tested | SD card, MBR + FAT32, tested | to try | written blind (EMMC2) |
| `include`, `ls`, `cat`, `autoexec.fs` at boot | tested | tested | to try | written blind |
| `paste` (a whole file over serial) | tested | same code, not run by the test | to try | written blind |

Here "tested" means that `make test` (`tools/baremetal_qemu_test.py`) checks it
on every run, 121 checks in all:

* Answers are read on the serial line.
* The screen is read through QEMU's `screendump`. Lines, text and images
  are compared pixel by pixel with a reference computed in Python.
* Keys and the pointer are driven through QMP `input-send-event`: held,
  released, absolute and relative motion, and typing at the prompt.
* virt's sound is recorded with `-audiodev wav` and analyzed: frequency
  and length of beeps, the fade of a hit, the notes of a tune.
* `examples/games/sdl2_breakout.fs` (sent line by line) and
  `examples/games/sdl2_roguelike.fs` (sent with `paste`) run unchanged.
* Each machine gets a FAT disk made by `tools/baremetal_mkdisk.py`: FAT16 and
  FAT12 on virt, MBR + FAT32 on raspi3b's SD card. The test runs `include`,
  `needs` into a subdirectory and a long name. It writes 20000 bytes over
  several clusters and reads them back, then renames, deletes and resizes.
  Once QEMU has stopped, the image is read back with `mtools` and checked
  with `fsck.fat`.

To check on a real Pi 3, for these were never run on one:

* The serial console. The PL011 UART is muxed onto GPIO 14/15 by the
  kernel itself, at 115200 baud, assuming the firmware's default 48 MHz
  UART clock.
* The screen. The mailbox goes through the 0xC0000000 bus alias, and the
  data cache is cleaned after each `flip`.
* The USB keyboard on a Pi 3 A+. On a Pi 3 B, the ports are behind the
  LAN9514 high speed hub, which needs split transactions (not done).
* The sound on the headphone jack: PWM clock from PLLD, DMA channel 5.
* The SD card. The EMMC controller is polled, the CPU moves the data, and
  the pins 48 to 53 are set to ALT3. The clock comes from the firmware's
  EMMC clock rate, and the card runs at 25 MHz on one data line. Real cards
  are slower to start than QEMU's.
* The other cores parked through the spin table.
* Real caches: there is no invalidation before the MMU goes on.

## Building and running (Linux x86_64 is fine, QEMU emulates the ARM)

```sh
sudo apt install gcc-aarch64-linux-gnu picolibc-aarch64-linux-gnu qemu-system-arm
make -C ports/rpi            # the three images
make -C ports/rpi run        # QEMU virt, console here; Ctrl-A then X quits
make -C ports/rpi run-pi3    # QEMU raspi3b
make -C ports/rpi test       # boots both, checks answers over the serial line
```

## The console on the screen

```sh
make -C ports/rpi gui              # QEMU raspi3b: the Forth prompt in the window
make -C ports/rpi gui BOARD=virt   # QEMU virt with -device ramfb
```

When there is a framebuffer, the console is drawn on it as well as sent to
the UART (`console.h`): 1280x960, 80x30 cells of the 8x8 font (glyphs doubled
in height, then x2). The size is a choice: `make gui ZOOM=1` builds a 640x480 console,
`1 console-zoom` (or 2, 3 for 1920x1440) changes it at the prompt, and the
line can go into `autoexec.fs`, a block cursor, scrolling, and the ANSI sequences of
`common/lineedit.fs` and `ansi.fs` (cursor moves, `H J K`, colors with `m`). The prompt
uses `lineedit.fs`: history of 64 lines (not kept), Tab completes words and file names;
Several terminals as on the PC (`Alt+F1` to `Alt+F4`, `n term`, see ports/baremetal/term_run.h): written blind, no aarch64 toolchain to
build it here; the logic is the PC's, checked under QEMU there.

`pager.fs` stops long output at each screenful of the console (`pager-off` to disable).
The keys of the window (USB or virtio keyboard) reach `key` as on the serial
line, so `make gui` needs no terminal. It is the same on a Pi with an HDMI
screen and a USB keyboard. Without a framebuffer (virt without `-device
ramfb`) nothing changes. The keyboard is US (QWERTY) at boot; type `azerty` for
the French layout (`qwerty` goes back). QEMU sends key positions, so the
layout is the board's: AltGr (right Alt) gives `~ # { [ | ` \\ ^ @ ] }`, and
`é è ç à ù` work. Put `azerty` in `autoexec.fs` on the SD card to have it at
boot. It changes the characters typed at the prompt, not the key codes of
`pressed?` (positions, as WASD needs).

The sdl2 word `screen` takes the framebuffer over, and `close-screen` gives
the console back (cleared). Fault messages still go to the UART only.

`graphic` (then `0 0 100 100 line`, `red color 10 10 50 30 box`, `5 ink ...`) draws under the console
without `screen`: the drawing is a 640x480 layer (`GFX-OPEN`, `fb_gfx` in screen.h) that console.h shows
through the cells with the default background, and shows at once. `cls` clears it, `page` clears the
text. `w h graphic-size` (up to 1920x1440) sets the resolution of the layer, stretched over the whole
console screen (`GFX-SIZE`); the default is 640x480. The layer ends at `screen`.

## ed and writhdeck

`ed` and `writhdeck` (editors/editor.fs, writhdeck.fs) run on the console: `make disk` copies them
to `/lib`, and the words `ed` and `writhdeck` of platform.fs include them on first use. They are the same
files as on Linux: `[defined] posix` picks the POSIX code (termios, getenv, mkdir, chdir...) or the board's
(`form` is 30 rows by 80 columns, `chdir`, `getcwd`, `delete-file`, `rename-file`, `unix-time`, and a
`readdir` whose names end with `/` for a directory). They need what the console now does: 256-color and
true-color SGR, underline, OSC titles ignored, and from the keyboard Home, End, PageUp, PageDown, Insert,
Delete, F1 to F4, Ctrl, Shift and Alt with the arrows (`CSI 1 ; m` as xterm sends them) and Alt+key as
ESC then the key. `CHDIR` and `GETCWD` (fat.h) give the disk a current directory. Creating a file needs
a long name is fine (`mkdir` and long names are done: the browser's backup makes `backups/`). `make test` types in both, saves
on the FAT, reads back with `cat` and enters a directory of the browser.

## Uxn / Varvara

`examples/system/varvara/varvara_rpi.fs` runs Uxn ROMs on the board, with all the Varvara devices (screen,
keys, mouse, console, files, sound, date): see `examples/system/varvara/README.md`. It needs `FB-BLIT8` (a
picture of 8-bit palette indexes drawn on the screen), `INPUT-TEXT` (the characters typed), `SND-QUEUE`
and `SND-QUEUED` (a queue of stereo frames played with the mixer's sound), `UNIX-TIME` and `UNIX-TIME!`
(the clock), and the keys Ctrl, Shift, Alt, Home, F1 to F4 and F12, which `input-down?` sees as the codes
224 to 232.

## Graphics: the sdl2 words

The vocabulary `sdl2` has the same short words as Linux
(`docs/en/SDL2-words.md`) and the web (`docs/en/SDL2-web.md`). So
`examples/games/sdl2_breakout.fs` runs unchanged, sent over the serial line:

```sh
make -C ports/rpi breakout             # QEMU raspi3b window; play from this terminal
make -C ports/rpi screen               # the same window, prompt only
make -C ports/rpi breakout BOARD=virt  # QEMU virt with -device ramfb
```

`tools/baremetal_qemu_run.py` (what both targets run) sends the file one line at a
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
* Images: `load-image free-image image-size draw draw-size draw-part
  image-alpha image-tint` (and `sdl2-image`, which does nothing). There is
  no PNG decoder on the board, so the images are built into the kernel: the PNG files of
  `ASSETS` in the Makefile (`examples/games/sdl2_tiles.png`,
  `examples/games/sdl2_tiles_alt.png`) are turned into pixels by
  `tools/baremetal_assets.py`. That script has its own PNG decoder and needs only
  zlib. `load-image` finds them by name, ignoring directories, and throws
  -38 for any other name. Add your own with
  `make ASSETS="a.png b.png"`.
* Time: `ticks delay dt`.
* Input: `events pressed? last-key quit? quit! mouse-x mouse-y wheel`, the
  `key-` and `-BUTTON` constants. `scan?` is always false.
* Sound: `beep drum-hit pitch-drop silence beeping? wave beep-volume
  audio-open audio-open? latency sample-rate`, as on Linux.
* Tunes: `sdl2-abc`, then `abc-play abc-loop tune-play tune-loop tune-stop
  tune-playing? music-volume`. The ABC reader is the shared `common/abc.fs`,
  and the notes are played by oscillators, as on the web. `soundfont` does
  nothing, and there is no `load-sound` or `load-music` (no files).

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

Keys and mouse (`input.h`, `usb.h`, `virtio.h`):

* A real keyboard and pointer give true key down and up, the position and
  the buttons, polled without interrupts. The keys typed also reach the
  prompt (`key`). While a game calls `events`, they are game keys only.
  * Pi 3: the USB host controller (DWC2), in DMA mode, enumerates through
    hubs. It keeps boot keyboards, boot mice and QEMU's `usb-tablet`
    (absolute). QEMU: `-device usb-kbd -device usb-tablet`.
  * virt: `virtio-input` (`-device virtio-keyboard-device -device
    virtio-tablet-device`, or `virtio-mouse-device`), over modern
    virtio-mmio, hence `-global virtio-mmio.force-legacy=false` (QEMU's
    default is legacy). The same virtio layer serves the sound.
  * `make breakout`, `make screen` and `tools/baremetal_qemu_run.py` add these
    devices, so the QEMU window's keyboard and mouse work.
  * Not done: split transactions. A full or low speed keyboard behind a high
    speed hub needs them, and the USB ports of a real Pi 3 B are behind one
    (LAN9514). A Pi 3 A+ has its port on the controller itself. The Pi 4's
    USB-A ports are on an xHCI controller, not handled.
* The serial terminal still works: it sends a byte when a key goes down (and
  again for each auto-repeat) but nothing when it goes up. So a key typed
  there counts as held for `key-hold` ms (120) after its last byte.
  `xset r rate 200 40` shortens the pause of a held key before it repeats.

`make test` drives the keyboard and pointer through QEMU's QMP
(`input-send-event`) on virt and raspi3b. It checks keys held and released,
the button, absolute and relative motion, and typing `2 3 * .` at the
prompt.

`make test` does this on raspi3b and on virt with ramfb. It loads breakout
this way, reads the screen through QEMU's
`screendump` (bricks, paddle and ball colors), launches the ball with space,
and leaves with Esc.

Sound (`sound.h`) is mixed in C at 44100 Hz:

* Beeps play in a queue; hits play at once, 8 at a time, fading out, with
  their pitch falling by `pitch-drop`; tune notes have a short attack and a
  linear decay.
* It is fed at every `pause` (so by `ms`, `delay` and `key`) and while the
  prompt waits.
* QEMU virt: virtio-sound (`-device virtio-sound-device,audiodev=...`),
  16-bit, 8 periods of 10 ms in flight.
* Pi 3: the PWM of the headphone jack (GPIO 40 and 41), fed by DMA from
  two half buffers, the CPU linking the next one while the DMA plays the
  other. It is **written without a board**. QEMU has the PWM's registers but
  plays nothing, and it runs a DMA chain at once, without the PWM's pacing,
  so `snd_init` notices that and stays silent there.
* `make test` records virt's sound through `-audiodev wav` and checks it:
  440 then 660 Hz for 300 ms with 200 ms of silence between, a `drum-hit`
  that fades, and the notes C E G c of a tune.

## Files: the SD card

The file words work on the FAT of the SD card (QEMU raspi3b: `-drive
if=sd,format=raw,file=card.img`, a size that is a power of 2) or, on virt,
of a virtio-blk disk (`-drive if=none,id=d0,format=raw,file=disk.img
-device virtio-blk-device,drive=d0`). These words are `open-file`,
`create-file`, `read-file`, `write-file`, `file-size`, `reposition-file`,
`resize-file`, `delete-file`, `rename-file`, `open-dir` and `read-dir`. So
`include`, `needs`, `ls` and `cat` work, and `rpi disk?` tells whether a
disk was found.

```sh
make -C ports/rpi disk                          # out/rpi/disk.img: the examples
make -C ports/rpi screen DISK=out/rpi/disk.img  # then: ls  include sdl2_roguelike.fs
tools/baremetal_mkdisk.py card.img prog.fs lib/util.fs=util.fs autoexec.fs
```

* `autoexec.fs` at the root, if there is one, runs at boot after the
  banner. With an HDMI screen and a USB keyboard, that makes a Pi that
  starts on its own program.
* FAT12, FAT16 and FAT32, on the first partition of an MBR (what an SD
  card formatted on a PC has) or on a disk without one. Paths start at the
  root, `/` between directories, case ignored.
* Long names (VFAT) are read and written, in UTF-8: a name that fits 8.3 in
  one case is a short entry (lower case kept, as Linux does); any other
  (long, mixed case, accents) gets long-name entries and a short alias
  `BASE~N.EXT`. Only a name of over 255 characters, or with a character FAT
  forbids (`\ / : * ? " < > |`, a trailing dot or space), is refused
  (error 36, 22).
* Each `write-file` is on the card before it returns (data, FAT, size). On
  FAT32 the FSInfo free count is kept right.
* `mkdir` and `rmdir` (vocabulary `rpi`, `( a n -- ior )`; `md` and `rd` at
  the prompt) make and remove a directory, with its `.` and `..`; `chdir` and
  `getcwd` give a current directory. `rmdir` refuses a directory that is not empty (39)
  or is the current one.
* Dates come from the clock of the board (the RTC of QEMU virt, else the day
  of the build, `unix-time!` sets it), as the creation, access and
  modification time of files and directories.
* Errors are errno numbers: 2 not found, 17 exists, 22 bad name, 39 not empty,
  36 name too long, 28 full...

## Sending files: paste

Without a card, type `paste`, send the text, then Ctrl-D (byte 4).
The text is kept and evaluated at once, as `include` would, so strings and
comments may span lines: `examples/games/sdl2_roguelike.fs` keeps its tunes in
`r~ ... ~` over several lines, which line by line sending breaks.
`tools/baremetal_qemu_run.py` sends files this way (`make roguelike`). Up to
`paste-max` bytes (256K).

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

* Files: no rename of a directory, no `fsck` of a card pulled before a write ended.
* USB: no split transactions (real Pi 3 B), no Pi 4. Sound on the Pi 3 is
  untried, and there is none on the Pi 4.
* Real hardware: no data cache invalidation before the MMU goes on. The
  firmware runs with caches off, so this should not matter, but it is
  unchecked. The Pi 4 GIC addresses (0xFF841000/0xFF842000) are from the
  documentation, not from a board.
* A 32-bit build for the Pi Zero/1 (ARMv6).
