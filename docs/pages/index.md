# Lugforth

Lugforth is a Forth interpreter in a small C kernel plus Forth boot code, in the
spirit of eForth. It started as a fork of
[uEforth / ESP32forth](https://github.com/flagxor/ueforth) by Bradley D. Nelson
and keeps its credits (see [Credits](credits.html)); it is an unofficial fork,
not endorsed by the original author.

<div class="cards">
<div class="card"><h3>Download</h3>
Linux (tarball, AppImage) and Windows builds are on the
<a href="https://github.com/luginf/lugforth/releases">releases page</a>.</div>
<div class="card"><h3>Try it</h3>
The <a href="demo/terminal.html">web version</a> runs in your browser, with
<a href="demo/sdl2_breakout.html">Breakout</a>,
<a href="demo/sdl2_drums.html">drums</a> and a
<a href="demo/sdl2_roguelike.html">roguelike</a>.</div>
<div class="card"><h3>Words</h3>
The <a href="words.html">full word list</a>, the
<a href="new-words.html">words added by Lugforth</a> and the
<a href="sdl2.html">SDL2 graphics and sound</a> words.</div>
<div class="card"><h3>Platforms</h3>
Linux, Windows, web, Android, ESP32, Raspberry Pi Pico, pico-ice and
Game Boy Advance. See <a href="ports.html">ports</a>.</div>
</div>

## What is new compared with uEforth

* **ANS words** missing from the core: doubles (`d+`, `d.`, `m*`...), `pick`,
  `roll`, `2swap`, `move`, `compare`, `search`, `>number`, `marker`, `random`...
  (`common/ansi_compat.fs`, about 70 words, with tests).
* **SDL2 bindings** (`sdl2`, `sdl2-image`, `sdl2-ttf`, `sdl2-mixer`): windows,
  drawing, keyboard and mouse, images, text, sound, ABC music and soundfonts,
  also on the web with the same words.
* **An on-screen console** drawn with SDL and its own bitmap font
  (`ports/posix/sdl2_console.fs`), used by the Android app.
* **New ports**: Android (command line, WebView and native SDL2 apps), Game Boy
  Advance, Raspberry Pi Pico (RP2040) with `tools/pico_send.py` to load programs
  over USB.
* **Examples**: games, benchmarks against C and uxn, and a txt2tags to HTML
  converter written in Forth.
* **Fixes**: the terminal is restored when the program exits.

## Quick start

```
git clone https://github.com/luginf/lugforth.git
cd lugforth
sudo apt install ninja-build
./configure.py
ninja
out/posix/lugforth
```

```
--> : square dup * ;
--> 12 square .
144  ok
```
