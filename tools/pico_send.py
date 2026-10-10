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
first error. Needs pyserial (pip install pyserial).
"""

import argparse
import sys
import time

try:
  import serial
except ImportError:
  sys.exit('pyserial is needed: pip install pyserial')

PROMPT = b'-->'
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


def main():
  ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
  ap.add_argument('file', help='Forth source file to send')
  ap.add_argument('-p', '--port', default='/dev/ttyACM0')
  ap.add_argument('-b', '--baud', type=int, default=115200)
  ap.add_argument('-t', '--timeout', type=float, default=10.0,
                  help='seconds to wait for the prompt after a line')
  ap.add_argument('-q', '--quiet', action='store_true',
                  help="do not show the board's answers")
  args = ap.parse_args()

  lines = open(args.file, encoding='utf-8', errors='replace').read().splitlines()
  for n, line in enumerate(lines, 1):
    if len(line.encode('utf-8')) > MAX_LINE:
      sys.exit('%s:%d: line longer than %d characters, the board would '
               'truncate it' % (args.file, n, MAX_LINE))

  port = serial.Serial(args.port, args.baud, timeout=0.05)
  show = not args.quiet
  port.write(b'\r')                       # get a fresh prompt
  wait_prompt(port, 2.0, False)
  for n, line in enumerate(lines, 1):
    if not line.strip():
      continue
    port.write(line.encode('utf-8') + b'\r')
    got = wait_prompt(port, args.timeout, show)
    if PROMPT not in got:
      sys.exit('\n%s:%d: no prompt after %.0f s: %s' %
               (args.file, n, args.timeout, line))
    # The echo of the line itself is part of the answer: look only after it.
    answer = got.split(line.encode('utf-8'), 1)[-1]
    for err in ERRORS:
      if err in answer:
        sys.exit('\n%s:%d: %s: %s' % (args.file, n, err.decode(), line))
  print('\nsent %d lines' % len(lines))


if __name__ == '__main__':
  main()
