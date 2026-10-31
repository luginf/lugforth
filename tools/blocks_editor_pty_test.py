#!/usr/bin/env python3
"""Drives `blocks-editor` (common/blocks_editor.fs) in a pseudo terminal:
typing, insert mode, lines cut and pasted, blocks changed, Ctrl+L, undo,
and the prompt working again afterwards. Standard library only.

    python3 tools/blocks_editor_pty_test.py out/posix/lugforth
"""
import os, pty, select, sys, tempfile, time

LF = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else 'out/posix/lugforth')
failed = 0
UP, DOWN, HOME = '\x1b[A', '\x1b[B', '\x1b[H'
PGUP, PGDN = '\x1b[5~', '\x1b[6~'


class Term:
  def __init__(self, home):
    env = dict(os.environ, TERM='xterm', LUGFORTH_HOME=home, LINES='24', COLUMNS='80')
    self.pid, self.fd = pty.fork()
    if self.pid == 0:
      os.chdir(home)
      os.execvpe(LF, [LF], env)
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

  def send(self, data, wait=0.3):
    for part in data if isinstance(data, list) else [data]:
      os.write(self.fd, part.encode())
      self.pump(wait)

  def wait_for(self, text, secs=6):
    end = time.time() + secs
    while time.time() < end:
      if text.encode() in self.buf:
        return True
      self.pump(0.1)
    return False

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


def block_line(path, n, y):
  with open(path, 'rb') as f:
    data = f.read()
  return data[n * 1024 + y * 64: n * 1024 + y * 64 + 63].decode('latin-1').rstrip()


with tempfile.TemporaryDirectory() as home:
  fb = os.path.join(home, 't.fb')
  t = Term(home)
  check('prompt', t.wait_for('ok'))
  t.send('use t.fb  blocks-editor 1\r', 0.8)
  check('opens block 1', t.wait_for(' Block 1 of 2'))
  t.send([': sq dup * ;', DOWN, HOME, '7 sq .', '\x13'])
  check('Ctrl+S says saved', t.wait_for('Saved'))
  check('line 0 written', block_line(fb, 1, 0) == ': sq dup * ;', repr(block_line(fb, 1, 0)))
  check('line 1 written', block_line(fb, 1, 1) == '7 sq .', repr(block_line(fb, 1, 1)))

  # insert mode: a character pushes the line right, backspace takes it back
  t.send([UP, HOME, '\x14', 'X', '\x13'])
  check('insert mode', block_line(fb, 1, 0) == 'X: sq dup * ;', repr(block_line(fb, 1, 0)))
  t.send(['\x7f', '\x14', '\x13'])
  check('backspace', block_line(fb, 1, 0) == ': sq dup * ;', repr(block_line(fb, 1, 0)))

  # cut line 0 (line 1 comes up), paste it back above
  t.send(['\x18', '\x13'])
  check('cut line', block_line(fb, 1, 0) == '7 sq .', repr(block_line(fb, 1, 0)))
  t.send(['\x16', '\x13'])
  check('paste line', block_line(fb, 1, 0) == ': sq dup * ;' and block_line(fb, 1, 1) == '7 sq .')

  # blocks: PgDn, PgUp, Ctrl+G
  t.buf = b''
  t.send(PGDN)
  check('PgDn', t.wait_for(' Block 2 of'))
  t.buf = b''
  t.send(PGUP)
  check('PgUp', t.wait_for(' Block 1 of'))
  t.buf = b''
  t.send(['\x07', '3', '\r'])
  check('Ctrl+G', t.wait_for(' Block 3 of'))
  t.send(['\x07', '1', '\r'])

  # Ctrl+U: the changes since the last save go away
  t.send(['zzz', '\x15', '\x13'])
  check('undo', block_line(fb, 1, 0) == ': sq dup * ;', repr(block_line(fb, 1, 0)))

  # Ctrl+L loads the block, a key comes back
  t.buf = b''
  t.send('\x0c', 0.8)
  check('Ctrl+L runs the block', t.wait_for('49') and t.wait_for('Done'))
  t.buf = b''
  t.send(' ', 0.8)
  check('back in the editor', t.wait_for(' Block 1 of'))

  # Ctrl+Q: the prompt works, with echo, and the word defined by the block
  t.buf = b''
  t.send('\x11', 0.8)
  t.send('5 sq . 1 list\r', 0.8)
  check('prompt after quit', t.wait_for('25') and t.wait_for('5 sq .'))
  check('list shows the block', t.wait_for(': sq dup * ;'))
  t.close()

print('blocks-editor pty test:', 'ALL OK' if not failed else '%d FAILED' % failed)
sys.exit(1 if failed else 0)
