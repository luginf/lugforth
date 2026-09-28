#! /usr/bin/env python3
# Copyright 2026 luginf
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

# Patches the GBA cartridge header checksum (byte 0xBD) that the BIOS boot
# check verifies, in place. crt0.s writes 0 there since the assembler can't
# compute it (it depends on the surrounding bytes it is itself part of);
# this is the usual place to fix that up, same idea as the devkitARM/libgba
# "gbafix" tool. Formula from GBATEK ("Backup Memory (Save Data)" ->
# "Header" there, under "BIOS Checks").
#
#   arm-none-eabi-objcopy -O binary gba.elf gba.gba
#   python3 gba/fix_header.py gba.gba

import sys

path = sys.argv[1]
data = bytearray(open(path, "rb").read())
chk = 0
for i in range(0xA0, 0xBD):
    chk -= data[i]
chk = (chk - 0x19) & 0xFF
data[0xBD] = chk
open(path, "wb").write(data)
print(f"{path}: header checksum set to {chk:#04x}")
