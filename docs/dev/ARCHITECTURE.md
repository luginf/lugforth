# Architecture, build and tests

How the interpreter is built and tested. Part of the notes for developers (and for Claude): see [README.md](../../README.md).

## Overview

µEforth is an EForth-inspired Forth implementation bootstrapped from a minimalist C kernel. It targets multiple platforms: POSIX/Linux, ESP32 (Arduino), pico-ice, Web (WASM/JS), and Windows.

[WORDS.md](../WORDS.md) is a generated reference of the words of the `forth` vocabulary and the other Linux/web vocabularies, with stack effects and descriptions where known (`tools/words_reference.py`, run it again after adding words). SDL2 words have their own reference, [docs/SDL2-words.md](../../docs/SDL2-words.md).

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
- `common/interp.h` — the interpreter loop using GNU computed-goto dispatch (`goto **(void **) w`)
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
- `editors/` — `ed` and `writhdeck` (shared by posix, windows, efi, rpi, gforth)
- `ports/posix/` — Linux/macOS: C entry point, POSIX bindings, sockets, X11, pthreads
- `ports/esp32/` — Arduino `.ino` + C++ builtins; optional extensions in `ports/esp32/optional/`
- `ports/pico-ice/` — RP2040-based FPGA board
- `ports/web/` — JavaScript/WASM target
- `ports/tic80/` — wasm32 module for the TIC-80 console (wasm3), sandboxed: see PORTS.md
- `ports/windows/` — Win32 build via MSVC cross-compilation from WSL
- `tools/` — build-time Python scripts
- `docs/pages/` — the GitHub Pages site (Markdown, built by `tools/make_pages.py`); `attic/oldsite/` — the documentation website inherited from uEforth (see its README); `docs/dev/` — these notes
