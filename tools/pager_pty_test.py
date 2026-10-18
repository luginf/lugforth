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

"""Tests the pager (common/pager.fs) of the POSIX port in a pty of 24 rows:
    tools/pager_pty_test.py out/posix/lugforth
"""

import fcntl
import os
import pty
import select
import struct
import subprocess
import sys
import termios
import time

MORE = b'-- more --'
failures = 0


def check(name, ok, got=b''):
  global failures
  print('  %s  %s' % ('ok  ' if ok else 'FAIL', name))
  if not ok:
    failures += 1
    print('       got: %r' % got[-300:])


class Session:
  def __init__(self, exe, rows=24):
    self.pid, self.fd = pty.fork()
    if self.pid == 0:
      os.execv(exe, [exe])
    fcntl.ioctl(self.fd, termios.TIOCSWINSZ, struct.pack('HHHH', rows, 80, 0, 0))
    self.out = b''
    self.wait(b'--> ')

  def wait(self, text, timeout=5):
    end = time.time() + timeout
    start = len(self.out)
    while time.time() < end:
      if text in self.out[start:]:
        break
      r, _, _ = select.select([self.fd], [], [], 0.1)
      if r:
        try:
          self.out += os.read(self.fd, 65536)
        except OSError:
          break
    return self.out[start:]

  def send(self, data, expect, timeout=5):
    time.sleep(0.1)
    os.write(self.fd, data)
    return self.wait(expect, timeout)

  def close(self):
    try:
      os.write(self.fd, b'bye\r')
      time.sleep(0.3)
      os.kill(self.pid, 9)
    except OSError:
      pass
    os.waitpid(self.pid, 0)


def main():
  exe = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else 'out/posix/lugforth')
  s = Session(exe)
  got = s.send(b'words\r', MORE)
  check('words stops after a screenful', MORE in got, got)
  s.send(b'q', b'--> ')
  s.send(b': t 100 0 do i . cr loop ; t\r', MORE)
  check('about a page of lines (%d)' % got.count(b'\n'), 15 <= got.count(b'\n') <= 24, got)
  got = s.send(b' ', MORE)
  check('space shows the next page', MORE in got and got.count(b'\n') >= 15, got)
  got = s.send(b'\r', MORE)
  check('Enter shows one more line', MORE in got and got.count(b'\n') <= 3, got)
  got = s.send(b'q', b'--> ')
  check('q gives the prompt back, without an error', b'--> ' in got and b'ERROR' not in got, got)
  got = s.send(b'1 2 + .\r', b'--> ')
  check('the line editor still works', b' 3 ' in got or b'3' in got, got)
  got = s.send(b't\r', MORE)
  got = s.send(b'\x1b[A', b'--> ', 2)
  check('Esc stops, and the arrow key sequence is eaten', b'--> ' in got, got)
  got = s.send(b'7 8 * .\r', b'--> ')
  check('nothing left of the arrow after Esc', b'56' in got and b'ERROR' not in got, got)
  got = s.send(b'pager-off words\r', b'--> ', 10)
  check('pager-off: no pause', MORE not in got and b'--> ' in got, got)
  got = s.send(b'pager-on\r', b'--> ')
  s.send(b': u 40 0 do i . cr key drop loop ;\r', b'--> ')
  os.write(s.fd, b'u\r')
  got = b''
  for _ in range(40):
    got += s.send(b'x', b'\xff', 0.15)
  check('a program that reads keys is not stopped', MORE not in got and got.count(b'\n') > 30, got)
  s.close()
  # no terminal: no pause
  p = subprocess.run([exe], input=b'words\nbye\n', capture_output=True, timeout=20)
  check('a pipe is not paged', MORE not in p.stdout and len(p.stdout) > 1000, p.stdout)
  print('ALL OK' if not failures else '%d FAILED' % failures)
  sys.exit(1 if failures else 0)


if __name__ == '__main__':
  main()
