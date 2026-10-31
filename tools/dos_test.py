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

"""Runs ports/dos's LUGFORTH.EXE in DOSBox without a window, its input and
output redirected to files (lugforth < IN.TXT > OUT.TXT), and checks the
answers: the banner, autoexec.fs, numbers, floats, a fault, the file words
(the files are then read on the Linux side), directories, blocks, time.
HDPMI32.EXE (the DPMI host, see the Makefile) must be beside the program.
Then, if Xvfb and xdotool are installed, DOSBox in a window of a virtual X
display, typed into with xdotool: the text screen, the keyboard, the line
editor (history, Home) and blocks-editor; the screen is written to a file
by screen-char and read back.

  tools/dos_test.py [out/dos]          (what `make -C ports/dos test` runs)
"""

import os
import re
import shutil
import subprocess
import sys
import tempfile
import time

failures = 0


def check(name, got, ok):
  global failures
  print('  %s  %s' % ('ok  ' if ok else 'FAIL', name) + ('' if ok else ': %r' % (got,)))
  if not ok:
    failures += 1


def host(d, name):   # DOS writes its names in capitals: the file on the Linux side
  for f in os.listdir(d):
    if f.lower() == name.lower():
      return os.path.join(d, f)
  return None


SCRIPT = r'''1 2 + . cr
: sq dup * ;  7 sq .
3.5e 2e f* f.
-8 @ .
depth .
s" hello.fs" w/o create-file throw value fh
s" : hi 42 . ;" fh write-file throw  fh close-file throw
include hello.fs  hi
s" hello.fs" r/o open-file throw dup file-size throw d. close-file throw
s" hello.fs" r/o open-file throw value fh  pad 20 fh read-file throw .  fh close-file throw  pad 4 type
md sub
s" sub/in.txt" w/o create-file throw close-file .
cd sub
pwd
cd ..
s" hello.fs" s" moved.fs" rename-file .
ls
s" moved.fs" delete-file .
s" moved.fs" r/o open-file nip .
s" big.txt" w/o create-file throw value fh  5000 s>d fh resize-file .  fh file-size throw d.  fh close-file .
use b.fb  2 block 1024 blank  s" 6 7 * ." 2 block swap cmove update flush
2 load
ms-ticks 300 ms ms-ticks swap - 250 > .
bye
'''


def answers(out, script):
  """What each line of the script printed: the text between its echo and the next prompt."""
  out = out.replace('\r', '')
  parts = re.split(r'\n> ', out)
  res = {}
  for line in script.strip().split('\n'):
    for i, p in enumerate(parts):
      if p.startswith(line):
        res[line] = p[len(line):].replace(' ok', '').strip()
        parts[i] = ''
        break
  return parts[0], res


DUMP = r'''also dos
create crlf 13 c, 10 c,
: dump-screen ( -- )
  s" screen.txt" w/o create-file throw { fh }
  con-rows 0 do  con-cols 0 do  i j screen-char pad c! pad 1 fh write-file throw  loop
    crlf 2 fh write-file throw  loop
  fh close-file throw ;
dump-screen bye
'''


def interactive(exe, dpmi):
  if not shutil.which('Xvfb') or not shutil.which('xdotool'):
    print('  skip  the text screen and the keyboard (Xvfb and xdotool needed)')
    return
  d = tempfile.mkdtemp(prefix='lugdosi')
  xvfb = subprocess.Popen(['Xvfb', ':97', '-screen', '0', '1024x768x24'],
                          stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
  box = None
  try:
    shutil.copy(exe, os.path.join(d, 'LUGFORTH.EXE'))
    shutil.copy(dpmi, os.path.join(d, 'HDPMI32.EXE'))
    with open(os.path.join(d, 'DUMP.FS'), 'w') as f:
      f.write(DUMP.replace('\n', '\r\n'))
    conf = os.path.join(d, 'dosbox.conf')
    with open(conf, 'w') as f:
      f.write('[cpu]\ncycles=max\n[sdl]\nusescancodes=false\n')
    env = dict(os.environ, DISPLAY=':97', SDL_AUDIODRIVER='dummy')
    time.sleep(1)
    box = subprocess.Popen(['dosbox', '-conf', conf, '-c', 'mount c "%s"' % d, '-c', 'c:',
                            '-c', 'hdpmi32 -r > nul', '-c', 'lugforth', '-c', 'exit'],
                           env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    time.sleep(6)
    win = subprocess.run(['xdotool', 'search', '--class', 'dosbox'], env=env, capture_output=True,
                         text=True).stdout.split()
    if win:
      subprocess.run(['xdotool', 'windowfocus', win[0]], env=env)

    def typ(text):
      subprocess.run(['xdotool', 'type', '--delay', '80', text], env=env)
      time.sleep(0.5)

    def key(*names):
      subprocess.run(['xdotool', 'key', '--delay', '150'] + list(names), env=env)
      time.sleep(0.8)

    typ('use e.fb  blocks-editor 1'); key('Return'); time.sleep(1)   # first: it clears the screen when it ends
    typ(': cube dup dup * * ;'); key('ctrl+s'); key('ctrl+q')
    typ('1 load 3 cube .'); key('Return')
    typ('7 8 * .'); key('Return')
    key('Up', 'Home'); typ('100 . '); key('Return')          # the line again, with 100 . in front
    typ('include dump.fs'); key('Return')
    for _ in range(20):
      if box.poll() is not None:
        break
      time.sleep(0.5)
    scr = host(d, 'screen.txt')
    text = open(scr, encoding='latin-1').read() if scr else ''
    rows = [r.rstrip() for r in text.split('\n')]
    check('the text screen: an answer after its line', rows, '> 7 8 * .' in rows and '56  ok' in rows)
    check('the line editor: Up, Home, typing in front', rows, '> 100 . 7 8 * .' in rows and '100 56  ok' in rows)
    check('blocks-editor: typed, saved, loaded', rows, '27  ok' in rows)
    b = host(d, 'e.fb')
    blk = open(b, 'rb').read()[1024:1024 + 20] if b else b''
    check('blocks-editor: the block on the disk', blk, blk == b': cube dup dup * * ;')
    if failures:
      print(text)
  finally:
    if box and box.poll() is None:
      box.kill()
    xvfb.kill()
    shutil.rmtree(d, ignore_errors=True)


def main():
  out_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), '..', 'out', 'dos')
  exe, dpmi = os.path.join(out_dir, 'lugforth.exe'), host(out_dir, 'HDPMI32.EXE')
  if not dpmi:
    print('dos: no HDPMI32.EXE in %s (make -C ports/dos hdpmi)' % out_dir)
    sys.exit(1)
  d = tempfile.mkdtemp(prefix='lugdos')
  try:
    shutil.copy(exe, os.path.join(d, 'LUGFORTH.EXE'))
    shutil.copy(dpmi, os.path.join(d, 'HDPMI32.EXE'))
    with open(os.path.join(d, 'IN.TXT'), 'w') as f:
      f.write(SCRIPT.replace('\n', '\r\n'))
    with open(os.path.join(d, 'AUTOEXEC.FS'), 'w') as f:
      f.write('.( autoexec ran) cr\r\n')
    env = dict(os.environ, SDL_VIDEODRIVER='dummy', SDL_AUDIODRIVER='dummy')
    conf = os.path.join(d, 'dosbox.conf')
    with open(conf, 'w') as f:
      f.write('[cpu]\ncycles=max\n[sdl]\nusescancodes=false\n')
    subprocess.run(['dosbox', '-conf', conf, '-c', 'mount c "%s"' % d, '-c', 'c:', '-c', 'hdpmi32 -r > nul',
                    '-c', 'lugforth < in.txt > out.txt', '-c', 'exit'],
                   env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=180)
    o = host(d, 'OUT.TXT')
    out = open(o, encoding='latin-1').read() if o else ''
    banner, r = answers(out, SCRIPT)
    lines = SCRIPT.strip().split('\n')
    check('banner', banner, 'Lugforth (dos)' in banner)
    check('autoexec.fs ran at start', banner, 'autoexec ran' in banner)
    check('arithmetic', r.get(lines[0]), r.get(lines[0]) == '3')
    check('a definition', r.get(lines[1]), r.get(lines[1]) == '49')
    check('floats', r.get(lines[2]), (r.get(lines[2]) or '').startswith('7.0'))
    check('a fault is caught (-8 @)', r.get(lines[3]), 'ERROR' in (r.get(lines[3]) or ''))
    check('the stack is empty after it', r.get(lines[4]), r.get(lines[4]) == '0')
    check('a file written then included', r.get(lines[7]), r.get(lines[7]) == '42')
    check('file-size', r.get(lines[8]), r.get(lines[8]) == '11')
    check('read-file', r.get(lines[9]), r.get(lines[9]) == '11 : hi')
    check('a file in a new directory', r.get(lines[11]), r.get(lines[11]) == '0')
    check('cd and pwd', r.get(lines[13]), (r.get(lines[13]) or '').lower().endswith('/sub'))
    check('rename-file', r.get(lines[15]), r.get(lines[15]) == '0')
    ls = (r.get(lines[16]) or '').split()
    check('ls', ls, 'moved.fs' in ls and 'sub/' in ls and 'lugforth.exe' in ls)
    check('delete-file', r.get(lines[17]), r.get(lines[17]) == '0')
    check('a missing file is an error', r.get(lines[18]), r.get(lines[18]) not in (None, '0'))
    check('resize-file grows a file', r.get(lines[19]), r.get(lines[19]) == '0 5000 0')
    check('a block written then loaded', r.get(lines[21]), r.get(lines[21]) == '42')
    check('ms waits (ms-ticks)', r.get(lines[22]), r.get(lines[22]) == '-1')
    check('bye ends the program', out[-200:], out.rstrip().endswith('> bye'))
    sub = host(d, 'sub')
    check('the directory on the disk', sub, sub is not None and host(sub, 'in.txt') is not None)
    big = host(d, 'big.txt')
    check('the grown file on the disk', big, big is not None and os.path.getsize(big) == 5000)
    b = host(d, 'b.fb')
    blk = open(b, 'rb').read()[2048:2048 + 8] if b else b''
    check('the block file on the disk', blk, blk == b'6 7 * . ')
    if failures:
      print(out)
  finally:
    shutil.rmtree(d, ignore_errors=True)
  interactive(exe, dpmi)
  print('dos: %s' % ('ALL OK' if failures == 0 else '%d FAILED' % failures))
  sys.exit(1 if failures else 0)


if __name__ == '__main__':
  main()
