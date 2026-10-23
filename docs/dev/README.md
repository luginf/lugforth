# Notes for developers (and for Claude)

What a person or an agent working on Lugforth should read before touching the code. The rules of work
(branches, commits, version) are in [CLAUDE.md](../../CLAUDE.md) at the root of the repository.

| file | what |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | build system (`configure.py`, `ninja`), tests, the C kernel and the Forth boot, platform tree |
| [FEATURES.md](FEATURES.md) | what exists and how it was tested: SDL2 words, editors (`ed`, `writhdeck`), games and ports of examples, `graphic`, `play` |
| [PORTS.md](PORTS.md) | Android, Raspberry Pi, UEFI PC, terminals, the web build and console, GBA, the bare-metal disk |
| [PITFALLS.md](PITFALLS.md) | traps met: Forth, loaders, SDL2, web, new platform boot chains, testing tricks |
| [IDEAS.md](IDEAS.md) | open questions (ideas left), ideas left, other Forths |

For users: [MANUAL.md](../MANUAL.md) (tutorial and reference), [WORDS.md](../WORDS.md) (every word),
[docs/pages](../pages) (the website), [attic/oldsite](../../attic/oldsite/README.md) (the pages inherited from uEforth).
