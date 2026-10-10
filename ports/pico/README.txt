+-------------------------------------+
|  Lugforth for the Raspberry Pi Pico  |
+-------------------------------------+

Note: this port was adapted by Luginfo10 from the pico-ice port of uEforth by
Bradley D. Nelson. It is not part of upstream uEforth and is not endorsed
by its author.

This is Lugforth v{{VERSION}} (Revision {{REVISION}}) for the Raspberry Pi Pico
(RP2040).

Lugforth is an indirect threaded Forth.
It uses a small C "kernel" that boots via inline Forth code.

To install:
  * Hold down the BOOTSEL button on the Pico.
  * Connect the Pico to USB (then release BOOTSEL).
  * Copy lugforth-pico.uf2 to the mounted RPI-RP2 drive.
    The board reboots by itself and the drive disappears.
  * Open the USB serial port with a terminal (115200 8N1, any speed works).
    The console is the USB port itself (no UART pins to wire). On Linux the
    port shows up as /dev/ttyACM0 (check with `ls /dev/ttyACM*` or `dmesg`):
      picocom -b 115200 /dev/ttyACM0       (quit with Ctrl-A then Ctrl-X)
      screen /dev/ttyACM0 115200           (quit with Ctrl-A, K, then y)
    You should see the Lugforth banner and the --> prompt.

Sending a program from the PC:
  Do not use `cat file.fs > /dev/ttyACM0`: the board echoes everything back,
  nobody reads it, and the transfer hangs. Use the helper script instead
  (needs `pip install pyserial`):
    tools/pico_send.py file.fs
    tools/pico_send.py -p /dev/ttyACM1 file.fs
  It reads the answers while it writes, sends one line at a time, waits for
  the prompt (with a progress bar: bytes sent and left; -v shows the board's
  answers instead) and stops at the first error. Lines must stay under 200
  characters. If the last line starts a program that waits for keys (a game),
  the script turns into a small terminal so you can use it (Ctrl-A then Ctrl-X quits).
  Nothing is kept after a power cycle: the dictionary lives in RAM.

Troubleshooting:
  * "Permission denied" on /dev/ttyACM0: add yourself to the dialout group
    (sudo usermod -aG dialout $USER, then log in again), or use sudo.
  * Nothing on the serial port: check that the RPI-RP2 drive disappeared after
    the copy, otherwise the firmware was not loaded.
  * The board does not show up at all: try another USB cable, some are
    charge-only.

Example (blink the on-board LED):
  pico
  PICO_DEFAULT_LED_PIN constant led
  led gpio_init  led GPIO_OUT gpio_set_dir
  : blink  10 0 do led 1 gpio_put 250 ms led 0 gpio_put 250 ms loop ;
  blink

Documentation, source code and issue tracker of Lugforth:
https://luginf.github.io/lugforth/
https://github.com/luginf/lugforth
The original uEforth is at https://github.com/flagxor/ueforth
