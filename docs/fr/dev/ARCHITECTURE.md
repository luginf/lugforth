# Architecture, compilation et tests

*Traduction française de [ARCHITECTURE.md](../../dev/ARCHITECTURE.md) (l'anglais fait foi).*

Comment l'interpréteur est construit et testé. Fait partie des notes pour les développeurs (et pour Claude): voir
[README.md](README.md).

## Vue d'ensemble

µEforth est une implémentation de Forth inspirée d'EForth, amorcée à partir d'un noyau C minimaliste. Elle vise
plusieurs plateformes: POSIX/Linux, ESP32 (Arduino), pico-ice, Web (WASM/JS) et Windows.

[WORDS.md](../../WORDS.md) est une référence générée des mots du vocabulaire `forth` et des autres vocabulaires
Linux/web, avec effets de pile et descriptions quand ils sont connus (`tools/words_reference.py`, relancez-le
après avoir ajouté des mots). Les mots SDL2 ont leur propre référence, [docs/SDL2-words.md](../SDL2-words.md).

## Système de compilation

La compilation se fait en deux temps: `configure.py` génère `build.ninja`, puis `ninja` compile.

```sh
./configure.py        # détecte les outils, génère build.ninja
ninja                 # compile toutes les plateformes activées

./configure.py -f     # configuration rapide, exclut les composants lents (web, windows)
```

Cibles par plateforme:
```sh
ninja posix
ninja esp32
ninja pico-ice
ninja win32
ninja win64
ninja web
ninja install         # installe la version posix dans /usr/bin/lugforth
```

Cibles de flashage ESP32: `ninja esp32-flash`, `ninja esp32s2-flash`, `ninja esp32s3-flash`,
`ninja esp32c3-flash`, `ninja esp32cam-flash`. Mettez `PORT=com3` etc. pour choisir la carte.

Paquets nécessaires: `sudo apt install ninja-build gcc-arm-none-eabi` (les outils arm sont nécessaires pour
pico-ice; nodejs pour le web).

## Tests

Les tests s'exécutent pendant la compilation de la plateforme (par exemple `ninja posix` compile et lance tous
les tests POSIX). Il n'y a pas de commande de test séparée.

Pour lancer les tests à la main après la compilation:
```sh
out/posix/lugforth common/all_tests.fs   # lance tous les tests communs
out/posix/lugforth common/base_tests.fs  # lance un seul fichier de test
```

Chaque fichier de test `common/*_tests.fs` définit des mots préfixés par `test-`. Le cadre de test
(`common/testing.fs`) découvre et lance tous les mots `test-*` du vocabulaire courant avec `run-tests`. Mots
d'assertion principaux: `=assert`, `assert`, `<assert`, `>assert`.

## Architecture

### Noyau C + amorçage Forth

Le système est un interpréteur à code enfilé écrit en C avec des extensions propres à chaque plateforme,
amorcé par des sources Forth compilées dans le binaire sous forme d'en-têtes C.

**Couche C** (`common/`):
- `tier0_opcodes.h` — types primitifs de cellule/pile et macros de bas niveau (PUSH, DROP, DUP, PARK/UNPARK, THROWIT)
- `tier1_opcodes.h`, `tier2_opcodes.h` — opcodes intégrés de plus haut niveau
- `common/interp.h` — la boucle de l'interpréteur avec l'aiguillage GNU « computed goto » (`goto **(void **) w`)
- `core.h` — recherche dans le dictionnaire, conversion des nombres, disposition de l'en-tête de mot
- `bits.h` — aides pour la taille des cellules
- `floats.h`, `calls.h` — opcodes des flottants et des appels étrangers

Chaque plateforme définit `PLATFORM_OPCODE_LIST` (ses propres macros C pour les opcodes spécifiques) et
`VOCABULARY_LIST`, puis fait un `#include` des en-têtes communs. Par exemple `ports/posix/main.c` (en fait
`ports/posix/interp.h`) ajoute `DLSYM`, `ERRNO` et `CALLING_OPCODE_LIST`.

**Couche d'amorçage Forth**:
Le source Forth n'est jamais chargé depuis des fichiers à l'exécution — il est compilé dans le binaire sous la
forme d'un en-tête C `const char[]` avec `tools/importation.py`. Pour POSIX: `ports/posix/posix_boot.fs` →
`out/gen/posix_boot.h`.

Séquence de démarrage (plateforme, par exemple posix):
1. `common/phase1.fs` — mots Forth de base (contrôle de flot, reconnaisseurs, vocabulaires, flottants, structures)
2. `ports/posix/posix.fs` propre à la plateforme — liaisons avec les appels système POSIX
3. `common/phase2.fs` — utilitaires de plus haut niveau, génération de code, locales, case
4. `common/phase_filetools.fs`, `common/phase_desktop.fs` — E/S de fichiers, intégration au bureau
5. `common/fini.fs` — transfère les mots intégrés dans le dictionnaire, installe la base de sauvegarde/restauration, exécute le xt d'autoboot

### L'outil `importation.py`

`tools/importation.py` est le préprocesseur de compilation. Il:
- Résout les directives `needs fichier.fs` (comme un include-once pour les fichiers Forth)
- Résout les `#include "fichier.h"` dans les fichiers C/C++
- Fait les substitutions de chaînes `-D` (par exemple `VERSION`, `STABLE_VERSION`)
- Fait les substitutions par contenu de fichier `-F` (par exemple `REVISION`)
- Peut émettre des en-têtes C qui enveloppent le contenu assemblé (options `--name`, `--header`)

### Fichiers BUILD

`configure.py` évalue les fichiers `BUILD` (un DSL Python) pour générer `build.ninja`. Le `BUILD` racine appelle
`Include('ports/posix')`, `Include('ports/esp32')`, etc. Chaque `BUILD` de plateforme utilise des fonctions
comme `Importation()`, `Compile()`, `ForthTest()`, `Alias()`, `Default()`.

`ForthTest(target, forth_binary, test_file)` lance un test Forth en exécutant `interp forth test` et en
capturant la sortie dans `target`.

### Structure des plateformes

- `common/` — sources Forth et en-têtes C partagés par toutes les plateformes
- `editors/` — `ed` et `writhdeck` (partagés par posix, windows, efi, rpi, gforth)
- `ports/posix/` — Linux/macOS: point d'entrée C, liaisons POSIX, sockets, X11, pthreads
- `ports/esp32/` — `.ino` Arduino + builtins C++; extensions facultatives dans `ports/esp32/optional/`
- `ports/pico-ice/` — carte FPGA à base de RP2040
- `ports/web/` — cible JavaScript/WASM
- `ports/tic80/` — module wasm32 pour la console TIC-80 (wasm3), en bac à sable: voir PORTS.md
- `ports/windows/` — version Win32 par compilation croisée MSVC depuis WSL
- `tools/` — scripts Python de compilation
- `docs/pages/` — le site GitHub Pages (Markdown, construit par `tools/make_pages.py`); `attic/oldsite/` — le site de documentation hérité d'uEforth (voir son README); `docs/dev/` — ces notes
