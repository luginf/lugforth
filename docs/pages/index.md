# Lugforth

A small Forth interpreter: a C kernel plus Forth boot code, in the spirit of
eForth. Unofficial fork of [uEforth / ESP32forth](https://github.com/flagxor/ueforth)
by Bradley D. Nelson, not endorsed by him (see [Credits](credits.html)).

<div class="cards">
<div class="card"><h3>Download</h3>
Linux (tarball, AppImage) and Windows builds on the
<a href="https://github.com/luginf/lugforth/releases">releases page</a>.</div>
<div class="card"><h3>Try it</h3>
The <a href="demo/terminal.html">web version</a>, with
<a href="demo/sdl2_breakout.html">Breakout</a>,
<a href="demo/sdl2_drums.html">drums</a>, a
<a href="demo/sdl2_roguelike.html">roguelike</a> and a
<a href="demo/sdl2_pinball.html">pinball</a> (touch buttons on phones).
Any game file also runs with <code>terminal.html?run=sdl2_roguelike.fs</code>.
Drawing from HTML buttons: <a href="demo/button.html">Forth word called by a button</a>,
<a href="demo/vector_logo.html">vector lettering</a>,
<a href="demo/vector_logo_outline.html">outline lettering</a>,
<a href="demo/string_art.html">string art</a>
(<a href="https://github.com/luginf/lugforth/tree/main/examples/web">sources</a>).</div>
<div class="card"><h3>Words</h3>
<a href="words.html">All words</a>,
<a href="new-words.html">words added by Lugforth</a>,
<a href="sdl2.html">SDL2 graphics and sound</a>.</div>
<div class="card"><h3>Platforms</h3>
Linux, Windows, web, Android, ESP32, Raspberry Pi Pico, pico-ice, Game Boy
Advance. See <a href="ports.html">ports</a>.</div>
</div>

## Added to uEforth

* About 70 missing ANS words (`pick`, `roll`, `compare`, `>number`, doubles...).
* SDL2 bindings: windows, drawing, input, images, text, sound, ABC music, also
  on the web with the same words.
* A terminal line editor: history, cursor keys, Tab completion of words and
  file names. A pager (`common/pager.fs`) stops long output at each screenful
  on a terminal and on the bare-metal consoles: space, Enter, `q`; `pager-off`.
* New ports: Android, Game Boy Advance, Raspberry Pi Pico.
* Examples: games (Breakout, roguelike, 2048 with animation), turning 3D wireframes, a rule based inference engine, benchmarks, a txt2tags converter.

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
