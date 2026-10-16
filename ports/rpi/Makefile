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

# Bare-metal AArch64 lugforth. Needs gcc-aarch64-linux-gnu and
# picolibc-aarch64-linux-gnu (Debian/Ubuntu); qemu-system-arm to run it.
#
#   make              out/rpi/virt/lugforth.elf, out/rpi/pi3/kernel8.img,
#                     out/rpi/pi4/kernel8.img
#   make run          QEMU virt, console in this terminal (Ctrl-A X quits)
#   make run-pi3      QEMU raspi3b, same kernel8.img as a real Pi 3
#   make run-pty      QEMU virt on a pty, for tools/pico_send.py -p /dev/pts/N
#   make test         boots both under QEMU and checks the answers

ROOT := ../..
OUT ?= $(ROOT)/out/rpi
CROSS ?= aarch64-linux-gnu-
CC := $(CROSS)gcc
OBJCOPY := $(CROSS)objcopy
QEMU ?= qemu-system-aarch64

VERSION := $(shell sed -n "s/.*version='\([^']*\)'.*/\1/p" $(ROOT)/BUILD | head -1)
REVISION := $(shell git rev-parse HEAD 2>/dev/null || echo unknown)

CFLAGS := --specs=picolibc.specs -mcpu=cortex-a53 -O2 -Wall -Werror -Wno-missing-braces \
  -ffreestanding -fno-stack-protector -fno-exceptions -fno-omit-frame-pointer \
  -I $(ROOT) -I $(OUT)
LDFLAGS := -nostartfiles -T rpi.ld -Wl,--no-warn-rwx-segments

SOURCES := start.S main.c builtins.h rpi.ld
FORTH := rpi_boot.fs allocation.fs platform.fs autoboot.fs $(wildcard $(ROOT)/common/*.fs)

# virt: RAM at 1G (QEMU's default -m 128M). Pi: loaded at 0x80000 by the
# firmware, the C heap stops at 512M (every Pi 3 and 4 has that much).
VIRT_DEFS := -DRPI_BOARD_VIRT -Wl,--defsym=__load_addr=0x40080000 -Wl,--defsym=__ram_end=0x48000000
PI3_DEFS := -DRPI_BOARD_PI3 -Wl,--defsym=__load_addr=0x80000 -Wl,--defsym=__ram_end=0x20000000
PI4_DEFS := -DRPI_BOARD_PI4 -Wl,--defsym=__load_addr=0x80000 -Wl,--defsym=__ram_end=0x20000000

all: $(OUT)/virt/lugforth.elf $(OUT)/pi3/kernel8.img $(OUT)/pi4/kernel8.img

$(OUT)/gen/rpi_boot.h: $(FORTH)
	mkdir -p $(OUT)/gen
	printf '%s' '$(REVISION)' > $(OUT)/gen/REVISION
	printf '%s' '$(REVISION)' | cut -c1-7 > $(OUT)/gen/REVSHORT
	python3 $(ROOT)/tools/importation.py -i rpi_boot.fs -o $@ -I $(ROOT) -I $(OUT) \
	  --name boot --header cpp -DVERSION=$(VERSION) -DSTABLE_VERSION=$(VERSION) \
	  -FREVISION=$(OUT)/gen/REVISION -FREVSHORT=$(OUT)/gen/REVSHORT

$(OUT)/virt/lugforth.elf: $(SOURCES) $(OUT)/gen/rpi_boot.h
	mkdir -p $(dir $@)
	$(CC) $(CFLAGS) $(VIRT_DEFS) $(LDFLAGS) start.S main.c -o $@ -lm

$(OUT)/%/lugforth.elf: $(SOURCES) $(OUT)/gen/rpi_boot.h
	mkdir -p $(dir $@)
	$(CC) $(CFLAGS) $(if $(filter pi3,$*),$(PI3_DEFS),$(PI4_DEFS)) $(LDFLAGS) \
	  start.S main.c -o $@ -lm

$(OUT)/%/kernel8.img: $(OUT)/%/lugforth.elf
	$(OBJCOPY) -O binary $< $@

run: $(OUT)/virt/lugforth.elf
	$(QEMU) -M virt -cpu cortex-a53 -m 128M -nographic -kernel $<

run-pi3: $(OUT)/pi3/kernel8.img
	$(QEMU) -M raspi3b -nographic -no-reboot -kernel $<

run-pty: $(OUT)/virt/lugforth.elf
	$(QEMU) -M virt -cpu cortex-a53 -m 128M -display none -monitor none -serial pty -kernel $<

test: all
	python3 $(ROOT)/tools/rpi_qemu_test.py $(OUT)

clean:
	rm -rf $(OUT)

.SECONDARY:
.PHONY: all run run-pi3 run-pty test clean
