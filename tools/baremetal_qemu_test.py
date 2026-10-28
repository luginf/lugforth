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
and that `bye` makes QEMU exit. On raspi3b it also loads
examples/games/sdl2_breakout.fs over the serial line, plays it, and checks the
screen's pixels (QEMU monitor `screendump`); the same on virt with
-device ramfb. Each board also gets a FAT disk (made with dosfstools and
mtools: FAT16 and FAT12 on virt's virtio-blk, MBR + FAT32 on raspi3b's SD
card), used through the file words and checked afterwards with mtools and
fsck.fat.

  tools/baremetal_qemu_test.py [out/rpi]        (what `make -C ports/rpi test` runs)
"""

import json
import os
import random
import re
import socket
import subprocess
import sys
import threading
import time

QEMU = os.environ.get('QEMU', 'qemu-system-aarch64')
PROMPT = '> '


class Board:
  def __init__(self, args, tmp):
    self.tmp = tmp
    self.sock = os.path.join(tmp, 'monitor.sock')
    self.qmp_sock = os.path.join(tmp, 'qmp.sock')
    self.qmp_file = None
    self.proc = subprocess.Popen(
        [QEMU] + args + ['-display', 'none', '-serial', 'stdio',
                         '-monitor', 'unix:%s,server,nowait' % self.sock,
                         '-qmp', 'unix:%s,server,nowait' % self.qmp_sock],
        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    self.monitor = None
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
    echo = line.encode().decode('latin-1')   # output is read as bytes
    if text.startswith(echo):
      text = text[len(echo):]
    return text.strip()

  def qmp(self, events):
    """Input events (QMP input-send-event): keys, buttons, pointer."""
    if not self.qmp_file:
      q = socket.socket(socket.AF_UNIX)
      q.connect(self.qmp_sock)
      self.qmp_file = q.makefile('rw')
      self.qmp_file.readline()
      self.qmp_file.write(json.dumps({'execute': 'qmp_capabilities'}) + '\n')
      self.qmp_file.flush()
      self.qmp_file.readline()
    self.qmp_file.write(json.dumps({'execute': 'input-send-event',
                                    'arguments': {'events': events}}) + '\n')
    self.qmp_file.flush()
    return 'return' in self.qmp_file.readline()

  def send(self, data):
    self.proc.stdin.write(data)
    self.proc.stdin.flush()

  def screen(self):
    """The screen as (width, height, rgb bytes), from a QEMU screendump."""
    if not self.monitor:
      self.monitor = socket.socket(socket.AF_UNIX)
      self.monitor.connect(self.sock)
      self.monitor.settimeout(5)
      time.sleep(0.2)
      self.monitor.recv(65536)
    path = os.path.join(self.tmp, 'screen.ppm')
    if os.path.exists(path):
      os.remove(path)
    self.monitor.send(('screendump %s\n' % path).encode())
    end = time.time() + 10
    while time.time() < end:
      time.sleep(0.2)
      try:
        data = open(path, 'rb').read()
        parts = data.split(b'\n', 3)
        w, h = map(int, parts[1].split())
        if len(parts[3]) >= w * h * 3:
          return w, h, parts[3]
      except (OSError, IndexError, ValueError):
        pass
    raise RuntimeError('no screendump')

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


def pixel(screen, x, y):
  w, h, rgb = screen
  i = (y * w + x) * 3
  return '%02x%02x%02x' % (rgb[i], rgb[i + 1], rgb[i + 2])


def bresenham(x1, y1, x2, y2):
  """The plain loop of ports/web/sdl2.fs, every point."""
  dx, sx = abs(x2 - x1), 1 if x1 < x2 else -1
  dy, sy = -abs(y2 - y1), 1 if y1 < y2 else -1
  err = dx + dy
  while True:
    yield x1, y1
    if x1 == x2 and y1 == y2:
      return
    e2 = 2 * err
    if e2 >= dy:
      err += dy
      x1 += sx
    if e2 <= dx:
      err += dx
      y1 += sy


def test_lines(b):
  """Lines on and off a 160x120 screen, zoom 1: the clipped C code must
  light exactly the visible pixels of the plain loop. Then lines 10^12
  long, which the plain loop could never finish."""
  w, h = 160, 120
  rnd = random.Random(7)
  lines = [(rnd.randint(-300, 460), rnd.randint(-300, 420),
            rnd.randint(-300, 460), rnd.randint(-300, 420)) for _ in range(60)]
  lines += [(-50, 10, 300, 10), (20, -40, 20, 500), (159, 119, 400, 119),
            (5, 5, 5, 5), (-10, -10, 200, 140), (170, 60, 150, 61),
            # visible at their last point only, from each side
            (100, 200, 90, 119), (300, 50, 159, 60), (-90, 30, 0, 40), (70, -80, 60, 0)]
  b.run('sdl2 1 to zoom %d %d screen black color cls white color' % (w, h))
  for i in range(0, len(lines), 5):
    b.run(' '.join('%d %d %d %d line' % l for l in lines[i:i + 5]))
  b.run('flip')
  want = set()
  for l in lines:
    want |= {(x, y) for x, y in bresenham(*l) if 0 <= x < w and 0 <= y < h}
  s = b.screen()
  got = {(x, y) for y in range(h) for x in range(w) if pixel(s, x, y) == 'ffffff'}
  check('clipped lines: %d pixels as the plain loop' % len(want),
        sorted(want ^ got)[:5], got == want)
  r = b.run(': far ticks 0 0 1000000000000 300000000000 line '
            '-5 -7 70 3000000000000 line ticks swap - . ; far')
  check('lines 10^12 long (%s ms)' % r.split()[0], r, int(r.split()[0]) < 50)
  b.run('close-screen previous')


def font8x8():
  """The glyphs of ports/baremetal/font8x8.h, by code point."""
  root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
  rows = re.findall(r'\{(0x[^}]*)\},\s*// U\+([0-9A-F]+)',
                    open(os.path.join(root, 'ports', 'baremetal', 'font8x8.h')).read())
  return {int(cp, 16): [int(v, 16) for v in vals.split(',')] for vals, cp in rows}


def test_text(b):
  """text at scale 1 and 2, accents included, against the font file."""
  w, h = 160, 60
  font = font8x8()
  items = [(2, 3, 1, 'Forth 42 !?'), (3, 20, 2, 'Été à'), (150, 50, 1, 'xyz')]
  b.run('sdl2 1 to zoom %d %d screen black color cls white color' % (w, h))
  for x, y, scale, t in items:
    r = b.run('s" x" %d load-font font %d %d s" %s" text' % (8 * scale, x, y, t))
    check('text %r' % t, r, 'ERROR' not in r)
  b.run('flip')
  want = set()
  for x, y, scale, t in items:
    for k, ch in enumerate(t):
      g = font.get(ord(ch), font[ord('?')])
      for j in range(8):
        for i in range(8):
          if g[j] >> i & 1:
            for dy in range(scale):
              for dx in range(scale):
                px, py = x + 8 * scale * k + i * scale + dx, y + j * scale + dy
                if 0 <= px < w and 0 <= py < h:
                  want.add((px, py))
  s = b.screen()
  got = {(x, y) for y in range(h) for x in range(w) if pixel(s, x, y) == 'ffffff'}
  check('text: %d pixels as font8x8.h' % len(want), sorted(want ^ got)[:5], got == want)
  r = b.run('1 font s" Été" text-size . . 3 font s" ab" text-size . . 1 font')
  check('text-size', r, r.split()[:4] == ['8', '24', '24', '48'])
  b.run('close-screen previous')


def test_images(b):
  """Built-in images: draw, draw-size, draw-part, tint and alpha, every pixel
  against the same arithmetic on the PNG (tools/baremetal_assets.py decodes it)."""
  root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
  sys.path.insert(0, os.path.join(root, 'tools'))
  import baremetal_assets
  iw, ih, px = baremetal_assets.decode_png(open(os.path.join(root, 'examples', 'games', 'sdl2_tiles.png'), 'rb').read())
  w, h, bg = 120, 60, 0x101830
  fb = [[bg] * w for _ in range(h)]

  def draw(sx, sy, sw, sh, x, y, dw, dh, tint, alpha):
    tr, tg, tb = tint >> 16 & 255, tint >> 8 & 255, tint & 255
    for j in range(max(0, y), min(h, y + dh)):
      v = sy + (j - y) * sh // dh
      for i in range(max(0, x), min(w, x + dw)):
        u = sx + (i - x) * sw // dw
        c = px[v * iw + u]
        a = (c >> 24) * alpha // 255
        if not a:
          continue
        r, g, bb = (c >> 16 & 255) * tr // 255, (c >> 8 & 255) * tg // 255, (c & 255) * tb // 255
        if a < 255:
          d = fb[j][i]
          r = (r * a + (d >> 16 & 255) * (255 - a)) // 255
          g = (g * a + (d >> 8 & 255) * (255 - a)) // 255
          bb = (bb * a + (d & 255) * (255 - a)) // 255
        fb[j][i] = r << 16 | g << 8 | bb

  draw(0, 0, iw, ih, 2, 2, iw, ih, 0xffffff, 255)            # draw
  draw(0, 0, iw, ih, -10, 14, 2 * iw, 3 * ih, 0xffffff, 255)   # draw-size, clipped
  draw(16, 0, 8, 8, 100, 40, 24, 24, 0xff8040, 255)          # draw-part, tinted
  draw(8, 0, 8, 8, 10, 40, 16, 16, 0xffffff, 128)            # half transparent
  b.run('sdl2 1 to zoom %d %d screen $101830 color cls' % (w, h))
  r = b.run('s" examples/games/sdl2_tiles.png" load-image value img  img image-size . .')
  check('image-size', r, r.split()[:2] == [str(ih), str(iw)])
  b.run('img 2 2 draw  img -10 14 %d %d draw-size' % (2 * iw, 3 * ih))
  b.run('img $ff8040 image-tint  img 16 0 8 8 100 40 24 24 draw-part  img $ffffff image-tint')
  b.run('img 128 image-alpha  img 8 0 8 8 10 40 16 16 draw-part  img 255 image-alpha  flip')
  s = b.screen()
  bad = [(x, y) for y in range(h) for x in range(w) if pixel(s, x, y) != '%06x' % fb[y][x]]
  check('images: %d pixels as the PNG' % (w * h), bad[:5], not bad)
  r = b.run('s" nothing.png" load-image')
  check('unknown image throws', r, 'ERROR' in r)
  b.run('img free-image close-screen previous')


def qkey(name, down):
  return {'type': 'key', 'data': {'down': down, 'key': {'type': 'qcode', 'data': name}}}


def qbtn(down):
  return {'type': 'btn', 'data': {'down': down, 'button': 'left'}}


def test_console(b):
  """The Forth console is drawn on the screen: 1280x960, the banner as light
  pixels at the top, an inverse block cursor; a game's screen replaces it
  and close-screen brings it back."""
  def lit(scr, y0, y1):
    return sum(1 for y in range(y0, y1) for x in range(0, 1280, 4) if pixel(scr, x, y) != '000000')
  scr = b.screen()
  check('console: 1280x960', scr[:2], scr[:2] == (1280, 960))
  check('console: banner drawn', lit(scr, 0, 300), lit(scr, 0, 300) > 200)
  b.run(': t40 40 0 do i . loop ; t40')
  b.run('sdl2 320 240 screen $ff0000 color cls flip')
  scr = b.screen()
  check('console: the sdl2 screen replaces it', pixel(scr, 5, 5), pixel(scr, 5, 5) == 'ff0000')
  b.run('close-screen')
  scr = b.screen()
  check('console: back after close-screen', scr[:2], scr[:2] == (1280, 960))
  check('console: cleared, prompt drawn', lit(scr, 0, 32), 0 < lit(scr, 0, 32) < 200)
  b.run('1 console-zoom')
  scr = b.screen()
  check('console: 1 console-zoom is 640x480', scr[:2], scr[:2] == (640, 480))
  check('console: and drawn', lit(scr, 0, 16), lit(scr, 0, 16) > 50)
  b.run('2 console-zoom 1 2 + .')


def varvara_quit(b):
  b.qmp([qkey('f12', True)])
  time.sleep(0.3)
  b.qmp([qkey('f12', False)])
  b.wait_for(PROMPT, b.pos, 15)
  b.pos = len(b.out)
  r = b.run('1 2 + .')
  check('varvara: F12 gives the prompt back, stack clean', r, r.split()[0] == '3')


def test_varvara(b):
  """A Uxn ROM on the board (examples/system/varvara/varvara_rpi.fs): boot.rom draws
  a yellow square and a text, F12 ends it and the console comes back."""
  r = b.run('include system/varvara/varvara_rpi.fs')
  check('varvara: loads', r, 'ERROR' not in r and 'rror' not in r)
  b.send(b's" system/varvara/boot.rom" varvara\r')
  time.sleep(6)
  scr = b.screen()
  check('varvara: screen 672x256 (336x128, zoom 2)', scr[:2], scr[:2] == (672, 256))
  check('varvara: background', pixel(scr, 5, 5), pixel(scr, 5, 5) == '110033')
  sq = pixel(scr, 104, 168)
  check('varvara: the yellow square', sq, sq == 'ffcc44')
  varvara_quit(b)
  # The File, Datetime and Console devices: devtest.rom writes, reads, lists,
  # stats and deletes x.txt, tries to leave the sandbox, prints the date
  # (2027-01-15 09:00:00 UTC+1, a Friday, the 14th day), then echoes its
  # arguments and what comes on the serial line.
  b.run('also rpi 1800000000 unix-time! previous 60 tz!')
  b.run('s" argone" varvara-arg s" argtwo" varvara-arg')
  b.send(b's" system/varvara/devtest.rom" varvara\r')
  time.sleep(5)
  b.send(b'hi\r')
  time.sleep(2)
  out = b.out[b.pos:].replace('\r', '')
  check('varvara file: read back', out, out.startswith('hello') or '\nhello\n' in out)
  check('varvara file: listed and stat', out, out.count('0005 x.txt') == 2)
  check('varvara file: sandbox', out, 'blocked attempt to access ../x.txt outside of sandbox' in out)
  check('varvara datetime', out, '07eb000f09000005000e' in out)
  check('varvara console: arguments', out, '[2a][2r][2g][2o][2n][2e][3\n][2a][2r][2g][2t][2w][2o][4\n]' in out)
  check('varvara console: serial input', out, '[1h][1i]' in out)
  varvara_quit(b)
  # xo5.rom (a 5x5 tic-tac-toe, https://github.com/luginf/uxneries): 128x128, drawn
  b.send(b's" system/varvara/xo5.rom" varvara\r')
  time.sleep(6)
  scr = b.screen()
  colors = {pixel(scr, x, y) for x in range(0, 256, 4) for y in range(0, 256, 4)}
  check('xo5.rom: screen 256x256 (128x128, zoom 2)', scr[:2], scr[:2] == (256, 256))
  check('xo5.rom: drawn (%d colors)' % len(colors), sorted(colors), len(colors) >= 2)
  b.qmp([{'type': 'abs', 'data': {'axis': 'x', 'value': 6000}}, {'type': 'abs', 'data': {'axis': 'y', 'value': 6000}},
         qbtn(True)])
  time.sleep(0.3)
  b.qmp([qbtn(False)])
  time.sleep(2)
  scr2 = b.screen()
  check('xo5.rom: a click changes the board', '', scr2 != scr)
  varvara_quit(b)
  r = b.run('s" x.txt" r/o open-file nip .')
  check('varvara file: deleted', r, r.split()[0] != '0')


def qtap(b, *names):
  """Keys held together (a modifier first), then released."""
  b.qmp([qkey(n, True) for n in names])
  time.sleep(0.2)
  b.qmp([qkey(n, False) for n in reversed(names)])
  time.sleep(1.5)


def test_editors(b, browser_dir='/lib'):
  """ed and writhdeck on the console (editors/editor.fs, writhdeck.fs, run
  from /lib): text typed on the serial line, Home, End and Delete from the USB
  keyboard, saved on the FAT, read back with cat; writhdeck's file browser
  enters a directory."""
  b.send(b'ed edtest.txt\r')
  time.sleep(10)
  scr = b.screen()
  lit = sum(1 for x in range(0, 1280, 8) for y in range(930, 960, 4) if pixel(scr, x, y) != '000000')
  check('ed: status bar drawn (%d lit pixels)' % lit, '', lit > 300)
  b.send(b'hello rpi')
  time.sleep(4)
  qtap(b, 'home')
  qtap(b, 'y')
  qtap(b, 'end')
  qtap(b, 'x')
  qtap(b, 'home')
  qtap(b, 'delete')
  b.send(b'\x13')      # Ctrl+S
  time.sleep(3)
  b.send(b'\x11')      # Ctrl+Q
  b.wait_for(PROMPT, b.pos, 20)
  b.pos = len(b.out)
  r = b.run('cat edtest.txt')
  check('ed: text, keys and save on the FAT', r, r.startswith('hello rpix'))
  b.send(b'writhdeck wd.txt\r')
  time.sleep(10)
  b.send('Bonjour tout le monde'.encode())
  time.sleep(6)
  b.send(b'\x13')
  time.sleep(3)
  b.send(b'\x11')
  b.wait_for(PROMPT, b.pos, 20)
  b.pos = len(b.out)
  r = b.run('cat wd.txt')
  check('writhdeck: text saved on the FAT', r, 'Bonjour tout le monde' in r)
  b.send(b'writhdeck\r')   # the file browser: the second entry is lib/
  time.sleep(10)
  qtap(b, 'down')
  qtap(b, 'ret')
  b.send(b'q')
  b.wait_for(PROMPT, b.pos, 20)
  b.pos = len(b.out)
  r = b.run('pwd')
  check('writhdeck: the browser went into %s/' % browser_dir[1:], r, r.split()[0] == browser_dir)
  b.run('cd /')


def test_input(b, what):
  """A keyboard and a pointer (virtio on virt, USB on raspi3b), driven by
  QMP: keys held and released, the button, absolute then relative motion;
  then typing at the prompt."""
  b.run('sdl2 1 to zoom 320 240 screen')
  b.run(': st [char] a pressed? . key-left pressed? . LEFT-BUTTON pressed? . '
        'mouse-x . mouse-y . last-key . ;')
  b.run(': watch ( ms -- ) ticks + begin events dup ticks - 0< until drop st ;')

  def during_watch(events):
    b.send(b'600 watch\r')
    time.sleep(0.2)
    ok = b.qmp(events)
    b.wait_for(PROMPT, b.pos, 10)
    i = b.out.rfind(PROMPT)
    text = b.out[b.pos:i].replace('\r', '')
    b.pos = i + len(PROMPT)
    return ok, text.replace('600 watch', '').split()[:6]

  abs_xy = [{'type': 'abs', 'data': {'axis': 'x', 'value': 16384}},
            {'type': 'abs', 'data': {'axis': 'y', 'value': 8192}}]
  ok, r = during_watch([qkey('a', True), qkey('left', True), qbtn(True)] + abs_xy)
  check('%s: keys, button, pointer down' % what, r, ok and r == ['-1', '-1', '-1', '160', '60', '208'])
  ok, r = during_watch([qkey('a', False)])
  check('%s: a released' % what, r, ok and r == ['0', '-1', '-1', '160', '60', '208'])
  rel = [{'type': 'rel', 'data': {'axis': 'x', 'value': 10}},
         {'type': 'rel', 'data': {'axis': 'y', 'value': 5}}]
  ok, r = during_watch([qkey('left', False), qbtn(False)] + rel)
  check('%s: all released, mouse moved by 10 5' % what, r, ok and r == ['0', '0', '0', '170', '65', '208'])
  # Back to the middle, where breakout's paddle starts (it follows the mouse).
  during_watch(abs_xy)
  b.run('close-screen previous')
  # QEMU's USB keyboard queues 16 events at most: type in two goes.
  keys = []
  for k in ['2', 'spc', '3', 'spc']:
    keys += [qkey(k, True), qkey(k, False)]
  b.qmp(keys)
  time.sleep(0.3)
  keys = [qkey('shift', True), qkey('8', True), qkey('8', False), qkey('shift', False)]
  for k in ['spc', 'dot', 'ret']:
    keys += [qkey(k, True), qkey(k, False)]
  b.qmp(keys)
  b.wait_for(PROMPT, b.pos, 10)
  i = b.out.rfind(PROMPT)
  r = b.out[b.pos:i].replace('\r', '')
  b.pos = i + len(PROMPT)
  check('%s: typing at the prompt' % what, r, '2 3 * .' in r and '6' in r.split())


def paste(b, text, wait_prompt=True):
  """Send a whole text with the paste word: evaluated at once, as include."""
  b.send(b'paste\r' + text + b'\x04')
  if not wait_prompt:
    return ''
  b.wait_for(PROMPT, b.pos, 60)
  i = b.out.rfind(PROMPT)
  r = b.out[b.pos:i].replace('\r', '')
  b.pos = i + len(PROMPT)
  return r


def test_roguelike(b):
  """examples/games/sdl2_roguelike.fs (images, tints, mouse, ABC tunes in r~
  strings over several lines) sent with paste: it draws and plays."""
  root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
  t0 = time.time()
  start = len(b.out)
  paste(b, open(os.path.join(root, 'examples', 'games', 'sdl2_roguelike.fs'), 'rb').read(), False)
  b.wait_for('esc: quit', b.pos, 60)
  check('roguelike received by paste (%.1f s)' % (time.time() - t0), '', True)
  time.sleep(1.5)
  s = b.screen()
  lit = sum(1 for y in range(0, s[1], 3) for x in range(0, s[0], 3) if pixel(s, x, y) != '000000')
  red = sum(1 for y in range(s[1] - 48, s[1], 3) for x in range(0, 300, 3) if pixel(s, x, y)[:2] > 'c0'
            and pixel(s, x, y)[2:4] < '60')
  check('roguelike drawn (%d lit, %d red heart pixels)' % (lit, red), '', lit > 1000 and red > 20)
  b.send(b'\x1b')
  b.wait_for(PROMPT, b.pos, 10)
  b.pos = len(b.out)
  r = b.run('tune-playing? . tune-stop tune-playing? .')
  bad = 'ERROR' in b.out[start:] or 'no music' in b.out[start:]
  check('roguelike: no error, its tune plays', r, r.split()[:2] == ['-1', '0'] and not bad)
  b.run('previous')


def test_sound_play(b):
  """Beeps, a hit and a tune, recorded by QEMU (-audiodev wav); check_sound
  reads the first four sounds of the file."""
  b.run('sdl2 440 300 beep 0 200 beep 660 300 beep 1500 ms')
  b.run('1 to wave 300 400 drum-hit 700 ms 0 to wave')
  paste(b, b'sdl2-abc\nr~ X:1\nL:1/4\nQ:1/4=120\nK:C\nC E G c\n~ abc-play 2500 ms\n')
  b.run('s" o5 a1, o4 a8 r8" play 2600 ms')                      # two voices: 2 s, not 2.5 s
  b.run('400 0 100 0 adsr 440 1000 beep 1300 ms adsr-off')       # attack over 400 ms


def test_varvara_audio(b):
  """tone.rom, on virt (virtio-sound): one second at 261.6 Hz, found in the
  recording by check_sound."""
  b.run('include system/varvara/varvara_rpi.fs')
  b.send(b's" system/varvara/tone.rom" varvara\r')
  time.sleep(3)
  varvara_quit(b)


def wav_segments(x, rate):
  """(start, end) sample ranges that are not silent."""
  segs, on, quiet, gap = [], None, 0, int(rate * 0.004)
  for i, v in enumerate(x):
    if abs(v) > 60:
      if on is None:
        on = i
      quiet = 0
    elif on is not None:
      quiet += 1
      if quiet > gap:
        segs.append((on, i - quiet))
        on = None
  if on is not None:
    segs.append((on, len(x)))
  return segs


def wav_freq(x, a, b, rate):
  z = sum(1 for k in range(a + 1, b) if (x[k - 1] < 0) != (x[k] < 0))
  return z / 2 / ((b - a) / rate)


def check_sound(path):
  import wave
  import struct
  w = wave.open(path)
  rate, ch = w.getframerate(), w.getnchannels()
  d = w.readframes(w.getnframes())
  x = struct.unpack('<%dh' % (len(d) // 2), d)[::ch]
  segs = [(a, b) for a, b in wav_segments(x, rate) if b - a > rate * 0.05]
  check('sound: %d sounds recorded' % len(segs), [(round(a / rate, 2), round((b - a) / rate, 2)) for a, b in segs],
        len(segs) >= 4)
  if len(segs) < 9:
    return
  (a1, b1), (a2, b2), (a3, b3) = segs[:3]
  f1, f2 = wav_freq(x, a1, b1, rate), wav_freq(x, a2, b2, rate)
  check('beep 440 Hz 300 ms (%.0f Hz, %.3f s)' % (f1, (b1 - a1) / rate), '',
        abs(f1 - 440) < 10 and abs((b1 - a1) / rate - 0.3) < 0.01)
  check('then 200 ms of silence (%.3f s)' % ((a2 - b1) / rate), '', abs((a2 - b1) / rate - 0.2) < 0.01)
  check('beep 660 Hz 300 ms (%.0f Hz, %.3f s)' % (f2, (b2 - a2) / rate), '',
        abs(f2 - 660) < 10 and abs((b2 - a2) / rate - 0.3) < 0.01)
  n = (b3 - a3) // 10
  head, tail = max(abs(v) for v in x[a3:a3 + n]), max(abs(v) for v in x[b3 - 2 * n:b3 - n])
  check('drum-hit fades out (%d then %d)' % (head, tail), '', head > 2500 and tail < head / 3)
  # The tune: C E G c, a quarter note each at 120 per minute.
  t0 = segs[3][0]
  want = [261.6, 329.6, 392.0, 523.3]
  got = [wav_freq(x, t0 + int(rate * (0.5 * k + 0.05)), t0 + int(rate * (0.5 * k + 0.4)), rate) for k in range(4)]
  check('tune C E G c (%s Hz)' % ' '.join('%.0f' % f for f in got), '',
        all(abs(g - f) / f < 0.03 for g, f in zip(got, want)))
  # Two voices start together (one segment of 2 s, a second voice of 0.5 s inside it) and the ADSR attack rises.
  (a4, b4), (a5, b5) = segs[7:9]   # the tune is four segments (3 to 6)
  check('play with two voices lasts as long as the longest (%.2f s)' % ((b4 - a4) / rate),
        [(round(a / rate, 2), round((b - a) / rate, 2)) for a, b in segs[:9]],
        abs((b4 - a4) / rate - 2.0) < 0.05)
  q = int(rate * 0.05)
  e_head = max(abs(v) for v in x[a5:a5 + q]); e_end = max(abs(v) for v in x[a5 + int(rate * 0.6):a5 + int(rate * 0.65)])
  check('adsr: the attack rises (%d then %d)' % (e_head, e_end), '', e_head < e_end / 3 and e_end > 1000)
  # varvara's tone.rom: a second of 261.6 Hz, among the later sounds. A loaded
  # machine chops it (the output runs dry between two frames of the ROM), so
  # what is checked is the total length of the pieces at that pitch.
  pieces = [(a, b) for a, b in segs[9:] if abs(wav_freq(x, a, b, rate) - 261.6) < 8]
  total = sum(b - a for a, b in pieces) / rate
  check('varvara audio: tone.rom at 262 Hz (%d pieces, %.2f s)' % (len(pieces), total),
        [(round(a / rate, 2), round((b - a) / rate, 2)) for a, b in segs[9:]], 0.6 < total < 3)


# ---- files: a FAT disk made here with dosfstools and mtools, used by the
# board, then read back and checked (fsck.fat) once QEMU has stopped ----
DISK_FILES = {
    'hello.fs': b': hi ." Bonjour du disque" cr ;\nhi\nneeds lib/util.fs\n',
    'lib/util.fs': b': twice 2 * ;\n." util loaded" cr\n',
    'un-nom-assez-long.fs': b'." nom long ok" cr\n',
    'autoexec.fs': b'." autoexec ran" cr\n',
}


def make_disk(path, fat, mbr):
  """A disk image with DISK_FILES (tools/baremetal_mkdisk.py), its mtools spec."""
  sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
  import baremetal_mkdisk
  spec = baremetal_mkdisk.make_disk(path, fat, mbr)
  tmp = path + '.d'
  for name, data in DISK_FILES.items():
    os.makedirs(os.path.dirname(os.path.join(tmp, name)), exist_ok=True)
    with open(os.path.join(tmp, name), 'wb') as f:
      f.write(data)
    baremetal_mkdisk.put(spec, os.path.join(tmp, name), name)
  # The Varvara port (examples/system/varvara) is loaded from the disk by test_varvara.
  vdir = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'examples', 'system', 'varvara')
  for name in ('uxn.fs', 'screen.fs', 'audio.fs', 'file.fs', 'varvara_rpi.fs', 'boot.rom', 'devtest.rom', 'tone.rom', 'xo5.rom'):
    baremetal_mkdisk.put(spec, os.path.join(vdir, name), 'system/varvara/' + name)
  # ed and writhdeck (editors/) are loaded from /lib by test_editors.
  pdir = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "ports", "posix")
  edir = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "editors")
  for name in ('editor.fs', 'writhdeck.fs'):
    baremetal_mkdisk.put(spec, os.path.join(edir, name), 'lib/' + name)
  baremetal_mkdisk.put(spec, os.path.join(pdir, 'eval_copy.fs'), 'lib/eval_copy.fs')
  return spec


def test_files(b, what, long_create=False, mkdir=False):   # mkdir: the board's own FAT driver, with mixed case names too
    """The file words on the disk: ls, include and needs (a subdirectory, a
    long name), a file over several clusters written and read back, rename
    into a directory, delete, resize, and the errors."""
    r = b.run('rpi disk? . forth')
    check('%s: disk mounted' % what, r, r.split()[0] == '-1')
    r = b.run('ls')
    names = r.lower().split()
    check('%s: ls' % what, r, all(n in names for n in ['hello.fs', 'un-nom-assez-long.fs', 'lib/']))
    r = b.run('include hello.fs  21 twice .')
    check('%s: include, needs lib/util.fs' % what, r,
          'Bonjour du disque' in r and 'util loaded' in r and '42' in r.split())
    r = b.run('include un-nom-assez-long.fs')
    check('%s: a long name' % what, r, 'nom long ok' in r)
    b.run('0 value buf  0 value fh  : pattern ( -- ) 20000 0 do i 7 * 255 and buf i + c! loop ;')
    b.run(': big  20000 allocate throw to buf  pattern  s" big.bin" w/o create-file throw to fh '
          '20 0 do buf i 1000 * + 1000 fh write-file throw loop  fh close-file throw ;')
    r = b.run('big')
    check('%s: write 20000 bytes' % what, r, 'ERROR' not in r)
    b.run(': same? ( a -- f ) -1 swap 20000 0 do dup i + c@ i 7 * 255 and <> if swap drop 0 swap then loop drop ;')
    b.run(': back  s" big.bin" r/o open-file throw to fh  fh file-size throw d>s . '
          '20000 allocate throw dup 20000 fh read-file throw .  fh close-file throw  dup same? .  free throw ;')
    r = b.run('back')
    check('%s: read back, size and bytes' % what, r, r.split()[:3] == ['20000', '20000', '-1'])
    r = b.run('s" big.bin" s" lib/moved.bin" rename-file . s" big.bin" r/o open-file . drop')
    check('%s: rename into lib, old name gone' % what, r, r.split()[:2] == ['0', '2'])
    b.run(': note  s" notes.txt" w/o create-file throw to fh  s" ecrit par lugforth" fh write-file throw '
          ' 5 0 fh resize-file throw  fh file-size throw d>s .  fh close-file throw ;')
    r = b.run('note')
    check('%s: resize-file' % what, r, r.split()[0] == '5')
    r = b.run('s" tmp.txt" w/o create-file throw close-file . s" tmp.txt" delete-file . '
              's" tmp.txt" r/o open-file . drop  s" nothing/here.fs" r/o open-file . drop')
    check('%s: delete, missing file and directory' % what, r, r.split()[:4] == ['0', '0', '2', '2'])
    if long_create:   # the UEFI firmware's FAT driver makes long names
        r = b.run('s" much-too-long-name.text" w/o create-file . close-file .')
        check('%s: a long new name is made' % what, r, r.split()[:2] == ['0', '0'])
    else:
        r = b.run('s" much-too-long-name.text" w/o create-file . drop')
        check('%s: a long new name is refused' % what, r, r.split()[0] == '36')
    if mkdir:
        r = b.run('s" much-too-long-name.text" r/o open-file . close-file .  ls')
        check('%s: the long name is found and listed' % what, r,
              r.split()[:2] == ['0', '0'] and 'much-too-long-name.text' in r)
        r = b.run('s" Mixte.Txt" w/o create-file . close-file .  s" Été-à-Noël.txt" w/o create-file . close-file .  ls')
        check('%s: mixed case and accents in a new name' % what, r,
              r.split()[:4] == ['0', '0', '0', '0'] and 'Mixte.Txt' in r.split() and
              'Été-à-Noël.txt'.encode().decode('latin-1') in r)
    if mkdir:
        r = b.run('also rpi s" sub" mkdir . s" sub" mkdir . s" sub/in.txt" w/o create-file . close-file . '
                  's" sub" rmdir . s" sub/in.txt" delete-file . s" sub" rmdir . s" sub" open-dir . drop previous')
        check('%s: mkdir, exists, file in it, rmdir refused then done' % what, r,
              [t for t in r.split() if t.lstrip('-').isdigit()][:8] == ['0', '17', '0', '0', '39', '0', '0', '2'])
        r = b.run('also rpi s" nodir/x" mkdir . previous')
        check('%s: mkdir without its parent' % what, r, r.split()[0] == '2')
        r = b.run('also rpi s" keepdir" mkdir . s" keepdir/a long file name.txt" w/o create-file . close-file . '
                  's" keepdir/deeper" mkdir . previous')
        check('%s: a long name in a new directory' % what, r, r.split()[:3] == ['0', '0', '0'])


def check_disk(path, spec, what, long_create=False, mkdir=False):
    """After QEMU: what the board wrote, as Linux sees it, and fsck.fat."""
    def mtype(name):
        p = subprocess.run(['mtype', '-i', spec, '::/' + name], capture_output=True)
        return p.stdout if p.returncode == 0 else None
    want = bytes(i * 7 & 255 for i in range(20000))
    check('%s: lib/moved.bin as written (mtools)' % what, '', mtype('lib/moved.bin') == want)
    check('%s: notes.txt is "ecrit"' % what, mtype('notes.txt'), mtype('notes.txt') == b'ecrit')
    gone = [n for n in ['big.bin', 'tmp.txt'] if mtype(n) is not None]
    check('%s: deleted and renamed names are gone' % what, gone, not gone)
    listing = subprocess.run(['mdir', '-i', spec, '-b', '::/'], capture_output=True, text=True).stdout
    check('%s: lower case names stay lower case' % what, listing, '::/notes.txt' in listing)
    if mkdir:
        check('%s: long and mixed case names as Linux sees them' % what, listing,
              '::/much-too-long-name.text' in listing and '::/Mixte.Txt' in listing)
    if mkdir:
        sub = subprocess.run(['mdir', '-i', spec, '-b', '::/keepdir'], capture_output=True, text=True).stdout
        check('%s: the new directory and what is in it (mtools)' % what, sub,
              '::/keepdir/a long file name.txt' in sub and '::/keepdir/deeper' in sub)
    part = path
    if spec.endswith('@@1M'):
        part = path + '.part'
        with open(path, 'rb') as f, open(part, 'wb') as g:
            f.seek(1 << 20)
            g.write(f.read())
    p = subprocess.run(['fsck.fat', '-n', part], capture_output=True, text=True)
    check('%s: fsck.fat finds nothing wrong' % what, p.stdout[-300:], p.returncode == 0)


def test_breakout(b):
  """Loads examples/games/sdl2_breakout.fs line by line (the line `run` starts
  the game), checks the screen, launches the ball, quits with Esc."""
  root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
  lines = open(os.path.join(root, 'examples', 'games', 'sdl2_breakout.fs')).read().split('\n')
  start = lines.index('run')
  errors = ''
  for line in lines[:start]:
    r = b.run(line)
    if 'ERROR' in r or 'NOT FOUND' in r:
      errors += line + ': ' + r + '\n'
  check('breakout loads', errors, errors == '')
  b.send(b'run\r')
  time.sleep(1.5)
  s = b.screen()
  check('breakout screen 960x720 (zoom 3)', s[:2], s[:2] == (960, 720))
  # Logical pixels times 3: background, top-left red brick, paddle, ball.
  got = [pixel(s, 3 * x, 3 * y) for x, y in [(160, 150), (16, 37), (160, 227), (160, 220)]]
  check('breakout drawn', got, got == ['101830', 'ff0000', 'ffffff', 'ffff00'])
  b.send(b' ')
  time.sleep(0.4)   # 3 pixels a frame upward: gone from the paddle, not yet lost
  s = b.screen()
  got = pixel(s, 3 * 160, 3 * 220)
  check('ball launched', got, got == '101830')
  b.send(b'\x1b')   # Esc: the game ends, close-screen, the prompt is back
  b.wait_for(PROMPT, b.pos, 10)
  b.pos = len(b.out)
  r = b.run('2 3 * .')
  check('prompt after breakout', r, r.split()[0] == '6')


def disk_args(tmp, name, fat, mbr, sd):
  path = os.path.join(tmp, name)
  spec = make_disk(path, fat, mbr)
  if sd:
    return path, spec, ['-drive', 'if=sd,format=raw,file=%s' % path]
  return path, spec, ['-drive', 'if=none,id=disk0,format=raw,file=%s' % path,
                      '-device', 'virtio-blk-device,drive=disk0']


def test_screen_only(name, args, tmp):
  """virt with -device ramfb, virtio input and sound: the same screen and
  input tests as on raspi3b, and the sound, recorded in tmp/sound.wav."""
  wav = os.path.join(tmp, 'sound.wav')
  if os.path.exists(wav):
    os.remove(wav)
  args = args + ['-audiodev', 'wav,id=snd0,path=%s' % wav, '-device', 'virtio-sound-device,audiodev=snd0']
  disk, spec, dargs = disk_args(tmp, 'fat12.img', 12, False, False)
  args = args + dargs
  print('%s: qemu %s' % (name, ' '.join(args)))
  b = Board(args, tmp)
  try:
    banner = b.boot()
    check('virt FAT12: autoexec.fs ran at boot', banner, 'autoexec ran' in banner)
    test_files(b, 'virt FAT12', long_create=True, mkdir=True)
    test_sound_play(b)   # first: the other tests make sounds too
    test_input(b, 'virtio')
    test_lines(b)
    test_text(b)
    test_images(b)
    test_breakout(b)
    test_roguelike(b)
    test_varvara_audio(b)
    code = b.bye()
    check('bye exits qemu', code, code is not None)
  except Exception as e:
    check('session', str(e), False)
  finally:
    b.kill()
  if os.path.exists(wav):
    check_sound(wav)
  else:
    check('sound recorded', wav, False)
  check_disk(disk, spec, 'virt FAT12', long_create=True, mkdir=True)


def test_board(name, args, has_gpio, tmp):
  fat, mbr, what = (32, True, 'pi3 SD card, MBR + FAT32') if has_gpio else (16, False, 'virt FAT16')
  disk, spec, dargs = disk_args(tmp, 'fat%d.img' % fat, fat, mbr, has_gpio)
  args = args + dargs
  print('%s: qemu %s' % (name, ' '.join(args)))
  b = Board(args, tmp)
  try:
    banner = b.boot()
    check('banner', banner, ('Lugforth (%s)' % name) in banner)
    check('%s: autoexec.fs ran at boot' % what, banner, 'autoexec ran' in banner)
    test_files(b, what, long_create=True, mkdir=True)
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
      r = b.run('sdl2 320 240 screen')
      check('no screen on virt without ramfb', r, 'ERROR' in r)
    if has_gpio:
      test_console(b)
      test_varvara(b)
      test_editors(b, '/keepdir')   # test_files made it: the first directory of the browser
      test_input(b, 'usb')
      test_lines(b)
      test_text(b)
      test_images(b)
      test_breakout(b)
    code = b.bye()
    check('bye exits qemu', code, code is not None)
  except Exception as e:
    check('session', str(e), False)
  finally:
    b.kill()
  check_disk(disk, spec, what, long_create=True, mkdir=True)


def main():
  out = sys.argv[1] if len(sys.argv) > 1 else 'out/rpi'
  tmp = os.path.join(out, 'test')
  os.makedirs(tmp, exist_ok=True)
  test_board('qemu-virt', ['-M', 'virt', '-cpu', 'cortex-a53', '-m', '128M',
                           '-global', 'virtio-mmio.force-legacy=false',
                           '-kernel', os.path.join(out, 'virt', 'lugforth.elf')], False, tmp)
  test_screen_only('qemu-virt + ramfb + virtio input',
                   ['-M', 'virt', '-cpu', 'cortex-a53', '-m', '128M', '-device', 'ramfb',
                    '-global', 'virtio-mmio.force-legacy=false', '-device', 'virtio-keyboard-device',
                    '-device', 'virtio-tablet-device', '-device', 'virtio-mouse-device',
                    '-kernel', os.path.join(out, 'virt', 'lugforth.elf')], tmp)
  test_board('pi3', ['-M', 'raspi3b', '-no-reboot', '-device', 'usb-kbd', '-device', 'usb-tablet',
                     '-device', 'usb-mouse',
                     '-kernel', os.path.join(out, 'pi3', 'kernel8.img')], True, tmp)
  print('%s' % ('ALL OK' if failures == 0 else '%d FAILED' % failures))
  sys.exit(1 if failures else 0)


if __name__ == '__main__':
  main()
