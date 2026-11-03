# Lugforth in Termux (Android)

The terminal Lugforth of `ports/posix`, built inside
[Termux](https://termux.dev) on the phone: the prompt, `ed`,
`writhdeck`, `blocks-editor`, the files, the line editor, the pager,
sockets. For the
graphics and the games on a phone, the APK of `ports/android` is the
better way (touch, a native SDL2 window, no X11).

## Install

From a git clone (the build reads the revision with git):

    pkg install git
    git clone https://github.com/luginf/lugforth
    sh lugforth/ports/termux/install.sh     # --test: also the tests

`install.sh` installs what is missing (`clang`, `python`, `ninja`),
runs `configure.py` and `ninja`, and copies `lugforth` and
`lugforth-mini` into `$PREFIX/bin`. By hand it is the same as on
Debian: `python3 configure.py && ninja posix`.

## What differs from Debian

`configure.py` sees Termux (`TERMUX_VERSION`, a `PREFIX` in
`com.termux`, or the `android` platform of Python 3.13 and later):

- It compiles with `clang++` (Termux has no gcc), with `-x c++`, since
  `main.c` is C++, and without two warnings that only clang gives.
- It builds a position independent executable (`-fPIE -pie`, no
  `-no-pie`): Android refuses to run any other kind.
- `strip` falls back on `llvm-strip` when binutils is missing.

Because of the second point, the binary is loaded at another address at
each run (ASLR). An image made by `save` or `remember` holds the
addresses of the code, so `restore` and `revive` refuse an image of
another run ("This image was saved by another run") instead of jumping
into it. Save the source and `include` it again.

## State

Not tried on a phone yet. Checked here: on Debian x86-64 a build
configured as Termux (clang, PIE) passes `ninja posix`, the pty tests
of the editors, of `blocks-editor` and of the pager, and `install.sh`.
Bionic, Android's libc, has the same layout as glibc on 64 bits for
what the Forth reads (`dirent`, `termios`, `winsize`, the `O_*` and
`MAP_*` values), and `dlsym(RTLD_DEFAULT, ...)` finds its functions.
32-bit phones (armv7) were not looked at.

The SDL2 words could run under Termux:X11 (`pkg install x11-repo sdl2`),
but Termux names its libraries without the version suffix
(`libSDL2-2.0.so`, not `libSDL2-2.0.so.0`): not done, the APK covers it.
