# Lugforth manual

## Goal

Lugforth is a playground to learn and experiment with Forth, and to build applications with it: virtual
ones, on the PC (Linux, Windows, the web browser) and on Android, and real ones, on boards such as the
Raspberry Pi Pico and the ESP32 (and on bare Raspberry Pi and PC hardware). The same language and the
same words run everywhere: you try an idea at the prompt, keep it in a file, and carry it to another target.

Two parts. **Learn** is a tutorial: start at chapter 1 and type every example. **Reference** has the
details of Lugforth itself (binaries, editors, examples, ports, building). Word reference:
[docs/WORDS.md](WORDS.md). Pitfalls: [docs/dev/PITFALLS.md](dev/PITFALLS.md).

**Learn**
[1 First steps](#1-first-steps) -
[2 The stack](#2-the-stack) -
[3 New words](#3-new-words) -
[4 Decisions](#4-decisions) -
[5 Loops](#5-loops) -
[6 Memory](#6-memory-variables-constants-arrays) -
[7 Text and numbers](#7-text-and-numbers) -
[8 Files and editors](#8-files-and-editors) -
[9 When it goes wrong](#9-when-it-goes-wrong) -
[10 Three small programs](#10-three-small-programs) -
[Answers](#answers-to-the-exercises)

**Reference**
[Layout](#layout) -
[Binaries](#binaries) -
[Console window](#console-window) -
[Mini build](#mini-build) -
[Line editing](#line-editing) -
[Pager](#pager) -
[ed](#ed) -
[writhdeck](#writhdeck) -
[Examples](#examples) -
[Varvara](#varvara-running-uxn-roms) -
[Bare metal](#bare-metal) -
[Building](#building)

# Learn

Conventions: what you type follows the prompt `-->`; what Lugforth answers is on the next line or
after your input. Case does not matter: `DUP`, `dup` and `Dup` are the same word. Words are
separated by spaces, and almost anything that is not a space can be part of a word name.

## 1. First steps

Start `lugforth` (see [Binaries](#binaries) or build it with `ninja posix`). You get a banner and the
prompt:

```
Lugforth v2026.10.23.2
:; ===========>/>
type 'info' for help
 ok
-->
```

Type a line and press Enter. Lugforth reads each word, executes it, and says ` ok` when the line is done.

```
--> 2 3 + .
5  ok
```

Forth writes **postfix**: the operands come first, then the operator. `2 3 +` means "put 2, put 3,
add". The `.` word prints the result. Without it, the result stays unseen:

```
--> 2 3 +
 ok
--> .
5  ok
```

The 5 waited between the two lines. Where? On the **stack**, the subject of the next chapter.
Arithmetic first:

```
--> 10 3 - .
7  ok
--> 7 6 * .
42  ok
--> 17 5 / .
3  ok
--> 17 5 mod .
2  ok
--> -7 abs .
7  ok
--> 3 9 max .
9  ok
```

All numbers are whole numbers (64 bits). `/` drops the fraction, rounding toward minus infinity:
`-7 2 /` gives `-4`.

A line can hold many words: `1 2 + 3 * .` prints 9, that is `(1 + 2) * 3`. There are no parentheses
and no precedence rules: the order of the words is the order of the work.

`info` (or `help`) shows the revision and the memory, and `bye` leaves.

## 2. The stack

Numbers wait on the **stack**. A number you type is pushed; a word like `+` pops what it needs and
pushes its result. `.s` shows the stack without changing it. The count is in angle brackets, the top
of the stack is on the right:

```
--> 1 2 3 .s
<3> 1 2 3  ok
--> + .s
<2> 1 5  ok
--> . .
5 1  ok
```

Each `.` pops the top number and prints it, so the numbers come out in reverse order.

Words that rearrange the stack. The comment in parentheses is a **stack effect**: what is taken
before `--`, and what is left after. `( a b -- b a )` for `swap` means "a below b becomes b below a".

| Word | Stack effect | Action |
|---|---|---|
| `dup` | `( a -- a a )` | copy the top |
| `drop` | `( a -- )` | throw the top away |
| `swap` | `( a b -- b a )` | exchange the two on top |
| `over` | `( a b -- a b a )` | copy the second one to the top |
| `rot` | `( a b c -- b c a )` | bring the third one to the top |
| `nip` | `( a b -- b )` | drop the second |
| `tuck` | `( a b -- b a b )` | copy the top under the second |
| `2dup` | `( a b -- a b a b )` | copy the top pair |
| `2drop` | `( a b -- )` | drop the top pair |

Try each one with `.s`:

```
--> 1 2 swap .s
<2> 2 1  ok
--> 2drop 5 dup * .
25  ok
```

`dup *` squares a number. Here is the whole idea of Forth: small words that work on the stack, glued
together. Keep a pencil handy, and write the stack down after each word until it becomes natural.

To empty the stack, use `drop` until `.s` shows `<0>`. `depth` gives the number of items.

**Exercises**

1. What does `3 4 swap - .` print?
2. What is on the stack after `1 2 3 rot`?
3. Using only the words above, compute `(5 + 3) * (5 - 3)` while typing 5 only once.

## 3. New words

A **definition** starts with `:` (colon), the name, the body, and ends with `;` (semicolon). From
then on the new word is part of the language.

```
--> : square  dup * ;
 ok
--> 5 square .
25  ok
```

Always write the stack effect right after the name; the compiler ignores it, but you will not:

```
: square ( n -- n*n )  dup * ;
: cube ( n -- n*n*n )  dup square * ;
```

A word can use words defined before it. This is **factoring**: when a phrase appears twice, give it a
name. Words are tiny and testable one by one:

```
--> 3 cube .
27  ok
: celsius>f ( c -- f )  9 * 5 / 32 + ;
--> 100 celsius>f .
212  ok
```

Two tools to look around:

```
--> see square
: square 
    DUP * 
; 
 ok
--> words
```

`see` decompiles a word. `words` lists every word of the current vocabulary, most recent first (your
`square` comes first). `\` starts a comment to the end of the line, and `( ... )` is a comment inside
a line.

Typing a definition at the prompt is fine, but nothing is kept after `bye`. Chapter 8 shows how to
keep your words in a file.

**Exercises**

1. Define `double-sum ( a b -- n )` that gives twice `a + b`.
2. Define `area ( w h -- a )` and use it to define `volume ( w h d -- v )`.
3. Define `fahrenheit>c ( f -- c )`, the reverse of `celsius>f`.

## 4. Decisions

A comparison leaves a **flag**: `-1` for true, `0` for false.

```
--> 3 4 < .
-1  ok
--> 4 3 < .
0  ok
--> 3 3 = .
-1  ok
--> 5 0= .
0  ok
```

The comparisons are `=`, `<>`, `<`, `>`, `<=`, `>=`, and `0=`, `0<`, `0>` for a comparison with zero.
`and`, `or`, `xor` and `invert` combine flags.

`if ... then` runs its body when the flag is true. `else` adds the other case:

```
: sign ( n -- )
  dup 0< if drop ." negative" else
  0= if ." zero" else ." positive" then then ;
--> -5 sign
negative ok
--> 0 sign
zero ok
--> 8 sign
positive ok
```

`if` takes its flag from the stack and consumes it, hence `dup` before the first test and `drop` in
the branch that does not need the number.

For several values of one number, `case`:

```
: kind ( n -- )
  case
    1 of ." one" endof
    2 of ." two" endof
    ." many"
  endcase ;
--> 2 kind
two ok
```

> **Control words outside a definition.** `if`, `do`, `begin`, `case`, `for` and their partners also work
> typed at the prompt, at the top level of a file or in an `evaluate`d string: `3 0 do i . loop`
> prints `0 1 2`, `1 if ." yes" then` prints `yes`. The structure is compiled into a temporary
> anonymous word, run as soon as it is closed (`loop`, `then`, `until`...), then forgotten. It may span
> several lines, and stack items left before it stay available. A structure still open when an error
> occurs is dropped; the next one starts afresh. (`common/interpret_control.fs`.)

**Exercises**

1. Define `abs2 ( n -- |n| )` with `if` and `negate`.
2. Define `between? ( n lo hi -- flag )` true when `lo <= n <= hi`.

## 5. Loops

`do ... loop` repeats with a limit and a start: `limit start do ... loop`. Inside, `i` is the counter.

```
: stars ( n -- )  0 ?do 42 emit loop ;
--> 5 stars
***** ok
: table ( n -- )  11 1 do dup i * . loop drop ;
--> 7 table
7 14 21 28 35 42 49 56 63 70  ok
```

`42 emit` prints the character with code 42 (`*`). `?do` skips the loop when the limit equals the
start; `do` would run it a whole turn around the number line. Do not give `?do` a start above
the limit either.

* `+loop` adds a step instead of 1: `: t 10 0 do i . 3 +loop ;` prints `0 3 6 9`.
  A negative step counts down: `: t 0 5 do i . -1 +loop ;` prints `5 4 3 2 1 0`.
* `leave` leaves the loop at once.
* `j` is the counter of the enclosing loop in nested loops.

When the number of turns is not known in advance, `begin ... until` loops until the flag is true,
and `begin ... while ... repeat` tests at the start:

```
: countdown ( n -- )  begin dup . 1- dup 0= until drop ;
--> 5 countdown
5 4 3 2 1  ok
: gcd ( a b -- g )  begin dup while tuck mod repeat drop ;
--> 48 18 gcd .
6  ok
```

A word can call itself with `recurse`:

```
: fact ( n -- n! )  dup 1 > if dup 1- recurse * then ;
--> 10 fact .
3628800  ok
```

**Exercises**

1. Define `sum-to ( n -- sum )` giving `1 + 2 + ... + n`, with a loop.
2. Define `triangle ( n -- )` that prints a triangle of `#` signs of n lines (`[char] #` gives the code
   of `#` inside a definition).
3. Print the multiplication table of 1 to 9 as a grid, using two nested loops.

## 6. Memory: variables, constants, arrays

A **variable** is a named cell of memory. Its name pushes its *address*; `!` stores and `@` fetches.

```
--> variable score
--> 10 score !
--> score @ .
10  ok
--> 5 score +!
--> score @ .
15  ok
```

`?` is the shortcut for `@ .`. A **constant** is a name for a value that does not change:

```
--> 42 constant answer
--> answer .
42  ok
```

A **value** is read like a constant and changed with `to`; handy for a counter you read often:

```
--> 0 value lives
--> 3 to lives
--> lives .
3  ok
```

An **array** is a `create`d block of memory. A cell is 8 bytes, so use `cells` to index:

```
create primes  2 , 3 , 5 , 7 , 11 ,      \ , stores a number and moves on
--> primes 3 cells + @ .
7  ok
create buf 10 allot                       \ 10 bytes, filled with whatever was there
```

Bytes are fetched with `c@` and stored with `c!`. Memory is not cleared for you: use `buf 10 erase` or
`buf 10 1 fill`.

**Locals** give names to the stack items inside a definition:

```
: hyp2 { a b -- r }  a a *  b b * + ;
--> 3 4 hyp2 .
25  ok
```

Declare them at the very top of the word, never inside an `if` or a loop body.

**Exercises**

1. Make a variable `count` and a word `tick` that adds 1 to it, and `.count` that prints it.
2. Fill an array of 5 cells with the squares `0 1 4 9 16` in a loop, then print them in another.

## 7. Text and numbers

```
--> : hello ." Hello, world!" ;
--> hello
Hello, world! ok
```

`."` prints the text up to the closing quote; use it inside definitions. `s"` gives a string as
an address and a length, to be used with `type`:

```
--> : greet ( a n -- )  ." Hello, " type ." !" ;
--> s" Forth" greet
Hello, Forth! ok
```

Output words: `cr` (new line), `space`, `n spaces`, `n emit` (a character code), `type`, and `.r` to
right-align a number in a field: `5 3 .r 7 3 .r` prints `  5  7`.

Numbers use the current **base**. `hex` and `decimal` switch it:

```
--> 255 hex . decimal
FF  ok
```

More operators: `1+ 1- 2* 2/ negate`, `and or xor`, `lshift rshift`, `/mod` (remainder and
quotient), `*/` (multiply, then divide, without losing precision: `2 3 4 */` is `2 * 3 / 4`).
`u.` prints an unsigned number: `-1 u.` prints `18446744073709551615`.

Floating point exists too, with its own stack and a trailing `e` on literals:

```
--> 1.5e 2.5e f+ f.
4.000000  ok
```

Random numbers: `n random` gives a number from 0 to n-1. Seed it first, or you get the same
sequence each run: `ms-ticks 1 or rand-seed !`.

## 8. Files and editors

Write your words in a file with any editor, or with Lugforth's own: `ed mygame.fs` (details in
[ed](#ed)). Save with Ctrl+S, quit with Ctrl+Q. A file is just the text you would type:

```
\ squares.fs
: square ( n -- n*n )  dup * ;
: squares ( n -- )  1+ 1 ?do i square . loop ;
```

Load it, then use the words:

```
--> include squares.fs
--> 5 squares
1 4 9 16 25  ok
```

`include` is relative to the file that contains it. `needs name` loads a library once. To run a
program directly: `lugforth squares.fs`. Add `bye` as the last line to exit afterward.

If a file produces `Error including: file` and nothing else, an error was thrown while loading:
bisect by putting `.` or `.s` lines in the file to find where it stops.

## 9. When it goes wrong

| Message | Meaning |
|---|---|
| `ERROR: foo NOT FOUND!` | the word `foo` is not defined (typo, or not loaded yet) |
| `STACK UNDERFLOW` | a word wanted more numbers than the stack held |
| `Error including: file` | an error stopped the loading of that file |

After an error Lugforth empties the stack and gives the prompt back. A few habits save hours:

* Write the stack effect first and check it with `.s` after each test.
* Test every word alone before using it in another.
* `see word` shows what was really compiled.
* A word that leaves the stack changed, or takes more than it should, is the cause of most bugs.
* More traps in [docs/dev/PITFALLS.md](dev/PITFALLS.md).

## 10. Three small programs

**FizzBuzz.** Numbers 1 to 15; multiples of 3 give Fizz, of 5 give Buzz, of both FizzBuzz.

```
: fizzbuzz ( -- )
  16 1 do
    i 15 mod 0= if ." FizzBuzz " else
    i 3 mod 0= if ." Fizz " else
    i 5 mod 0= if ." Buzz " else
    i . then then then
  loop ;
--> fizzbuzz
1 2 Fizz 4 Buzz Fizz 7 8 Fizz Buzz 11 Fizz 13 14 FizzBuzz  ok
```

**A sieve of Eratosthenes.** An array of flags, one byte per number; striking out the multiples of
each prime found. Note the test before `?do`: for `i * i` above the limit, there is nothing to strike
and `?do` must not be entered.

```
50 constant limit
create flags limit allot
: sieve ( -- )
  flags limit 1 fill
  limit 2 do
    flags i + c@ if
      i .
      i i * limit < if
        limit i i * do 0 flags i + c! j +loop
      then
    then
  loop ;
--> sieve
2 3 5 7 11 13 17 19 23 29 31 37 41 43 47  ok
```

**A word that takes a word.** `'` (tick) gives the address of a word, `execute` runs it. This is how
Forth passes behavior around:

```
: .name ( -- ) ." Lugforth" ;
: rep ( n xt -- )  swap 0 ?do dup execute space loop drop ;
--> 3 ' .name rep
Lugforth Lugforth Lugforth  ok
```

**Where next.** Games with graphics and sound use the SDL2 words ([SDL2.md](SDL2.md) is the tour, [SDL2-words.md](SDL2-words.md) the list of every word, [SDL2-web.md](SDL2-web.md) the web build): read `examples/games/sdl2_snake.fs`, the
shortest, then `sdl2_tetris.fs` and `sdl2_breakout.fs` ([Examples](#examples)). `ed` and `writhdeck`
are written in Forth too, in `ports/posix/`: reading them shows the same words at a larger scale. For the
rest of the language, [docs/WORDS.md](WORDS.md) lists every word with a short description. To go
deeper, the classic books are *Starting Forth* and *Thinking Forth* by Leo Brodie, both free online.

## Answers to the exercises

**2.** (1) `3 4 swap -` gives `4 3 -`, that is 1. (2) `2 3 1`. (3) `5 dup 3 + swap 3 - * .` prints 16.
Check with `.s`.

**3.** (1) `: double-sum ( a b -- n ) + 2* ;` (2) `: area ( w h -- a ) * ;` then
`: volume ( w h d -- v ) >r area r> * ;`, or with `rot`: `: volume ( w h d -- v ) -rot * * ;`
(3) `: fahrenheit>c ( f -- c ) 32 - 5 * 9 / ;`

**4.** (1) `: abs2 ( n -- |n| ) dup 0< if negate then ;`
(2) `: between? ( n lo hi -- flag ) rot tuck >= -rot <= and ;`

**5.** (1) `: sum-to ( n -- sum ) 0 swap 1+ 1 do i + loop ;`, `100 sum-to .` prints 5050.
(2) `: triangle ( n -- ) 1+ 1 ?do i 0 ?do [char] # emit loop cr loop ;`
(3) `: grid ( -- ) 10 1 do 10 1 do i j * 3 .r loop cr loop ;`

**6.** (1) `variable count`, `: tick 1 count +! ;`, `: .count count @ . ;`
(2) `create sq 5 cells allot`, `: fill5 5 0 do i i * sq i cells + ! loop ;`,
`: show5 5 0 do sq i cells + @ . loop ;`

# Reference

## Layout

```
common/      shared core (Forth sources and C headers)
ports/       one directory per platform: posix, web, windows, esp32, pico,
             pico-ice, gba, android, rpi (bare metal: QEMU, Raspberry Pi 3/4), efi (bare x86-64 PC)
examples/    example programs and benchmarks
tools/       build and conversion scripts
docs/        MANUAL.md, WORDS.md, dev/ (notes for developers), pages/ (website), site/ (uEforth pages), images/
lib/         shared Forth libraries (hashing)
attic/       old experiments
```

## Binaries

Pushing a tag such as `v7.0.8.1` (or running `.github/workflows/release.yml` by hand) builds and publishes them.

| Platform | Notes |
|---|---|
| Linux | Tarball and AppImage, built on Debian 10 (glibc 2.27 or newer). The AppImage carries the SDL2 libraries, the examples and a font. From a console it gives the terminal interface; a click on the desktop opens the window of `lugforth --gui` (a terminal if it cannot open). Examples are copied on first run to `~/.local/share/lugforth/examples` (`LUGFORTH_HOME` changes the directory), never overwritten. |
| Android | `lugforth-VERSION-android.apk` (arm64), built by hand with `ports/android/build_apk.sh` and attached to the release. Signed with a debug key kept in `out/android/debug.keystore`: keep that file to be able to update an installed copy. |
| Windows x64 | `lugforth-<version>-windows-x64.zip`: `lugforth.exe`, `SDL2.dll` and `SDL2_ttf.dll` (zlib and FreeType licenses), the Forth bindings in `lib/` and `examples/`. `sdl2` and `sdl2-ttf` load them (tested in CI with the dummy video driver); run lugforth from its folder. `ed`, `writhdeck`, the console window and the sound (`play`) are POSIX only. |
| macOS | `lugforth-<version>-macos-universal.zip` (Apple silicon and Intel, macOS 11 or later). Not signed: right click, Open, or `xattr -d com.apple.quarantine lugforth`. Tested in CI on Apple silicon: the prompt, the tests, Tab completion and the SDL2 words (38 tests, headless) after `brew install sdl2 sdl2_ttf` (the libraries are looked up in the system and in `/opt/homebrew/lib`, `/usr/local/lib`); the console window, the editors and sockets have not been tried. |

Static binaries are not possible: Lugforth reaches libc, SDL2 and X11 through `dlopen`/`dlsym`.

## Console window

`lugforth --gui [file.fs]` (any Linux build with SDL2) opens a console window with a TrueType font,
mouse selection and paste, and scrolling back. The menu is hidden until the button at the top right
(or F10) is used: examples, text size, colors (with a picker), system fonts, help. The Examples menu is there only when
the current folder (or `LUGFORTH_EXAMPLES`) holds `.fs` files. A right click on the button hides it, a right click on its place
shows it again; F10 still opens the menu. A middle click pastes the X11 selection, as in other Linux programs: the text just selected in this window or in another program (not the clipboard, which Ctrl+V pastes; SDL2 2.26 or newer, with an older one the middle click pastes the clipboard). Text selected here with the mouse is offered to the other programs the same way. Ctrl + and Ctrl - change the text size, Ctrl 0 resets it. Alt+Enter is full screen.

Settings are kept in `~/.local/share/lugforth/lugforth.ini` (`LUGFORTH_HOME` changes the directory):

| Key | Value |
|---|---|
| `size=` | text size |
| `margin=` | margin |
| `fg=`, `bg=` | colors, hexadecimal RRGGBB |
| `font=` | a TrueType file |
| `fullscreen=` | the key, `Alt+Enter` by default |

## Drawing at the prompt

The words of this section and the sound one below are the SDL2 short words, described one by one in
[SDL2-words.md](SDL2-words.md) (tour: [SDL2.md](SDL2.md), web build: [SDL2-web.md](SDL2-web.md)); other names for
the same word, such as `plot` for `dot`, are listed under Aliases there.

Type `graphic` once, then draw as in BASIC on an 8 bit micro: the drawing shows at once, under the
text, with no `screen` and no `flip`.

    graphic
    0 0 100 100 line
    red color  200 100 50 30 box
    5 ink  320 240 60 circle          \ a color by number
    cls                               \ clears the drawing, `page` clears the text

The drawing area is 640x480 by default, whatever the console size; `w h graphic-size` changes it (the
bigger, the finer: `1280 960 graphic-size` on a console of 1280x960 gives one drawing pixel per screen pixel,
and on the desktop `1024 640 graphic-size` fills a window of that size); `dot`, `line`, `box`, `frame`, `disc`, `circle`
(and `text` on the boards) are those of the `sdl2` words. `ink` takes 0 black, 1 red, 2 green, 3 yellow, 4 blue,
5 magenta, 6 cyan, 7 white, 8 to 15 gray, orange, pink, brown, purple and light colors; `$rrggbb color`
and `r g b rgb` give any other. Works in the console window (`lugforth --gui`, and the Android app) and on
the Pi and the PC screens. A program that opens its own `screen` replaces the layer. In a terminal there is
no window: `graphic` says so.

## Sound at the prompt

In the console window (`lugforth --gui`) type `sound` once; elsewhere type `sdl2` (nothing to open, no window needed on Linux, the sound comes out of the speakers):

    440 300 beep                          \ a note: frequency in Hz, length in ms
    sine                                  \ the shape: square triangle saw noise sine
    s" t140 o4 l8 c d e c  e4 g4 r4 >c2" play

`play` takes notes written in MML (Music Macro Language, the `PLAY` of MSX and GW-BASIC): letters `c d e f g a b`,
`#` sharp, `-` flat, a length after the note (4 quarter, 8 eighth, a dot lengthens by half), `r` or `p` a rest,
`o4` the octave (`<` and `>` go down and up; `o4 c` is the middle C), `l8` the default length, `t120` the
tempo, `n49` a note by number (n49 is the middle C, n0 a rest), `&` ties two notes of the same pitch
(`c4&c8`), `v8` the volume (0 to 15), `w1` the wave. The full table is in [SDL2.md](SDL2.md).

**Several voices.** A comma in the string starts another voice, and all the voices start together
(4 at most, 512 notes each; the sound lasts as long as the longest):

    s" t120 o4 l8 e d c d e e e4, o3 l4 c g c g c2" play

Examples (each one is a single line to type after `sdl2`):

    \ a melody over a bass: the bass has its own octave, wave and volume
    s" t140 o4 l8 w2 v10 c d e f g4 g4, w0 v6 o3 l4 c g c g, w1 v8 o2 l2 c c" play

    \ a canon: the second voice waits a whole note (r1) and plays the same four notes
    s" t120 o4 l4 c d e c, r1 c d e c" play

    \ a chord of three voices, one note each: C, E and G
    s" l1 o4 c, o4 e, o4 g" play

    \ a note held while another moves: the sound lasts as long as the longest voice
    s" t120 l8 o4 v12 w4 e g b >e4 <b4, v7 w1 o3 l1 e" play

Each voice begins with the octave, length, tempo, wave and volume that the `play` started with, so repeat
`t` or `w` in the voices that need them. After the `play` the settings are those of the last voice.
On the web, on the Pi and on the PC the notes of all the voices are queued at once; with `sdl2-mixer`,
`play` is a different word (see [SDL2.md](SDL2.md)).

**Envelope.** `adsr ( attack decay sustain release -- )` shapes every note of `beep` and `play`: the attack
and the decay are in ms, the sustain is a level in percent of the peak, the release in ms is the end of the
note (it does not make it longer). `10 80 60 120 adsr` is a plucked sound, `300 0 100 300 adsr` a slow
swell. Examples:

    10 80 60 120 adsr                      \ pluck: quick attack, falls to 60 %
    s" t160 o4 l8 c e g >c4" play

    300 0 100 300 adsr                     \ swell: slow attack, slow release
    s" t80 o3 l2 c g" play

    5 0 100 40 adsr  sine                  \ soft organ: no click, short release
    s" t120 o4 l4 e g e c2" play

    20 150 40 400 adsr                     \ bell-like, with voices together
    s" t90 o5 l2 e c, o3 l2 c g" play

    adsr-off                               \ back to the plain note

`adsr@` reads the four values back and `adsr-off` returns to the plain note (the default: a flat note with a very short fade at its end). Hits
(`drum-hit`) and ABC tunes are not touched.

`wave`, `beep-volume` and `pitch-drop` take their value from the stack, as `color` does: `1 wave`, `5000 beep-volume`
(read them back with `wave@`, `beep-volume@`, `pitch-drop@`; `to wave` still works). `silence` stops. Tunes in ABC notation: `sdl2-abc`, `abc-play`.

## Mini build

`lugforth-mini` (in the tarball, also alone) is the POSIX build without the editors, SDL2, X11, the
console window and the web and telnet servers: about 110 KB against 260 KB. `ninja posix` builds both
(`out/posix/lugforth-mini`); `--gui` says there is no window. A part can still be loaded from a source
checkout: `needs ports/posix/sdl2.fs`, then `sdl2` (SDL2 itself must be installed).

## Line editing

Available in a terminal, in the web terminal, in the SDL console of the Android app and on the
bare-metal Pi and PC (`common/lineedit.fs`).

* Cursor keys, Home, End, Delete, Ctrl-A/E/B/F/D/K/U/L.
* History of 64 lines (up, down), not kept between sessions.
* Tab completes words. After `include`, `needs`, `required`, `included`, `s"`, `z"`, `r|` and `r~` it
  completes file names instead (not on the web).
* Piped input and the other ports use the plain `accept`.

## Pager

On a POSIX terminal and on the bare-metal consoles (`common/pager.fs`), the output of a line typed at
the prompt stops at each screenful with `-- more --`: space for the next page, Enter for one line,
`q` or Esc to stop.

* On by default. `pager-off` and `pager-on` switch it; put `pager-off` in `autoexec.fs` to keep it off.
* Not active on a pipe, while a file is included, in a program that reads keys, or in the SDL console.
* Lines wider than the screen count as one. A telnet session ending puts the plain `type` back.

## ed

`ed file.fs` (POSIX build, in a terminal or in the `--gui` window) opens or creates a text file.
Code: `ports/posix/editor.fs`, compiled on first use; tests: `ports/posix/editor_tests.fs`.

| Keys | Action |
|---|---|
| arrows, Home, End, PageUp, PageDown, Delete | movement and deletion |
| Ctrl+arrows, Ctrl+Home/End | by word, start/end of text |
| Tab, Enter | two spaces; newline keeping the indentation |
| Shift+arrows, Ctrl+K | select (Ctrl+K starts and stops a selection) |
| Ctrl+C, Ctrl+X, Ctrl+V | copy, cut, paste |
| Ctrl+Z, Ctrl+Y | undo, redo |
| Ctrl+F, Ctrl+N or F3 | find, next |
| Ctrl+G | go to line |
| Ctrl+S, Ctrl+Q | save, quit (twice if the text changed) |
| Ctrl+T | next color theme |

`.fs`, `.fth` and `.4th` files are highlighted (comments, strings, words of the language, numbers, the
name after `:`) in a terminal with colors. Themes: `default`, `colorforth` (red definitions, green
words), `light`, `solarized`, `mono`. The theme is kept in `editor.ini` beside `lugforth.ini`, in the
format of WrithDeck's ini files (see [writhdeck.ini](#writhdeckini)); the keys it also holds
(`limit_width`, `word_goal`, `wrap`) are those of `writhdeck`, which reads them once if it has no
`writhdeck.ini` yet. On the boards (Pi, PC) it is in the root of the disk.

## writhdeck

`writhdeck file.t2t` is the same editor for writing, after [WrithDeck](https://github.com/luginf/writhdeck)
(`ports/posix/writhdeck.fs`, built on `editor.fs`: both files are needed). Lines are wrapped in a
centered column; txt2tags and Markdown are highlighted (headings, lists, `**bold**`, `//italic//`,
`__underline__`, `%` comments). The status bar shows the words, the words of the session and the
progress to the daily goal.

Esc menu:

| Key | Action |
|---|---|
| `w` | wrap |
| `f` | focus: dims all paragraphs but the current one, the line stays in the middle |
| `h` | Hemingway: no deleting or moving back |
| `t` | timer, stopwatch or countdown |
| `o` | outline (TOC, table of contents) of the headings |
| `v` | word occurrences, most used first |
| `s` | statistics |
| `r` | replace all |
| `g` | daily goal |
| `a` | autosave |
| `+` `-` | width |
| `c` | color theme |
| `q` | quit |
| `?` | help |

Ctrl+Y redoes. Ctrl+O saves and goes back to the file browser, where `writhdeck` alone starts: Enter
opens, `n` new, `/` filter, `r` rename, `d` delete, `b` backup.

### writhdeck.ini

The settings are in `writhdeck.ini`, in the format of [WrithDeck](https://github.com/luginf/writhdeck)
(the Tcl program): `key = value` lines, `[editor]`, `[behaviour]` and `[misc]` headings (ignored when
reading), `#` or `%` for comments (alone on a line, or after a blank), `yes`/`no` for booleans. Only
the keys this editor uses are written. The file is read each time a text is opened and rewritten
when a setting changes in the Esc menu. It is **not** the Tcl program's file
(`~/Documents/writhdeck/writhdeck.ini`): it lives beside `lugforth.ini` (`~/.local/share/lugforth`,
or `LUGFORTH_HOME`), and in the root of the disk on the Pi and PC. As in WrithDeck, a file named
`writhdec.ini`, `writhd.ini`, `wrid.ini` or `writhdeck.ini` in the current folder wins (first found,
in that order). Without any, the `editor.ini` of older versions is read once.

| Key | Default | Meaning |
|---|---|---|
| `scheme` | `default` | `default`, `colorforth`, `light`, `solarized`, `mono` (this editor's own, not the Tcl ones) |
| `limit_width` | `72` | columns of the centered text column (30 to 300) |
| `wrap` | `yes` | soft wrapped lines in that column (this editor only) |
| `hemingway_mode` | `no` | start in Hemingway mode |
| `word_goal` | `500` | daily goal in words, `0` for none |
| `bar` | `yes` | status bar; `no` hides it, a message still shows there |
| `help_bar` | `Esc: menu` | text of the `help_bar` token when there is no message |
| `status_left`, `status_center`, `status_right` | see below | the tokens of each zone of the status bar |
| `autosave_enabled` | `no` | autosave (the Tcl default is `yes`) |
| `autosave_interval` | `1` | minutes between two autosaves (1 to 60) |

The status bar has three zones, each a list of tokens separated by blanks. The default is
`status_left = filename dirty words session goal timer modes help_bar`, the two others empty.
`filename` `dirty` `words` `goal` `timer` `help_bar` and `space` are WrithDeck's tokens. `session`
(words written since the text was opened) and `modes` (`[focus]`, `[hem]`, `[auto]`) are this
editor's; other tokens of WrithDeck (`ln`, `col`, `chars`, `clock`...) are not implemented, and any
unknown word is shown as it is. To hide the goal, take `goal` out of the list, or set `word_goal = 0`.
A message (`Wrap on`, `Autosaved`...) shows in the place of `help_bar`, or after the right zone if there is
none. Example, name on the left, words in the middle, the goal on the right:

```
status_left   = filename dirty
status_center = words
status_right  = goal help_bar
```

## Examples

`examples/` has games, benchmarks and demos; see [examples/README.md](../examples/README.md).

| Program | What |
|---|---|
| `sdl2_breakout.fs` | breakout |
| `sdl2_roguelike.fs` | roguelike |
| `sdl2_tetris.fs`, `sdl2_snake.fs`, `sdl2_minesweeper.fs` | rewritten after the games of r3forth |
| `sdl2_mastermind.fs` | mastermind: four colors out of six in ten tries, keyboard or mouse |
| `sdl2_3d.fs` | turning wireframes |
| `sdl2_2048.fs` | sliding tiles; `--no-anim` turns the animation off |
| `2048.fs` | 2048 in the terminal |
| `fm/sdl2_fm.fs` | FM synthesizer, MIDI keyboard on Linux and in the browser |
| `inference/` | small rule based inference engine, README in English and French, Makefile with scenarios |

Licenses: 2048, 3d and the inference engine are ports of projects of Marc Petremann
([eForth Windows](https://github.com/MPETREMANN11/eForth-Windows), GPL); tetris, snake and minesweeper
follow [r3forth](https://github.com/phreda4/r3) by Pablo Hugo Reda (MIT, the Lugforth code is rewritten).
The rest of the repository is Apache 2.0. See [NOTICE](../NOTICE).

Tests: `make -C examples test-ports` runs the logic tests, `make -C examples smoke` loads every SDL2
example headless.

## Varvara: running Uxn ROMs

`examples/system/varvara/` is a [Uxn/Varvara](https://100r.co/site/uxn.html) virtual machine written in Lugforth.

* `lugforth examples/system/varvara/varvara.fs program.rom` runs a ROM in an SDL2 window, like `uxnemu`
  (Screen, Controller, Mouse, Datetime, Audio, File, Console input). F1 zoom, F3 screenshot, F4 reboot;
  a ROM can be dropped on the window. Without a ROM it shows a usage screen.
* `uxncli.fs` runs a ROM in the terminal, like `uxncli`.
* `launch.fs` is a launcher with options: `-c` terminal, `-z` zoom, `-s WxH` screen size, `--no-mouse`,
  `-q`, and tokens for writhdeck-uxn.

The upstream opcode test (`opctest.rom`) passes. It is about 60 times slower than the C uxn, so it
suits ROMs that mostly wait for events. See `examples/system/varvara/README.md`.

## Several terminals (Alt+F1 to Alt+F4)

On the bare-metal ports (rpi and efi, not the desktop) `Alt+F2` opens a second terminal: another Forth,
with its own dictionary, stacks and screen, while the first one goes on running. `Alt+F1` is back in
the first, `Alt+F3` and `Alt+F4` open a third and a fourth. `n term` does the same from the keyboard (Alt+Fn may
be taken by the host or the firmware), `terminal` tells the number of this one and `terminals` how many are open.
`bye` in a terminal other than the first closes it (in the first it powers off).

So a program can run in one terminal while the manual (`cat MANUAL.md`) or an editor is open in another. The
terminals take turns at each `pause` (waiting for a key, `ms` and the frame wait of the games do it): a
computation that never pauses holds the others. A program that has the screen keeps it while another terminal
is shown and gets it back, drawn again, on return. The keyboard, the mouse and the serial line are those of
the terminal shown; files and sound are shared. A terminal after the first has a heap of 32 MB, and does not
run `autoexec.fs`.

## Bare metal

### Raspberry Pi and QEMU (`ports/rpi`)

Lugforth runs with no operating system on 64-bit ARM: QEMU `virt` and `raspi3b` (which run on a Linux
x86_64 PC), a Raspberry Pi 3, and a Pi 4 (written blind). The console is the serial line. The `sdl2`
words are there: screen, text, images, keyboard and mouse, beeps and ABC tunes. Files are on the SD
card (FAT12/16/32): `include`, `ls`, and an `autoexec.fs` run at boot. `examples/games/sdl2_breakout.fs` and
`sdl2_roguelike.fs` run unchanged.

```
sudo apt install gcc-aarch64-linux-gnu picolibc-aarch64-linux-gnu qemu-system-arm
make -C ports/rpi run                   # the prompt, under QEMU virt
make -C ports/rpi roguelike BOARD=virt  # a QEMU window, with sound
make -C ports/rpi test                  # boots both machines and checks 121 things
```

What works where, what is tested, what needs a real board: [ports/rpi/README.md](../ports/rpi/README.md).

### x86-64 PC, UEFI (`ports/efi`)

A UEFI application, `EFI/BOOT/BOOTX64.EFI` on a FAT32 USB stick, started by the firmware (Secure Boot
off, then the one-time boot menu of the firmware, F12 on many PCs). It uses the firmware's screen, keyboard and files:
console on the screen, the `sdl2` words, `include` and the editors from the stick. Sound through Intel HD Audio (tried under QEMU only), no mouse.

```
sudo apt install gnu-efi ovmf qemu-system-x86 dosfstools mtools
make -C ports/efi usb     # out/efi/usb.img, to dd onto a stick
make -C ports/efi run     # the same image under QEMU with OVMF
make -C ports/efi test    # 124 checks under QEMU
```

Several sticks: `volumes` lists the FAT volumes (`*` marks the one in use), `n mount` makes one the disk of
`include`, `ls`, `cd`; a stick plugged after the start is found. See the README of the port.

Other file systems (exFAT, ext2/3/4, NTFS...): the firmware only reads FAT, but UEFI file system
drivers can be loaded from the stick. Copy the x64 drivers of
[EfiFs](https://github.com/pbatard/efifs/releases) (GPL, not part of lugforth) to `EFI/drivers/` on the
stick: `exfat_x64.efi` for exFAT, `ext2_x64.efi` for ext2, ext3 and ext4. Then:

```
include drivers.fs   \ loads every .efi of /EFI/drivers/, then shows the volumes
2 mount ls           \ an ext4 or exFAT volume, now in the list
0 mount              \ back to the stick
```

`drivers.fs` (on the stick made by `make usb`) is not run at boot: add the line `include drivers.fs` to
`autoexec.fs` to have the drivers each time. They only read: `include`, `cat`, `ls` work on those
volumes, writing a file fails. One driver by hand: `s" /EFI/drivers/ext2_x64.efi" pc load-driver . forth`
(0 loaded, -1 file not read, -2 refused by the firmware: not an x64 UEFI image or Secure Boot on,
-3 its start failed). The stick itself stays FAT32.

See [ports/efi/README.md](../ports/efi/README.md).

## Building

```
sudo apt install ninja-build gcc-arm-none-eabi
git clone git@github.com:luginf/lugforth.git
cd lugforth
./configure.py
ninja
```

Output goes to `out/`: `posix`, `web`, `windows`, `esp32`, `pico-ice`, `pages` (the GitHub Pages
site, `tools/make_pages.py`), plus `*-sim` builds used for tests. One platform: `ninja posix`,
`ninja web`, `ninja win64`...

* `./configure.py -f` skips the slower components (web, windows).
* `ninja install` installs the posix build to `/usr/bin`.
* ESP32: `ninja esp32-flash` (also `esp32s2-flash`, `esp32s3-flash`, `esp32c3-flash`, `esp32cam-flash`);
  set `PORT=com3` etc. to pick the port.
