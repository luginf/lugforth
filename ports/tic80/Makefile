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
#   make -C ports/tic80 c        the module as C, by wasm2c (WABT): what TIC-80 compiles

ROOT := ../..
OUT ?= $(ROOT)/out/tic80
CLANG ?= clang
NODE ?= node
WASM2C ?= wasm2c

VERSION := $(shell sed -n "s/.*version='\([^']*\)'.*/\1/p" $(ROOT)/BUILD | head -1)
# The revision the module says it was built from. A tree with uncommitted changes (tracked files)
# makes it end in -dirty, and `make c` refuses to translate such a module: the C that TIC-80
# compiles must come from a committed revision, so that anyone can build it again.
DIRTY := $(shell git status --porcelain --untracked-files=no 2>/dev/null)
REVISION := $(shell git rev-parse HEAD 2>/dev/null || echo unknown)$(if $(DIRTY),-dirty)
REVSHORT := $(shell git rev-parse --short=7 HEAD 2>/dev/null || echo unknown)$(if $(DIRTY),-dirty)

CFLAGS := --target=wasm32 -mcpu=mvp -O2 -Wall -Werror -Wno-missing-braces -std=gnu11 \
  -ffreestanding -nostdlibinc -fno-builtin -I include -I $(ROOT) -I $(OUT)
# The module may grow its memory to 16 MiB, no more: the limit is in the module,
# where wasm2c and wasm3 both enforce it.
LDFLAGS := -nostdlib -Wl,--no-entry -Wl,--export-dynamic -Wl,--strip-all \
  -Wl,--allow-undefined-file=imports.txt -Wl,-z,stack-size=65536 -Wl,--max-memory=16777216

FORTH := tic80_boot.fs platform.fs allocation.fs autoboot.fs $(wildcard $(ROOT)/common/*.fs)
SOURCES := main.c libc.h builtins.h interp.h api.h $(wildcard include/*.h) \
  $(ROOT)/common/tier0_opcodes.h $(ROOT)/common/tier1_opcodes.h $(ROOT)/common/tier2_opcodes.h \
  $(ROOT)/common/floats.h $(ROOT)/common/core.h $(ROOT)/common/bits.h

all: $(OUT)/lugforth-tic80.wasm $(OUT)/lugforth-tic80.wasm.h

$(OUT)/gen/REVISION: FORCE
	mkdir -p $(OUT)/gen
	printf '%s' '$(REVISION)' | cmp -s - $@ || printf '%s' '$(REVISION)' > $@

$(OUT)/gen/tic80_boot.h: $(FORTH) $(ROOT)/BUILD $(OUT)/gen/REVISION
	printf '%s' '$(REVSHORT)' > $(OUT)/gen/REVSHORT
	python3 $(ROOT)/tools/importation.py -i tic80_boot.fs -o $@ -I $(ROOT) -I $(OUT) \
	  --name boot --header c -DVERSION=$(VERSION) -DSTABLE_VERSION=$(VERSION) \
	  -FREVISION=$(OUT)/gen/REVISION -FREVSHORT=$(OUT)/gen/REVSHORT

$(OUT)/lugforth-tic80.wasm: $(SOURCES) imports.txt $(OUT)/gen/tic80_boot.h
	$(CLANG) $(CFLAGS) $(LDFLAGS) main.c -o $@

# The module as a C array, for a host that links it in (TIC-80: src/api/lugforth.c).
$(OUT)/lugforth-tic80.wasm.h: $(OUT)/lugforth-tic80.wasm
	python3 -c "import sys; d=open(sys.argv[1],'rb').read(); \
	  print(',\n'.join(','.join('0x%02x' % b for b in d[i:i+16]) for i in range(0, len(d), 16)))" $< > $@

# The module translated to C: TIC-80 compiles it with the runtime of wasm2c, so
# the Forth runs natively and every access still goes through the bounds check
# of the WebAssembly memory. Needs wasm2c (package wabt).
c: check-clean $(OUT)/lugforth_w2c.c $(OUT)/VERSION

check-clean:
	@test -z "$(DIRTY)" || test -n "$(ALLOW_DIRTY)" || \
	  { echo "make c: the tree has uncommitted changes, commit first (ALLOW_DIRTY=1 to try anyway)"; exit 1; }

$(OUT)/lugforth_w2c.c: $(OUT)/lugforth-tic80.wasm
	$(WASM2C) $< -n lugforth -o $@

# The two lines to copy to VERSION in TIC-80: the version, then the full commit it was built from.
$(OUT)/VERSION: $(OUT)/lugforth_w2c.c
	printf '%s\n%s\n' '$(VERSION)' '$(REVISION)' > $@

test: $(OUT)/lugforth-tic80.wasm
	$(NODE) test_host.js $(OUT)/lugforth-tic80.wasm

clean:
	rm -rf $(OUT)

.PHONY: all c check-clean test clean FORCE
