#!/usr/bin/env python3
"""Send files from the PC to the flash file system of a board running Lugforth or
ESP32forth, through the serial line (no tool of the IDE needed; the serial monitor
must be closed). Two boards, two short names that call this script:

  tools/esp32_put.py /dev/ttyACM0 canon.abc       -> /spiffs/canon.abc of an ESP32
  tools/pico_put.py  /dev/ttyACM0 canon.abc       -> canon.abc in the flash of a Pico
  tools/board_put.py --pico /dev/ttyACM0 a.fs b.abc -b 115200     (the script itself:
                      --esp32, the default, or --pico)
  tools/pico_put.py --print canon.abc             -> the Forth lines on stdout,
                                                     to paste in a serial monitor

The file is sent as hexadecimal, 90 bytes a line (the prompt of the board takes 200
characters at most), each line waited for. An existing file of the same name is replaced.
On the Pico the names have no /spiffs/ (no folders, see the Pico section of the manual).
Needs pyserial (pip install pyserial) except with --print."""
import os, re, sys, time

HEAD = [
    "variable pf-h   create pf-buf 100 allot",
    ": hx1 ( c -- n ) dup 57 > if 55 - else 48 - then ;",
    ": hx ( \"hex\" -- ) bl parse { a n }  n 2/ 0 do  a i 2* + c@ hx1 16 * a i 2* + 1+ c@ hx1 + pf-buf i + c!  loop  pf-buf n 2/ pf-h @ write-file drop ;",
]
CHUNK = 90          # bytes a line: "hx " and 180 hex digits stay under the 200 characters of the prompt


ROOT = '/spiffs/'    # '' for the Pico, which keeps its files in the flash, with no folder


def lines(path, name):
    data = open(path, 'rb').read()
    yield from HEAD
    yield 's" %s%s" delete-file drop' % (ROOT, name)
    yield 's" %s%s" w/o bin create-file throw pf-h !' % (ROOT, name)
    for i in range(0, len(data), CHUNK):
        yield 'hx ' + data[i:i + CHUNK].hex().upper()
    yield "pf-h @ close-file drop"
    yield 's" %s%s" r/o open-file throw dup file-size throw drop . close-file drop' % (ROOT, name)


def main(argv, board='esp32'):
    global ROOT
    for flag in ('--esp32', '--pico'):
        if flag in argv:
            board = flag[2:]; argv = [a for a in argv if a != flag]
    ROOT = '' if board == 'pico' else '/spiffs/'
    if argv and argv[0] == '--print':
        for f in argv[1:]:
            for l in lines(f, os.path.basename(f)):
                print(l)
        return 0
    baud = 115200
    if '-b' in argv:
        i = argv.index('-b'); baud = int(argv[i + 1]); del argv[i:i + 2]
    if len(argv) < 2:
        print(__doc__); return 1
    import serial
    # DTR high (a native USB port sends nothing to a host that has not raised it) and
    # RTS low (high resets the chip with an auto-reset circuit, and the USB port
    # disappears for a moment: "device reports readiness to read ...").
    port = serial.Serial()
    port.port = argv[0]; port.baudrate = baud; port.timeout = 0.02
    port.dtr = True; port.rts = False
    try:
        port.exclusive = True            # fails at once if minicom, the IDE monitor... has it
    except Exception:
        pass
    port.open()
    def rd():
        for _ in range(20):              # a hiccup of the USB serial: wait and try again
            try:
                return port.read(256)
            except serial.SerialException:
                time.sleep(0.25)
        raise SystemExit('the serial port keeps failing: is another program (minicom, '
                         'the IDE monitor, ModemManager) using it? Is the board unplugged?')
    def send(l):
        port.write(l.encode() + b'\r\n')
        buf = b''; t = time.time()
        while time.time() - t < 5:
            buf += rd()
            if re.search(rb' ok\r?\n', buf) and (buf.endswith(b'> ') or buf.endswith(b'>')):
                return buf.decode('latin-1')            # " ok", then the prompt: done
        raise SystemExit('no answer to: ' + l[:60] + '\n' + buf.decode('latin-1'))
    sys.stderr.write('waiting for the board on %s...\n' % argv[0])
    for _ in range(5):                       # wake it up: an empty line gives " ok"
        port.reset_input_buffer(); port.write(b'\r\n'); time.sleep(0.4)
        if re.search(rb'ok', port.read(256)):
            break
    else:
        raise SystemExit('the board does not answer on %s at %d baud: wrong port or '
                         'speed, program stuck in a loop (tune playing: send a key), '
                         'or a USB serial that wants DTR/RTS differently.' % (argv[0], baud))
    for f in argv[1:]:
        name = os.path.basename(f); out = ''
        all_lines = list(lines(f, name)); n = len(all_lines); t0 = time.time()
        for k, l in enumerate(all_lines, 1):
            out = send(l)
            if sys.stderr.isatty():
                done = int(30 * k / n)
                eta = (time.time() - t0) * (n - k) / k
                sys.stderr.write('\r%s [%s%s] %3d%%  %ds left ' % (
                    name, '#' * done, '.' * (30 - done), 100 * k // n, eta))
                sys.stderr.flush()
        if sys.stderr.isatty():
            sys.stderr.write('\r' + ' ' * (len(name) + 50) + '\r')
        print('%s: %d bytes sent, the board reads %s' % (name, os.path.getsize(f), (re.findall(r'(\d+)\s+ok', out) or ['?'])[-1]))
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
