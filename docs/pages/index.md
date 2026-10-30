# Lugforth

A small Forth interpreter: a C kernel plus Forth boot code, in the
spirit of (u)eForth.

<div class="cards">
<div class="card"><h3>Download</h3>
<a href="https://github.com/luginf/lugforth/releases/latest/download/lugforth-manual.pdf">The manual (PDF)</a>.
Builds on the <a
href="https://github.com/luginf/lugforth/releases/latest">latest
release</a>: Linux (tarball, AppImage), Windows (zip, built with MSVC or
MinGW, with the SDL2 DLLs, examples and docs), macOS (zip), Android
(APK), Raspberry Pi and PC UEFI images. The zips carry
<code>examples/</code> and <code>docs/</code>.</div>
<div class="card"><h3>Try it</h3>
The <a href="demo/terminal.html">web version</a>, with
<a href="demo/gfx_breakout.html">Breakout</a>,
<a href="demo/gfx_drums.html">drums</a>, a
<a href="demo/gfx_roguelike.html">roguelike</a>, a 3D maze with the
<a href="demo/gfx_raycaster.html">raycaster</a> (steered with the mouse) and a
<a href="demo/gfx_pinball.html">pinball</a> (touch buttons on phones).
More games, run in the same page:
<a href="demo/terminal.html?run=2048.fs,gfx_2048.fs">2048</a>,
<a href="demo/terminal.html?run=gfx_snake.fs">Snake</a>,
<a href="demo/terminal.html?run=gfx_tetris.fs">Tetris</a>,
<a href="demo/terminal.html?run=gfx_minesweeper.fs">Minesweeper</a>,
<a href="demo/terminal.html?run=gfx_mastermind.fs">Mastermind</a>,
<a href="demo/terminal.html?run=gfx_invaders.fs">Space Invaders</a>,
<a href="demo/terminal.html?run=gfx_magnoquill.fs">Magnoquill</a>,
<a href="demo/terminal.html?run=3d.fs,gfx_3d.fs">a 3D cube</a>,
<a href="demo/terminal.html?run=gfx_turtle.fs">Logo turtle</a> (type commands under the
drawing) and the
<a href="demo/terminal.html?run=gfx_demo.fs">SDL2 tour</a>.
Any game file also runs with
<code>terminal.html?run=gfx_roguelike.fs</code> (several files:
<code>?run=2048.fs,gfx_2048.fs</code>). Drawing from HTML
buttons: <a href="demo/button.html">Forth word called by a button</a>,
<a href="demo/vector_logo.html">vector lettering</a>,
<a href="demo/vector_logo_outline.html">outline lettering</a>,
<a href="demo/string_art.html">string art</a>
(<a
href="https://github.com/luginf/lugforth/tree/main/examples/web">sources</a>).</div>
<div class="card"><h3>Words</h3>
<a href="words.html">All words</a>,
<a href="new-words.html">words added by Lugforth</a>,
<a href="sdl2.html">SDL2 graphics and sound</a>.</div>
<div class="card"><h3>Platforms</h3>
Linux, macOS, Windows, web, Android, Raspberry Pi, PC (UEFI), ESP32,
Raspberry Pi Pico, pico-ice, Game Boy Advance. See <a
href="ports.html">ports</a>.</div>
</div>

## Added to uEforth

* About 70 missing ANS words (`pick`, `roll`, `compare`, `>number`,
  doubles...).
* SDL2 bindings: windows, drawing, input, images, text, sound, ABC
  music, also on the web with the same words.
* A terminal line editor: history, cursor keys, Tab completion of words
  and file names. A pager (`common/pager.fs`) stops long output at each
  screenful on a terminal and on the bare-metal consoles: space, Enter,
  `q`; `pager-off`.
* New ports: Android, Game Boy Advance, Raspberry Pi Pico, Raspberry Pi
  and PC UEFI (bare metal), SDL2 on Windows and macOS, a console window
  in SDL2 (<code>lugforth --gui</code>).
* Two text editors, <code>ed</code> and <code>writhdeck</code> (Linux,
  macOS, Windows; also under gforth).
* Examples: games (Breakout, roguelike, 2048 with animation, a Z-machine
  interpreter for interactive fiction), turning 3D wireframes, a rule
  based inference engine, benchmarks, a txt2tags converter.

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
> : square dup * ;
> 12 square .
144  ok
```
