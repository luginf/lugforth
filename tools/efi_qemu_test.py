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

"""Boots ports/efi's BOOTX64.EFI under QEMU (q35, OVMF), from a USB stick
image made like a real one (MBR, FAT32, EFI/BOOT/BOOTX64.EFI), and checks
it over the serial line and the screen, with the checks of
tools/baremetal_qemu_test.py where they apply: arithmetic, floats, faults, the
console on the screen, lines, text, images, breakout, the roguelike from
the stick, the file words (then the image is read back with mtools and
checked with fsck.fat), the keyboard through QMP, and the editors.

  tools/efi_qemu_test.py [out/efi]        (what `make -C ports/efi test` runs)
"""

import os
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import baremetal_qemu_test as t   # noqa: E402
import baremetal_mkdisk           # noqa: E402
from baremetal_qemu_test import check, pixel, PROMPT   # noqa: E402

t.QEMU = os.environ.get('QEMU_X86', 'qemu-system-x86_64')
OVMF = os.environ.get('OVMF', '/usr/share/OVMF/OVMF_CODE_4M.fd')
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


class EfiBoard(t.Board):
  """The screen of a PC is the firmware's whole mode (1280x1024 here): a
  program's screen is centered in it. region = (w, h) crops screen() to
  that centered part, as the tests of the Pi expect."""
  region = None

  def screen(self):
    w, h, rgb = t.Board.screen(self)
    if not self.region:
      return w, h, rgb
    rw, rh = self.region
    x0, y0 = (w - rw) // 2, (h - rh) // 2
    rows = [rgb[((y0 + y) * w + x0) * 3:((y0 + y) * w + x0 + rw) * 3] for y in range(rh)]
    return rw, rh, b''.join(rows)


def basics(b):
  r = b.run('1 2 + .')
  check('arithmetic', r, r.split()[0] == '3')
  r = b.run('1e fsin f. 1e fcos f. 1e 1e fatan2 f. 2e 10e f** f. 1e fexp f. 10e fln f. 2e fsqrt f. -2.5e floor f.')
  check('floats (x87 libc)', r, r.split()[:8] == ['0.841470', '0.540302', '0.785398', '1024.000000',
                                                 '2.718281', '2.302585', '1.414213', '-3.000000'])
  r = b.run(': t 3 0 do i . loop ; t')
  check('loop', r, r.startswith('0 1 2'))
  r = b.run('ms-ticks 300 ms ms-ticks swap - .')
  n = int(r.split()[0])
  check('300 ms (%d)' % n, r, 290 <= n <= 450)
  r = b.run('here 1+ @ drop 42 .')
  check('unaligned @', r, r.split()[0] == '42')
  r = b.run('$800000000000 @')
  check('#GP (non-canonical address) becomes a throw', r, 'lugforth: fault 0x000000000000000d' in r and 'ERROR' in r)
  r = b.run('$7f0000000000 @')
  check('#PF (unmapped page) becomes a throw', r, 'fault 0x000000000000000e' in r and 'ERROR' in r)
  r = b.run('5 5 * .')
  check('prompt after the faults', r, r.split()[0] == '25')
  r = b.run('rpi board type forth')
  check('board, through the rpi alias', r, r.split()[0] == 'pc-efi')
  r = b.run('pc unix-time . forth')
  check('date from the battery clock', r, int(r.split()[0]) > 1700000000)


def volumes(b):
  """A second stick: volumes lists both, mount switches the disk of the file words."""
  r = b.run('volumes')
  check('volumes lists two FAT volumes', r, r.count(' MB') == 2 and '* 0' in r)
  r = b.run('1 mount ls')
  check('1 mount: ls shows the second stick', r, 'second.txt' in r and 'EFI/' not in r)
  r = b.run('cat second.txt')
  check('1 mount: cat reads its file', r, 'from stick two' in r)
  r = b.run('volumes')
  check('volumes marks the one in use', r, '* 1' in r)
  r = b.run('0 mount ls')
  check('0 mount: back on the boot stick', r, 'EFI/' in r and 'second.txt' not in r)
  r = b.run('9 mount')
  check('mount of a missing volume says so', r, 'no such volume' in r)
  b.run('variable hf  s" sdl2_snake.fs" r/o open-file drop hf !')
  r = b.run('1 mount pc volume-now . forth')
  check('mount refused while a file is open', r, 'close the open files first' in r and r.split()[-2] == '0')
  b.run('hf @ close-file drop')
  r = b.run('1 mount pc volume-now . forth 0 mount')
  check('mount accepted once it is closed', r, r.split()[-2] == '1')


def hotplug(b, img3):
  """A third stick plugged in while the system runs: volumes finds it after a rescan."""
  b.screen()                                            # opens the monitor
  b.monitor.send(('drive_add 0 if=none,id=stick3,format=raw,file=%s\n' % img3).encode())
  time.sleep(0.5)
  b.monitor.send(b'device_add usb-storage,drive=stick3,id=dev3\n')
  time.sleep(3)
  r = b.run('volumes')
  if r.count(' MB') < 3:
    time.sleep(5)
    r = b.run('volumes')
  check('a stick plugged after the start is listed', r, r.count(' MB') == 3)
  r = b.run('2 mount ls')
  check('2 mount: its file is there', r, 'third.txt' in r)
  b.run('0 mount')


def console(b):
  """The console on the GOP screen, 1280x960 centered: the banner lit, the
  sdl2 screen over it, back after close-screen."""
  def lit(scr, y0, y1):
    return sum(1 for y in range(y0, y1) for x in range(0, scr[0], 4) if pixel(scr, x, y) != '000000')
  r = b.run('pc screen-size . . forth')
  check('GOP mode at least 1280x960', r, int(r.split()[1]) >= 1280 and int(r.split()[0]) >= 960)
  b.region = (1280, 960)
  scr = b.screen()
  check('console: banner drawn', lit(scr, 0, 32), lit(scr, 0, 32) > 200)
  b.run('sdl2 320 240 screen $ff0000 color cls flip')
  b.region = (320, 240)
  scr = b.screen()
  check('console: the sdl2 screen replaces it', pixel(scr, 5, 5), pixel(scr, 5, 5) == 'ff0000')
  b.run('close-screen previous')
  b.region = (1280, 960)
  scr = b.screen()
  check('console: back after close-screen', lit(scr, 0, 32), 0 < lit(scr, 0, 32) < 200)


def keyboard(b):
  """A USB keyboard, through OVMF and UEFI's console input (QEMU's PS/2
  keyboard does not repeat a held key, a real one does): typing
  at the prompt, and a key held (the firmware repeats it) seen by pressed?
  while held, let go soon after."""
  keys = []
  for k in ['2', 'spc', '3', 'spc']:
    keys += [t.qkey(k, True), t.qkey(k, False)]
  b.qmp(keys)
  time.sleep(0.5)
  keys = [t.qkey('shift', True), t.qkey('8', True), t.qkey('8', False), t.qkey('shift', False)]
  for k in ['spc', 'dot', 'ret']:
    keys += [t.qkey(k, True), t.qkey(k, False)]
  b.qmp(keys)
  b.wait_for(PROMPT, b.pos, 10)
  i = b.out.rfind(PROMPT)
  r = b.out[b.pos:i].replace('\r', '')
  b.pos = i + len(PROMPT)
  check('keyboard: typing at the prompt', r, '2 3 * .' in r and '6' in r.split())
  b.run('sdl2 0 value held 0 value frames')
  b.run(': watch ( ms -- ) ticks + begin events 1 +to frames key-left pressed? if 1 +to held then '
        '10 ms dup ticks - 0< until drop ;')
  b.send(b'0 to held 0 to frames 2500 watch held . frames .\r')
  time.sleep(0.5)
  b.qmp([t.qkey('left', True)])
  time.sleep(1.5)
  b.qmp([t.qkey('left', False)])
  b.wait_for(PROMPT, b.pos, 10)
  i = b.out.rfind(PROMPT)
  r = [w for w in b.out[b.pos:i].replace('\r', '').split() if w.lstrip('-').isdigit()]
  b.pos = i + len(PROMPT)
  held, frames = int(r[-2]), int(r[-1])
  # Held for 1.5 s out of 2.5: the firmware's repeat delay leaves a gap at first.
  check('keyboard: a held key seen held (%d of %d frames)' % (held, frames), r,
        frames > 0 and 0.35 < held / frames < 0.75)
  b.run('previous')


def history(b):
  """The line editor of the desktop (common/lineedit.fs): Up recalls the last line,
  Tab completes a word."""
  r = b.run('7 8 * .')
  check('history: first line', r, r.split()[0] == '56')
  b.qmp([t.qkey('up', True), t.qkey('up', False), t.qkey('ret', True), t.qkey('ret', False)])
  b.wait_for(PROMPT, b.pos, 10)
  i = b.out.rfind(PROMPT)
  r = b.out[b.pos:i].replace('\r', '')
  b.pos = i + len(PROMPT)
  check('history: Up recalls the line', r, '7 8 * .' in r and '56' in r.split())
  b.send(b'1 2 swa\t')
  time.sleep(0.5)
  b.send(b'. .\r')
  b.wait_for(PROMPT, b.pos, 10)
  i = b.out.rfind(PROMPT)
  r = b.out[b.pos:i].replace('\r', '')
  b.pos = i + len(PROMPT)
  check('Tab completes swa to swap', r, 'NOT FOUND' not in r and r.split()[-3:] == ['1', '2', 'ok'])

  for typed, want in [('include sdl2_rog\t', 'sdl2_roguelike.fs'), ('include lib/edi\t', 'lib/editor.fs'),
                      ('s" lib/ev\t', 'lib/eval_copy.fs')]:
    b.send(typed.encode())
    time.sleep(0.7)
    with b.lock:
      shown = b.out[b.pos:]
    check('Tab completes the file name %r' % typed, shown, want in shown)
    b.send(b'\x15')
    time.sleep(0.2)
    b.run('')


def accents(b):
  """An accented letter typed one UTF-8 byte at a time (AZERTY key 2 gives e acute)
  is drawn as one glyph: the accent in the top rows of its cell."""
  b.region = (1280, 960)
  b.run('azerty')
  b.run('page')
  b.qmp([t.qkey('2', True), t.qkey('2', False)])
  time.sleep(0.8)
  scr = b.screen()
  top = 0
  for row in range(0, 6):
    y0 = row * 32
    n_top = sum(1 for y in range(y0, y0 + 8) for x in range(4 * 16, 5 * 16) if pixel(scr, x, y) != '000000')
    n_body = sum(1 for y in range(y0 + 12, y0 + 28) for x in range(4 * 16, 5 * 16) if pixel(scr, x, y) != '000000')
    if n_top and n_body:
      top = n_top
  check('typed e acute is one glyph with its accent (%d accent pixels)' % top, top, top > 0)
  b.send(b'\x15')
  p = len(b.out)
  b.qmp([t.qkey('bracket_left', True), t.qkey('bracket_left', False), t.qkey('e', True), t.qkey('e', False),
         t.qkey('ret', True), t.qkey('ret', False)])   # the dead key ^ then e
  b.wait_for(PROMPT, p, 10)
  b.pos = len(b.out)
  check('dead key ^ then e gives e circumflex', b.out[p:], '\xc3\xaa' in b.out[p:])
  p = len(b.out)
  b.qmp([t.qkey('alt_r', True), t.qkey('comma', True), t.qkey('comma', False), t.qkey('dot', True),
         t.qkey('dot', False), t.qkey('alt_r', False), t.qkey('ret', True), t.qkey('ret', False)])   # AltGr+; AltGr+:
  b.wait_for(PROMPT, p, 10)
  b.pos = len(b.out)
  check('AZERTY: AltGr+; and AltGr+: give < and >', b.out[p:], '<>' in b.out[p:])
  b.run('azerty')   # typed on an AZERTY layout, this is the word qwerty


def sound_ini(b):
  """sound-off / sound-on: the HD Audio controller stopped or started again, the choice kept in lugforth.ini."""
  b.run('also rpi')
  r = b.run('sound-off snd-ok .')
  check('sound-off: no sound (snd-ok 0)', r, r.split()[0] == '0')
  r = b.run('cat lugforth.ini')
  check('sound-off writes lugforth.ini', r, 'sound = off' in r)
  r = b.run('sound snd-ok .')
  check('sound: the controller starts again (snd-ok -1)', r, r.split()[0] == '-1')
  r = b.run('cat lugforth.ini')
  check('sound-on writes lugforth.ini', r, 'sound = on' in r)
  b.run('previous')


def terminals(b):
  """Alt+F1..F4 and term: a second Forth with a screen of its own. The first keeps
  running while the second has the screen; bye closes the second."""
  import hashlib
  b.region = (1280, 960)

  def shot():
    time.sleep(1.5)
    return hashlib.md5(b.screen()[2]).hexdigest()

  def alt(name):
    b.qmp([t.qkey('alt', True), t.qkey(name, True), t.qkey(name, False), t.qkey('alt', False)])

  def ans(line):
    return b.run(line).replace(' ok', '').strip()

  def prompt():                              # the prompt of the terminal that has the screen
    p = len(b.out)
    b.send(b'\r')
    b.wait_for(PROMPT, p, 30)
    b.pos = len(b.out)

  b.run('page')
  b.run('variable v  42 v !  variable cnt')
  b.run(': spin ( ms -- ) 0 cnt ! ms-ticks + begin 1 cnt +! pause dup ms-ticks < until drop ;')
  b.run('page')
  b.run('.( first terminal) cr')
  one = shot()
  p = len(b.out)
  b.send(b'3000 spin\r')                       # terminal 1 is busy for 3 s
  time.sleep(0.3)
  alt('f2')
  b.wait_for('terminal 2', p, 60)
  b.wait_for(PROMPT, p, 30)
  b.pos = len(b.out)
  r = ans('terminal . terminals .')
  check('Alt+F2 opens a second terminal', r, r == '2 2')
  two = shot()
  check('the second terminal has a screen of its own', two, two != one)
  r = b.run('v @ .')
  check('its dictionary is its own', r, 'NOT FOUND' in r)
  b.run('variable w  7 w !')
  r = b.run('-8 @ .')
  check('a fault in it is caught, the prompt comes back', r, 'fault' in r)
  b.run('graphic  red color  0 0 600 400 line')
  gfx = shot()
  check('it can draw (graphic)', gfx, gfx != two)
  time.sleep(3)                                # terminal 1 finishes meanwhile
  alt('f1')
  time.sleep(1)
  prompt()
  r = ans('terminal . v @ .')
  check('Alt+F1 is back in the first, its variable kept', r, r == '1 42')
  r = ans('cnt @ 100 > .')
  check('it ran while the second had the screen', r, r == '-1')
  back = shot()
  check('the drawing of the second is not on the first one screen', back, back != gfx)
  b.send(b'2 term\r')
  time.sleep(2.5)
  prompt()
  r = ans('terminal . w @ .')
  check('term 2 is back in the second, its variable kept', r, r == '2 7')
  b.send(b'bye\r')
  time.sleep(2.5)
  prompt()
  r = ans('terminal . terminals . v @ .')
  check('bye in the second terminal closes it, the first is back', r, r == '1 1 42')
  b.run('page')


def pager(b):
  """common/pager.fs: a long listing stops at each screenful of the console."""
  more = '-- more --'
  b.run(': pgt 100 0 do i . cr loop ;')
  b.send(b'pgt\r')
  b.wait_for(more, b.pos, 10)
  check('pager: a long listing stops at the end of the screen', more, True)
  p = len(b.out)
  b.send(b' ')
  b.wait_for(more, p, 10)
  check('pager: space shows another page, which stops again', more, True)
  b.send(b'q')
  b.wait_for(PROMPT, p, 10)
  b.pos = len(b.out)
  r = b.run('1 2 + .')
  check('pager: q gives the prompt back', r, r.split()[0] == '3')
  b.run('pager-off')
  p = len(b.out)
  b.send(b'pgt\r')
  b.wait_for(PROMPT, p, 10)
  check('pager-off: no pause', b.out[p:], more not in b.out[p:])
  b.pos = len(b.out)
  b.run('pager-on')


def games(b):
  """The games after r3forth, from the stick: each one drawn, a key acts on it,
  Esc gives the prompt back."""
  def count(scr, colors, step=3):
    return sum(1 for y in range(0, scr[1], step) for x in range(0, scr[0], step) if pixel(scr, x, y) in colors)
  def start(name, region):
    b.region = region
    b.send(('include %s\r' % name).encode())
    time.sleep(4)
  def key(k):
    b.qmp([t.qkey(k, True)])
    time.sleep(0.15)
    b.qmp([t.qkey(k, False)])
    time.sleep(0.6)
  def leave(name):
    b.send(b'\x1b')
    b.wait_for(PROMPT, b.pos, 15)
    b.pos = len(b.out)
    r = b.run('2 3 + .')
    check('%s: the prompt after Esc' % name, r, r.split()[0] == '5')

  r = b.run('sdl2 s" t140 o4 l8 c d e c  e4 g4 r4 >c2" play  sine wave@ . square wave@ . depth .')
  check('play and the waves by name', r, r.split()[:3] == ['4', '0', '0'])
  r = b.run('3 wave wave@ . 2 to wave wave@ . 0 wave 4000 beep-volume beep-volume@ . 3000 beep-volume s" c4&c8 n49 p v8 o3 g" play depth .')
  check('wave and beep-volume are settings, play understands MML', r, r.split()[:4] == ['3', '2', '4000', '0'])
  # graphics under the console: no SCREEN, the drawing words show at once
  def shot():   # the console shows a change when it waits for a key: under QEMU's software CPU, give it time
    time.sleep(1.5)
    return b.screen()
  old_region = b.region
  b.region = (640, 480)
  b.run('1 console-zoom')
  b.run('graphic')
  b.run('red color 100 300 200 100 box')
  b.run('$00ff00 color 0 0 639 479 line')
  b.run('3 ink 400 50 100 100 box')
  scr = shot()
  check('graphic: a box drawn at the prompt', pixel(scr, 150, 350), pixel(scr, 150, 350) == 'ff0000')
  check('graphic: a line', pixel(scr, 320, 240), pixel(scr, 320, 240) == '00ff00')
  check('graphic: ink 3 is yellow', pixel(scr, 450, 100), pixel(scr, 450, 100) == 'ffff00')
  check('graphic: the empty background stays black', pixel(scr, 600, 400), pixel(scr, 600, 400) == '000000')
  b.run('page')
  scr = shot()
  check('graphic: page clears the text, not the drawing', pixel(scr, 150, 350), pixel(scr, 150, 350) == 'ff0000')
  b.run('cls')
  scr = shot()
  check('graphic: cls clears the drawing', pixel(scr, 150, 350), pixel(scr, 150, 350) == '000000')
  b.run('2 console-zoom')
  b.region = (1280, 960)
  b.run('white color 10 10 20 20 box')
  scr = shot()
  check('graphic: stays with the console zoom', pixel(scr, 30, 30), pixel(scr, 30, 30) == 'ffffff')
  b.run('1280 960 graphic-size')
  b.run('blue color 1000 800 100 100 box')
  b.run('cyan color 0 0 1279 959 line')
  scr = shot()
  check('graphic-size: a layer as large as the screen', pixel(scr, 1050, 850), pixel(scr, 1050, 850) == '0000ff')
  check('graphic-size: the line reaches the corner', pixel(scr, 1279, 959), pixel(scr, 1279, 959) == '00ffff')
  b.run('page')
  b.region = old_region

  pieces = {'00d8e0', 'e8d000', 'a040e0', '30c030', 'e03030', '3060f0', 'f08020'}
  def floor_cells(scr):   # the cells of the bottom row filled (their middle: the ghost is an outline)
    return sum(1 for c in range(10) if pixel(scr, (8 + c * 11 + 5) * 3 + 1, (10 + 19 * 11 + 5) * 3 + 1) in pieces)
  start('sdl2_tetris.fs', (600, 720))
  scr = b.screen()
  check('tetris: the well drawn', pixel(scr, 60 * 3, 120 * 3), pixel(scr, 60 * 3, 120 * 3) == '0c0c14')
  before = floor_cells(scr)
  key('spc')
  scr = b.screen()
  after = floor_cells(scr)
  check('tetris: space drops the piece to the floor (%d -> %d)' % (before, after), '', before == 0 and after >= 2)
  leave('tetris')

  start('sdl2_snake.fs', (960, 720))
  scr = b.screen()
  n_head, n_body, n_fruit = count(scr, {'f0f040'}, 1), count(scr, {'30b040', '40c850'}), count(scr, {'e03030'}, 1)
  check('snake: head, body and fruit drawn (%d %d %d)' % (n_head, n_body, n_fruit), '', n_head and n_body and n_fruit)
  leave('snake')

  start('sdl2_minesweeper.fs', (1104, 660))
  scr = b.screen()
  closed = count(scr, {'a8a8b0'})
  check('minesweeper: the board drawn, all closed (%d)' % closed, '', closed > 1000 and count(scr, {'d0d0d8'}) == 0)
  key('spc')
  scr = b.screen()
  opened = count(scr, {'d0d0d8'})
  check('minesweeper: space opens cells (%d)' % opened, '', opened > 100)
  leave('minesweeper')

  def band(scr, r):   # the color of row r of the board, between its small pegs and its green box
    y = (tries_y(r) - 6) * 3 + 1
    return pixel(scr, 120 * 3 + 1, y)
  def tries_y(r):
    return (9 - r) * 18 + 22 + 9 + 2
  start('sdl2_mastermind.fs', (540, 720))
  scr = b.screen()
  check('mastermind: the first row is the one played', band(scr, 0) + band(scr, 1), band(scr, 0) == '8a6038' and band(scr, 1) == '6b4a2b')
  for k in ['1', '2', '3', '4']:
    key(k)
  scr = b.screen()
  colors = [pixel(scr, (16 + 20 * i) * 3 + 1, tries_y(0) * 3 + 1) for i in range(4)]
  check('mastermind: keys 1 to 4 put four colors', colors, colors == ['e03030', 'f0d020', '30b040', '3070f0'])
  key('ret')
  scr = b.screen()
  check('mastermind: enter checks it, the next row is played', band(scr, 0) + band(scr, 1),
        band(scr, 1) == '8a6038' or '30b040' in pixel(scr, 30 * 3, 10 * 3))   # or found at once (1 in 1296)
  leave('mastermind')


def drivers_errors(b):
  """load-driver and drivers.fs without any driver at hand: what they say."""
  r = b.run('s" /nope.efi" pc load-driver . forth')
  check('load-driver: a missing file gives -1', r, r.split()[0] == '-1')
  r = b.run('s" /autoexec.fs" pc load-driver . forth')
  check('load-driver: a file that is no UEFI image gives -2', r, r.split()[0] == '-2')
  r = b.run('include drivers.fs')
  check('drivers.fs without EFI/drivers/ says so', r, 'no EFI/drivers/' in r)


def drivers_full(out, tmp):
  """With EFIFS=dir holding exfat_x64.efi and ext2_x64.efi (EfiFs, not in the repository):
  drivers.fs loads them from EFI/drivers/ of the stick, and an exFAT disk and an ext4
  disk (made with mkfs.exfat and mkfs.ext4 -d) show in volumes; the ext4 one is read."""
  d = os.environ.get('EFIFS')
  drv = ['exfat_x64.efi', 'ext2_x64.efi']
  if not d or not all(os.path.exists(os.path.join(d, f)) for f in drv):
    print('  skip  file system drivers (EFIFS=dir with %s from https://github.com/pbatard/efifs)' % ' '.join(drv))
    return
  img = os.path.join(tmp, 'drv.img')
  spec = baremetal_mkdisk.make_disk(img, 32, True)
  baremetal_mkdisk.put(spec, os.path.join(ROOT, 'ports/efi/disk/drivers.fs'), 'drivers.fs')
  for f in drv:
    baremetal_mkdisk.put(spec, os.path.join(d, f), 'EFI/drivers/' + f)
  baremetal_mkdisk.put(spec, os.path.join(out, 'BOOTX64.EFI'), 'EFI/BOOT/BOOTX64.EFI')
  e4, ex = os.path.join(tmp, 'ext4.img'), os.path.join(tmp, 'exfat.img')
  e4d = os.path.join(tmp, 'ext4.d')
  os.makedirs(e4d, exist_ok=True)
  with open(os.path.join(e4d, 'hi4.fs'), 'w') as f:
    f.write('." hello from ext4" cr\n')
  for p in (e4, ex):
    if os.path.exists(p):
      os.remove(p)
    with open(p, 'wb') as f:
      f.truncate(16 << 20)
  subprocess.run(['mkfs.ext4', '-q', '-F', '-L', 'LUGEXT4', '-d', e4d, e4], check=True)
  subprocess.run(['mkfs.exfat', '-L', 'LUGEXFAT', ex], check=True, stdout=subprocess.DEVNULL)
  args = ['-machine', 'q35', '-m', '512M', '-drive', 'if=pflash,format=raw,readonly=on,file=%s' % OVMF,
          '-drive', 'if=none,id=stick,format=raw,file=%s' % img, '-device', 'qemu-xhci',
          '-device', 'usb-storage,drive=stick', '-device', 'usb-kbd',
          '-drive', 'if=none,id=d4,format=raw,file=%s' % e4, '-device', 'usb-storage,drive=d4',
          '-drive', 'if=none,id=dx,format=raw,file=%s' % ex, '-device', 'usb-storage,drive=dx']
  b = EfiBoard(args, tmp)
  try:
    b.boot(120)
    r = b.run('volumes')
    check('drivers: before, only the FAT stick', r, r.count(' MB') == 1)
    r = b.run('include drivers.fs')
    check('drivers: both loaded', r, r.count(': loaded') == 2)
    check('drivers: exFAT and ext4 volumes seen', r, 'LUGEXFAT' in r and 'LUGEXT4' in r)
    n = [l.replace('*', ' ').split()[0] for l in r.splitlines() if 'LUGEXT4' in l][0]
    r = b.run('%s mount ls' % n)
    check('drivers: ls on ext4', r, 'hi4.fs' in r)
    r = b.run('include hi4.fs')
    check('drivers: include from ext4', r, 'hello from ext4' in r)
    r = b.run('s" new.fs" w/o create-file . drop')
    check('drivers: ext4 is read only (create refused)', r, r.split()[0] != '0')
    n = [l.replace('*', ' ').split()[0] for l in b.run('volumes').splitlines() if 'LUGEXFAT' in l][0]
    r = b.run('%s mount' % n) + ' ' + b.run('ls') + ' '
    r += b.run('pc volume-now . forth')
    check('drivers: the empty exFAT volume mounted', r, 'ERROR' not in r and r.split()[-2] == n)
  except Exception as e:
    check('drivers session', str(e), False)
  finally:
    b.kill()


def main():
  out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, 'out', 'efi')
  tmp = os.path.join(out, 'test')
  os.makedirs(tmp, exist_ok=True)
  img = os.path.join(tmp, 'usb.img')
  spec = baremetal_mkdisk.make_disk(img, 32, True)
  tmpd = img + '.d'
  for name, data in t.DISK_FILES.items():
    os.makedirs(os.path.dirname(os.path.join(tmpd, name)), exist_ok=True)
    with open(os.path.join(tmpd, name), 'wb') as f:
      f.write(data)
    baremetal_mkdisk.put(spec, os.path.join(tmpd, name), name)
  for src, dest in [('examples/sdl2_roguelike.fs', 'sdl2_roguelike.fs'),
                    ('examples/sdl2_tetris.fs', 'sdl2_tetris.fs'), ('examples/sdl2_snake.fs', 'sdl2_snake.fs'),
                    ('examples/sdl2_minesweeper.fs', 'sdl2_minesweeper.fs'), ('examples/sdl2_mastermind.fs', 'sdl2_mastermind.fs'),
                    ('ports/posix/editor.fs', 'lib/editor.fs'), ('ports/posix/writhdeck.fs', 'lib/writhdeck.fs'),
                    ('ports/posix/eval_copy.fs', 'lib/eval_copy.fs'), ('ports/efi/disk/drivers.fs', 'drivers.fs')]:
    baremetal_mkdisk.put(spec, os.path.join(ROOT, src), dest)
  baremetal_mkdisk.put(spec, os.path.join(out, 'BOOTX64.EFI'), 'EFI/BOOT/BOOTX64.EFI')
  img2 = os.path.join(tmp, 'usb2.img')                 # a second stick, to switch to
  spec2 = baremetal_mkdisk.make_disk(img2, 32, True)
  with open(img2 + '.txt', 'wb') as f:
    f.write(b'from stick two\n')
  baremetal_mkdisk.put(spec2, img2 + '.txt', 'second.txt')
  img3 = os.path.join(tmp, 'usb3.img')                 # a third one, plugged while the system runs
  spec3 = baremetal_mkdisk.make_disk(img3, 32, True)
  with open(img3 + '.txt', 'wb') as f:
    f.write(b'third\n')
  baremetal_mkdisk.put(spec3, img3 + '.txt', 'third.txt')
  wav = os.path.join(tmp, 'sound.wav')
  if os.path.exists(wav):
    os.remove(wav)
  args = ['-machine', 'q35', '-m', '512M', '-drive', 'if=pflash,format=raw,readonly=on,file=%s' % OVMF,
          '-drive', 'if=none,id=stick,format=raw,file=%s' % img, '-device', 'qemu-xhci',
          '-device', 'usb-storage,drive=stick', '-device', 'usb-kbd',
          '-drive', 'if=none,id=stick2,format=raw,file=%s' % img2, '-device', 'usb-storage,drive=stick2',   # OVMF repeats a held USB key
          '-audiodev', 'wav,id=snd0,path=%s' % wav, '-device', 'intel-hda', '-device', 'hda-duplex,audiodev=snd0']
  print('pc-efi: qemu %s' % ' '.join(args))
  b = EfiBoard(args, tmp)
  try:
    banner = b.boot(120)
    check('banner', banner, 'Lugforth (pc-efi)' in banner)
    check('autoexec.fs ran at boot', banner, 'autoexec ran' in banner)
    basics(b)
    t.test_sound_play(b)   # first: the other tests make sounds too
    volumes(b)
    hotplug(b, img3)
    console(b)
    keyboard(b)
    history(b)
    accents(b)
    pager(b)
    terminals(b)
    b.region = (160, 120)
    t.test_lines(b)
    b.region = (160, 60)
    t.test_text(b)
    b.region = (120, 60)
    t.test_images(b)
    b.region = (960, 720)
    t.test_breakout(b)
    games(b)
    drivers_errors(b)
    b.region = (960, 624)
    b.send(b'include sdl2_roguelike.fs\r')   # the game: the prompt comes back after Esc
  except Exception as e:
    check('session', str(e), False)
  try:
    b.wait_for('esc: quit', b.pos, 60)
    time.sleep(2)
    s = b.screen()
    lit = sum(1 for y in range(0, s[1], 3) for x in range(0, s[0], 3) if pixel(s, x, y) != '000000')
    check('roguelike from the stick drawn (%d lit)' % lit, '', lit > 1000)
    b.send(b'\x1b')
    b.wait_for(PROMPT, b.pos, 10)
    b.pos = len(b.out)
    b.run('tune-stop')
    sound_ini(b)
    b.region = (1280, 960)
    t.test_files(b, 'USB stick FAT32 (UEFI)', long_create=True)
    t.test_editors(b, '/EFI')   # the browser sorts: EFI/ comes first on the stick
    code = b.bye()
    check('bye powers off (QEMU exits)', code, code is not None)
  except Exception as e:
    check('session', str(e), False)
  finally:
    b.kill()
  drivers_full(out, tmp)
  t.check_sound(wav)   # the Intel HD Audio driver (hda.h), recorded by QEMU
  t.check_disk(img, spec, 'USB stick FAT32 (UEFI)')
  print('%s' % ('ALL OK' if t.failures == 0 else '%d FAILED' % t.failures))
  sys.exit(1 if t.failures else 0)


if __name__ == '__main__':
  main()
