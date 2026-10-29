# Idées, questions ouvertes et autres Forth

*Traduction française de [docs/en/dev/IDEAS.md](../../en/dev/IDEAS.md)
(l'anglais fait foi).*

Décisions en attente et choses non faites. Fait partie de
[README.md](README_fr.md). La liste de choses à faire des éditeurs est
dans [FEATURES.md](FEATURES_fr.md).

## Structures de contrôle hors définition - fait le 2026-10-23

`common/interpret_control.fs` (tests dans `interpret_control_tests.fs`)
met en œuvre l'option 3 ci-dessous: un reconnaisseur (`rec-ctl`, premier
dans `RECSTACK`) attrape `if begin do ?do for case aft ahead` en état
d'interprétation, ouvre un `:noname`, compile chaque mot jusqu'au
`then until again repeat loop +loop next endcase` correspondant
(profondeur comptée à partir d'une table de deltas), l'exécute, puis le
récupère (gardé si la structure a alloué des données). Chargé par les
démarrages posix, web, efi, rpi et windows; PAS par esp32, pico,
pico-ice, gba (non compilés ici). Limite connue: une erreur de
compilation à l'intérieur de la structure laisse l'interpréteur en état
de compilation jusqu'à `[`, comme pour n'importe quelle définition;
l'ouvreur suivant repart proprement.

Le standard laisse indéfinie la sémantique d'interprétation de ces mots.
gforth les rend réservés à la compilation (erreur) et propose à la place
les crochets `[DO] [?DO] [LOOP] [BEGIN] [UNTIL]...` (point 2). Autres
options examinées:

1. Erreur seulement (`-14 throw`). 2. Boucles entre crochets qui
   relisent la source. 3. Définition anonyme temporaire (choisie). 4.
   Citations (RetroForth, Factor).

## Idées restantes, d'après DurexForth (à décider plus tard)

DurexForth (https://github.com/jkotlinski/durexforth, C64; manuel
https://jkotlinski.github.io/durexforth/, lu le 2026-10-20, licence non
vérifiée avant d'emprunter du code: écrire le nôtre) suggère, pour
`graphic` et `play`:
- dessin: `paint ( x y -- )` remplissage, `plot`, `peek`, `pen` en mode
  inverse (XOR), `line` depuis le dernier point;
- tortue (examples/graphics/sdl2_turtle.fs): `back`, `turtle@` /
  `turtle!` (sauvegarde et restauration de l'état, pour les fractales
  récursives), `moveto ( x y deg -- )`;
- son: `voice! note! cutoff! filter!` à la SID (l'enveloppe ADSR,
  `adsr`, et les voix de `play`, séparées par des virgules, sont faites
  depuis 7.0.11.11);
- les autres réglages (`zoom`, `width`, `height`, `key-hold`) sont
  encore des valeurs avec `to`, contrairement à `color`, `wave`,
  `beep-volume`.

## Autres projets à créditer ou dont s'inspirer

Vérifiez un projet en ligne avant de le nommer dans le README («
Inspiration »), NOTICE ou une liste d'idées. Bifurcan, un temps suggéré
comme Forth pour Uxn, est un programme Tal (une horloge), pas un Forth.

Autres candidats de r3forth: bdash (Boulder Dash, sprites propres
nécessaires), sokoban (niveaux de licence peu claire), démos de terminal
(feu, matrix, tore), util/penner.r3 (assouplissements), lib/sdl2poly.r3
(remplissage de polygones). D'eForth Windows: plus d'exemples
d'inférence (`INFERENCE/docs/*.odt`), extras `3Dmodelisation` /
`dumpTool`, autres projets en amont (lotto, 6809, flog, BMP, HTTP).

## Autres Forth, pour comparaison (ne font pas partie de ce dépôt)

- gforth 0.7.9 n'embarque pas SDL. Il existe des liaisons:
  `JeremiahCheatham/Gforth-SDL2-Bindings` (52 fichiers, SDL2 + image +
  mixer + ttf, API brute, énumérations d'événements suffixées `_ENUM`),
  `foggynight/gforth-sdl2` (généré par SWIG, copie dans
  `garvalf/forth-is-fun` `gforth/lib` avec seulement SDL, événements,
  rendu, scancode, timer, vidéo, plus une petite aide `sdl_init.fth` et
  la tortue `forthtoise.fth`), et la cartouche `magnoquill-gforth.fth`.
- Elles utilisent `c-library` (du C compilé au chargement, demande gcc +
  libsdl2-dev; flottants et structures possibles) et sont en
  correspondance 1:1 avec l'API C; gforth a besoin de listes de mots
  sensibles à la casse (`table >order`) pour éviter le même conflit
  `SDL_Quit`/`SDL_QUIT`. Cette branche utilise `dlopen` (seul le `.so`
  est nécessaire), a un vocabulaire de haut niveau, et est la seule qui
  tourne aussi dans un navigateur.

## Portages vers des plateformes rétro (analyse du 2026-10-29)

Faite à la lecture du dépôt, sans rien compiler. Ce qui compte: un
compilateur C 32 bits pour la cible, assez de RAM, l'ordre des octets.
Le noyau est en C/C++ avec des cellules de la taille d'un pointeur (32
bits minimum); le port GBA tient dans 288 Ko de RAM sur un ARM7 à 16,8
MHz; `lugforth-mini` fait 150 Ko sur PC. `common/*.h` ne gère pas
l'ordre des octets: tout a été testé en petit-boutiste.

Réalistes, du plus utile au moins utile:

1. **Atari ST** (68000, 8 MHz, 512 Ko à 4 Mo): le meilleur candidat.
   Compilateur `m68k-atari-mint-gcc`; fichiers par GEMDOS, console et
   clavier par BIOS/XBIOS, écran par la mémoire vidéo. Risques: le 68000
   est gros-boutiste (accès par octet à une cellule, `w@`, `l@`,
   chaînes, dictionnaire) et refuse les accès mot à adresse impaire (`@`
   et `,` s'ils le supposent permis). Hatari est installé sur ce PC; le
   compilateur croisé manque.
2. **Amiga** (68000, 512 Ko ou plus): mêmes risques, chaîne
   `m68k-amigaos-gcc`; FS-UAE est installé ici.
3. **DOS 386** (DJGPP, 32 bits protégé): le plus facile, cellules de 32
   bits, petit-boutiste, mode VGA 13h.
4. **Nintendo DS** (ARM9, 4 Mo): très proche de la GBA (devkitARM).
5. **PlayStation 1, N64, Dreamcast, PSP** (MIPS, SH4, GCC): faisables
   mais lourds en pilotes pour peu d'intérêt.

Peu réalistes:

- **C64, Atari 8 bits, ZX Spectrum, Amstrad** (6502, Z80, 64 Ko):
  cellules de 32 bits trop lentes, noyau de 150 Ko trop gros pour 64 Ko,
  chaînes C limitées (cc65 est un C89 partiel, llvm-mos plus complet).
  Il faudrait un autre Forth à cellules de 16 bits écrit en assembleur,
  comme DurexForth (cité plus haut): un projet à part.
- **Sega Mega Drive**: 68000 correct mais 64 Ko de RAM, seulement avec
  un noyau réduit.
- **Atari Falcon, TT**: un Atari ST en plus gros.

Autre voie pour les machines très anciennes: la machine virtuelle
Uxn/Varvara que Lugforth exécute déjà (`examples/system/varvara`);
plusieurs plateformes anciennes ont une implémentation Uxn (à vérifier
lesquelles).

Pour un Atari ST: installer le compilateur croisé 68k; compiler le noyau
en simulateur POSIX avec un ordre d'octets inversé fictif, ou sous
`qemu-m68k` / `hatari`, pour trouver les accès non portables; écrire
`crt0` et les mots d'entrée/sortie et d'écran sur le modèle du port GBA.
