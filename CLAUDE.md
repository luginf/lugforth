# CLAUDE.md

The guide to this repository (build, tests, architecture, gotchas) is
[docs/SKILLS.md](docs/SKILLS.md); read it first.

## Open question: control structures outside a definition

Decision pending (the maintainer is thinking about it), nothing is implemented yet.

Today `do`, `?do`, `if`, `begin`... typed outside a definition (at the prompt,
at the top level of a file, in an `evaluate`d string) give no error and a wrong
result: `3 0 do 42 . loop` prints 42 once and leaves `3 0` on the stack,
`1 if 7 . then` runs its body whatever the flag and leaves the `1`. Wrap such
code in a definition (`: t 3 0 do ... loop ; t`), tests included.

The standard leaves this case undefined. What other Forths do:

1. An error, the word being compile-only (gforth, pForth): `-14 throw`.
2. Interpreted loops in brackets that re-read the source, like the standard
   `[IF] [ELSE] [THEN]`: gforth has `[DO] [?DO] [LOOP] [+LOOP] [BEGIN] [UNTIL]
   [AGAIN] [WHILE] [REPEAT] [FOR] [NEXT]`.
3. A temporary anonymous definition: met in interpret state, `do`/`if`/`begin`
   open a `:noname`, compiled up to the end of the structure, then run and
   forgotten (Win32Forth, for example).
4. Quotations, always compiled (RetroForth, Konilo, Factor): `#10 [ ... ] times`.

Lugforth has only the conditionals in brackets (`common/conditionals.fs`:
`[IF] [ELSE] [THEN] [DEFINED] [UNDEFINED] DEFINED?`); none of the bracket loops
of point 2 exist.

Options considered:

- Point 1 only: the control words throw -14 in interpret state, a clear
  message instead of a silent wrong result. Smallest change.
- Point 3: what is typed at the prompt works as in a definition (and runs
  compiled). Nesting has to be handled, and the boot, ESP32 and Pico checked.
- Point 2: `[DO] [?DO] [LOOP] [+LOOP] [I] [BEGIN] [UNTIL] [AGAIN] [WHILE]
  [REPEAT]` beside `[IF]`, with tests in `common/conditionals_tests.fs`. Easy
  within one line (reset `>in`); across lines it needs `save-input` /
  `restore-input` (not checked whether lugforth has them), and lines typed at
  the prompt are gone. Mostly useful to generate code or data while a file loads.
