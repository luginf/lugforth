# Inference engine

`inference.fs` is a small forward-chaining inference engine: it applies "if ... then ..."
rules to known facts until nothing new can be deduced. It is a port of the INFERENCE project of
Marc Petremann ([eForth Windows](https://github.com/MPETREMANN11/eForth-Windows), GNU General
Public License): the facts and the rule definitions are his, the `infer` loop is new.

Files (in this directory): `inference.fs` (the engine), `weather.fs` and `lamp_diagnosis.fs`
(examples), `inference_tests.fs` and `lamp_diagnosis_tests.fs` (tests, `make test-ports`).

## Facts

A fact is a named value, true (`-1`) or false (`0`). `FACT:` defines it, `to` changes it.

```forth
FALSE FACT: rain
TRUE  FACT: cold
-1 to rain
rain .             \ -1
```

Facts live in the `facts` vocabulary: type `also facts` to use them at the prompt.

## Rules

A rule is a condition (a word that leaves a flag) and an action (a word that changes facts).
`:CONDITION` and `:ACTION` start two anonymous words, `RULE:` names the pair and keeps it
in the `rules` vocabulary.

```forth
:CONDITION  rain cold and ;      \ if it rains AND it is cold
:ACTION     TRUE to ice ;        \ then there is ice
RULE: r-ice
```

## The `infer` loop

1. Every rule is marked "not run yet".
2. Rules are tried one after the other. A rule that has not run and whose condition is true
   runs its action and is marked as run.
3. If at least one rule ran during that pass, go back to 2.
4. Otherwise stop.

So rules **chain**: the action of one rule can make the condition of another true, even if that
other rule was defined first, and the order of definition does not matter. A rule runs at most
once per `infer`, which guarantees that the loop ends.

Other words: `.facts` shows every fact and its value, `clear-facts` sets them all to false,
`.rules` counts the rules, `clear-rules` forgets them, `reset-rules` marks them all "not run".

## Example 1: the weather (`weather.fs`)

Facts: `cold rain ice snow umbrella slippery`.

| Rule | Condition | Action |
|---|---|---|
| `r-ice` | rain AND cold | `ice` |
| `r-snow` | rain AND cold AND no ice | `snow` |
| `r-slippery` | ice | `slippery` |
| `r-umbrella` | rain AND NOT cold | `umbrella` |

```forth
include examples/utils/inference/inference.fs
include examples/utils/inference/weather.fs
also facts
-1 to rain  -1 to cold
infer
.facts
```

Result: `ice = -1`, `slippery = -1`, `snow = 0`, `umbrella = 0`.
First pass: `r-ice` runs and the ice appears; `r-snow` is tried after it, sees the ice, so `snow`
stays false; `r-slippery` sees the ice and runs. Second pass: nothing runs, the engine stops.
With only `-1 to rain`, just `r-umbrella` runs.

## Example 2: a lamp that does not light (`lamp_diagnosis.fs`)

Observed facts: `no-light`, `socket-ok`. Conclusions: `change-bulb`, `mains-failure`.

```forth
include examples/utils/inference/inference.fs
include examples/utils/inference/lamp_diagnosis.fs
also facts
-1 to no-light  -1 to socket-ok
infer  .facts
```

`r-bulb` (no light AND the socket works) concludes `change-bulb = -1`. With the socket dead
(`-1 to no-light` only), `r-mains` concludes `mains-failure = -1`. For a new case, call
`clear-facts` before setting the new facts.

## Try it: `make`

`make` in this directory runs seven concrete scenarios (weather, lamp, a chain of rules), prints the facts after `infer` and checks the expected conclusions (`-> OK`), then the unit tests. `make weather-rain-cold`, `make lamp-bulb`... run one.

## Writing your own

```forth
include examples/utils/inference/inference.fs
FALSE FACT: a   FALSE FACT: b   FALSE FACT: c
:CONDITION a ;   :ACTION TRUE to b ;   RULE: a-gives-b
:CONDITION b ;   :ACTION TRUE to c ;   RULE: b-gives-c
also facts  -1 to a  infer  .facts      \ a, b and c are all true: a chain of two rules
```

## Limits

* The engine is **forward chaining**: it goes from the facts to their consequences. It cannot
  answer "what would be needed to get X?".
* Facts are true or false; a fact can hold a number, but the engine does nothing special with it.
* At most 64 facts and 64 rules (`MAXFACTS`, `MAXRULES` in `inference.fs`).
