# Pitfalls learned the hard way

Read before touching Forth sources, the loaders, the web build or a new platform. Part of [README.md](../../README.md).

## Forth, loaders and SDL2

- In a loader (a colon definition compiled at boot), a name such as `sdl2` or `sdl2-mixer` written outside the evaluated string is bound to the loader that existed then: running it again reloads everything and creates a new empty vocabulary. Put such names in a string run by `evaluate`.
- A control structure (`if`, `do`, `begin`) outside a definition works since 7.0.11.11 (`common/interpret_control.fs`), but not on esp32, pico, pico-ice, gba boots. `2swap`, `0>` and `within` do exist (`common/ansi_compat.fs`).
- Locals declared with `{ }` inside an `if` branch corrupt the return stack when the branch is skipped (a silent -9); declare them at the top of the word or use values.
- `dump-file` takes the data first and the file name last.
- A nested `( ... )` inside a stack comment closes the comment at the first `)`: `( optional xt ( n -- ): text )` leaves `: text )` to be compiled, which started a definition and gave "n NOT FOUND" when `sdl2` loaded (found while adding `render-hook`). Write `( xt, n -- : text )`. `ninja posix` does not load `sdl2`, so run the SDL tests after touching `ports/posix/sdl2.fs`.
- A `render-hook` (see `examples/music/fm`) adds into `mix`, which holds 4096 cells: never ask it for more samples than that (`latency` is 2205, and a test that calls `fm-render` directly stays under 4096 or it overwrites the memory after `mix` and fails in unrelated tests).

- **r| copies** (changed 2026-10-13). `r"`, `r|` and `r~` used to keep, in a definition, the address of the text they were read from, not a copy. That is fine while the text stays: the boot text lives in the binary. It is wrong for a file read with `include` or `needs` (freed after loading: a lazy loader such as `: sdl2 r| ... | evaluate ;` found garbage later, seen as "he NOT FOUND" in lugforth-mini), for a line typed at the prompt (overwritten by the next line) and for a string evaluated then freed. Now `r-copy` (an `internals` variable, set to -1 by `common/fini.fs` at the end of the boot) makes them copy their text exactly as `s"` does; during the boot they still keep addresses, so the boot costs nothing more, and on ESP32 or Pico only what the user defines later is copied. `include-file` also keeps the text of a file alive when an address was taken (`src-ref`), which only happens if `r-copy` is 0. Test: `test-r-copy` in `common/ansi_compat_tests.fs`. **To go back** to addresses only: delete the line `-1 r-copy !` in `common/fini.fs` (the rest is then inert; `include-file` still protects included files). Possible side effects to watch: a program that relied on `r|` giving an address inside its own source (none in this repository), and the dictionary use of the strings defined after the boot (a 60 KB `r|` is copied once).
- `ms` (`common/tasks.fs`) used to keep one CPU at 100% (a loop on `ms-ticks` with `pause`); on POSIX it now sleeps 1 ms between two `pause`. Other ports still busy-wait. In a program that polls (`key?` in a loop, a game or editor waiting for a key) sleep with `usleep` instead, which is in the `posix` vocabulary: `50000 usleep drop` for 50 ms (see `wd-wait` in `ports/posix/writhdeck.fs`). Also, the SDL console window only redraws in `key` and in `key?`, so a loop that never calls either shows nothing.
- lugforth ignores case. `SDL_Quit` (function) and `SDL_QUIT` (event constant) are the same name, so the function is bound as `sdl-shutdown`. Check new bindings for such collisions.
- FFI (`shared-library` + `sofunc`) passes cells only: no float or struct by value, no C to Forth callbacks (audio uses `SDL_QueueAudio`, not a callback).
- `beep` queues a whole tone, so beeps never overlap. `drum-hit` (percussive, 8 voices, `pitch-drop`) is mixed in Forth by `pump`, which `drum-hit`, `flip` and `delay` call through `audio-hook` and which keeps about 50 ms queued: a loop that never calls `flip` or `delay` gets no sound from hits. Web `drum-hit` is just a WebAudio node started at once.
- A `|` anywhere in an `r|` block (even in a stack comment like `( -- img | 0 )`, or a JS `||`) ends the string early and hangs the boot or the page ("ERROR: | NOT FOUND!").
- Locals declared with `{ }` inside a loop body break (silent "Error including"): declare them at the top of the word. `SDL_Color` works because it fits in one register.
- Every C call pushes a result, even `void` ones: `drop` it. `int` results need `sign-extend`.
- Lazy loaders are `: name r| ...source... | evaluate ;`, so the source must not contain `|`. They end with `sdl2 definitions` so user words land in vocabulary `sdl2` and are not shadowed by library names (`px`...).
- `do` runs ~65k times when the count is 0: use `?do`. `roll`, `0>`, `2constant`, `within` do not exist. But `?do` only skips cleanly when `start = limit` (a count of 0); `start > limit` is not a safe skip and can run away (seen with `limit start dup * ?do`, where `start` is a squared value that can exceed `limit`) or corrupt memory instead of doing nothing: guard explicitly (`start limit < if ... then`) whenever start could legitimately exceed limit, don't rely on `?do` for that case.
- A `{ }` local named the same as a builtin word (e.g. `{ pat trk anchor from to }`) silently shadows it for the rest of that word: `from 1+ to et-c` then just pushes two numbers instead of assigning, with no error at all (`et-c` quietly keeps its old value). Found in `examples/music/sdl2_pianoroll.fs` (a local called `to` broke its own `to et-c`, making a drag-to-extend-a-held-note gesture always extend back to column 0). Avoid common word names (`to`, `over`, `n`, ...) for locals; when a word misbehaves with no error at all, grep its own local list against the word list first.
- Web: `JSWORD:` bodies also live inside an `r|` block, so no `||` in the JS. `flip` must `yield` (it waits for requestAnimationFrame). `bye` on the web build throws `objects[op] is not a function` (already true for stock `terminal.html`), so the page is inert afterwards: the breakout page shows a "Play again" button that reloads.
- Real mouse events are converted to logical pixels by `SDL_RenderSetLogicalSize`; injected ones (`SDL_PushEvent`) are not. `pressed?` reads the state once per `events`, so a sub-frame tap is missed on POSIX (the web version also catches it).
- Test tricks: `SDL_RenderReadPixels` to check pixels, `SDL_PushEvent` to fake input, Xvfb + `xdotool` for a real pointer, headless Chrome over CDP for the web page (needs http, not `file://`: `cd out/web && python3 -m http.server`).
- `[char]` is compile-only: fine inside a `:` definition, but used directly (e.g. in a top-level `create ... [char] C , ...`) it throws a silent stack underflow with no message. Use plain `char` outside a definition.
- After `sdl2` (and `sdl2-mixer`, ...) finish loading, `posix` is no longer in the search order for the code that follows (the loader's own `previous`s remove it): a plain example that wants `sysfunc`/`sofunc` directly (e.g. for its own `mkstemps`) needs its own `also posix` first, same as `sdl2_abc.fs`'s loader does internally. **Danger**: that `also posix` stays in effect for the rest of the file and can shift the search order enough that a later word of yours with a common name (`remember`, ...) gets shadowed by an unrelated word already defined in `forth`/`internals` (e.g. `common/filetools.fs` has its own `remember`, for `rm`/`reset`). Calling the wrong word silently corrupts state instead of raising a normal error, and the symptom is a bare "Error including" with no message, from wherever the corrupted stack/memory is next touched, not from the real call site (`ports/posix/faults.h` also converts a real SIGSEGV/SIGBUS/SIGFPE/SIGINT into the same silent throw, so check with `gdb -batch -ex run -ex bt` first: a clean process exit, not a caught signal, points at exactly this). Once found in `examples/music/sdl2_pianoroll.fs` (its own `remember` collided this way, only visible when the app was launched with a cwd where evaluation order happened to expose it), the fix was to rename the word (`key-remember`). Give any word defined after `also posix` a name unlikely to already exist (or check with `see`/a fresh vocabulary).
- Testing with Xvfb + `xdotool`: `xdotool mousemove --window <id> x y` is window-relative and immune to the window's on-screen position, unlike plain root coordinates with `import -window root` (a window is not always at 0,0: check with `xdotool getwindowgeometry`). A `click` (down+up in one call) can be faster than a frame and get missed; a separate `mousedown`/`sleep 0.2`/`mouseup`, with the pointer already settled a moment before, is reliable. A screenshot taken right after a click can show the pre-click frame (redraw lag): re-screenshot, or check side effects (a growing recorded audio file, a written test file) instead of trusting one image.

## More traps and test tricks

- Found while porting eForth Windows and the editors (2026-10): an `if`, `begin` or `do` at top level (outside a definition) does not work, no error: `s" --x" option? if false to flag then` ran its `then` part every time, which silently switched off the animation of `sdl2_2048.fs`. Put it in a word. `<# #s #>` takes ONE number here (not a double). `include` is relative to the file that includes, not to the working directory. `b@` and `b!` in `ed`/`writhdeck` take a position in the text, not an address. `ms-ticks 1 or rand-seed !` seeds `random` ( n -- r ); `rnd` does not exist.
- Look at the real thing: this machine can have an X display (`echo $DISPLAY`). Run the program, `xdotool windowfocus`, press a key with `keydown`, a short `sleep`, `keyup` (`xdotool key` is faster than a frame and is missed by `pressed?`), and `import -window $(xdotool search --name TITLE | head -1) shot.png`. A slowed down copy of an animation (3 s instead of 0.2 s) makes it visible in screenshots. Do not `pkill -f` a pattern that appears in your own shell command line.
- A Makefile that checks results: `examples/utils/inference/Makefile` runs scenarios, prints the facts and greps the expected ones (`-> OK` or `-> FAIL`). Strip `\r` from the output of lugforth before grepping.

- Web (ports/web) traps met 2026-10-22, plus the next ones (keyboard of a phone): a JSWORD body must not declare a variable named `sp` (it shadows the stack pointer: the word runs but does not pop its arguments); `[defined] x [if] ... [then]` inside a colon definition breaks the web interpreter (wrap whole definitions at top level); a JS source with a literal non-ASCII character is read as Windows-1252 when the page has no `<meta charset>` and the server sends none (use `'\u200b'`); a `keypress` handler that calls preventDefault on a phone stops the hidden textarea from getting the character. Test the web page with headless Chromium over CDP (`chromium --headless=new --remote-debugging-port`, python `websockets`, `Input.dispatchKeyEvent`, `Emulation.setDeviceMetricsOverride mobile`), after `ninja web` and from `out/web`.

- A page without `<meta charset>` served without an encoding reads a JS source in Windows-1252: no literal non-ASCII character in the web sources.
- On a phone, a keyboard that covers the page leaves the last line unreachable if there is nothing below it: add padding below the text (`context.KeepInView`).
- Do not `pkill -f` a pattern that appears in your own shell command line (it kills the shell); list the processes with `ps` and kill by PID.

## New platform ports (ports/gba): boot-chain gotchas

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
- **Ported while porting `examples/games/sdl2_roguelike.fs` to `ports/gba/roguelike.fs`**
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
    living on that same stack. `examples/games/sdl2_roguelike.fs`'s original
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

## Adding words

A new C function looked up by `sysfunc` also goes in the table of `ports/posix/main.c` (static build, see PORTS.md).

After adding words to the interpreter, build (`ninja posix web`) and run `python3 tools/words_reference.py`: it asks the real
interpreters for their words and rewrites `docs/WORDS.md` (descriptions from `attic/oldsite/*.html`, `tools/words_notes.py`,
the stack comments of the sources; add a note in `words_notes.py` for a word it leaves undescribed). `tools/words_added.py`
lists what differs from upstream uEforth for the "New words" page of the site.

Write the explanation of a new word in the source: `: name ( stack ) ( what it does )` or `: name ( stack )   \ what it does`
on the same line, or `\ lines` right above the definition. `words_reference.py` takes them (not from `*sdl2*` files nor
`_tests.fs`). Helpers with a common prefix (`le-`, `pg-`, `hist`, `pool`) get a note per family (`PREFIX_NOTES` in
`words_reference.py`). The words of `sdl2` are written by hand in `docs/SDL2-words.md` (`docs/SDL2.md` is the tour,
`docs/SDL2-web.md` the web build): add a row there when a short word is added in one of the three `sdl2.fs`.

## Forth pitfalls hit again and again

- `b@` in the editor takes a *position in the text*, not an address.
- `{ locals }` inside a conditional or loop body break: declare them at the top of the word.
- A `|` inside an `r|` block ends it. `[char]` only inside definitions. `?do` with start > limit runs away.
- A silent "Error including: file" is a throw somewhere: bisect with a scratch file using `.` prints.

- An opcode used by a shared Forth file must exist in every `main*.c` that loads it: the Android app has its own list (ports/android/app/native/main_android.cpp). `host-mac?` (used by `os` in ports/posix/posix.fs) was missing there in v0.5.0 and the SDL2 console stayed black, with no message. After adding an opcode to ports/posix/main.c, add it to main_android.cpp too, and try the APK on a phone (`adb logcat` shows nothing from the Forth side).
