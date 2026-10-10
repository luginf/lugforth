# Build and test the examples: the two benchmarks (bench_sieve, bench_lines
# -- lugforth vs plain C vs uxn) and a headless load-test of the SDL2 demos.
#
#   make bench            build + run both benchmarks, all three languages
#   make view-lines        watch bench_lines draw, in a real window (lugforth)
#   make smoke             headless load-test of every SDL2 example
#   make help              list all targets
#
# Needs, depending on the target: a C compiler + SDL2 dev headers (pkg-config
# sdl2), uxnasm/uxncli/uxnemu (https://100r.co/site/uxn.html), and
# ../out/posix/lugforth built (ninja posix at the repo root).

LUGFORTH   ?= ../out/posix/lugforth
CC        ?= cc
CFLAGS    ?= -O2
SDL2_FLAGS := $(shell pkg-config --cflags --libs sdl2 2>/dev/null)
UXNASM    ?= uxnasm
UXNCLI    ?= uxncli
UXNEMU    ?= uxnemu
SMOKE_TIMEOUT ?= 5

SDL2_EXAMPLES := sdl2_demo.fs sdl2_breakout.fs sdl2_drums.fs sdl2_pianoroll.fs sdl2_roguelike.fs

.PHONY: help bench \
        bench-sieve bench-sieve-c bench-sieve-fs bench-sieve-uxn \
        bench-lines bench-lines-c bench-lines-fs bench-lines-uxn \
        view-lines view-lines-c view-lines-uxn view \
        smoke clean

help:
	@echo "make bench             build + run both benchmarks, all three languages"
	@echo "make bench-sieve        just the pure-computation benchmark (sieve of Eratosthenes)"
	@echo "make bench-lines        just the drawing-throughput benchmark (headless, for timing)"
	@echo "make view-lines         bench_lines.fs in a real SDL2 window (lugforth)"
	@echo "make view-lines-c       bench_lines.c in a real SDL2 window (C)"
	@echo "make view-lines-uxn     bench_lines.tal in uxnemu (real window)"
	@echo "make view              all three of the above, one after another"
	@echo "make smoke              headless load-test of every SDL2 example (catches load/syntax errors)"
	@echo "make clean              remove built binaries and .rom files"

# ---- bench_sieve: pure computation, no display ----

bench-sieve: bench-sieve-c bench-sieve-fs bench-sieve-uxn

bench_sieve: bench_sieve.c
	$(CC) $(CFLAGS) -o $@ $<

bench-sieve-c: bench_sieve
	@echo "--- C ---"
	@./bench_sieve

bench-sieve-fs:
	@echo "--- lugforth ---"
	@$(LUGFORTH) bench_sieve.fs

bench_sieve.rom: bench_sieve.tal
	$(UXNASM) $< $@

bench-sieve-uxn: bench_sieve.rom
	@echo "--- uxn ---"
	@time $(UXNCLI) $<

# ---- bench_lines: drawing throughput, headless timing ----

bench-lines: bench-lines-c bench-lines-fs bench-lines-uxn

bench_lines: bench_lines.c
	$(CC) $(CFLAGS) -o $@ $< $(SDL2_FLAGS)

bench-lines-c: bench_lines
	@echo "--- C+SDL2 (headless) ---"
	@SDL_VIDEODRIVER=dummy ./bench_lines

bench-lines-fs:
	@echo "--- lugforth+SDL2 (headless) ---"
	@BENCH_HEADLESS=1 SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy $(LUGFORTH) bench_lines.fs

bench_lines.rom: bench_lines.tal
	$(UXNASM) $< $@

bench-lines-uxn: bench_lines.rom
	@echo "--- uxn (headless) ---"
	@time $(UXNCLI) $<

bench: bench-sieve bench-lines

# ---- visual: the same bench_lines, but with a real window left open ----

view-lines:
	$(LUGFORTH) bench_lines.fs

view-lines-c: bench_lines
	./bench_lines

view-lines-uxn: bench_lines.rom
	$(UXNEMU) $<

view: view-lines view-lines-c view-lines-uxn

# ---- smoke test: every interactive SDL2 example loads and runs a while
# without erroring out (catches the "Error including" class of bug found
# repeatedly while writing these: a couple of seconds under a dummy video/
# audio driver is enough to reach the main loop and stay there) ----

smoke:
	@for f in $(SDL2_EXAMPLES); do \
	  printf "%-22s " "$$f"; \
	  log=$$(mktemp); \
	  SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy timeout $(SMOKE_TIMEOUT) $(LUGFORTH) $$f >"$$log" 2>&1; \
	  code=$$?; \
	  if [ $$code = 124 ]; then echo "ok (reached the main loop)"; \
	  elif [ $$code = 0 ]; then echo "ok (exited cleanly)"; \
	  else echo "FAIL (exit $$code):"; sed 's/^/    /' "$$log"; fi; \
	  rm -f "$$log"; \
	done

clean:
	rm -f bench_sieve bench_lines bench_sieve.rom bench_lines.rom bench_sieve.rom.sym bench_lines.rom.sym
