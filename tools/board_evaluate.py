#!/usr/bin/env python3
# Copyright 2026 Luginfo10
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

"""Evaluates a Forth source file on a board over a serial port: the text is typed
at the prompt, line by line, and nothing is kept (to store a file in the flash of a
board, use tools/pico_put.py or tools/esp32_put.py). Short names for the two boards:

  tools/pico_evaluate.py cyoa.fs                 (uses /dev/ttyACM0)
  tools/pico_evaluate.py -p /dev/ttyACM1 cyoa.fs
  tools/esp32_evaluate.py file.fs                (line by line only: no paste on the ESP32)
  tools/board_evaluate.py file.fs                (this script: the Pico by default, --esp32)

A plain `cat file > /dev/ttyACM0` hangs: the board echoes everything back and
nobody reads it. This script reads the answers while it writes, sends one line
at a time and waits for the prompt before the next one, and stops at the
first error. When the last line starts something that waits for keys (a game
that never shows the prompt again), the script becomes a small terminal so you
can play: keys go to the board, Ctrl-A then Ctrl-X quits (as in picocom;
Ctrl-A Ctrl-A sends a real Ctrl-A, Ctrl-] also quits). Needs pyserial (pip install pyserial).

A file with a string over several lines (r~ ... ~ or r| ... |, a tune in ABC
notation for example) or a line of more than 199 characters cannot be typed
line by line: it is sent as a whole with the word `paste` of the board, kept
there (32 KB at most) and evaluated at once. This is automatic; --paste
forces it, --lines forbids it.
"""

import argparse
import os
import select
import sys
import termios
import time
import tty

try:
  import serial
except ImportError:
  sys.exit('pyserial is needed: pip install pyserial')

PROMPT = b'>'
ERRORS = (b'NOT FOUND', b'ERROR', b'STACK UNDERFLOW', b'STACK OVERFLOW')
MAX_LINE = 199  # the board's line buffer holds 200 characters


def wait_prompt(port, timeout, show):
  """Reads until the prompt shows up; returns what was read."""
  got = b''
  end = time.time() + timeout
  while time.time() < end:
    chunk = port.read(256)
    if chunk:
      got += chunk
      if show:
        sys.stdout.write(chunk.decode('utf-8', 'replace'))
        sys.stdout.flush()
      if PROMPT in got:
        return got
  return got


def multiline_string(lines):
  """True when a r~ or r| string is not closed on the line that opens it."""
  for line in lines:
    for opener in ('r~', 'r|'):
      i = line.find(opener)
      if i >= 0 and line.find(opener[1], i + 2) < 0:
        return True
  return False


def send_paste(port, text, show, label):
  """`paste`, the text, Ctrl-D: the board keeps it and evaluates it at once."""
  data = text.replace('\r\n', '\n').encode('utf-8')
  if len(data) > 32768:
    sys.exit('%s: %d bytes, the board keeps 32768 at most with paste' % (label, len(data)))
  port.write(b'paste\r')
  time.sleep(0.3)
  port.read(256)
  sent = 0
  while sent < len(data):
    chunk = data[sent:sent + 64]
    port.write(chunk)
    sent += len(chunk)
    time.sleep(0.01)                     # the board reads the keys one by one
    port.read(256)
    if not show:
      progress(sent, len(data))
  port.write(b'\x04')
  got = wait_prompt(port, 15.0, show)
  if PROMPT not in got:
    sys.exit('\n%s: no prompt after the text (an error that stops the board?)' % label)
  for err in ERRORS:
    if err in got:
      sys.exit('\n%s: %s: %s' % (label, err.decode(), got.decode('utf-8', 'replace').strip()))
  print('\nsent %d bytes with paste' % len(data))


def progress(sent, total):
  """One line, rewritten in place: bar, bytes sent and bytes left."""
  width = 30
  filled = width * sent // total if total else width
  sys.stderr.write('\r[%s%s] %d%%  %d / %d bytes, %d left   ' % (
      '#' * filled, '-' * (width - filled), 100 * sent // max(total, 1),
      sent, total, total - sent))
  sys.stderr.flush()


def sync(port):
  """Gets a fresh prompt: Return, then Ctrl-C (some programs stop on it)."""
  for attempt in (b'\r', b'\x03\r', b' \r', b'\r'):
    port.write(attempt)
    if PROMPT in wait_prompt(port, 3.0, False):
      return True
  return False


def terminal(port):
  """Relays the keyboard to the board and the board to the screen."""
  if not sys.stdin.isatty():
    return
  print('\n[terminal mode: Ctrl-A then Ctrl-X to quit]')
  old = termios.tcgetattr(sys.stdin)
  tty.setraw(sys.stdin.fileno())
  prefix = False
  try:
    while True:
      ready, _, _ = select.select([sys.stdin, port], [], [], 0.1)
      if port in ready:
        data = port.read(256)
        if data:
          os.write(sys.stdout.fileno(), data)
      if sys.stdin in ready:
        for ch in os.read(sys.stdin.fileno(), 32):
          if prefix:                      # the key after Ctrl-A
            prefix = False
            if ch in (0x18, ord('x'), ord('X'), 0x11, ord('q')):
              return                      # Ctrl-X, x, Ctrl-Q, q
            if ch == 0x01:
              port.write(b'\x01')         # Ctrl-A Ctrl-A: a real Ctrl-A
          elif ch == 0x01:
            prefix = True
          elif ch == 0x1d:                # Ctrl-]
            return
          else:
            port.write(bytes([ch]))
  finally:
    termios.tcsetattr(sys.stdin, termios.TCSADRAIN, old)
    print()


def main(board='pico'):
  ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
  ap.add_argument('file', help='Forth source file to send')
  ap.add_argument('-p', '--port', default='/dev/ttyACM0')
  ap.add_argument('-b', '--baud', type=int, default=115200)
  ap.add_argument('-t', '--timeout', type=float, default=10.0,
                  help='seconds to wait for the prompt after a line')
  ap.add_argument('-v', '--verbose', action='store_true',
                  help="show the board's answers instead of the progress bar")
  ap.add_argument('--paste', action='store_true', help='send the file as a whole with paste')
  ap.add_argument('--lines', action='store_true', help='send line by line even for a multi-line string')
  ap.add_argument('--esp32', action='store_true', help='an ESP32 board: no paste, line by line')
  ap.add_argument('--pico', action='store_true', help='a Raspberry Pi Pico (the default)')
  args = ap.parse_args()
  if args.esp32 or (board == 'esp32' and not args.pico):
    args.lines = True

  text = open(args.file, encoding='utf-8', errors='replace').read()
  lines = text.splitlines()
  too_long = any(len(l.encode('utf-8')) > MAX_LINE for l in lines)
  paste = args.paste or ((too_long or multiline_string(lines)) and not args.lines)
  if not paste:
    for n, line in enumerate(lines, 1):
      if len(line.encode('utf-8')) > MAX_LINE:
        sys.exit('%s:%d: line longer than %d characters, the board would '
                 'truncate it' % (args.file, n, MAX_LINE))

  port = serial.Serial()
  port.port = args.port; port.baudrate = args.baud; port.timeout = 0.05
  port.dtr = True; port.rts = False       # RTS high resets an ESP32 with auto-reset
  port.open()
  show = args.verbose
  total = sum(len(l.encode('utf-8')) + 1 for l in lines)
  sent = 0
  sys.stderr.write('%s: waiting for the prompt...\n' % args.port)
  if not sync(port):
    sys.exit('No prompt from the board. It may still be running a program '
             'that waits for keys: press reset (or unplug and replug it), '
             'then try again. Use -v to see what it sends.')
  if paste:
    send_paste(port, text, show, args.file)
    return
  for n, line in enumerate(lines, 1):
    if not line.strip():
      sent += len(line.encode('utf-8')) + 1
      continue
    port.write(line.encode('utf-8') + b'\r')
    sent += len(line.encode('utf-8')) + 1
    got = wait_prompt(port, args.timeout, show)
    if not show:
      progress(sent, total)
    if PROMPT not in got and n == len(lines):
      terminal(port)                      # the last line wants keys
      return
    if PROMPT not in got:
      sys.exit('\n%s:%d: no prompt after %.0f s: %s' %
               (args.file, n, args.timeout, line))
    # The echo of the line itself is part of the answer: look only after it.
    answer = got.split(line.encode('utf-8'), 1)[-1]
    for err in ERRORS:
      if err in answer:
        sys.exit('\n%s:%d: %s: %s' % (args.file, n, err.decode(), line))
  if not show:
    progress(total, total)
  print('\nsent %d lines, %d bytes' % (len(lines), total))


if __name__ == '__main__':
  main()
