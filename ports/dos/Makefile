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

# Lugforth for DOS (386 and up, 32-bit protected mode), built with the DJGPP
# cross compiler. Debian and Ubuntu have no package for it (binutils-djgpp
# only): `make djgpp` (ports/dos/install-djgpp.sh) installs the prebuilt one of
# https://github.com/andrewwutw/build-djgpp in /opt/djgpp-x or ~/.local/djgpp-x,
# where it is looked for; elsewhere, DJGPP=its djgpp directory. Tested with
# DOSBox and DOSBox-X (apt install dosbox dosbox-x).
#
#   make djgpp        installs the DJGPP cross compiler
#   make              out/dos/lugforth.exe
#   make hdpmi        out/dos/HDPMI32.EXE, the DPMI host of the HX DOS extender
#                     (freeware, https://github.com/Baron-von-Riedesel/HX): DOSBox
#                     has none; on a real DOS, CWSDPMI.EXE or HDPMI32 works
#   make stage        out/dos/disk: the program, the examples, ed, writhdeck, the
#                     manual, every name cut to 8.3 (tools/dos_stage.py)
#   make run          DOSBox on out/dos/disk (a window)
#   make test         runs it in DOSBox without a window and checks the answers

ROOT := ../..
OUT ?= $(ROOT)/out/dos
DJGPP ?= $(firstword $(wildcard /opt/djgpp-x/djgpp $(HOME)/.local/djgpp-x/djgpp) /opt/djgpp-x/djgpp)
CC := $(DJGPP)/bin/i586-pc-msdosdjgpp-gcc
DOSBOX ?= dosbox

VERSION := $(shell sed -n "s/.*version='\([^']*\)'.*/\1/p" $(ROOT)/BUILD | head -1)
REVISION := $(shell git rev-parse HEAD 2>/dev/null || echo unknown)

CFLAGS := -O2 -Wall -Werror -Wno-unused-function -Wno-unused-variable -Wno-missing-braces -std=gnu11 -march=i386 -mtune=i686 -I $(ROOT) -I $(OUT)
SOURCES := main.c files.h builtins.h video.h sb16.h $(ROOT)/ports/baremetal/builtins.h \
  $(ROOT)/ports/baremetal/screen.h $(ROOT)/ports/baremetal/input.h $(ROOT)/ports/baremetal/mixer.h \
  $(ROOT)/ports/baremetal/font8x8.h $(ROOT)/ports/baremetal/term.h
ASSETS ?= $(ROOT)/examples/games/gfx_tiles.png $(ROOT)/examples/games/gfx_tiles_alt.png
FORTH := dos_boot.fs platform.fs autoboot.fs $(ROOT)/ports/baremetal/allocation.fs \
  $(ROOT)/ports/baremetal/files.fs $(ROOT)/ports/baremetal/sdl2.fs $(wildcard $(ROOT)/common/*.fs)

all: $(OUT)/lugforth.exe

$(OUT)/gen/REVISION: FORCE
	mkdir -p $(OUT)/gen
	printf '%s' '$(REVISION)' | cmp -s - $@ || printf '%s' '$(REVISION)' > $@

$(OUT)/gen/dos_boot.h: $(FORTH) $(OUT)/gen/REVISION
	printf '%s' '$(REVISION)' | cut -c1-7 > $(OUT)/gen/REVSHORT
	python3 $(ROOT)/tools/importation.py -i dos_boot.fs -o $@ -I $(ROOT) -I $(OUT) \
	  --name boot --header cpp -DVERSION=$(VERSION) -DSTABLE_VERSION=$(VERSION) \
	  -FREVISION=$(OUT)/gen/REVISION -FREVSHORT=$(OUT)/gen/REVSHORT

$(OUT)/gen/baremetal_assets.h: $(ASSETS) $(ROOT)/tools/baremetal_assets.py
	mkdir -p $(OUT)/gen
	python3 $(ROOT)/tools/baremetal_assets.py -o $@ $(ASSETS)

$(OUT)/lugforth.exe: $(SOURCES) $(OUT)/gen/dos_boot.h $(OUT)/gen/baremetal_assets.h
	@test -x $(CC) || { echo "No DJGPP compiler at $(CC): run make -C ports/dos djgpp (or give DJGPP=)"; exit 1; }
	$(CC) $(CFLAGS) main.c -o $@ -lm

djgpp:
	sh install-djgpp.sh

HX_ZIP := https://github.com/Baron-von-Riedesel/HX/releases/download/v2.23/HXRT223.zip
hdpmi: $(OUT)/HDPMI32.EXE
$(OUT)/HDPMI32.EXE:
	mkdir -p $(OUT)/hx
	curl -sSL -o $(OUT)/hx/hxrt.zip $(HX_ZIP)
	cd $(OUT)/hx && unzip -o -q hxrt.zip BIN/HDPMI32.EXE
	cp $(OUT)/hx/BIN/HDPMI32.EXE $@

stage: $(OUT)/lugforth.exe $(OUT)/HDPMI32.EXE
	python3 $(ROOT)/tools/dos_stage.py $(OUT)

run: stage
	$(DOSBOX) -c "mount c $(abspath $(OUT))/disk" -c "c:" -c "hdpmi32 -r" -c "lugforth"

test: stage
	python3 $(ROOT)/tools/dos_test.py $(OUT)

FORCE:
.PHONY: all djgpp hdpmi stage run test FORCE
