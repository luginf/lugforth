\ Copyright 2026 Luginfo10
\
\ Licensed under the Apache License, Version 2.0 (the "License");
\ you may not use this file except in compliance with the License.
\ You may obtain a copy of the License at
\
\     http://www.apache.org/licenses/LICENSE-2.0
\
\ Unless required by applicable law or agreed to in writing, software
\ distributed under the License is distributed on an "AS IS" BASIS,
\ WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
\ See the License for the specific language governing permissions and
\ limitations under the License.

\ drivers.fs: loads the UEFI file system drivers put in EFI/drivers/ on the
\ stick, so that exFAT, ext2/3/4 (or NTFS...) disks and partitions show in
\ `volumes` and can be `mount`ed. The firmware alone only reads FAT.
\
\ Not run at boot. To use it:   include drivers.fs
\ To have it at each boot, add the line  include drivers.fs  to autoexec.fs.
\
\ The drivers are not part of lugforth: take them from EfiFs
\ (https://github.com/pbatard/efifs/releases, GPL), the x64 ones:
\   exfat_x64.efi   exFAT
\   ext2_x64.efi    ext2, ext3 and ext4
\ (ntfs_x64.efi, btrfs_x64.efi, iso9660_x64.efi... work the same way)
\ and copy them to EFI/drivers/ on the stick. They READ only: files on those
\ volumes can be included and read, not written. Secure Boot off (needed for
\ lugforth itself anyway), else the firmware refuses them.

also pc

create drv-path 300 allot   0 value drv-len
: drv+ ( a n -- )   dup >r drv-path drv-len + swap cmove  r> +to drv-len ;

: .efi? ( a n -- f )   ( the name ends in .efi, whatever the case )
  dup 4 < if 2drop 0 exit then
  + 4 - 4 s" .efi" { a n b m }
  -1  4 0 do a i + c@ 32 or  b i + c@ <> if drop 0 then loop ;

: load-one ( a n -- )   ( a driver, from EFI/drivers/ )
  0 to drv-len  s" /EFI/drivers/" drv+  drv+
  drv-path drv-len 2dup type ."  : "
  load-driver
  dup 0= if drop ." loaded" cr exit then
  dup -1 = if drop ." cannot read it" cr exit then
  dup -2 = if drop ." refused (not an x64 UEFI image, or Secure Boot on)" cr exit then
  drop ." started with an error (nothing to drive?)" cr ;

: drivers ( -- )   ( every .efi file of EFI/drivers/, then the volumes seen )
  s" /EFI/drivers" open-dir if drop ." no EFI/drivers/ on this disk" cr exit then
  { dh }
  begin dh read-dir dup while
    2dup .efi? if load-one else 2drop then
  repeat 2drop
  dh close-dir drop
  volumes ;

previous

drivers
