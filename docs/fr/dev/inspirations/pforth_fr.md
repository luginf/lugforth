# pforth: points forts, points faibles, ce qu'on peut en retirer

*Note en français seulement.*

Notes du 2026-10-29, prises sur pforth 2.0.0 (paquet Debian `1:2.0.1-1`,
`/usr/bin/pforth`, 140 Ko, construit en 32 bits « LE/32 ») et sur ses
fichiers `README.md` et `RELEASES.md`. Auteur: Phil Burk (3DO), avec
Larry Polansky, David Rosenboom, Darren Gibbs; cellules 64 bits par
Aleksej Saushev. Code sur GitHub (`philburk/pforth`).

## Points forts

- **Très portable**: écrit en C ANSI, il n'a besoin que d'une sortie et
  d'une entrée de caractères; il n'exige pas de système d'exploitation
  (fait pour mettre au point du matériel embarqué).
- **Petit**: binaire de 140 Ko, 1,8 Mo de mémoire au démarrage, démarre
  instantanément.
- **Dictionnaire exportable en source C**: on compile des tests sur un
  PC, puis on les intègre à une cible qui n'a pas de fichiers (little ou
  big endian).
- Licence permissive (type 0BSD: « permission to use, copy, modify,
  and/or distribute... »), donc réutilisable sans condition.
- Insensible à la casse comme lugforth (vérifié: `: Foo 1 ; foo .`
  affiche 1).
- Sait faire: locales `{ a b -- c }`, flottants, `S\"`, `[DEFINED]`,
  structures ANS, `BYE-CODE` (code de sortie testable en intégration
  continue), tests sur GitHub Actions et CMake.

## Points faibles

- « Basé sur ANS Forth mais pas 100 % compatible » (son propre README):
  moins complet que gforth ou nous (qui passons la suite de Hayes et
  Gerry Jackson).
- Version Debian en **32 bits** (le 64 bits existe mais dépend du
  build).
- Pas un environnement de bureau: l'auteur le dit (pas d'outils
  graphiques, pas de SDL, ni réseau, ni FFI dynamique).
- Entrées/sorties minimales, pas d'éditeur de ligne riche.
- Un peu lent (0,48 s sur notre banc, comme lugforth).
- Observé: lancé avec l'entrée standard fermée et sans `bye`, il ne
  s'arrête pas (il tourne en boucle).
- Peu de bibliothèques fournies, peu d'exemples.

## Ce qu'on pourrait en retirer

1. L'**export du dictionnaire en source C** (son README décrit la
   compilation sur PC puis l'export en C): une manière de construire une
   image pour une cible sans fichiers (nos ports `pico`, `esp32`, `gba`
   ont des chaînes propres, mais l'idée est comparable).
2. `BYE-CODE`: faire échouer un test avec un code de retour non nul,
   pratique pour nos scripts (`make test`).
3. Sa **structure de projet**: `csrc/` (noyau C), `fth/` (Forth),
   `platforms/` par plateforme.
4. Licence et structure sont une bonne référence pour un noyau minimal
   facile à porter (comme notre noyau C).
