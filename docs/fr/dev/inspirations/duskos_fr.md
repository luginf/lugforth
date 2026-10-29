# Dusk OS: ce qu'on pourrait emprunter

*Note en français seulement (pas de version anglaise).*

Notes prises en lisant une copie locale de Dusk OS (`~/src2/duskos`,
l'arbre `fs/`), le 2026-10-29. Rien n'a encore été porté: c'est une
liste d'idées, à peu près par ordre d'intérêt.

Dusk OS est un Forth 32 bits, « grand frère » de Collapse OS, conçu pour
rester utile quand on ne sait plus fabriquer d'ordinateurs modernes. Il
a son propre dialecte (`needs`, `unit`, `struct`, signatures de pile sur
32 bits): tout ce qui suit est donc un portage, pas une copie.

## Points forts

- Très compact et cohérent : un Forth qui se compile lui-même, avec un
  compilateur « presque C » de moins de 40 Ko (contre 1400 Ko pour tcc,
  selon `doc/design/simple.txt`).
- Un seul Forth pour plusieurs processeurs (i386, amd64, ARM, m68k,
  RISC-V) grâce à la couche d'assemblage harmonisée (`doc/hal.txt`), et
  des pilotes matériels (PCI, ACPI, EFI, PS/2, réseau, FAT).
- Documentation abondante : une page par unité (`fs/doc/`), avec la
  théorie puis l'API, et une visite guidée interactive.
- Bibliothèques variées et courtes : structures, mémoire, diff, macros,
  Lisp, assembleurs, émulateurs (6502, uxn/varvara).
- Licence CC0 : réutilisable sans condition (hors parties tierces).

## Points faibles

- Dialecte non standard et sans filet : pas de Forth 2012, noms et
  signatures propres (`needs`, `unit`, `const`), pile 32 bits. Le code
  ne se copie pas, il se porte.
- « Système peu indulgent » (c'est dit dans le tour) : il faut déjà
  connaître Forth et Dusk pour s'en servir.
- Peu de tests récents : la politique est de ne tester qu'après une
  régression ; la visite guidée n'est vérifiée qu'avant les versions.
- Logiciel surtout orienté usage « effondrement » : peu de jeux ou
  d'exemples graphiques, rendu et son limités.
- Portage Oberon, compilateurs C et Lisp : de grosses pièces dont
  l'entretien dépend d'une seule personne.

## Licence

Le code propre à Dusk est en CC0 (domaine public). Les parties venues
d'ailleurs gardent leur licence, dans `licenses/`: uxn (MIT: les
programmes `.tal` et les polices UF1/UF2), Oberon (`oberon/`, son propre
texte), `puff` (zlib, `ar/puff.c`). Le code repris de ces parties va
dans NOTICE avec son texte (voir CLAUDE.md).

## Facile et utile

- `text/diff.fs` (137 lignes): diff unifié par lignes, un checksum par
  ligne puis des « hunks ». Lugforth n'a pas de diff: un
  `examples/utils/diff.fs`, peut-être une commande dans `writhdeck`.
- `lib/macro.fs` (53 lignes): `thrice" ...%0..." Foo Bar Baz`, des
  macros textuelles qui détournent l'entrée. Pour les définitions
  répétitives.
- `gr/damage.fs` (60 lignes): un rectangle « sale » rangé dans un seul
  entier de 32 bits. Pourrait accélérer le redessin de `desktop.fs` et
  de la console EFI, dont le rendu logiciel est lent.
- `bench/*.tal`: bunnymark (`bunny.tal`), tests d'écran, de manette et
  de console (`uxnscr.tal`, `uxnctrl.tal`, `uxncon.tal`), du projet uxn.
  Bancs d'essai pour notre `varvara.fs`, à côté de
  `examples/bench/bench_sieve.tal`.
- `app/turye.tal`: un éditeur de polices UF2 en uxn, et les polices de
  `data/font` (atari8, sans12, term12, chicago12): utilisables comme
  polices pour les jeux SDL2.

## Plus ambitieux

- `emul/6502.fs` (131 lignes) avec `asm/6502.fs` et le désassembleur
  `asm/6502d.fs`: un émulateur 6502 en Forth, un exemple « émulateur » à
  côté de varvara. Dusk a aussi des assembleurs ARM, m68k, RISC-V et x86
  (`asm/`) à comparer avec `code_x64.fs` et `asm_xtensa.fs`.
- `comp/lisp/` (environ 400 lignes): un petit Lisp compilé en mots
  Forth. Un exemple-jouet.
- `app/ed.fs`: un éditeur façon vi où Ctrl+Entrée exécute la ligne sous
  le curseur, si bien que les documents (`doc/tour.txt`) sont
  interactifs. Un mode « document exécutable » pour `writhdeck`, par
  exemple pour le MANUAL.
- `lib/coop.fs` (141 lignes): une boucle d'événements coopérative.
  Chaque application s'inscrit pour les événements clavier, souris et
  inactivité, et plusieurs peuvent cohabiter sous une seule boucle. Un
  modèle pour `desktop.fs` et `filer.fs`.
- `mem/` (arena, pool, kv, cons, sort, logroll...) et `num/` (crc,
  xxhash, chacha, poly1305): des bibliothèques où puiser au besoin.
- `ar/puff.c` et `ar/ungz.fs`: un décompresseur gzip, utile sur les
  disques EFI.

## Notes de conception à lire

`doc/design/*.txt` (la simplicité comme argument, des tests seulement
après une régression, contraintes de portage) et `doc/hal.txt` (la
couche d'assemblage harmonisée, une couche portable au-dessus de
plusieurs processeurs).
