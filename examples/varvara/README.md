# Varvara in Forth

A [Uxn/Varvara](https://100r.co/site/uxn.html) virtual machine written in
lugforth, so that Uxn ROMs run inside lugforth.

| file | what it is |
|---|---|
| `uxn.fs` | the Uxn CPU (32 opcodes, modes 2/r/k, 16 banks of 64 KB), the System device (stack pointers, memory fill and copy, debug, halt) and Console output |
| `uxncli.fs` | runs a ROM in the terminal, like `uxncli` |
| `uxn_tests.fs` | 102 tests: random programs, jumps and calls, System device |

```
lugforth examples/varvara/uxncli.fs program.rom [arguments]
lugforth examples/varvara/uxn_tests.fs
```

`uxncli.fs` passes the arguments then stdin to the Console vector (type 2 for
an argument byte, 3 between arguments, 1 for stdin, 4 at the end) and exits
with the code written to System/state.

## Tests

The expected results of `uxn_tests.fs` come from
[uxn.wasm](https://github.com/remko/uxn.wasm), which passes the upstream
opcode test suite: 100 random programs without jumps or devices (the two
stacks and a hash of the memory are compared), one program with jumps, calls,
`LDR`/`STR` and keep mode (its Console output), and one with the System
device, checked by hand. The upstream `opctest.rom` has not been run yet.

## Speed

Each Uxn instruction costs about a microsecond (one pass of `bench_sieve.tal`
takes 0.86 s, against 7 ms in the C uxn), so about 100 times slower than
uxn: enough for ROMs that mostly wait for events, not for heavy ones.

## Not done yet

Screen, Controller, Mouse, File, Datetime and Audio devices.
