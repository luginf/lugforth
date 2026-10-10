# Lugforth

> Unofficial fork of [uEforth](https://github.com/flagxor/ueforth) by Bradley D. Nelson,
> maintained by Luginfo10. It is not affiliated with the original author and is not
> endorsed by him. See [NOTICE](NOTICE) for what was added.

This EForth inspired implementation of Forth is bootstraped from a minimalist C kernel.

Documentation site: [luginf.github.io/lugforth](https://luginf.github.io/lugforth/)

[WORDS.md](docs/WORDS.md) is a reference of the words of this build, generated from the interpreters themselves (`tools/words_reference.py`).

## Layout

```
common/      shared core (Forth sources and C headers)
ports/       one directory per platform: posix, web, windows, esp32, pico,
             pico-ice, gba, android
examples/    example programs and benchmarks
tools/       build and conversion scripts
docs/        site/ (documentation website), SKILLS.md, WORDS.md, images/
lib/         shared Forth libraries (hashing)
attic/       old experiments
```

## Binaries

`.github/workflows/release.yml` builds the release binaries. Run it from the
Actions tab (or `gh workflow run release.yml -R luginf/lugforth`) to get them as
build artifacts, or push a tag such as `v7.0.8.1` to publish them in a GitHub
release.

* Linux (tarball and AppImage): built on Debian 10, needs glibc 2.27 or newer.
  The AppImage also carries the SDL2 libraries. Tested in CI.
* Windows x64: built with MSVC, starts are not tested in CI yet.
* macOS universal: builds but does not start properly yet, so it is not
  published (see the workflow). `LUGFORTH_FAULTS=1` makes the posix build report
  faults on stderr, useful to debug a new platform.

Static binaries are not possible: Lugforth reaches libc, SDL2 and X11 through
`dlopen`/`dlsym`, which a static executable cannot do.

## Building from Source

To build from source:

```
sudo apt install ninja-build gcc-arm-none-eabi
git clone git@github.com:luginf/lugforth.git
cd lugforth
./configure.py
ninja
```

The resulting output will have this structure:

* out/deploy - A copy of the old uEforth documentation site (kept from upstream).
* out/pages - The GitHub Pages site (`tools/make_pages.py`).
* out/esp32 - A source build for ESP32.
* out/esp32-sim - A POSIX build approximating ESP32 for testing.
* out/pico-ice - A build for pico-ice.
* out/pico-ice-sim - A POSIX build approximating pico-ice for testing.
* out/gen - Intermediate / generated files.
* out/posix - A build for Linux / POSIX.
* out/resources - Intermediate / generated resources.
* out/web - A build for Web.
* out/windows - A build for Windows.

Individual platforms can be built as follows:

```
ninja posix
ninja esp32
ninja pico-ice
ninja win32
ninja win64
ninja web
```

A build that excludes the slower components can be configured with:

```
./configure.py -f
```

To install to /usr/bin on Linux / POSIX do:

```
ninja install
```

ESP32 boards can be compiled and flashed with:

```
ninja esp32-flash
ninja esp32s2-flash
ninja esp32s3-flash
ninja esp32c3-flash
ninja esp32cam-flash
```

Set PORT=com3 etc. to select board.
