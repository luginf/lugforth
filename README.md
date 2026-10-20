# Lugforth

A small, case-insensitive, 64-bit Forth built on a minimal C kernel. It runs on Linux, in the browser,
on Android, on microcontrollers (ESP32, Pico, GBA) and on bare metal (Raspberry Pi, x86-64 PC via UEFI).

Site: [luginf.github.io/lugforth](https://luginf.github.io/lugforth/) -
[Manual](MANUAL.md) - [Word reference](docs/WORDS.md)

## Quick start

```
sudo apt install ninja-build
./configure.py
ninja posix
out/posix/lugforth
```

At the prompt, `info` (or `help`) shows the revision, the memory and a few starting words.

Binaries (Linux tarball and AppImage, Android APK) are attached to each release. Builds for the other
targets are in the [manual](MANUAL.md#building).

## What is in it

* **Interpreter**: line editing with history and Tab completion, a pager for long output, a console
  window with a TrueType font (`lugforth --gui`).
* **Editors**: `ed`, a text editor in Forth with syntax highlighting, and `writhdeck`, a writing editor
  (focus mode, Hemingway, daily goal, outline or TOC).
* **SDL2 words**: graphics, images, sound, ABC tunes, MIDI and an FM synthesizer.
* **Examples**: breakout, roguelike, tetris, snake, minesweeper, 2048, 3D wireframes, an inference
  engine, a Uxn/Varvara virtual machine that runs `.rom` files.
* **Bare metal**: the same prompt, screen, keyboard and files on a Raspberry Pi 3/4 and on a PC booted
  from a USB stick, with no operating system.

Details of each are in the [manual](MANUAL.md).

## Documentation

* [MANUAL.md](MANUAL.md): a tutorial in the spirit of Starting Forth, then usage, editors, examples, ports, building
* [docs/WORDS.md](docs/WORDS.md): reference of the words
* [docs/SKILLS.md](docs/SKILLS.md): pitfalls learned the hard way
* [examples/README.md](examples/README.md), [ports/rpi/README.md](ports/rpi/README.md),
  [ports/efi/README.md](ports/efi/README.md)

## Inspiration

* [uEforth](https://github.com/flagxor/ueforth) by Bradley D. Nelson: Lugforth started as a fork of it.
  It is unofficial and not endorsed by him.
* [eForth Windows](https://github.com/MPETREMANN11/eForth-Windows) by Marc Petremann: 2048, the 3D
  library and the inference engine are ports of his work.
* [Forth is Fun](https://gitlab.com/garvalf/forth-is-fun) by Eric Forgeot (garvalf): forthtoise, the turtle graphics (`examples/sdl2_turtle.fs`)
* [r3forth](https://github.com/phreda4/r3) by Pablo Hugo Reda (phreda): tetris, snake and minesweeper
  are rewritten after his games.
* [WrithDeck](https://github.com/luginf/writhdeck): the model of `writhdeck`.

Credits and licenses of the added code: [NOTICE](NOTICE).
