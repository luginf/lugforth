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
examples/sdl2_breakout.fs over the serial line, plays it, and checks the
screen's pixels (QEMU monitor `screendump`); the same on virt with
-device ramfb.

  tools/rpi_qemu_test.py [out/rpi]        (what `make -C ports/rpi test` runs)
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
PROMPT = '--> '


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
  """The glyphs of ports/rpi/font8x8.h, by code point."""
  root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
  rows = re.findall(r'\{(0x[^}]*)\},\s*// U\+([0-9A-F]+)',
                    open(os.path.join(root, 'ports', 'rpi', 'font8x8.h')).read())
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
  against the same arithmetic on the PNG (tools/rpi_assets.py decodes it)."""
  root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
  sys.path.insert(0, os.path.join(root, 'tools'))
  import rpi_assets
  iw, ih, px = rpi_assets.decode_png(open(os.path.join(root, 'examples', 'sdl2_tiles.png'), 'rb').read())
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
  r = b.run('s" examples/sdl2_tiles.png" load-image value img  img image-size . .')
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


def test_breakout(b):
  """Loads examples/sdl2_breakout.fs line by line (the line `run` starts
  the game), checks the screen, launches the ball, quits with Esc."""
  root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
  lines = open(os.path.join(root, 'examples', 'sdl2_breakout.fs')).read().split('\n')
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


def test_screen_only(name, args, tmp):
  """virt with -device ramfb and virtio input: the same screen and input
  tests as on raspi3b."""
  print('%s: qemu %s' % (name, ' '.join(args)))
  b = Board(args, tmp)
  try:
    b.boot()
    test_input(b, 'virtio')
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


def test_board(name, args, has_gpio, tmp):
  print('%s: qemu %s' % (name, ' '.join(args)))
  b = Board(args, tmp)
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
      r = b.run('sdl2 320 240 screen')
      check('no screen on virt without ramfb', r, 'ERROR' in r)
    if has_gpio:
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


def main():
  out = sys.argv[1] if len(sys.argv) > 1 else 'out/rpi'
  tmp = os.path.join(out, 'test')
  os.makedirs(tmp, exist_ok=True)
  test_board('qemu-virt', ['-M', 'virt', '-cpu', 'cortex-a53', '-m', '128M',
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
