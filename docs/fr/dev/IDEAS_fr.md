# Idées, questions ouvertes et autres Forth

*Traduction française de [IDEAS.md](IDEAS_fr.md) (l'anglais fait foi).*

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
