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

"""Makes a FAT disk image for ports/rpi, with files on it: an SD card for
QEMU raspi3b (an MBR and a FAT32 partition at 1 MiB, 64 MiB, a power of
2 as QEMU wants) or a disk for QEMU virt's virtio-blk. Needs dosfstools
and mtools (apt install dosfstools mtools).

  tools/baremetal_mkdisk.py out/rpi/disk.img examples/sdl2_breakout.fs ...
  tools/baremetal_mkdisk.py --fat 16 --no-mbr disk.img dir/file.fs=file.fs

A file goes to the root under its own name; SRC=DEST puts it elsewhere
(DEST may have directories, made as needed).
"""

import argparse
import os
import struct
import subprocess


def make_disk(path, fat=32, mbr=True, size=None):
  """An empty FAT12/16/32 image, on an MBR partition at 1 MiB or not.
  Returns the mtools image spec (path, or path@@1M)."""
  if size is None:
    size = 64 << 20 if mbr else (4 << 20 if fat == 12 else 32 << 20)
  with open(path, 'wb') as f:
    f.truncate(size)
  spec, offset = path, 0
  if mbr:
    with open(path, 'r+b') as f:
      m = bytearray(512)
      m[446:462] = struct.pack('<B3sB3sII', 0, b'\0\0\0', 0x0C, b'\0\0\0', 2048, size // 512 - 2048)
      m[510:512] = b'\x55\xaa'
      f.write(m)
    offset, spec = 2048, path + '@@1M'
  subprocess.run(['mkfs.fat', '-F', str(fat), '--offset', str(offset), path,
                  str((size // 512 - offset) // 2)],
                 check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
  return spec


def put(spec, src, dest):
  """Copy a host file to DEST on the image, making its directories."""
  parts = dest.strip('/').split('/')
  for i in range(1, len(parts)):
    d = '::/' + '/'.join(parts[:i])
    if subprocess.run(['mdir', '-i', spec, d], capture_output=True).returncode != 0:
      subprocess.run(['mmd', '-i', spec, d], check=True)
  subprocess.run(['mcopy', '-o', '-i', spec, src, '::/' + '/'.join(parts)], check=True)


def main():
  ap = argparse.ArgumentParser()
  ap.add_argument('--fat', type=int, choices=[12, 16, 32], default=32)
  ap.add_argument('--no-mbr', action='store_true', help='no partition table (a "superfloppy")')
  ap.add_argument('image')
  ap.add_argument('files', nargs='*', help='SRC or SRC=DEST')
  args = ap.parse_args()
  spec = make_disk(args.image, args.fat, not args.no_mbr)
  for f in args.files:
    src, _, dest = f.partition('=')
    put(spec, src, dest or os.path.basename(src))
  print('%s: FAT%d%s, %d files' % (args.image, args.fat, '' if args.no_mbr else ' on an MBR partition',
                                   len(args.files)))


if __name__ == '__main__':
  main()
