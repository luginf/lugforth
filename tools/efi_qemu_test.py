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
  b.run('azerty')   # typed on an AZERTY layout, this is the word qwerty


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
                    ('ports/posix/editor.fs', 'lib/editor.fs'), ('ports/posix/writhdeck.fs', 'lib/writhdeck.fs'),
                    ('ports/posix/eval_copy.fs', 'lib/eval_copy.fs')]:
    baremetal_mkdisk.put(spec, os.path.join(ROOT, src), dest)
  baremetal_mkdisk.put(spec, os.path.join(out, 'BOOTX64.EFI'), 'EFI/BOOT/BOOTX64.EFI')
  args = ['-machine', 'q35', '-m', '512M', '-drive', 'if=pflash,format=raw,readonly=on,file=%s' % OVMF,
          '-drive', 'if=none,id=stick,format=raw,file=%s' % img, '-device', 'qemu-xhci',
          '-device', 'usb-storage,drive=stick', '-device', 'usb-kbd']   # OVMF repeats a held USB key
  print('pc-efi: qemu %s' % ' '.join(args))
  b = EfiBoard(args, tmp)
  try:
    banner = b.boot(120)
    check('banner', banner, 'Lugforth (pc-efi)' in banner)
    check('autoexec.fs ran at boot', banner, 'autoexec ran' in banner)
    basics(b)
    console(b)
    keyboard(b)
    history(b)
    accents(b)
    pager(b)
    b.region = (160, 120)
    t.test_lines(b)
    b.region = (160, 60)
    t.test_text(b)
    b.region = (120, 60)
    t.test_images(b)
    b.region = (960, 720)
    t.test_breakout(b)
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
    b.region = (1280, 960)
    t.test_files(b, 'USB stick FAT32 (UEFI)', long_create=True)
    t.test_editors(b, '/EFI')   # the browser sorts: EFI/ comes first on the stick
    code = b.bye()
    check('bye powers off (QEMU exits)', code, code is not None)
  except Exception as e:
    check('session', str(e), False)
  finally:
    b.kill()
  t.check_disk(img, spec, 'USB stick FAT32 (UEFI)')
  print('%s' % ('ALL OK' if t.failures == 0 else '%d FAILED' % t.failures))
  sys.exit(1 if t.failures else 0)


if __name__ == '__main__':
  main()
