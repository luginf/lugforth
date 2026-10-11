# SKILLS.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

µEforth is an EForth-inspired Forth implementation bootstrapped from a minimalist C kernel. It targets multiple platforms: POSIX/Linux, ESP32 (Arduino), pico-ice, Web (WASM/JS), and Windows.

[WORDS.md](WORDS.md) is a generated reference of the words of the `forth` vocabulary and the other Linux/web vocabularies, with stack effects and descriptions where known (`tools/words_reference.py`, run it again after adding words). SDL2 words have their own reference, [ports/posix/SDL2-words.md](ports/posix/SDL2-words.md).

## Build System

The build uses a two-step process: `configure.py` generates `build.ninja`, then `ninja` builds.

```sh
./configure.py        # detect tools, generate build.ninja
ninja                 # build all enabled platforms

./configure.py -f     # fast config, excludes slower components (web, windows)
```

Individual platform targets:
```sh
ninja posix
ninja esp32
ninja pico-ice
ninja win32
ninja win64
ninja web
ninja install         # install posix build to /usr/bin/lugforth
```

ESP32 flash targets: `ninja esp32-flash`, `ninja esp32s2-flash`, `ninja esp32s3-flash`, `ninja esp32c3-flash`, `ninja esp32cam-flash`. Set `PORT=com3` etc. to select board.

Required packages: `sudo apt install ninja-build gcc-arm-none-eabi` (arm tools needed for pico-ice; nodejs for web).

## Tests

Tests run as part of the platform build (e.g., `ninja posix` compiles and runs all POSIX tests). There is no separate test command.

To run tests manually after building:
```sh
out/posix/lugforth common/all_tests.fs   # run all common tests
out/posix/lugforth common/base_tests.fs  # run a single test file
```

Each test file in `common/*_tests.fs` defines words prefixed `test-`. The testing framework (`common/testing.fs`) discovers and runs all `test-*` words in the current vocabulary using `run-tests`. Key assertion words: `=assert`, `assert`, `<assert`, `>assert`.

## Architecture

### C Kernel + Forth Bootstrap

The system is a threaded-code interpreter written in C with platform-specific extensions, bootstrapped by Forth source files compiled into the binary as C headers.

**C layer** (`common/`):
- `tier0_opcodes.h` — primitive cell/stack types and low-level macros (PUSH, DROP, DUP, PARK/UNPARK, THROWIT)
- `tier1_opcodes.h`, `tier2_opcodes.h` — higher-level built-in opcodes
- `interp.h` — the interpreter loop using GNU computed-goto dispatch (`goto **(void **) w`)
- `core.h` — dictionary lookup, number conversion, word header layout
- `bits.h` — cell size helpers
- `floats.h`, `calls.h` — float and foreign-call opcodes

Each platform defines `PLATFORM_OPCODE_LIST` (its own C macros for platform-specific opcodes) and `VOCABULARY_LIST`, then `#include`s the common headers. For example, `ports/posix/main.c` (actually `ports/posix/interp.h`) adds `DLSYM`, `ERRNO`, and `CALLING_OPCODE_LIST`.

**Forth bootstrap layer**:
Forth source is never loaded at runtime from files — it is compiled into the binary as a C `const char[]` header using `tools/importation.py`. For POSIX: `ports/posix/posix_boot.fs` → `out/gen/posix_boot.h`.

Boot sequence (platform e.g. posix):
1. `common/phase1.fs` — core Forth words (control flow, recognizers, vocabularies, floats, structures)
2. Platform-specific `ports/posix/posix.fs` — POSIX syscall bindings
3. `common/phase2.fs` — higher-level utilities, code generation, locals, case
4. `common/phase_filetools.fs`, `common/phase_desktop.fs` — file I/O, desktop integration
5. `common/fini.fs` — transfers builtins into dictionary, sets up save/restore baseline, executes autoboot xt

### The `importation.py` Tool

`tools/importation.py` is the build-time preprocessor. It:
- Resolves `needs filename.fs` directives (like include-once for Forth files)
- Resolves `#include "file.h"` in C/C++ files
- Performs `-D` string substitutions (e.g., `VERSION`, `STABLE_VERSION`)
- Performs `-F` file-content substitutions (e.g., `REVISION`)
- Can emit C headers wrapping the assembled content (`--name`, `--header` flags)

### BUILD Files

`configure.py` evaluates `BUILD` files (Python DSL) to generate `build.ninja`. The root `BUILD` calls `Include('ports/posix')`, `Include('ports/esp32')`, etc. Each platform `BUILD` uses functions like `Importation()`, `Compile()`, `ForthTest()`, `Alias()`, `Default()`.

`ForthTest(target, forth_binary, test_file)` runs a Forth test by executing `interp forth test` and capturing output to `target`.

### Platform Structure

- `common/` — shared Forth source and C headers used by all platforms
- `ports/posix/` — Linux/macOS: C entry point, POSIX bindings, sockets, X11, pthreads
- `ports/esp32/` — Arduino `.ino` + C++ builtins; optional extensions in `ports/esp32/optional/`
- `ports/pico-ice/` — RP2040-based FPGA board
- `ports/web/` — JavaScript/WASM target
- `ports/windows/` — Win32 build via MSVC cross-compilation from WSL
- `tools/` — build-time Python scripts
- `docs/site/` — documentation website sources

## SDL2 multimedia (branch `sdl2`)

Short game-oriented words on top of libSDL2, for the POSIX build and (same source, same words) the web build. Full docs: `ports/posix/SDL2.md`, `ports/posix/SDL2-words.md` (every word), `ports/web/SDL2.md`. Examples: `examples/sdl2_demo.fs`, `examples/sdl2_breakout.fs` and `examples/sdl2_drums.fs` (a 16 step drum machine); these two also run unchanged in the browser through `ports/web/sdl2_breakout.html` and `ports/web/sdl2_drums.html`. `examples/sdl2_roguelike.fs` uses the tile sheet `examples/sdl2_tiles.png` (drawn by `examples/make_sdl2_tiles.fs`, a lugforth program that writes the PNG by hand with CRC-32, Adler-32 and a stored zlib block: no outside assets; `examples/make_sdl2_tiles.py` is the same program in Python, kept to compare the two, same pixels) through `sdl2-image` (`draw-part`, `image-tint`); `ports/web/sdl2.fs` has the same image words on canvas and `ports/web/sdl2_roguelike.html` runs the same file. `load-image` retries a missing name without its directories on both platforms. `examples/sdl2_pianoroll.fs` is a piano-roll sequencer (4 tracks, chainable 16-step patterns, MIDI+soundfont playback, `.mid` and ABC export); POSIX only, not ported to the web build.

### Files

- `ports/posix/sdl2.fs` - core, lazy loaded: typing `sdl2` compiles the vocabulary. Extras loaded the same way: `sdl2-image` (`ports/posix/sdl2_image.fs`), `sdl2-ttf`, `sdl2-mixer`. All are pulled in by `ports/posix/posix_boot.fs`.
- `ports/web/sdl2.fs` - same vocabulary name and same words on a canvas (JS state in `context.sdl`, `JSWORD:` primitives). Only the short words exist there: no raw `SDL_` words, no image/ttf/mixer, no `wait sdl-error window-handle renderer pen sample-rate`. Pulled in by `ports/web/web_boot.fs`; `ports/web/BUILD` copies the example pages and files to `out/web`.
- Tests are NOT in the ninja build. Run by hand:
  `SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy out/posix/lugforth ports/posix/sdl2_tests.fs` (22 tests) `ports/posix/sdl2_media_tests.fs` (7 tests) and `ports/posix/sdl2_abc_tests.fs` (18 tests). `common/forth_namespace_tests.fs` lists the new vocabularies.

### Music: soundfont and ABC tunes

- `ports/posix/sdl2_mixer.fs` has `soundfont ( a n -- )` and `soundfont?` (`Mix_SetSoundFonts`): a `.sf2` or `.sf3` file, looked for as given, then by base name in the current directory, then in the directory of the script (`1 argv`). FluidSynth reads only files, so a soundfont cannot come from memory. Loading a MIDI from memory (`SDL_RWFromConstMem`, `Mix_LoadMUS_RW`) failed with "Unrecognized audio format" and was not pursued.
- `sdl2-abc` (`ports/posix/sdl2_abc.fs`, `ports/web/sdl2_abc.fs`) reads tunes in ABC notation and plays them. The reader is shared: `common/abc.fs` defines `abc-parser` in `internals`, a word that returns the reader's source as a string, which each platform loader evaluates (words in vocabulary `abc-int`, public ones in `sdl2`). POSIX writes a standard MIDI file to a temporary file made by `mkstemps` (a fixed name in `/tmp` failed when another user had made it), deletes it once loaded and plays it with the mixer; the web loader schedules WebAudio oscillators. Tests: `ports/posix/sdl2_abc_tests.fs` (18). A tune is written with `r~ ... ~` because ABC uses `|`.
- In a loader (a colon definition compiled at boot), a name such as `sdl2` or `sdl2-mixer` written outside the evaluated string is bound to the loader that existed then: running it again reloads everything and creates a new empty vocabulary. Put such names in a string run by `evaluate`.
- `2swap` and `0>` do not exist; a control structure (`if`, `begin`) outside a definition is not valid, even inside an `evaluate`d string.
- Locals declared with `{ }` inside an `if` branch corrupt the return stack when the branch is skipped (a silent -9); declare them at the top of the word or use values.
- `dump-file` takes the data first and the file name last.

### Gotchas (all hit while writing it)

- lugforth ignores case. `SDL_Quit` (function) and `SDL_QUIT` (event constant) are the same name, so the function is bound as `sdl-shutdown`. Check new bindings for such collisions.
- FFI (`shared-library` + `sofunc`) passes cells only: no float or struct by value, no C to Forth callbacks (audio uses `SDL_QueueAudio`, not a callback).
- `beep` queues a whole tone, so beeps never overlap. `drum-hit` (percussive, 8 voices, `pitch-drop`) is mixed in Forth by `pump`, which `drum-hit`, `flip` and `delay` call through `audio-hook` and which keeps about 50 ms queued: a loop that never calls `flip` or `delay` gets no sound from hits. Web `drum-hit` is just a WebAudio node started at once.
- A `|` anywhere in an `r|` block (even in a stack comment like `( -- img | 0 )`, or a JS `||`) ends the string early and hangs the boot or the page ("ERROR: | NOT FOUND!").
- Locals declared with `{ }` inside a loop body break (silent "Error including"): declare them at the top of the word. `SDL_Color` works because it fits in one register.
- Every C call pushes a result, even `void` ones: `drop` it. `int` results need `sign-extend`.
- Lazy loaders are `: name r| ...source... | evaluate ;`, so the source must not contain `|`. They end with `sdl2 definitions` so user words land in vocabulary `sdl2` and are not shadowed by library names (`px`...).
- `do` runs ~65k times when the count is 0: use `?do`. `roll`, `0>`, `2constant`, `within` do not exist. But `?do` only skips cleanly when `start = limit` (a count of 0); `start > limit` is not a safe skip and can run away (seen with `limit start dup * ?do`, where `start` is a squared value that can exceed `limit`) or corrupt memory instead of doing nothing: guard explicitly (`start limit < if ... then`) whenever start could legitimately exceed limit, don't rely on `?do` for that case.
- A `{ }` local named the same as a builtin word (e.g. `{ pat trk anchor from to }`) silently shadows it for the rest of that word: `from 1+ to et-c` then just pushes two numbers instead of assigning, with no error at all (`et-c` quietly keeps its old value). Found in `examples/sdl2_pianoroll.fs` (a local called `to` broke its own `to et-c`, making a drag-to-extend-a-held-note gesture always extend back to column 0). Avoid common word names (`to`, `over`, `n`, ...) for locals; when a word misbehaves with no error at all, grep its own local list against the word list first.
- Web: `JSWORD:` bodies also live inside an `r|` block, so no `||` in the JS. `flip` must `yield` (it waits for requestAnimationFrame). `bye` on the web build throws `objects[op] is not a function` (already true for stock `terminal.html`), so the page is inert afterwards: the breakout page shows a "Play again" button that reloads.
- Real mouse events are converted to logical pixels by `SDL_RenderSetLogicalSize`; injected ones (`SDL_PushEvent`) are not. `pressed?` reads the state once per `events`, so a sub-frame tap is missed on POSIX (the web version also catches it).
- Test tricks: `SDL_RenderReadPixels` to check pixels, `SDL_PushEvent` to fake input, Xvfb + `xdotool` for a real pointer, headless Chrome over CDP for the web page (needs http, not `file://`: `cd out/web && python3 -m http.server`).
- `[char]` is compile-only: fine inside a `:` definition, but used directly (e.g. in a top-level `create ... [char] C , ...`) it throws a silent stack underflow with no message. Use plain `char` outside a definition.
- After `sdl2` (and `sdl2-mixer`, ...) finish loading, `posix` is no longer in the search order for the code that follows (the loader's own `previous`s remove it): a plain example that wants `sysfunc`/`sofunc` directly (e.g. for its own `mkstemps`) needs its own `also posix` first, same as `sdl2_abc.fs`'s loader does internally. **Danger**: that `also posix` stays in effect for the rest of the file and can shift the search order enough that a later word of yours with a common name (`remember`, ...) gets shadowed by an unrelated word already defined in `forth`/`internals` (e.g. `common/filetools.fs` has its own `remember`, for `rm`/`reset`). Calling the wrong word silently corrupts state instead of raising a normal error, and the symptom is a bare "Error including" with no message, from wherever the corrupted stack/memory is next touched, not from the real call site (`ports/posix/faults.h` also converts a real SIGSEGV/SIGBUS/SIGFPE/SIGINT into the same silent throw, so check with `gdb -batch -ex run -ex bt` first: a clean process exit, not a caught signal, points at exactly this). Once found in `examples/sdl2_pianoroll.fs` (its own `remember` collided this way, only visible when the app was launched with a cwd where evaluation order happened to expose it), the fix was to rename the word (`key-remember`). Give any word defined after `also posix` a name unlikely to already exist (or check with `see`/a fresh vocabulary).
- Testing with Xvfb + `xdotool`: `xdotool mousemove --window <id> x y` is window-relative and immune to the window's on-screen position, unlike plain root coordinates with `import -window root` (a window is not always at 0,0: check with `xdotool getwindowgeometry`). A `click` (down+up in one call) can be faster than a frame and get missed; a separate `mousedown`/`sleep 0.2`/`mouseup`, with the pointer already settled a moment before, is reliable. A screenshot taken right after a click can show the pre-click frame (redraw lag): re-screenshot, or check side effects (a growing recorded audio file, a written test file) instead of trusting one image.

### `ports/gba/` (a new-platform port, not SDL2): boot-chain gotchas

Full detail in `ports/gba/README.md`; short version, since these apply to any new
platform's `builtins.h`/`platform.fs`/boot-chain `.fs` file, not just GBA:

- A word compiled into `internals` (via `also internals definitions ...
  previous`) instead of `forth` (via `forth definitions internals ...
  forth`, a bare vocabulary word being a search-order change, not a
  compile-target change) disappears once `internals` drops out of the
  search order: `' default-type` (or whatever) then silently finds
  nothing, and the "not found" error is itself unprintable at that point,
  since `type` is the very word not yet bound. `ports/pico-ice/platform.fs` gets
  this right; copy its exact idiom for a new platform's I/O setup.
- A `\` comment placed *before* the first `needs` line of a boot-chain
  **entry** file is unsafe: `\` and `(` are themselves defined by
  `phase1.fs`, not available before it is `needs`'d, and (unlike every
  other file's `\ Copyright ...` header) a non-Copyright comment there is
  not stripped by `tools/importation.py`, so it lands in the final boot
  text ahead of comment support existing at all.
- Every platform must define `ok` itself (`internals : ok ." <banner>"
  raw-ok ; forth`, matching `ports/posix/posix.fs`/`ports/pico-ice/platform.fs`); there
  is no default anywhere in `common/*.fs`. `common/fini.fs`'s last act is
  `execute` on the xt `autoboot.fs` leaves on the stack, then evaluating
  the bare word `ok` — forgetting to define it means `' ok` in a minimal
  `autoboot.fs` finds nothing, another silent crash.
- All three symptoms above are the *same* silent `SIGSEGV`, no error text,
  because whatever is missing is, transitively, `type`. If a from-scratch
  boot chain crashes with zero output, suspect one of these before
  anything else; `gdb -batch -ex run -ex bt` will just show a generic
  `forth_run`/`TIER0_OPCODE_LIST` frame either way, not the real cause.
- **The one that actually broke real hardware** (all four above only hit
  the POSIX simulator): a hand-written `crt0.s` for a new bare-metal target
  must copy `.data` from its ROM/flash image to its RAM runtime address,
  not just zero `.bss`. `.data` is where a C library's own internal state
  can live — with newlib, `_impure_ptr` and everything it points at (errno,
  malloc's bookkeeping) — and skipping the copy leaves that pointer
  pointing at whatever garbage was already in RAM. Nothing here calls
  newlib directly, but something in `forth_init()`'s own setup apparently
  does, and the result was a wild write through that garbage pointer,
  landing in video RAM: a real GBA/mgba screen stayed uniformly white with
  zero error output, reproduced with `forth_init()` alone, before any Forth
  text is ever interpreted. Standard fix, missing here until found: add
  `__data_load`/`__data_start`/`__data_end` symbols to the linker script
  and a copy loop in `crt0.s`, ahead of the `.bss` clear. `ports/gba/README.md`'s
  "Fixed, and confirmed booting" section has the full bisection trail.
  Separately: an emulator running much slower than real hardware speed (in
  this case, mgba under a GPU-less, shared-machine sandbox) can look
  exactly like this same bug in a screenshot taken too early — give it
  real wall-clock time before concluding the screen is actually stuck.
- **Ported while porting `examples/sdl2_roguelike.fs` to `ports/gba/roguelike.fs`**
  (this Forth's `{ local }` locals and boolean chains have two more sharp
  edges, neither specific to GBA):
  - A boolean `and`/`or` chain of more than two terms needs one combinator
    *per pair*, not one per extra term: combining four flags needs three
    `and`/`or`s total (e.g. `a b or  c d or  or`), not two. Writing it one
    short leaves a stray flag sitting on the stack forever after — no
    crash at the call site itself, just a slow stack-depth leak that
    overflows later, arbitrarily far away, looking unrelated. Caught twice
    in the same file (two viewport bounds checks, each an `and` of two
    `or`s), the worse of the two called on every visible tile of every
    frame, overflowing within a few dozen frames as a bus error.
  - A `{ local }` declared **inside** a `do ... loop` body is unsafe:
    `common/locals.fs` implements locals with `>r` on the return stack,
    unwound only once, at the *enclosing word's* own `;`, not once per
    iteration — so a loop body that declares one leaks a return-stack cell
    every time around, silently corrupting the loop's own index/limit
    living on that same stack. `examples/sdl2_roguelike.fs`'s original
    `draw-bar` sidesteps this by using a stateful `shade` word instead of a
    per-iteration local in its hearts loop; the fix when porting is either
    the same (leave the value on the data stack instead of naming it) or
    factor the loop body into its own word, called once per iteration, so
    it gets its own scope cleaned up at its own `;`.
  - Neither bug produces a repeatable crash on the very first call: both
    need enough iterations to overflow a stack, so they show up as a crash
    after N frames (N varying run to run, since it depends on incidental
    state like map layout) rather than immediately — misleading if you
    expect a deterministic reproduction. Bisecting with `." CHECKPOINT" cr`
    prints between suspect calls (same technique as the silent-`SIGSEGV`
    cases above) still finds them quickly once you know to look for this
    shape of bug, not just a missing/misplaced word.
  - **Once correct, it was unplayably slow: ~20s per redraw on real mgba**,
    confirmed by the user. A per-pixel Forth loop (`gba-dot`/`tint`/
    `tile-pixel`, one interpreted word dispatch per pixel) is fine for a
    handful of static tiles (the boot demo) but not for 30000+ pixels a
    turn (the roguelike's full 30x18-tile viewport). Fix: move just the hot
    inner loop — one tile blit, one rect fill — into plain compiled C
    (`gba_blit_tile`/`gba_fill_rect` in `main.c`, two new opcodes), keeping
    everything above it (what to draw, camera, game state) in Forth;
    `platform.fs`'s `draw-tile-tinted`/`gba-box` kept their exact original
    names and stack effects, so nothing calling them had to change. General
    lesson for any future platform port on hardware this modest: budget for
    the hot pixel/byte-shuffling loop needing to be C, not Forth, once it
    runs per-pixel at any real resolution — full detail, with the exact
    numbers, in `ports/gba/README.md`'s "Performance" section.

### Other Forths, for comparison (not part of this repo)

- gforth 0.7.9 bundles no SDL. Bindings exist: `JeremiahCheatham/Gforth-SDL2-Bindings` (52 files, SDL2 + image + mixer + ttf, raw API, event enums suffixed `_ENUM`), `foggynight/gforth-sdl2` (SWIG generated, copy in `garvalf/forth-is-fun` `gforth/lib` with only SDL, events, render, scancode, timer, video, plus a small `sdl_init.fth` helper and the `forthtoise.fth` turtle), and the `magnoquill-gforth.fth` cart.
- They use `c-library` (C compiled at load time, needs gcc + libsdl2-dev; floats and structs possible) and are 1:1 with the C API; gforth needs case-sensitive wordlists (`table >order`) to avoid the same `SDL_Quit`/`SDL_QUIT` clash. This branch uses `dlopen` (only the `.so` is needed), has a high level vocabulary, and is the only one that also runs in a browser.
