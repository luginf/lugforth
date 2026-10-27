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

# Lugforth as a UEFI application for x86-64 PCs (BOOTX64.EFI). Needs gcc,
# gnu-efi (Debian/Ubuntu: apt install gnu-efi), and to run it ovmf and
# qemu-system-x86; dosfstools and mtools for the disk images.
#
#   make              out/efi/BOOTX64.EFI
#   make usb          out/efi/usb.img: a USB stick image (GPT-less MBR, FAT32,
#                     EFI/BOOT/BOOTX64.EFI, the examples, ed and writhdeck)
#   make run          QEMU with OVMF on that image: a window, and the serial
#                     line in this terminal (Ctrl-A X quits)
#   make test         boots it under QEMU and checks the answers

ROOT := ../..
OUT ?= $(ROOT)/out/efi
QEMU ?= qemu-system-x86_64
OVMF ?= /usr/share/OVMF/OVMF_CODE_4M.fd
EFIINC := /usr/include/efi
EFILIB := /usr/lib

VERSION := $(shell sed -n "s/.*version='\([^']*\)'.*/\1/p" $(ROOT)/BUILD | head -1)
REVISION := $(shell git rev-parse HEAD 2>/dev/null || echo unknown)

CFLAGS := -O2 -Wall -Werror -Wno-missing-braces -std=gnu11 \
  -fpic -ffreestanding -fno-stack-protector -fno-stack-check -fshort-wchar -mno-red-zone \
  -maccumulate-outgoing-args -fno-builtin -U_FORTIFY_SOURCE -D_FORTIFY_SOURCE=0 \
  -DGNU_EFI_USE_MS_ABI -I $(EFIINC) -I $(EFIINC)/x86_64 -I $(ROOT) -I $(OUT)
LDFLAGS := -nostdlib -znocombreloc -T $(EFILIB)/elf_x86_64_efi.lds -shared -Bsymbolic \
  -L $(EFILIB) $(EFILIB)/crt0-efi-x86_64.o

SOURCES := main.c libc.h files.h builtins.h start.S \
  $(ROOT)/ports/baremetal/builtins.h $(ROOT)/ports/baremetal/screen.h $(ROOT)/ports/baremetal/console.h $(ROOT)/ports/baremetal/input.h $(ROOT)/ports/baremetal/font8x8.h $(ROOT)/ports/baremetal/mixer.h hda.h $(ROOT)/ports/baremetal/term.h $(ROOT)/ports/baremetal/term_run.h
# The examples keep their folders on the disk (games/, graphics/, music/, utils/, system/varvara/)
EXAMPLE_FILES := $(foreach c,games graphics music utils,$(foreach f,$(wildcard $(ROOT)/examples/$(c)/*.fs),$(f)=$(c)/$(notdir $(f))))

ASSETS ?= $(ROOT)/examples/games/sdl2_tiles.png $(ROOT)/examples/games/sdl2_tiles_alt.png
FORTH := efi_boot.fs platform.fs sound_ini.fs $(wildcard $(ROOT)/ports/baremetal/*.fs) $(wildcard $(ROOT)/common/*.fs)

all: $(OUT)/BOOTX64.EFI

$(OUT)/gen/REVISION: FORCE
	mkdir -p $(OUT)/gen
	printf '%s' '$(REVISION)' | cmp -s - $@ || printf '%s' '$(REVISION)' > $@

$(OUT)/gen/efi_boot.h: $(FORTH) $(OUT)/gen/REVISION
	printf '%s' '$(REVISION)' | cut -c1-7 > $(OUT)/gen/REVSHORT
	python3 $(ROOT)/tools/importation.py -i efi_boot.fs -o $@ -I $(ROOT) -I $(OUT) \
	  --name boot --header cpp -DVERSION=$(VERSION) -DSTABLE_VERSION=$(VERSION) \
	  -FREVISION=$(OUT)/gen/REVISION -FREVSHORT=$(OUT)/gen/REVSHORT

$(OUT)/gen/baremetal_assets.h: $(ASSETS) $(ROOT)/tools/baremetal_assets.py
	mkdir -p $(OUT)/gen
	python3 $(ROOT)/tools/baremetal_assets.py -o $@ $(ASSETS)

$(OUT)/main.o: $(SOURCES) $(OUT)/gen/efi_boot.h $(OUT)/gen/baremetal_assets.h
	$(CC) $(CFLAGS) -c main.c -o $@

$(OUT)/start.o: start.S
	mkdir -p $(OUT)
	$(CC) $(CFLAGS) -c start.S -o $@

$(OUT)/lugforth.so: $(OUT)/main.o $(OUT)/start.o
	ld $(LDFLAGS) $^ -o $@ -lgnuefi $(shell $(CC) -print-libgcc-file-name)

$(OUT)/BOOTX64.EFI: $(OUT)/lugforth.so
	objcopy -j .text -j .sdata -j .data -j .rodata -j .dynamic -j .dynsym -j .rel \
	  -j .rela -j '.rel.*' -j '.rela.*' -j .reloc --target efi-app-x86_64 --subsystem=10 $< $@

# The USB stick: the application where the firmware looks for it, and the
# files the console uses (the examples, the editors, varvara).
USB_FILES := $(OUT)/BOOTX64.EFI=EFI/BOOT/BOOTX64.EFI \
  $(EXAMPLE_FILES) $(ROOT)/ports/rpi/disk/autoexec.fs=autoexec.fs \
  $(ROOT)/editors/editor.fs=lib/editor.fs $(ROOT)/editors/writhdeck.fs=lib/writhdeck.fs \
  $(ROOT)/ports/posix/eval_copy.fs=lib/eval_copy.fs \
  $(foreach f,MANUAL.md WORDS.md SDL2.md SDL2-words.md SDL2-web.md,$(ROOT)/docs/$(f)=docs/$(f)) $(foreach f,MANUAL_fr.md SDL2_fr.md SDL2-words_fr.md SDL2-web_fr.md,$(ROOT)/docs/fr/$(f)=docs/$(f)) $(ROOT)/ports/efi/disk/drivers.fs=drivers.fs \
  $(foreach f,$(wildcard $(ROOT)/examples/games/zmachine/*.z3 $(ROOT)/examples/games/zmachine/*.z5),$(f)=games/zmachine/$(notdir $(f))) \
  $(foreach f,$(wildcard $(ROOT)/examples/system/varvara/*.fs $(ROOT)/examples/system/varvara/*.rom),$(f)=system/varvara/$(notdir $(f)))

usb: $(OUT)/BOOTX64.EFI
	python3 $(ROOT)/tools/baremetal_mkdisk.py $(OUT)/usb.img $(USB_FILES)

# The same files as a tree to copy onto an existing FAT32 stick (make stage
# DEST=/media/me/STICK also works): EFI/BOOT/BOOTX64.EFI, lib/, varvara/...
DEST ?= /tmp/lugforth
stage: $(OUT)/BOOTX64.EFI
	@for f in $(USB_FILES); do \
	  src=$${f%%=*}; dst=$${f#*=}; [ "$$dst" = "$$f" ] && dst=$$(basename $$src); \
	  mkdir -p $(DEST)/$$(dirname $$dst); cp $$src $(DEST)/$$dst; \
	done
	@echo "stick tree in $(DEST)"

# The sound goes to the host through AUDIODEV (pa, pipewire, alsa, sdl...):
# make run AUDIODEV=alsa
AUDIODEV ?= pa
run: usb
	$(QEMU) -machine q35 -m 512M -drive if=pflash,format=raw,readonly=on,file=$(OVMF) \
	  -drive if=none,id=stick,format=raw,file=$(OUT)/usb.img -device qemu-xhci -device usb-storage,drive=stick \
	  -device usb-kbd -audiodev $(AUDIODEV),id=snd0 -device intel-hda -device hda-duplex,audiodev=snd0 \
	  -serial mon:stdio

test: all
	python3 $(ROOT)/tools/efi_qemu_test.py $(OUT)

clean:
	rm -rf $(OUT)

.PHONY: all usb stage run test clean FORCE
FORCE:
