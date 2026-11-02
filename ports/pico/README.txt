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
    You should see the Lugforth banner and the > prompt.

Running a program from the PC (it is typed at the prompt and nothing is kept; to
store a file in the flash use tools/pico_put.py, see below):
  Do not use `cat file.fs > /dev/ttyACM0`: the board echoes everything back,
  nobody reads it, and the transfer hangs. Use the helper script instead
  (needs `pip install pyserial`):
    tools/pico_evaluate.py file.fs
    tools/pico_evaluate.py -p /dev/ttyACM1 file.fs
  It reads the answers while it writes, sends one line at a time, waits for
  the prompt (with a progress bar: bytes sent and left; -v shows the board's
  answers instead) and stops at the first error. Lines must stay under 200
  characters. If the last line starts a program that waits for keys (a game),
  the script turns into a small terminal so you can use it (Ctrl-A then Ctrl-X quits).
  Random music with a button: examples/music/abc_random.fs makes a new tune of
12 bars (arpeggio melody, chords or counterpoint) and plays it again and
again; a button on GP20 (to ground) starts another one:

  tools/pico_put.py /dev/ttyACM0 examples/music/abc_random.fs
  pico-abc  18 buzzer  include abc_random.fs  20 music-button  random-music

A key on the serial line ends it. The number shown ("tune 495408161") is the seed:
  495408161 music-seed                      plays that tune again
  495408161 rand-seed ! new-tune type       shows it as ABC text, to copy
  495408161 rand-seed ! save-tune t1.abc    writes it in a file (cat t1.abc)
abc-halt-with ( xt ) lets any word stop a
tune of abc-play (true stops; the default is a key).

The sound was heard on a Maker Pi Pico V1 (Pico of 2020).


Files in the flash
------------------

The last 1 MB of the flash holds a small file system: named files that stay
after a power cycle, with the usual words (open-file, create-file,
read-file, write-file, close-file, delete-file, rename-file, include) and
the commands of the other ports:

  s" t.txt" w/o create-file throw  dup s" hello" rot write-file throw  close-file drop
  ls                         \ the names
  cat t.txt                  \ the text
  include melody.fs          \ a file is a program
  rm t.txt                   \ remove it
  pico ffs_free_sectors . cr forth     \ free sectors of 4 KB (253 when empty)

Send a file from the PC (pip install pyserial, the serial monitor closed).
tools/pico_put.py calls tools/board_put.py, as tools/esp32_put.py does for the
ESP32 (there the names go under /spiffs/):

  tools/pico_put.py /dev/ttyACM0 melody.fs canon.abc

Conditions, to know before using it:

  - Names have 23 characters at most, no folders (a leading / is ignored),
    119 files at most, about 1 MB in all. One file at a time can be open
    for writing (the others for reading); 4 files open at most.
  - A file is written sector by sector (4 KB) into free sectors; the
    directory is written at close-file, so a power cut during a write
    leaves the old file as it was (the new one is lost). Replacing a file
    is atomic, but a file that is never closed is never kept.
  - Writing is slow: erasing and programming a sector of the flash takes
    about 50 ms with the interrupts off, so the USB console stops for a
    moment at each 4 KB written. Do not write in a fast loop of a game.
  - The flash wears: about 100 000 erases of a sector. Saving a small
    file erases 2 sectors (its data and the directory, which alternates
    between two sectors); do not save at each key.
  - Reading is straight from the memory of the chip, fast.
  - Loading a new program with a UF2 file (drag and drop in BOOTSEL) writes
    only the sectors of the program, so the files should stay; this was
    not tried on a board. Erasing the whole flash (flash_nuke.uf2 or
    picotool erase) removes them. There is no autoexec.fs: a broken one
    could only be removed by erasing the whole flash.
  - Blocks (use, block) are not supported: resize-file only accepts a size
    that is not larger.
  - It was written and tested in out/pico-sim only (the flash there is a
    file, LUGFORTH_PICO_FLASH=name keeps it between runs). On a board: not
    tried yet.
