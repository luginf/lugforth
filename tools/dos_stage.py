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

"""Makes the folder of Lugforth for DOS (out/dos/disk, what `make -C ports/dos
stage` runs), to copy on a disk, a floppy image or a folder that DOSBox mounts:
LUGFORTH.EXE, HDPMI32.EXE if it is there, the examples of games/ graphics/
music/ utils/, ed and writhdeck in LIB/, the manual.

Every name is cut to 8.3 as DOS cuts it (base 8 characters, extension 3):
`gfx_breakout.fs` is kept as GFX_BREA.FS. DOS without long names (DOSBox,
MS-DOS, FreeDOS without DOSLFN) cuts the names the same way when a program
opens a file, so `include games/gfx_breakout.fs` and the `needs` of the
editors find their files under the long names written in the sources. Two
files that would get the same short name are reported; the first is kept.

  tools/dos_stage.py out/dos [out/dos/disk]
"""

import os
import shutil
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def short(name):
  """The name as DOS keeps it: 8 characters, a dot, 3, in capitals."""
  base, dot, ext = name.partition('.')
  if '.' in ext:                       # a.b.c: DOS keeps the first extension
    ext = ext.split('.')[0]
  return (base[:8] + ('.' + ext[:3] if dot else '')).upper()


def main():
  out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, 'out', 'dos')
  disk = sys.argv[2] if len(sys.argv) > 2 else os.path.join(out, 'disk')
  if os.path.isdir(disk):
    shutil.rmtree(disk)
  os.makedirs(disk)
  files = [(os.path.join(out, 'lugforth.exe'), 'LUGFORTH.EXE')]
  if os.path.exists(os.path.join(out, 'HDPMI32.EXE')):
    files.append((os.path.join(out, 'HDPMI32.EXE'), 'HDPMI32.EXE'))
  for folder in ('games', 'graphics', 'music', 'utils'):
    src = os.path.join(ROOT, 'examples', folder)
    for f in sorted(os.listdir(src)):
      if f.endswith('.fs') and not f.endswith('_tests.fs'):
        files.append((os.path.join(src, f), short(folder) + '/' + short(f)))
  for src, name in [('editors/editor.fs', 'editor.fs'), ('editors/writhdeck.fs', 'writhdeck.fs'),
                    ('ports/posix/eval_copy.fs', 'eval_copy.fs')]:
    files.append((os.path.join(ROOT, src), 'LIB/' + short(name)))
  for src in ('docs/en/MANUAL.md', 'docs/en/WORDS.md', 'docs/fr/MANUAL_fr.md', 'ports/dos/README.md'):
    files.append((os.path.join(ROOT, src), 'DOC/' + short(os.path.basename(src))))
  taken = {}
  for src, name in files:
    if name in taken:
      print('dos_stage: %s left out, %s already has the name %s' % (src, taken[name], name))
      continue
    taken[name] = src
    dest = os.path.join(disk, name)
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    shutil.copy(src, dest)
  print('dos_stage: %d files in %s' % (len(taken), disk))


if __name__ == '__main__':
  main()
