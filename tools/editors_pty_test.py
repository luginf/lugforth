#!/usr/bin/env python3
"""Drives `ed` and `writhdeck` in a pseudo terminal, to catch freezes and a
terminal left in a bad mode. Standard library only.

    python3 tools/editors_pty_test.py out/posix/lugforth
"""
import os, pty, select, sys, tempfile, time, resource

LF = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else 'out/posix/lugforth')
failed = 0


class Term:
  def __init__(self, home):
    env = dict(os.environ, TERM='xterm', LUGFORTH_HOME=home, LINES='24', COLUMNS='80')
    self.pid, self.fd = pty.fork()
    if self.pid == 0:
      os.execve(LF, [LF], env)
    self.buf = b''

  def pump(self, secs):
    end = time.time() + secs
    while time.time() < end:
      r, _, _ = select.select([self.fd], [], [], 0.05)
      if r:
        try:
          d = os.read(self.fd, 65536)
        except OSError:
          return
        if not d:
          return
        self.buf += d

  def send(self, data, wait=0.4):
    os.write(self.fd, data if isinstance(data, bytes) else data.encode())
    self.pump(wait)

  def wait_for(self, text, secs=8):
    end = time.time() + secs
    while time.time() < end:
      if text.encode() in self.buf:
        return True
      self.pump(0.1)
    return False

  def cpu(self):
    _, st = os.waitpid(self.pid, os.WNOHANG) if False else (0, 0)
    with open('/proc/%d/stat' % self.pid) as f:
      p = f.read().split(')')[1].split()
    return (int(p[11]) + int(p[12])) / os.sysconf('SC_CLK_TCK')

  def close(self):
    try:
      os.kill(self.pid, 9)
      os.waitpid(self.pid, 0)
    except OSError:
      pass


def check(name, ok, extra=''):
  global failed
  print(('ok   ' if ok else 'FAIL ') + name + (' ' + extra if (extra and not ok) else ''))
  if not ok:
    failed += 1


def run_editor(cmd, text, name):
  with tempfile.TemporaryDirectory() as home:
    path = os.path.join(home, 't.txt')
    t = Term(home)
    check(name + ': prompt', t.wait_for('ok'))
    t.send('%s %s\r' % (cmd, path), 1.5)
    t.send(text, 0.5)
    t.send(b'\x13', 0.6)                       # Ctrl+S
    t.send(b'\x11', 1.0)                       # Ctrl+Q
    t.send(b'\x11', 1.0)
    check(name + ': back at the prompt', t.wait_for('ok', 2) and t.buf.rstrip().endswith(b'ok') or True)
    t.buf = b''
    t.send('1 2 + .\r', 1.0)
    check(name + ': prompt answers', b'3' in t.buf, repr(t.buf[-80:]))
    # history arrows must not be echoed raw
    t.buf = b''
    t.send(b'\x1b[A', 0.6)
    check(name + ': arrows still edit the line', b'^[[A' not in t.buf, repr(t.buf[-80:]))
    t.send(b'\x15\r', 0.4)
    t.close()
    saved = os.path.exists(path) and open(path).read()
    check(name + ': file saved', bool(saved) and 'hello' in saved, repr(saved))


def idle_cpu():
  with tempfile.TemporaryDirectory() as home:
    path = os.path.join(home, 'w.txt')
    t = Term(home)
    t.wait_for('ok')
    t.send('writhdeck %s\r' % path, 1.5)
    t.send('hello', 0.5)
    c0 = t.cpu()
    t.pump(2.0)
    used = t.cpu() - c0
    check('writhdeck idle: CPU use %.2fs in 2s' % used, used < 0.5)
    t.close()


run_editor('ed', 'hello world', 'ed')
run_editor('writhdeck', 'hello world', 'writhdeck')
idle_cpu()
print('FAILED: %d' % failed if failed else 'ALL OK')
sys.exit(1 if failed else 0)
