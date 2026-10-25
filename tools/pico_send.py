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

"""Sends a Forth source file to a Lugforth board over a serial port.

  tools/pico_send.py cyoa.fs                     (uses /dev/ttyACM0)
  tools/pico_send.py -p /dev/ttyACM1 cyoa.fs

A plain `cat file > /dev/ttyACM0` hangs: the board echoes everything back and
nobody reads it. This script reads the answers while it writes, sends one line
at a time and waits for the prompt before the next one, and stops at the
first error. When the last line starts something that waits for keys (a game
that never shows the prompt again), the script becomes a small terminal so you
can play: keys go to the board, Ctrl-A then Ctrl-X quits (as in picocom;
Ctrl-A Ctrl-A sends a real Ctrl-A, Ctrl-] also quits). Needs pyserial (pip install pyserial).
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


def main():
  ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
  ap.add_argument('file', help='Forth source file to send')
  ap.add_argument('-p', '--port', default='/dev/ttyACM0')
  ap.add_argument('-b', '--baud', type=int, default=115200)
  ap.add_argument('-t', '--timeout', type=float, default=10.0,
                  help='seconds to wait for the prompt after a line')
  ap.add_argument('-v', '--verbose', action='store_true',
                  help="show the board's answers instead of the progress bar")
  args = ap.parse_args()

  lines = open(args.file, encoding='utf-8', errors='replace').read().splitlines()
  for n, line in enumerate(lines, 1):
    if len(line.encode('utf-8')) > MAX_LINE:
      sys.exit('%s:%d: line longer than %d characters, the board would '
               'truncate it' % (args.file, n, MAX_LINE))

  port = serial.Serial(args.port, args.baud, timeout=0.05)
  show = args.verbose
  total = sum(len(l.encode('utf-8')) + 1 for l in lines)
  sent = 0
  sys.stderr.write('%s: waiting for the prompt...\n' % args.port)
  if not sync(port):
    sys.exit('No prompt from the board. It may still be running a program '
             'that waits for keys: press reset (or unplug and replug it), '
             'then try again. Use -v to see what it sends.')
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
