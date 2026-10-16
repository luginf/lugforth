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

"""Runs the bare-metal ports/rpi image under QEMU, with its screen in a
QEMU window, sends Forth files over the serial line, then turns this
terminal into the board's keyboard and console.

  tools/rpi_qemu_run.py examples/sdl2_breakout.fs
  tools/rpi_qemu_run.py --board virt examples/sdl2_breakout.fs   (ramfb screen)

The keyboard and mouse of the QEMU window work too (USB on raspi3b,
virtio on virt): in a game, and at the prompt, whose output stays here.
Each file is sent with the board's paste word, then evaluated at once as
include would. A file that does not give the prompt back within a few
seconds (a game loop) ends the sending. Sound goes to the host's
PulseAudio or PipeWire when there is one (--audio none: silent). Ctrl-]
quits (QEMU too).
"""

import argparse
import os
import select
import subprocess
import sys
import termios
import time
import tty

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PROMPT = b'--> '


def main():
  ap = argparse.ArgumentParser()
  ap.add_argument('--board', choices=['pi3', 'virt'], default='pi3')
  ap.add_argument('--out', default=os.path.join(ROOT, 'out', 'rpi'))
  ap.add_argument('--headless', action='store_true', help='no QEMU window')
  ap.add_argument('--audio', default=None,
                  help='QEMU audio driver (pa, pipewire, alsa, sdl, none...); default: pa or pipewire if running')
  ap.add_argument('files', nargs='*')
  args = ap.parse_args()

  qemu = [os.environ.get('QEMU', 'qemu-system-aarch64')]
  if args.board == 'pi3':
    qemu += ['-M', 'raspi3b', '-no-reboot', '-device', 'usb-kbd', '-device', 'usb-tablet',
             '-kernel', os.path.join(args.out, 'pi3', 'kernel8.img')]
  else:
    qemu += ['-M', 'virt', '-cpu', 'cortex-a53', '-m', '128M', '-device', 'ramfb',
             '-global', 'virtio-mmio.force-legacy=false',
             '-device', 'virtio-keyboard-device', '-device', 'virtio-tablet-device',
             '-kernel', os.path.join(args.out, 'virt', 'lugforth.elf')]
  audio = args.audio
  if audio is None:
    run = os.environ.get('XDG_RUNTIME_DIR', '/run/user/%d' % os.getuid())
    audio = ('pa' if os.path.exists(os.path.join(run, 'pulse', 'native')) else
             'pipewire' if os.path.exists(os.path.join(run, 'pipewire-0')) else 'none')
  if audio != 'none':
    qemu += ['-audiodev', '%s,id=snd0' % audio]
    if args.board == 'virt':
      qemu += ['-device', 'virtio-sound-device,audiodev=snd0']
  if args.headless:
    qemu += ['-display', 'none']
  qemu += ['-serial', 'stdio', '-monitor', 'none']
  proc = subprocess.Popen(qemu, stdin=subprocess.PIPE, stdout=subprocess.PIPE)
  fd = proc.stdout.fileno()
  out = sys.stdout.buffer

  def read_until_prompt(timeout):
    got = b''
    end = time.time() + timeout
    while time.time() < end:
      r, _, _ = select.select([fd], [], [], 0.05)
      if r:
        b = os.read(fd, 4096)
        if not b:
          return None
        out.write(b)
        out.flush()
        got += b
        if got.endswith(PROMPT):
          return got
    return b''

  if read_until_prompt(60) is None:
    return proc.wait()
  # Each file goes with the paste word: kept, then evaluated at once, as
  # include would (strings and comments may span lines). A file that ends
  # by starting a game does not give the prompt back: keys go to it.
  for name in args.files:
    with open(name, 'rb') as f:
      proc.stdin.write(b'paste\r' + f.read() + b'\x04')
    proc.stdin.flush()
    got = read_until_prompt(5)
    if got is None:
      return proc.wait()
    if not got:
      break

  sys.stderr.write('\n[keys go to the board; Ctrl-] quits]\n')
  old = termios.tcgetattr(0) if os.isatty(0) else None
  try:
    if old:
      tty.setraw(0)
    while proc.poll() is None:
      r, _, _ = select.select([0, fd], [], [], 0.1)
      if fd in r:
        b = os.read(fd, 4096)
        if not b:
          break
        out.write(b)
        out.flush()
      if 0 in r:
        b = os.read(0, 1024)
        if not b or b'\x1d' in b:
          break
        proc.stdin.write(b)
        proc.stdin.flush()
  finally:
    if old:
      termios.tcsetattr(0, termios.TCSADRAIN, old)
    if proc.poll() is None:
      proc.kill()
  return 0


if __name__ == '__main__':
  sys.exit(main())
