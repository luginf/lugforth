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
adapter). Without --boot it holds no DOS: start from your own (FreeDOS,
MS-DOS) and use it as the second disk.

With --boot DIR (DIR holds KERNEL.SYS and SYS.COM of the FreeDOS kernel, GPL,
fetched by `make bootimage`) it starts by itself: the boot code of
ports/dos/mbr.S in the MBR, the FreeDOS boot sector on the partition (made by
FreeDOS's own SYS, run in DOSBox-X on the image), and a CONFIG.SYS that loads
HDPMI32 and takes LUGFORTH.EXE as the shell: no COMMAND.COM, the PC starts
on the Lugforth prompt.

  DOSBox:    imgmount d lugforth.img -size 512,63,16,CYLINDERS
             (DOSBox-X reads the MBR: imgmount d lugforth.img)
  QEMU:      qemu-system-i386 -hda freedos.img -hdb lugforth.img

  tools/dos_image.py out/dos/disk out/dos/lugforth.img [MB] [--boot DIR]
  qemu-system-i386 -hda out/dos/lugforth-boot.img    (the bootable one)

Needs mtools (mformat, mcopy); --boot also dosbox-x. Prints the geometry.
"""

import os
import shutil
import struct
import subprocess
import sys
import tempfile

HEADS, SECTORS = 16, 63          # the usual translation of a small IDE disk
START = SECTORS                  # the partition starts on the second track, as DOS's FDISK puts it

# ports/dos/mbr.S, assembled (as --32, ld -Ttext 0x600 --oformat binary)
MBR_CODE = bytes.fromhex(
  'fa31c08ed88ec08ed0bc007cfbfcbe007cbf0006b90001f3a5ea1e060000bebe07b90400f604807507'
  '83c610e2f6eb4589f5b441bbaa55cd13721b81fb55aa75158b4608a3a9068b460aa3ab06bea106b442'
  'cd1373108a76018b4e02bb007cb80102cd13720f813efe7d55aa750789eeea007c0000be8b06ac84c0'
  '7409b40ebb0700cd10ebf2cd18f4ebfb4e6f20626f6f7461626c6520706172746974696f6e00100001'
  '00007c00000000000000000000')

CONFIG_SYS = (b'REM Lugforth: no COMMAND.COM, the shell is Lugforth itself\r\n'
              b'INSTALL=C:\\HDPMI32.EXE -r\r\n'
              b'SHELL=C:\\LUGFORTH.EXE\r\n')


def chs(lba, cyls):
  """The CHS fields of a partition entry for sector lba (1023 when too far)."""
  c = lba // (HEADS * SECTORS)
  h = (lba // SECTORS) % HEADS
  s = lba % SECTORS + 1
  if c > 1023:
    c, h, s = 1023, HEADS - 1, SECTORS
  return bytes([h, ((c >> 2) & 0xC0) | s, c & 0xFF])


def boot_sector(img, kdir):
  """The FreeDOS boot sector for the partition of img, as FreeDOS's SYS makes it (run in DOSBox-X)."""
  if not shutil.which('dosbox-x'):
    sys.exit('dos_image: --boot needs dosbox-x (apt install dosbox-x), to run FreeDOS\'s SYS')
  d = tempfile.mkdtemp(prefix='lugsys')
  try:
    for n in os.listdir(kdir):
      if n.lower() in ('sys.com', 'kernel.sys'):
        shutil.copy(os.path.join(kdir, n), os.path.join(d, n.upper()))
    conf = os.path.join(d, 'dosbox.conf')
    with open(conf, 'w') as f:
      f.write('[cpu]\ncycles=max\n')
    env = dict(os.environ, SDL_VIDEODRIVER='dummy', SDL_AUDIODRIVER='dummy')
    subprocess.run(['dosbox-x', '-conf', conf, '-c', 'mount c "%s"' % d, '-c', 'imgmount d "%s"' % img,
                    '-c', 'c:', '-c', 'sys c:\\ d: c:\\bs.bin /BOOTONLY /OEM:FD /FORCE:BIOSDRV > c:\\log.txt',
                    '-c', 'exit'], env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=120)
    bs = [n for n in os.listdir(d) if n.lower() == 'bs.bin']
    if not bs:
      log = [n for n in os.listdir(d) if n.lower() == 'log.txt']
      sys.exit('dos_image: SYS made no boot sector%s' % (': ' + open(os.path.join(d, log[0])).read() if log else ''))
    data = open(os.path.join(d, bs[0]), 'rb').read()
    if len(data) != 512 or data[510:512] != b'\x55\xaa':
      sys.exit('dos_image: the boot sector of SYS is not one')
    return data
  finally:
    shutil.rmtree(d, ignore_errors=True)


def main():
  args = sys.argv[1:]
  kdir = None
  if '--boot' in args:
    i = args.index('--boot')
    kdir = args[i + 1]
    del args[i:i + 2]
  src = args[0]
  img = args[1]
  mb = int(args[2]) if len(args) > 2 else 32
  cyls = mb * 1024 * 1024 // (HEADS * SECTORS * 512)
  total = cyls * HEADS * SECTORS
  count = total - START
  with open(img, 'wb') as f:
    f.truncate(total * 512)
    mbr = bytearray(512)
    if kdir:
      mbr[:len(MBR_CODE)] = MBR_CODE
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
  if kdir:
    kernel = [n for n in os.listdir(kdir) if n.lower() == 'kernel.sys']
    if not kernel:
      sys.exit('dos_image: no KERNEL.SYS in %s' % kdir)
    d = tempfile.mkdtemp(prefix='lugcfg')
    try:
      cfg = os.path.join(d, 'CONFIG.SYS')
      with open(cfg, 'wb') as f:
        f.write(CONFIG_SYS)
      subprocess.run(['mcopy', '-Q', '-i', part, os.path.join(kdir, kernel[0]), '::KERNEL.SYS'], check=True, env=env)
      subprocess.run(['mcopy', '-Q', '-i', part, cfg, '::CONFIG.SYS'], check=True, env=env)
    finally:
      shutil.rmtree(d, ignore_errors=True)
    bs = boot_sector(img, kdir)
    with open(img, 'r+b') as f:
      f.seek(START * 512)
      f.write(bs)
  print('dos_image: %s, %d MB, imgmount size 512,%d,%d,%d' % (img, mb, SECTORS, HEADS, cyls))


if __name__ == '__main__':
  main()
