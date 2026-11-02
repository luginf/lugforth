+--------------+
|  ESP32forth  |
+--------------+

This is ESP32forth v{{VERSION}} (Revision {{REVISION}}).

ESP32forth is an indirect threaded Forth for the ESP32 family of microcontrollers.
It uses a small C "kernel" that boots via inline Forth code.
The use of C allows developers to leverage C/C++ ESP32 libraries with minimal effort.

To compile and flash with the Arduino tools, load ESP32forth.ino.

Documentation, source code and issue tracker of Lugforth:
https://luginf.github.io/lugforth/
https://github.com/luginf/lugforth
The original ESP32forth is at https://github.com/flagxor/ueforth

There are several optional components that you can add into ESP32forth.
See: optional/README-optional.txt

Tunes in ABC notation on a passive piezo buzzer
-----------------------------------------------

Wire the piezo between a GPIO and GND (a passive one: an active buzzer
has its own oscillator and only beeps). Then, at the prompt:

  esp32-abc                  \ once: loads the ABC reader, about 12 KB
  4 buzzer                   \ the piezo is on GPIO 4
  r~ X:1
  T:Test
  K:C
  CDEF GABc ~ abc-play       \ any key typed on the serial line stops it

On the ESP32-C3 any GPIO can be used. Good ones: 0, 1, 3, 4, 5, 6, 7, 10.
Avoid 2, 8 and 9 (the boot-mode pins, 8 and 9 also carry the LED and the
BOOT button of many boards), 18 and 19 (the USB port) and 11 to 17 (the
flash). abc-play mixes up to 4 notes at once (the voices V:1, V:2... of the
tune and the notes of the chords) in software: a loop adds square waves and
writes the sum as the duty of an 8 bit PWM at 78 kHz that the speaker does
not follow. Its speed is measured when a tune starts, so the pitch does not
depend on it. Every voice has a wave and a volume of its own for the whole tune: when
one rests the others do not get louder. Voice 1, the melody, is a square
wave twice as loud as the others, which are narrower pulses (a quarter). The
loop takes the whole chip while it plays (it only yields to the serial
line every 100 ms or so). abc-arp plays the same tune by turns, one note
every 10 ms, if the mix is not good. Tempo and key come from the Q: and K:
lines. This was written without a board at
hand and tested in the simulator, for the Forth only.


A tune longer than a line
-------------------------

The prompt reads at most 200 characters per line and r~ ... ~ does not
continue on the next line when typed (it does inside a file): a long
s" ..." abc-play on one line is cut and nothing plays. Put the tune in a
file on the PC, send it to /spiffs (see below) and include it:

  \ melody.fs
  esp32-abc
  4 buzzer
  : canon r~ X:1
  K:D
  V:1
  f2 e2|d2 c2|
  V:2
  z4 f2 e2|
  ~ ;
  canon abc-play

  tools/esp32_put.py /dev/ttyACM0 melody.fs      (on the PC)
  include /spiffs/melody.fs                      (on the board)

Keeping files across a reboot (SPIFFS)
--------------------------------------

The flash has a file system (SPIFFS), mounted at /spiffs at each boot
(formatted by the first boot if it is empty). What is written there stays
after a reset or a power cut:

  s" /spiffs/canon.abc" w/o create-file throw  ( fh )
  dup s" X:1" rot write-file throw close-file drop   \ write, then close
  ls /spiffs                 \ the names of the files
  cat /spiffs/canon.abc      \ shows a file
  s" /spiffs/canon.abc" delete-file drop

Also: a file /spiffs/autoexec.fs is included at each boot (hit a key within 100 ms
of the boot to skip it), and "remember" / "revive" keep the dictionary in
/spiffs/myforth. The room left: also SPIFFS SPIFFS.totalBytes .
SPIFFS.usedBytes . previous (the words live in the SPIFFS vocabulary). The Arduino IDE must use a partition
scheme with SPIFFS (Tools > Partition Scheme: "Default 4MB with spiffs");
"Huge APP" has none. A tune read back from a file: include it, or read it
into memory and give it to abc-play.

Sending a file from the PC to /spiffs
-------------------------------------

With the serial monitor of the IDE closed (the port is free), from the
Lugforth sources:

  pip install pyserial
  tools/esp32_put.py /dev/ttyACM0 canon.abc melody.fs   # COM3 on Windows

Each file lands in /spiffs under its own name (an old one is replaced). It
is sent as numbers, 24 bytes a line, each line waited for: about 2 KB/s,
fine for tunes and sources. The last number printed is the size the board
reads back. Without a serial module, tools/esp32_put.py --print canon.abc
writes the same Forth lines on the standard output, to paste into a
serial monitor. Then on the board:

  ls /spiffs
  s" /spiffs/melody.fs" included          \ a source

A tune is read into memory first (this word was tried on a PC Forth, not
on the board):

  variable sf
  : slurp ( a n -- a' u )          \ the whole file in memory
    r/o open-file throw sf !
    sf @ file-size throw drop dup allocate throw
    swap 2dup sf @ read-file throw drop
    sf @ close-file drop ;
  s" /spiffs/canon.abc" slurp abc-play

The tool of the IDE for SPIFFS images (arduino-esp32 filesystem uploader)
also works but writes the whole partition: it erases what is already there
(myforth, autoexec.fs).

Editing files on the board: ed
------------------------------

From a terminal program on the serial line (minicom, picocom, screen: not
the serial monitor of the IDE, which does not know the escape codes) a
small editor works on the files of /spiffs. Its source is sent once, then:

  tools/esp32_put.py /dev/ttyACM0 ports/esp32/miniedit.fs

  ed /spiffs/melody.fs     \ the whole path; a new name makes a new file
  40 120 terminal-size     \ rows and columns of the terminal, if it is not 24 x 80

Keys: arrows, Home, End, PageUp, PageDown, Delete, Backspace, Enter, Tab
(two spaces); Ctrl+S or Ctrl+O saves; Ctrl+Q or Ctrl+X quits (twice if the
text was not saved); Ctrl+A, Ctrl+E start and end of the line; Ctrl+K
deletes to the end of the line. The whole file is held in memory (its size
plus 8 KB) and saved by writing it again: a power cut during a save loses
the file.

The editors of the other ports (editors/editor.fs, writhdeck.fs) need
about 140 KB of heap while they load; a C3 has about 70 KB left (`unused .`),
so this one is made small (about 6 KB compiled). The first use includes
/spiffs/miniedit.fs (a second or two), which redefines ed. The editing was
tested in the simulator and the file words on a PC Forth, but never on a
board: expect to find bugs.
