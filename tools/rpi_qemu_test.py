#! /usr/bin/env python3
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

"""Boots the bare-metal ports/rpi images under QEMU and talks to them over
the serial console: arithmetic, floats, timer, faults, allocation, GPIO,
and that `bye` makes QEMU exit.

  tools/rpi_qemu_test.py [out/rpi]        (what `make -C ports/rpi test` runs)
"""

import os
import re
import subprocess
import sys
import threading
import time

QEMU = os.environ.get('QEMU', 'qemu-system-aarch64')
PROMPT = '--> '


class Board:
  def __init__(self, args):
    self.proc = subprocess.Popen(
        [QEMU] + args + ['-display', 'none', '-serial', 'stdio', '-monitor', 'none'],
        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    self.out = ''
    self.lock = threading.Lock()
    threading.Thread(target=self.reader, daemon=True).start()

  def reader(self):
    while True:
      b = self.proc.stdout.read1(4096)
      if not b:
        return
      with self.lock:
        self.out += b.decode('latin-1')

  def wait_for(self, text, start, timeout):
    end = time.time() + timeout
    while time.time() < end:
      with self.lock:
        i = self.out.find(text, start)
        if i >= 0:
          return i
      time.sleep(0.02)
    raise RuntimeError('timeout waiting for %r, got:\n%s' % (text, self.out[start:]))

  def boot(self, timeout=60):
    i = self.wait_for(PROMPT, 0, timeout)
    banner = self.out[:i]
    self.pos = i + len(PROMPT)
    return banner

  def run(self, line, timeout=20):
    """Sends a line, returns what it printed before the next prompt."""
    self.proc.stdin.write(line.encode() + b'\r')
    self.proc.stdin.flush()
    i = self.wait_for(PROMPT, self.pos, timeout)
    text = self.out[self.pos:i]
    self.pos = i + len(PROMPT)
    text = text.replace('\r', '')
    if text.startswith(line):          # the echo
      text = text[len(line):]
    return text.strip()

  def bye(self, timeout=20):
    self.proc.stdin.write(b'bye\r')
    self.proc.stdin.flush()
    try:
      return self.proc.wait(timeout)
    except subprocess.TimeoutExpired:
      self.proc.kill()
      return None

  def kill(self):
    if self.proc.poll() is None:
      self.proc.kill()


failures = 0


def check(name, got, ok):
  global failures
  if ok:
    print('  ok    %s' % name)
  else:
    failures += 1
    print('  FAIL  %s: %r' % (name, got))


def test_board(name, args, has_gpio):
  print('%s: qemu %s' % (name, ' '.join(args)))
  b = Board(args)
  try:
    banner = b.boot()
    check('banner', banner, ('Lugforth (%s)' % name) in banner)
    r = b.run('1 2 + .')
    check('arithmetic', r, r.split()[0] == '3')
    r = b.run('3e 2e f* f.')
    check('floats', r, r.split()[0] == '6.000000')
    r = b.run(': t 3 0 do i . loop ; t')
    check('loop', r, r.startswith('0 1 2'))
    r = b.run('ms-ticks 200 ms ms-ticks swap - .')
    n = int(r.split()[0])
    check('200 ms (%d)' % n, r, 190 <= n <= 400)
    r = b.run('100 allocate throw dup 7 swap ! dup @ . free throw')
    check('allocate', r, r.split()[0] == '7')
    r = b.run('here 1+ @ drop 42 .')
    check('unaligned @', r, r.split()[0] == '42')
    r = b.run('$100000000 @')
    check('fault becomes a throw', r, 'lugforth: fault' in r and 'ERROR' in r)
    r = b.run('5 5 * .')
    check('prompt after the fault', r, r.split()[0] == '25')
    r = b.run('rpi board type forth')
    check('board', r, r.split()[0] == name)
    if has_gpio:
      r = b.run('also rpi 1 21 gpio-mode 21 gpio-on 21 gpio@ . 21 gpio-off 21 gpio@ . previous')
      check('gpio', r, r.split()[:2] == ['-1', '0'])
    else:
      r = b.run('also rpi 21 gpio@ previous')
      check('no gpio on virt', r, 'ERROR' in r)
    code = b.bye()
    check('bye exits qemu', code, code is not None)
  except Exception as e:
    check('session', str(e), False)
  finally:
    b.kill()


def main():
  out = sys.argv[1] if len(sys.argv) > 1 else 'out/rpi'
  test_board('qemu-virt', ['-M', 'virt', '-cpu', 'cortex-a53', '-m', '128M',
                           '-kernel', os.path.join(out, 'virt', 'lugforth.elf')], False)
  test_board('pi3', ['-M', 'raspi3b', '-no-reboot',
                     '-kernel', os.path.join(out, 'pi3', 'kernel8.img')], True)
  print('%s' % ('ALL OK' if failures == 0 else '%d FAILED' % failures))
  sys.exit(1 if failures else 0)


if __name__ == '__main__':
  main()
