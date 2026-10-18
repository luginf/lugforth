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
#   make screen       QEMU raspi3b with its screen in a window, keys from here
#   make gui          the same window as the Forth console itself: text on the
#                     screen, keys from the window (BOARD=virt: with -device ramfb)
#                     ZOOM=1 (640x480) or ZOOM=2 (1280x960, the default) sets its
#                     size at boot; `1 console-zoom` changes it at the prompt
#   make breakout     the same, with examples/sdl2_breakout.fs sent and started
#   make roguelike    the same with examples/sdl2_roguelike.fs (images, tunes)
#                     (BOARD=virt: on QEMU virt, -device ramfb, with sound)
#   make disk         out/rpi/disk.img: an SD card image (MBR, FAT32) with the
#                     examples, varvara/ and disk/autoexec.fs (azerty) as autoexec.fs, for DISK=out/rpi/disk.img (needs dosfstools, mtools)
#   make test         boots both under QEMU and checks the answers

ROOT := ../..
OUT ?= $(ROOT)/out/rpi
CROSS ?= aarch64-linux-gnu-
CC := $(CROSS)gcc
OBJCOPY := $(CROSS)objcopy
QEMU ?= qemu-system-aarch64

VERSION := $(shell sed -n "s/.*version='\([^']*\)'.*/\1/p" $(ROOT)/BUILD | head -1)
REVISION := $(shell git rev-parse HEAD 2>/dev/null || echo unknown)

ZOOM ?= 2
CFLAGS := -DCON_ZOOM=$(ZOOM) --specs=picolibc.specs -mcpu=cortex-a53 -O2 -Wall -Werror -Wno-missing-braces \
  -ffreestanding -fno-stack-protector -fno-exceptions -fno-omit-frame-pointer \
  -I $(ROOT) -I $(OUT)
LDFLAGS := -nostartfiles -T rpi.ld -Wl,--no-warn-rwx-segments

SOURCES := start.S main.c builtins.h $(addprefix $(ROOT)/ports/baremetal/,screen.h console.h font8x8.h input.h) virtio.h usb.h sound.h storage.h fat.h rpi.ld
# Images built into the kernel for load-image (no filesystem): PNG files.
ASSETS ?= $(ROOT)/examples/sdl2_tiles.png $(ROOT)/examples/sdl2_tiles_alt.png
FORTH := rpi_boot.fs platform.fs $(wildcard $(ROOT)/ports/baremetal/*.fs) $(wildcard $(ROOT)/common/*.fs)

# virt: RAM at 1G (QEMU's default -m 128M). Pi: loaded at 0x80000 by the
# firmware, the C heap stops at 512M (every Pi 3 and 4 has that much).
VIRT_DEFS := -DRPI_BOARD_VIRT -Wl,--defsym=__load_addr=0x40080000 -Wl,--defsym=__ram_end=0x48000000
PI3_DEFS := -DRPI_BOARD_PI3 -Wl,--defsym=__load_addr=0x80000 -Wl,--defsym=__ram_end=0x20000000
PI4_DEFS := -DRPI_BOARD_PI4 -Wl,--defsym=__load_addr=0x80000 -Wl,--defsym=__ram_end=0x20000000

all: $(OUT)/virt/lugforth.elf $(OUT)/pi3/kernel8.img $(OUT)/pi4/kernel8.img

# Rewritten only when the commit changes, so the banner's revision follows it.
$(OUT)/gen/REVISION: FORCE
	mkdir -p $(OUT)/gen
	printf '%s' '$(REVISION)' | cmp -s - $@ || printf '%s' '$(REVISION)' > $@

$(OUT)/gen/rpi_boot.h: $(FORTH) $(OUT)/gen/REVISION
	printf '%s' '$(REVISION)' | cut -c1-7 > $(OUT)/gen/REVSHORT
	python3 $(ROOT)/tools/importation.py -i rpi_boot.fs -o $@ -I $(ROOT) -I $(OUT) \
	  --name boot --header cpp -DVERSION=$(VERSION) -DSTABLE_VERSION=$(VERSION) \
	  -FREVISION=$(OUT)/gen/REVISION -FREVSHORT=$(OUT)/gen/REVSHORT

$(OUT)/gen/baremetal_assets.h: $(ASSETS) $(ROOT)/tools/baremetal_assets.py
	mkdir -p $(OUT)/gen
	python3 $(ROOT)/tools/baremetal_assets.py -o $@ $(ASSETS)

# Rewritten only when ZOOM changes, so that the kernels are rebuilt then.
$(OUT)/gen/ZOOM: FORCE
	mkdir -p $(OUT)/gen
	printf '%s' '$(ZOOM)' | cmp -s - $@ || printf '%s' '$(ZOOM)' > $@

$(OUT)/virt/lugforth.elf: $(SOURCES) $(OUT)/gen/ZOOM $(OUT)/gen/rpi_boot.h $(OUT)/gen/baremetal_assets.h
	mkdir -p $(dir $@)
	$(CC) $(CFLAGS) $(VIRT_DEFS) $(LDFLAGS) start.S main.c -o $@ -lm

$(OUT)/%/lugforth.elf: $(SOURCES) $(OUT)/gen/ZOOM $(OUT)/gen/rpi_boot.h $(OUT)/gen/baremetal_assets.h
	mkdir -p $(dir $@)
	$(CC) $(CFLAGS) $(if $(filter pi3,$*),$(PI3_DEFS),$(PI4_DEFS)) $(LDFLAGS) \
	  start.S main.c -o $@ -lm

$(OUT)/%/kernel8.img: $(OUT)/%/lugforth.elf
	$(OBJCOPY) -O binary $< $@

run: $(OUT)/virt/lugforth.elf
	$(QEMU) -M virt -cpu cortex-a53 -m 128M -nographic -global virtio-mmio.force-legacy=false -kernel $<

run-pi3: $(OUT)/pi3/kernel8.img
	$(QEMU) -M raspi3b -nographic -no-reboot -kernel $<

run-pty: $(OUT)/virt/lugforth.elf
	$(QEMU) -M virt -cpu cortex-a53 -m 128M -display none -monitor none -serial pty -kernel $<

BOARD ?= pi3
DISK ?=
RUN := python3 $(ROOT)/tools/baremetal_qemu_run.py --board $(BOARD) --out $(OUT) $(if $(DISK),--disk $(DISK))
screen: all
	$(RUN)

gui: all
	$(RUN) --gui

breakout: all
	$(RUN) $(ROOT)/examples/sdl2_breakout.fs

roguelike: all
	$(RUN) $(ROOT)/examples/sdl2_roguelike.fs

disk:
	mkdir -p $(OUT)
	python3 $(ROOT)/tools/baremetal_mkdisk.py $(OUT)/disk.img $(wildcard $(ROOT)/examples/*.fs) disk/autoexec.fs=autoexec.fs \
	  $(ROOT)/ports/posix/editor.fs=lib/editor.fs $(ROOT)/ports/posix/writhdeck.fs=lib/writhdeck.fs \
	  $(ROOT)/ports/posix/eval_copy.fs=lib/eval_copy.fs \
	  $(foreach f,$(wildcard $(ROOT)/examples/varvara/*.fs $(ROOT)/examples/varvara/*.rom),$(f)=varvara/$(notdir $(f)))

test: all
	python3 $(ROOT)/tools/baremetal_qemu_test.py $(OUT)

clean:
	rm -rf $(OUT)

.SECONDARY:
.PHONY: all run run-pi3 run-pty screen gui breakout roguelike disk test clean FORCE
FORCE:
