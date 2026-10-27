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

# Lugforth for TIC-80: a wasm32 module, built by clang with no sysroot.
#   make -C ports/tic80          out/tic80/lugforth-tic80.wasm and its C header
#   make -C ports/tic80 test     runs test_host.js on it (node)

ROOT := ../..
OUT ?= $(ROOT)/out/tic80
CLANG ?= clang
NODE ?= node

VERSION := $(shell sed -n "s/.*version='\([^']*\)'.*/\1/p" $(ROOT)/BUILD | head -1)
REVISION := $(shell git rev-parse HEAD 2>/dev/null || echo unknown)

CFLAGS := --target=wasm32 -mcpu=mvp -O2 -Wall -Werror -Wno-missing-braces -std=gnu11 \
  -ffreestanding -nostdlibinc -fno-builtin -I include -I $(ROOT) -I $(OUT)
LDFLAGS := -nostdlib -Wl,--no-entry -Wl,--export-dynamic -Wl,--strip-all \
  -Wl,--allow-undefined-file=imports.txt -Wl,-z,stack-size=65536

FORTH := tic80_boot.fs platform.fs allocation.fs autoboot.fs $(wildcard $(ROOT)/common/*.fs)
SOURCES := main.c libc.h builtins.h interp.h api.h $(wildcard include/*.h) \
  $(ROOT)/common/tier0_opcodes.h $(ROOT)/common/tier1_opcodes.h $(ROOT)/common/tier2_opcodes.h \
  $(ROOT)/common/floats.h $(ROOT)/common/core.h $(ROOT)/common/bits.h

all: $(OUT)/lugforth-tic80.wasm $(OUT)/lugforth-tic80.wasm.h

$(OUT)/gen/REVISION: FORCE
	mkdir -p $(OUT)/gen
	printf '%s' '$(REVISION)' | cmp -s - $@ || printf '%s' '$(REVISION)' > $@

$(OUT)/gen/tic80_boot.h: $(FORTH) $(ROOT)/BUILD $(OUT)/gen/REVISION
	printf '%s' '$(REVISION)' | cut -c1-7 > $(OUT)/gen/REVSHORT
	python3 $(ROOT)/tools/importation.py -i tic80_boot.fs -o $@ -I $(ROOT) -I $(OUT) \
	  --name boot --header c -DVERSION=$(VERSION) -DSTABLE_VERSION=$(VERSION) \
	  -FREVISION=$(OUT)/gen/REVISION -FREVSHORT=$(OUT)/gen/REVSHORT

$(OUT)/lugforth-tic80.wasm: $(SOURCES) imports.txt $(OUT)/gen/tic80_boot.h
	$(CLANG) $(CFLAGS) $(LDFLAGS) main.c -o $@

# The module as a C array, for a host that links it in (TIC-80: src/api/lugforth.c).
$(OUT)/lugforth-tic80.wasm.h: $(OUT)/lugforth-tic80.wasm
	python3 -c "import sys; d=open(sys.argv[1],'rb').read(); \
	  print(',\n'.join(','.join('0x%02x' % b for b in d[i:i+16]) for i in range(0, len(d), 16)))" $< > $@

test: $(OUT)/lugforth-tic80.wasm
	$(NODE) test_host.js $(OUT)/lugforth-tic80.wasm

clean:
	rm -rf $(OUT)

.PHONY: all test clean FORCE
