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

SDL2_EXAMPLES := graphics/sdl2_demo.fs games/sdl2_breakout.fs music/sdl2_drums.fs music/fm/sdl2_fm.fs music/sdl2_pianoroll.fs games/sdl2_roguelike.fs graphics/sdl2_3d.fs games/sdl2_2048.fs games/sdl2_tetris.fs games/sdl2_snake.fs games/sdl2_minesweeper.fs games/sdl2_mastermind.fs games/sdl2_raycaster.fs graphics/sdl2_turtle.fs games/pinball/pinball.fs games/pinball/play_raid.fs games/pinball/play_night.fs system/varvara/varvara.fs system/varvara/launch.fs utils/filer.fs utils/desktop.fs

.PHONY: help bench test-ports \
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
	@echo "make test-ports         tests of the logic of 2048, 3d, inference and the r3 games"
	@echo "make smoke              headless load-test of every SDL2 example (catches load/syntax errors)"
	@echo "make clean              remove built binaries and .rom files"

# ---- bench_sieve: pure computation, no display ----

bench-sieve: bench-sieve-c bench-sieve-fs bench-sieve-uxn

bench/bench_sieve: bench/bench_sieve.c
	$(CC) $(CFLAGS) -o $@ $<

bench-sieve-c: bench/bench_sieve
	@echo "--- C ---"
	@./bench/bench_sieve

bench-sieve-fs:
	@echo "--- lugforth ---"
	@$(LUGFORTH) bench/bench_sieve.fs

bench/bench_sieve.rom: bench/bench_sieve.tal
	$(UXNASM) $< $@

bench-sieve-uxn: bench/bench_sieve.rom
	@echo "--- uxn ---"
	@time $(UXNCLI) $<

# ---- bench_lines: drawing throughput, headless timing ----

bench-lines: bench-lines-c bench-lines-fs bench-lines-uxn

bench/bench_lines: bench/bench_lines.c
	$(CC) $(CFLAGS) -o $@ $< $(SDL2_FLAGS)

bench-lines-c: bench/bench_lines
	@echo "--- C+SDL2 (headless) ---"
	@SDL_VIDEODRIVER=dummy ./bench/bench_lines

bench-lines-fs:
	@echo "--- lugforth+SDL2 (headless) ---"
	@BENCH_HEADLESS=1 SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy $(LUGFORTH) bench/bench_lines.fs

bench/bench_lines.rom: bench/bench_lines.tal
	$(UXNASM) $< $@

bench-lines-uxn: bench/bench_lines.rom
	@echo "--- uxn (headless) ---"
	@time $(UXNCLI) $<

bench: bench-sieve bench-lines

# ---- visual: the same bench_lines, but with a real window left open ----

view-lines:
	$(LUGFORTH) bench/bench_lines.fs

view-lines-c: bench/bench_lines
	./bench/bench_lines

view-lines-uxn: bench/bench_lines.rom
	$(UXNEMU) $<

view: view-lines view-lines-c view-lines-uxn

# ---- smoke test: every interactive SDL2 example loads and runs a while
# without erroring out (catches the "Error including" class of bug found
# repeatedly while writing these: a couple of seconds under a dummy video/
# audio driver is enough to reach the main loop and stay there) ----

# the logic of the ports from eForth Windows (2048, 3d, inference) and of the games
# after r3forth (tetris, snake, minesweeper) and mastermind: each test prints PASS
test-ports:
	@for t in games/2048_tests.fs graphics/3d_tests.fs utils/inference/inference_tests.fs utils/inference/lamp_diagnosis_tests.fs games/games_tests.fs games/raycaster_tests.fs graphics/turtle_tests.fs games/zmachine_tests.fs; do \
	  printf "%-20s " "$$t"; \
	  out=$$(cd .. && SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy $(LUGFORTH:../%=%) examples/$$t 2>&1 | tail -1); echo "$$out"; \
	  case "$$out" in PASS*) ;; *) exit 1;; esac; \
	done

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
	rm -f bench/bench_sieve bench/bench_lines bench/bench_sieve.rom bench/bench_lines.rom bench/bench_sieve.rom.sym bench/bench_lines.rom.sym
