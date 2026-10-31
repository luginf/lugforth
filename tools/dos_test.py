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
Then, if Xvfb, xdotool and ImageMagick are installed, DOSBox in a window of
a virtual X display, typed into with xdotool: the text screen, the keyboard,
the line editor (history, Home) and blocks-editor (the screen written to a
file by screen-char and read back), two beeps on the Sound Blaster (recorded
by DOSBox, Ctrl+F6), breakout in a VESA mode (screenshots), Esc.

  tools/dos_test.py [out/dos]          (what `make -C ports/dos test` runs)
  DOSBOX=dosbox-x tools/dos_test.py    (the same in DOSBox-X)
  DOSBOX=dosbox-x DOS_LFN=1 tools/dos_test.py    (and with long names)
"""

import os
import re
import shutil
import subprocess
import sys
import tempfile
import time

failures = 0
DOSBOX = os.environ.get('DOSBOX', 'dosbox')   # or dosbox-x
# DOS_LFN=1 (with dosbox-x): DOS 7.1 with long names, where DOS no longer cuts
# the names to 8.3 and files.h finds the short names of the staged folder itself.
LFN = '[dos]\nver=7.1\nlfn=true\n' if os.environ.get('DOS_LFN') else ''


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
s" lib/writhdeck.fs" r/o open-file nip .
s" big.txt" w/o create-file throw value fh  5000 s>d fh resize-file .  fh file-size throw d.  fh close-file .
use b.fb  2 block 1024 blank  s" 6 7 * ." 2 block swap cmove update flush
2 load
ms-ticks 300 ms ms-ticks swap - 250 > .
also dos snd-ok . previous
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


DUMP = r"""also dos
create crlf 13 c, 10 c,
: dump-screen ( a n -- )   \ the text screen into the file a n
  w/o create-file throw { fh }
  con-rows 0 do  con-cols 0 do  i j screen-char pad c! pad 1 fh write-file throw  loop
    crlf 2 fh write-file throw  loop
  fh close-file throw ;
"""


def wav_check(path):
  """The two beeps recorded by DOSBox (Ctrl+F6): 440 Hz for 300 ms, then 660 Hz."""
  import wave
  import struct
  sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
  import baremetal_qemu_test as bt
  try:
    w = wave.open(path)
  except (OSError, EOFError, wave.Error) as e:
    check('sound: DOSBox recorded it', str(e), False)
    return
  rate, ch = w.getframerate(), w.getnchannels()
  d = w.readframes(w.getnframes())
  x = struct.unpack('<%dh' % (len(d) // 2), d)[::ch]
  segs = [(a, b) for a, b in bt.wav_segments(x, rate) if b - a > rate * 0.05]
  if not segs:
    check('sound: the Sound Blaster plays', 'silence', False)
    return
  a, b = segs[0]
  m = a + int(rate * 0.3)
  f1, f2 = bt.wav_freq(x, a + 500, m - 500, rate), bt.wav_freq(x, m + 500, b - 500, rate)
  check('sound: beep 440 Hz then 660 Hz on the Sound Blaster (%.0f, %.0f Hz, %.2f s)' % (f1, f2, (b - a) / rate),
        segs, abs(f1 - 440) < 5 and abs(f2 - 660) < 5 and abs((b - a) / rate - 0.6) < 0.02)


def interactive(disk):
  if not shutil.which('Xvfb') or not shutil.which('xdotool') or not shutil.which('import'):
    print('  skip  the text screen, the keyboard, graphics and sound (Xvfb, xdotool, ImageMagick needed)')
    return
  d = tempfile.mkdtemp(prefix='lugdosi')
  # A free display, chosen by Xvfb itself (a fixed one may be held by a lock left behind)
  xvfb = subprocess.Popen(['Xvfb', '-displayfd', '1', '-screen', '0', '1024x768x24'],
                          stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True)
  display = ':' + xvfb.stdout.readline().strip()
  box = None
  try:
    shutil.rmtree(d)
    shutil.copytree(disk, d)
    with open(os.path.join(d, 'DUMP.FS'), 'w') as f:
      f.write(DUMP.replace('\n', '\r\n'))
    with open(os.path.join(d, 'AUTOEXEC.FS'), 'w') as f:   # says when the prompt is there
      f.write('s" ready.txt" w/o create-file throw close-file throw\r\n')
    cap = os.path.join(d, 'cap')
    os.mkdir(cap)
    conf = os.path.join(d, 'dosbox.conf')
    with open(conf, 'w') as f:
      f.write('[dosbox]\ncaptures=%s\n[cpu]\ncycles=max\n[sdl]\nusescancodes=false\n' % cap + LFN)
    env = dict(os.environ, DISPLAY=display, SDL_AUDIODRIVER='dummy')
    time.sleep(1)
    box = subprocess.Popen([DOSBOX, '-conf', conf, '-c', 'mount c "%s"' % d, '-c', 'c:',
                            '-c', 'hdpmi32 -r > nul', '-c', 'lugforth', '-c', 'exit'],
                           env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    for _ in range(120):          # the first start of DOSBox in a new container is slow
      if host(d, 'ready.txt'):
        break
      time.sleep(0.5)
    time.sleep(1)
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

    def hold(name):   # a game reads keys held, not typed
      subprocess.run(['xdotool', 'keydown', name], env=env)
      time.sleep(0.3)
      subprocess.run(['xdotool', 'keyup', name], env=env)

    def shot():       # the whole X screen, raw RGB
      raw = os.path.join(d, 'shot.rgb')
      subprocess.run(['import', '-window', 'root', '-depth', '8', 'rgb:' + raw], env=env)
      return open(raw, 'rb').read() if os.path.exists(raw) else b''

    def count(rgb, color):   # at the precision of a 16-bit mode (5-6-5), which the game may get
      c = bytes.fromhex(color)
      q = (c[0] >> 3, c[1] >> 2, c[2] >> 3)
      return sum(1 for i in range(0, len(rgb) - 2, 3 * 7)
                 if (rgb[i] >> 3, rgb[i + 1] >> 2, rgb[i + 2] >> 3) == q)

    def screen(name):
      f = host(d, name)
      return [r.rstrip() for r in open(f, encoding='latin-1').read().split('\n')] if f else []

    typ('include dump.fs'); key('Return')
    typ('use e.fb  blocks-editor 1'); key('Return'); time.sleep(1)   # first: it clears the screen when it ends
    # (a second before Ctrl+S in the editors: the typing reaches them first)
    typ(': cube dup dup * * ;'); time.sleep(1); key('ctrl+s'); key('ctrl+q')
    typ('1 load 3 cube .'); key('Return')
    typ('7 8 * .'); key('Return')
    key('Up', 'Home'); typ('100 . '); key('Return')          # the line again, with 100 . in front
    typ('s" s1.txt" dump-screen'); key('Return')
    typ('ed hello.fs'); key('Return'); time.sleep(4)          # ed and writhdeck, from LIB
    typ(': hi 42 . ;'); time.sleep(1); key('ctrl+s'); key('ctrl+q')
    typ('writhdeck notes.txt'); key('Return'); time.sleep(5)
    typ('Bonjour tout le monde'); time.sleep(1); key('ctrl+s'); key('ctrl+q'); time.sleep(1)
    # 80 characters on the last row: the screen must not scroll (a terminal's pending wrap)
    typ('page also ansi 0 24 at-xy 80 0 do 35 emit loop s" s3.txt" dump-screen previous'); key('Return')
    key('ctrl+F6')                                           # DOSBox records its sound
    typ('sdl2 440 300 beep 660 300 beep 700 ms previous'); key('Return'); time.sleep(1.5)
    key('ctrl+F6')
    typ('include games/gfx_2048.fs'); key('Return'); time.sleep(4)   # 484x640: 1024x768, 16 bits in DOSBox
    tiles = shot()
    hold('Escape'); time.sleep(2)
    typ('include games/gfx_breakout.fs'); key('Return'); time.sleep(4)   # GAMES/GFX_BREA.FS
    game = shot()
    hold('space'); time.sleep(0.5)
    moving1 = shot(); time.sleep(0.3)
    moving2 = shot()
    hold('Escape'); time.sleep(2)
    typ('2 3 * .'); key('Return')
    typ('s" s2.txt" dump-screen bye'); key('Return')
    for _ in range(20):
      if box.poll() is not None:
        break
      time.sleep(0.5)
    rows = screen('s1.txt')
    check('the text screen: an answer after its line', rows, '> 7 8 * .' in rows and '56  ok' in rows)
    check('the line editor: Up, Home, typing in front', rows, '> 100 . 7 8 * .' in rows and '100 56  ok' in rows)
    check('blocks-editor: typed, saved, loaded', rows, '27  ok' in rows)
    b = host(d, 'e.fb')
    blk = open(b, 'rb').read()[1024:1024 + 20] if b else b''
    check('blocks-editor: the block on the disk', blk, blk == b': cube dup dup * * ;')
    wavs = [os.path.join(cap, f) for f in os.listdir(cap) if f.lower().endswith('.wav')]
    if 'dosbox-x' in DOSBOX:   # its shortcuts differ (F12 + ...): no recording here
      print('  skip  the beeps recorded and measured (DOSBox only)')
    else:
      wav_check(wavs[0] if wavs else os.path.join(cap, 'none.wav'))
    got = {c: count(tiles, c) for c in ('faf8ef', 'bbada0', 'edc22e')}
    check('2048 in a 16-bit VESA mode: background, board, title %r' % got, got,
          got['faf8ef'] > 3000 and got['bbada0'] > 3000 and got['edc22e'] > 300)
    got = {c: count(game, c) for c in ('101830', 'ff0000', '00ffff', 'ffffff')}
    check('breakout in VESA: background, red and cyan bricks, paddle %r' % got, got,
          got['101830'] > 5000 and got['ff0000'] > 300 and got['00ffff'] > 300 and got['ffffff'] > 20)
    check('breakout: the space held launches the ball (the picture moves)', '', moving1 != moving2 and moving1 != game)
    f = host(d, 'hello.fs')
    check('ed: a file written', f, f is not None and open(f).read().strip() == ': hi 42 . ;')
    f = host(d, 'notes.txt')
    check('writhdeck: a text written', f, f is not None and 'Bonjour tout le monde' in open(f).read())
    rows = screen('s3.txt')
    check('a full last row does not scroll the screen', rows[23:25], len(rows) > 24 and rows[24] == '#' * 80)
    rows = screen('s2.txt')
    check('Esc ends the game, the text prompt is back', rows, '6  ok' in rows)
  finally:
    if box and box.poll() is None:
      box.kill()
    xvfb.terminate()   # not kill: Xvfb then removes its lock file
    xvfb.wait()
    shutil.rmtree(d, ignore_errors=True)


def main():
  out_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), '..', 'out', 'dos')
  disk = os.path.join(out_dir, 'disk')
  if not host(disk, 'HDPMI32.EXE') or not host(disk, 'LUGFORTH.EXE'):
    print('dos: no LUGFORTH.EXE and HDPMI32.EXE in %s (make -C ports/dos stage)' % disk)
    sys.exit(1)
  d = tempfile.mkdtemp(prefix='lugdos')
  try:
    shutil.rmtree(d)
    shutil.copytree(disk, d)
    with open(os.path.join(d, 'IN.TXT'), 'w') as f:
      f.write(SCRIPT.replace('\n', '\r\n'))
    with open(os.path.join(d, 'AUTOEXEC.FS'), 'w') as f:
      f.write('.( autoexec ran) cr\r\n')
    with open(os.path.join(d, 'ARGTEST.FS'), 'w') as f:
      f.write('.( the file of the command line ran) cr  bye\r\n')
    env = dict(os.environ, SDL_VIDEODRIVER='dummy', SDL_AUDIODRIVER='dummy')
    conf = os.path.join(d, 'dosbox.conf')
    with open(conf, 'w') as f:
      f.write('[cpu]\ncycles=max\n[sdl]\nusescancodes=false\n' + LFN)
    subprocess.run([DOSBOX, '-conf', conf, '-c', 'mount c "%s"' % d, '-c', 'c:', '-c', 'hdpmi32 -r > nul',
                    '-c', 'lugforth < in.txt > out.txt', '-c', 'lugforth argtest.fs > out2.txt', '-c', 'exit'],
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
    check('ls', ls, 'moved.fs' in ls and 'sub/' in ls and 'lugforth.exe' in ls and 'games/' in ls)
    check('delete-file', r.get(lines[17]), r.get(lines[17]) == '0')
    check('a missing file is an error', r.get(lines[18]), r.get(lines[18]) not in (None, '0'))
    check('a long name opens its 8.3 file (lib/writhdeck.fs: WRITHDEC.FS)', r.get(lines[19]), r.get(lines[19]) == '0')
    check('resize-file grows a file', r.get(lines[20]), r.get(lines[20]) == '0 5000 0')
    check('a block written then loaded', r.get(lines[22]), r.get(lines[22]) == '42')
    check('ms waits (ms-ticks)', r.get(lines[23]), r.get(lines[23]) == '-1')
    check('a Sound Blaster 16 is found (snd-ok)', r.get(lines[24]), r.get(lines[24]) == '-1')
    check('bye ends the program', out[-200:], out.rstrip().endswith('> bye'))
    o2 = host(d, 'OUT2.TXT')
    out2 = open(o2, encoding='latin-1').read() if o2 else ''
    check('lugforth file.fs runs the file', out2[-300:], 'the file of the command line ran' in out2)
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
  interactive(disk)
  print('dos: %s' % ('ALL OK' if failures == 0 else '%d FAILED' % failures))
  sys.exit(1 if failures else 0)


if __name__ == '__main__':
  main()
