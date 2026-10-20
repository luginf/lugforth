#!/usr/bin/env python3
"""Checks `graphic` in the console window (lugforth --gui): drawing words typed at the
prompt show at once under the text. Needs Xvfb, xdotool and ImageMagick (import).
  python3 tools/gui_graphic_test.py out/posix/lugforth"""
import os, subprocess, sys, time

exe = sys.argv[1] if len(sys.argv) > 1 else 'out/posix/lugforth'
disp = ':97'
env = dict(os.environ, DISPLAY=disp)
xvfb = subprocess.Popen(['Xvfb', disp, '-screen', '0', '1280x1024x24'], stderr=subprocess.DEVNULL)
time.sleep(1)
lug = subprocess.Popen([exe, '--gui'], env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
failures = 0


def line(text):
  subprocess.run(['xdotool', 'type', '--delay', '30', text], env=env)
  subprocess.run(['xdotool', 'key', 'Return'], env=env)
  time.sleep(0.7)


def count(colors):
  png = subprocess.run(['import', '-window', 'root', '-depth', '8', 'ppm:-'], env=env, capture_output=True).stdout
  lines = [l for l in png.split(b'\n', 6) if not l.startswith(b'#')]
  rgb = b'\n'.join(lines[3:])      # after P6, the size and 255
  n = {c: 0 for c in colors}
  for i in range(0, len(rgb) - 2, 3):
    c = '%02x%02x%02x' % (rgb[i], rgb[i + 1], rgb[i + 2])
    if c in n:
      n[c] += 1
  return n


def check(name, ok, got):
  global failures
  print('  %s    %s: %s' % ('ok  ' if ok else 'FAIL', name, got))
  if not ok:
    failures += 1


try:
  time.sleep(4)
  line('graphic')
  line('red color 100 300 200 100 box')
  line('3 ink 400 50 100 100 box')
  line('$00ff00 color 0 0 639 479 line')
  n = count(['ff0000', 'ffff00', '00ff00'])
  check('a box drawn at the prompt', n['ff0000'] > 10000, n['ff0000'])
  check('ink 3 is yellow', n['ffff00'] > 5000, n['ffff00'])
  check('a line', n['00ff00'] > 200, n['00ff00'])
  line('graphic')
  n = count(['ff0000'])
  check('graphic again keeps the drawing', n['ff0000'] > 10000, n['ff0000'])
  line('cls')
  n = count(['ff0000', 'ffff00', '00ff00'])
  check('cls clears the drawing', not any(n.values()), n)
  line('red color 100 100 100 100 box')
  small = count(['ff0000'])['ff0000']
  line('cls')
  line('1280 960 graphic-size')
  line('red color 100 100 100 100 box')
  big = count(['ff0000'])['ff0000']
  check('graphic-size 1280 960: the same box is smaller on screen (%d, then %d)' % (small, big), 0 < big < small * 0.6, big)
  line('640 480 graphic-size')
  line('1 2 + .')
  line('red color 10 300 50 50 box')
  n = count(['ff0000'])
  check('the pen survives the redraw (red box after other lines)', n['ff0000'] > 2000, n['ff0000'])
finally:
  lug.kill()
  xvfb.kill()
print('ALL OK' if not failures else '%d FAILED' % failures)
sys.exit(1 if failures else 0)
