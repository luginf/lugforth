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

"""A hard disk image of Lugforth for DOS (`make -C ports/dos image`): the
folder of `make stage` (out/dos/disk) on one FAT16 partition, behind an MBR,
for an emulator or a real PC (dd to a CompactFlash or SD card in an IDE
adapter). Not bootable: it holds no DOS, start from your own (FreeDOS, MS-DOS)
and use it as the second disk.

  DOSBox:    imgmount d lugforth.img -size 512,63,16,CYLINDERS
             (DOSBox-X reads the MBR: imgmount d lugforth.img)
  QEMU:      qemu-system-i386 -hda freedos.img -hdb lugforth.img

  tools/dos_image.py out/dos/disk out/dos/lugforth.img [MB]

Needs mtools (mformat, mcopy). Prints the geometry it used.
"""

import os
import struct
import subprocess
import sys

HEADS, SECTORS = 16, 63          # the usual translation of a small IDE disk
START = SECTORS                  # the partition starts on the second track, as DOS's FDISK puts it


def chs(lba, cyls):
  """The CHS fields of a partition entry for sector lba (1023 when too far)."""
  c = lba // (HEADS * SECTORS)
  h = (lba // SECTORS) % HEADS
  s = lba % SECTORS + 1
  if c > 1023:
    c, h, s = 1023, HEADS - 1, SECTORS
  return bytes([h, ((c >> 2) & 0xC0) | s, c & 0xFF])


def main():
  src = sys.argv[1]
  img = sys.argv[2]
  mb = int(sys.argv[3]) if len(sys.argv) > 3 else 32
  cyls = mb * 1024 * 1024 // (HEADS * SECTORS * 512)
  total = cyls * HEADS * SECTORS
  count = total - START
  with open(img, 'wb') as f:
    f.truncate(total * 512)
    mbr = bytearray(512)
    kind = 0x06 if count >= 65536 else 0x04       # FAT16 above 32 MB of sectors, else small FAT16
    entry = bytes([0x80]) + chs(START, cyls) + bytes([kind]) + chs(total - 1, cyls) + struct.pack('<II', START, count)
    mbr[446:462] = entry
    mbr[510:512] = b'\x55\xaa'
    f.seek(0)
    f.write(mbr)
  part = '%s@@%d' % (img, START * 512)
  env = dict(os.environ, MTOOLS_SKIP_CHECK='1')
  subprocess.run(['mformat', '-i', part, '-T', str(count), '-h', str(HEADS), '-s', str(SECTORS),
                  '-H', str(START), '-v', 'LUGFORTH', '::'], check=True, env=env)
  names = sorted(os.listdir(src))
  subprocess.run(['mcopy', '-s', '-Q', '-i', part] + [os.path.join(src, n) for n in names] + ['::'],
                 check=True, env=env)
  print('dos_image: %s, %d MB, imgmount size 512,%d,%d,%d' % (img, mb, SECTORS, HEADS, cyls))


if __name__ == '__main__':
  main()
