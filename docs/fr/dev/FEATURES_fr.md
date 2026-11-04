# Fonctionnalités et exemples: ce qui existe et comment ça a été testé

*Traduction française de
[docs/en/dev/FEATURES.md](../../en/dev/FEATURES.md) (l'anglais fait
foi).*

Éditeurs, mots SDL2, jeux et portages d'exemples, avec ce qui a été
vérifié ou non. Fait partie de [README.md](README_fr.md).

## Multimédia SDL2

Des mots courts orientés jeu au-dessus de libSDL2, pour la version POSIX
et (même source, mêmes mots) la version web. Docs complètes:
`docs/en/SDL2.md`, `docs/en/SDL2-words.md` (chaque mot),
`docs/en/SDL2-web.md`. Exemples: `examples/graphics/gfx_demo.fs`,
`examples/games/gfx_breakout.fs` et `examples/music/gfx_drums.fs` (une
boîte à rythmes de 16 pas); ces deux derniers tournent aussi sans
changement dans le navigateur par `ports/web/gfx_breakout.html` et
`ports/web/gfx_drums.html`. `examples/games/gfx_roguelike.fs` utilise la
planche de tuiles `examples/games/gfx_tiles.png` (dessinée par
`examples/games/make_gfx_tiles.fs`, un programme lugforth qui écrit le
PNG à la main avec CRC-32, Adler-32 et un bloc zlib non compressé:
aucune ressource extérieure; `examples/games/make_gfx_tiles.py` est le
même programme en Python, gardé pour comparer les deux, mêmes pixels)
par `sdl2-image` (`draw-part`, `image-tint`); `ports/web/sdl2.fs` a les
mêmes mots d'image sur canevas et `ports/web/gfx_roguelike.html` exécute
le même fichier. `load-image` réessaie un nom manquant sans ses dossiers
sur les deux plateformes. `examples/music/fm/` est un synthétiseur FM à
deux opérateurs avec clavier à l'écran et entrée MIDI, sur les deux
plateformes, volontairement gardé hors de l'interpréteur (voir son
README); l'interpréteur n'a que les crochets `render-hook`,
`active-hook`, `latency` et `scan?`. `examples/music/gfx_pianoroll.fs`
est un séquenceur piano-roll (4 pistes, motifs de 16 pas chaînables,
lecture MIDI+soundfont, export `.mid` et ABC); POSIX seulement, pas
porté sur le web. `examples/music/abc_random.fs` fait des airs
aléatoires en ABC (12 mesures, voix 1 en arpège, voix 2 en accords ou
contrepoint) et les joue en boucle sur le PC, le Pico et l'ESP32 (voir
docs/fr/dev/PORTS_fr.md); ses tests sont dans
`make -C examples test-ports`.

### Fichiers

- `ports/posix/sdl2.fs` - noyau, chargé paresseusement: taper `sdl2`
  compile le vocabulaire. Les extras se chargent de la même façon:
  `sdl2-image` (`ports/posix/sdl2_image.fs`), `sdl2-ttf`, `sdl2-mixer`.
  Tous sont tirés par `ports/posix/posix_boot.fs`.
- `ports/web/sdl2.fs` - même nom de vocabulaire et mêmes mots sur un
  canevas (état JS dans `context.sdl`, primitives `JSWORD:`). Seuls les
  mots courts y existent: pas de mots bruts `SDL_`, pas
  d'image/ttf/mixer, pas de
  `wait sdl-error window-handle renderer pen sample-rate`. Tiré par
  `ports/web/web_boot.fs`; `ports/web/BUILD` copie les pages et fichiers
  d'exemple dans `out/web`.
- Les tests ne sont PAS dans la compilation ninja. À lancer à la main:
  `SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy out/posix/lugforth ports/posix/sdl2_tests.fs`
  (22 tests) `ports/posix/sdl2_media_tests.fs` (7 tests),
  `ports/posix/sdl2_abc_tests.fs` (18 tests) et
  `examples/music/fm/gfx_fm_tests.fs` (24 tests: lecteur MIDI et
  événements ALSA, voix, hauteur). `common/forth_namespace_tests.fs`
  liste les nouveaux vocabulaires.

### Musique: soundfont et airs ABC

- `ports/posix/sdl2_mixer.fs` a `soundfont ( a n -- )` et `soundfont?`
  (`Mix_SetSoundFonts`): un fichier `.sf2` ou `.sf3`, cherché tel que
  donné, puis par son nom de base dans le dossier courant, puis dans le
  dossier du script (`1 argv`). FluidSynth ne lit que des fichiers, donc
  une soundfont ne peut pas venir de la mémoire. Charger un MIDI depuis
  la mémoire (`SDL_RWFromConstMem`, `Mix_LoadMUS_RW`) a échoué avec «
  Unrecognized audio format » et n'a pas été poursuivi.
- `sdl2-abc` (`ports/posix/sdl2_abc.fs`, `ports/web/sdl2_abc.fs`) lit
  des airs en notation ABC et les joue. Le lecteur est partagé:
  `common/abc.fs` définit `abc-parser` dans `internals`, un mot qui
  renvoie le source du lecteur sous forme de chaîne, que chaque chargeur
  de plateforme évalue (mots dans le vocabulaire `abc-int`, les publics
  dans `sdl2`). POSIX écrit un fichier MIDI standard dans un fichier
  temporaire fait par `mkstemps` (un nom fixe dans `/tmp` échouait quand
  un autre utilisateur l'avait créé), le supprime une fois chargé et le
  joue avec le mixer; le chargeur web programme des oscillateurs
  WebAudio. Tests: `ports/posix/sdl2_abc_tests.fs` (18). Un air s'écrit
  avec `r~ ... ~` parce que l'ABC emploie `|`.

## Éditeurs: `ed` et `writhdeck` (editors/editor.fs, writhdeck.fs, *_tests.fs)

Les éditeurs sont passés de `ports/posix/` à `editors/` (2026-10-25):
les deux mêmes fichiers servent au démarrage POSIX
(`ports/posix/posix_boot.fs`), à Windows, aux disques EFI et Raspberry
Pi (`lib/`), aux paquets de publication et au chargeur gforth.
`editor.fs` charge `eval_copy.fs` (gardé dans `ports/posix/`) seulement
quand `eval-copy` n'est pas encore défini: le démarrage PC l'a déjà, une
carte l'a dans `lib/`. Sous gforth `type` et `emit` sont des defers lus
avec `defer@`, et `emit` ne passe pas par `type` (le cache de rangées
redirige les deux là).

`ed` est un éditeur de texte en Forth; `writhdeck` est un portage de
WrithDeck (original dans `~/src/writhdeck`, Tcl/C, manuel
`writhdeck_MANUAL.md`) construit par-dessus. Les deux sont des chaînes
compilées au premier usage (`ed-src`, `wd-src`), donc pas de `|` dans le
source.

Fait dans writhdeck: lignes repliées dans une colonne centrée, mode
focus, Hemingway, mots et objectif du jour, chronomètre et compte à
rebours, plan (Échap o), tout remplacer, enregistrement automatique,
coloration txt2tags/Markdown, thèmes, menu Échap
(`w f h t o v s r g a + - c x q ?`), occurrences des mots (Échap v),
rétablir (Ctrl+Y, aussi dans ed), navigateur de fichiers (`writhdeck`
seul ou Ctrl+O: Entrée, n nouveau, / filtre, r renomme, d supprime, b
sauvegarde dans `backups/`, f favori, v fait défiler dossier / favoris /
fichiers récents; les deux listes sont gardées dans `writhdeck.rec` à
côté de l'ini, lignes `F chemin` et `R chemin`, 20 favoris et 10
fichiers récents, les fichiers choisis dans le navigateur sont notés
comme récents; `*` marque un favori dans la vue du dossier). Vitesse sur
un gros texte (670 Ko): les couleurs Markdown ne sont calculées que pour
les lignes à l'écran (`md-window`; une marque ne quitte jamais sa ligne;
`wd-scroll` marque les couleurs périmées quand `top` ou le nombre de
rangées change), pas pour tout le texte à chaque touche (c'était 0,56
s); le décompte de mots de la barre d'état est refait au plus tous les
20 fois son propre coût (`word-count`, le `word-count!` exact sert aux
statistiques). La clé ini `highlight` (yes/no) coupe les couleurs.

Seules les rangées changées sont envoyées (vue repliée): `wrap-row`
dessine une rangée dans un tampon en redirigeant `type` (`rc-type`) et
ne l'envoie que si elle diffère de la dernière rangée envoyée à cette
ligne (`rc-cache`, `rc-send`); une touche coûte environ 170 octets au
lieu de 1400. Le cache est vidé (`rc-reset`) avant et après le menu
Échap (tout recouvrement en part), quand la taille change et à chaque
chargement. La vue simple (non repliée) redessine encore tout. Un saut
loin sous le haut compte les rangées en remontant depuis le curseur
(`wrap-scroll`), pas en descendant depuis l'ancien haut. Clé ini
`open_last` (yes/no): sans fichier donné, le premier démarrage ouvre le
fichier le plus récent de `writhdeck.rec` s'il existe encore (le
WrithDeck Tcl ouvre son navigateur).

Pas de scintillement: `tick` (une fois par seconde) ne redessine que si
le minuteur tourne, si un message a été posé, si le décompte de mots est
périmé ou si la taille a changé (au repos = 0 octet envoyé au terminal;
il redessinait tout l'écran chaque seconde), et `wrap-row` écrit le
texte d'abord et efface le reste de la rangée après (il effaçait la
rangée avant d'écrire).

Numéros de ligne (Ctrl+L, clé ini `line_numbers`, vue repliée
seulement): montrés sur la première rangée de chaque paragraphe, dans
une marge de 6 colonnes; le décompte de la première ligne montrée est
gardé du dernier dessin (`line-at`, `cl-pos`/`cl-line`/`cl-len`), donc
une touche coûte quelques lignes, pas un parcours du texte (le cache est
abandonné quand le texte change avant le curseur, au chargement et après
« tout remplacer »). Position du curseur: `writhdeck.rec` garde aussi
des lignes `P position chemin` (position en octets, 50 fichiers, le plus
ancien abandonné), donc un fichier s'ouvre là où on l'avait laissé
(`cp-start` au chargement, `cur-mem` suit le curseur, enregistré par
`stats-hook` et par `cp-flush` à la fin de chaque session d'édition). Le
WrithDeck Tcl garde son propre `.writhdeck.json` (curseurs en
ligne/colonne, favoris, récents); Lugforth ne le lit ni ne l'écrit,
exprès, pour ne pas risquer de le corrompre.

Statistiques quotidiennes: les mots écrits dans chaque fichier chaque
jour (jamais diminués, sessions plus tôt dans la journée comprises) sont
gardés sous forme de lignes `S jour mots chemin` dans `writhdeck.rec`
(jour = jours depuis 1970, décalé par la clé ini `utc_offset` en heures;
seules les entrées d'aujourd'hui et celles des favoris sont gardées).
Notées toutes les 10 s pendant l'édition et sur Ctrl+S, Ctrl+O, Ctrl+Q.
`Échap s` ajoute « Today, this file / all files »; `s` dans le
navigateur liste les 14 derniers jours. Testé dans les tests unitaires
(`dstr`, `st-put`) et le test pty, nativement et sous gforth
(`python3 tools/editors_pty_test.py ports/gforth/writhdeck-gforth.fs`:
les éditeurs doivent continuer à s'y charger, lancez-le après chaque
changement de writhdeck.fs ou editor.fs). Page préc./Page suiv. marchent
(vérifié dans un pty, ed et writhdeck).

Reste à faire, d'après l'original (par ordre de priorité):
1. Détection de répétitions (même mot dans un intervalle) et correcteur
   orthographique (Hunspell par un tube `hunspell -a` ?).
2. Deuxième espace de travail (F10) et vue partagée verticalement
   (F3/F4).
3. Ctrl+Q dans un texte édité retourne au navigateur (maintenant il
   quitte); commandes modales `t s w q` de l'original.
4. Profils/schémas INI, i18n. Échap suivi aussitôt d'une lettre est lu
   comme Alt+lettre: les tests pty attendent 0,5 s entre les deux.
   Réglages: `ed` garde `editor.ini`, `writhdeck` `writhdeck.ini`, tous
   deux au format ini de WrithDeck (Tcl): `clé = valeur`, commentaires
   `%`/`#`, `[sections]` ignorées, analyseur `ini-text` dans editor.fs,
   `ini-kv` / `ini-body` / `ini-defname` / `ini-load` sont différés et
   `wd-install` / `ed-plain` les échangent. Jamais le propre
   `~/Documents/writhdeck/writhdeck.ini` du programme Tcl (ses `[keys]`,
   `[schemes]`, `[profiles]` seraient perdus). Zones de la barre d'état
   `status_left/center/right` avec des jetons (`session`, `modes` sont
   les nôtres); `dry` fait que `stype` ne fait que compter, pour mesurer
   une zone. Tests: `test-wd-ini` écrit dans out/tests, pas dans le
   répertoire personnel de l'utilisateur. Pas lancé sur les cartes (pas
   de chaîne d'outils aarch64 ni gnu-efi ici). Les deux tournent aussi
   sur la carte en matériel nu (ports/rpi, mots `ed` / `writhdeck`,
   fichiers dans /lib): les parties propres à POSIX sont sous
   `[defined] posix [if] ... [else] ... [then]` dans les chaînes de
   source, gardez-les ainsi. Les éditeurs finissent par
   `only forth ...`: un fichier qu'on `include` doit sauvegarder
   `current @` et le remettre (`current !`) à sa fin si l'appelant peut
   avoir empilé un vocabulaire (`sdl2` à l'invite en empile un), comme
   le fait varvara_rpi.fs: `definitions` après `previous` laisserait
   sinon l'appelant définir dans `sdl2`.

## Blocs et `blocks-editor` (common/blocks.fs, blocks_editor.fs) - 2026-10-31

`common/blocks.fs` (uEforth) a les mots de blocs standard et un éditeur
de lignes (vocabulaire `editor`: `l n p r a d e wipe`). Un seul tampon:
un bloc qui en charge un autre casse (la suite du premier est lue dans
le second), vérifié. `block` n'agrandit le fichier qu'à n*1024 octets,
l'écriture de `save-buffers` le porte à (n+1)*1024: `blocks-editor`
affiche `max(blocs du fichier, n+1)`. Avant `use`, tout mot de bloc crée
`blocks.fb` dans le répertoire courant (un `1 list` dans le dépôt en
laisse un).

`blocks-editor [n]` (`common/blocks_editor.fs`, chargé par
phase_filetools.fs après visual.fs, donc dans toute amorce avec
fichiers) est une chaîne compilée au premier usage, comme `visual`: pas
de barre verticale dedans (`124 emit` dessine les barres). 63 places
éditables par ligne (la 64e est le saut de ligne de blocks.fs, déplacé
avec la ligne par couper/coller). Touches et usage: MANUAL_fr.md,
section Blocs. Mode brut comme `editor-raw` de ed (IXON et ISIG coupés,
pour que Ctrl+S, Ctrl+Q, Ctrl+C lui arrivent). Pièges rencontrés:
`vocabulary x x definitions` sans `also` remplace le haut de l'ordre de
recherche (ici termios: `[defined] new-termios` devenait faux et Ctrl+S
gelait le pty); les mots de l'éditeur sont liés à la compilation, donc
Ctrl+L charge avec l'ordre de l'appelant. Testé:
`tools/blocks_editor_pty_test.py` (17 vérifications, posix) et
`blocks()` dans tools/efi_qemu_test.py (3 vérifications: frappe sur la
ligne série, Ctrl+C/Ctrl+V, écrit sur la FAT, chargé). Pas essayé: la
fenêtre `--gui`, le Pi, Windows, l'ESP32 (la chaîne ajoute environ 7 Ko
à l'amorce).

## Portages d'eForth Windows (Marc Petremann, GPL) - fait le 2026-10-13

Source: https://github.com/MPETREMANN11/eForth-Windows (à cloner dans le
bloc-notes pour le lire). Son eForth est un ueForth (7.0.7.x), donc
lugforth (7.0.11) en charge la plupart tel quel: `value/to`, `{ }`,
`vocabulary`, `:noname`, flottants. Seules les parties Windows (GDI,
`Kernel32`, `structures` avec `L,`) ont dû être remplacées. Le projet
frère `~/src/forks/uf-9` a fait de même pour un Forth 16 bits (son
`PORTS-EFORTH.txt`).

Dans `examples/` (GPL, listés dans NOTICE; `make -C examples test-ports`
lance les tests de logique, `make smoke` celui de SDL2):
- `zmachine.fs` (+ `zmachine_tests.fs`, histoires dans `zmachine/`:
  `test.inf` v3/v5 nu, `cave.inf` un petit jeu avec la bibliothèque
  Inform 6, v5, à jouer:
  `lugforth examples/games/zmachine.fs examples/games/zmachine/cave.z5`;
  `cave_fr.inf` le même jeu en français, écrit pour la bibliothèque
  française de Lionel Ange (gitlab.com/auraes/inform6lib_fr, CC BY-SA,
  par-dessus la bibliothèque anglaise 6.12.x; PAS dans ce dépôt),
  compilé avec Inform 6.45 `-Cu`: voir l'en-tête du fichier; l'histoire
  apporte son propre alphabet et sa propre table Unicode, que
  `zmachine.fs` lit): interpréteur de Z-machine écrit d'après le
  standard 1.1, un fichier, versions 3, 4, 5 et 8, `zplay story.z5`.
  Texte seulement (pas de fenêtre haute: une ligne d'état v5 est
  abandonnée; celle de la v3 avec `-1 zm-status !`), format de
  sauvegarde propre, accents en UTF-8. Vérifié contre `dfrotz` sur
  `test.inf` (v3, v5, v8) et sur un petit jeu de la bibliothèque Inform
  6 (v5, v8: même texte, sauvegarde, restauration, redémarrage). Environ
  2 millions d'instructions par seconde. Tourne aussi sous gforth 0.7.9
  (`gforth ports/gforth/zmachine-gforth.fs story.z5`, tests:
  `gforth examples/games/zmachine_tests.fs`): trois endroits
  `[defined] gforth` dans le fichier (accept sur un terminal ou
  read-line, file-size double, ms-ticks); sortie identique à celle de
  Lugforth sur test.z3, test.z5 et le jeu de la bibliothèque z5/z8. Pas
  fait: versions 6 et 7, annulation, couleurs, son, saisie chronométrée,
  Quetzal. Fortz (GPL) n'a été qu'une inspiration, aucun code n'en est
  utilisé.
- `2048.fs` (+ `2048_tests.fs`): jeu en terminal, `2048-play`. Testé
  dans un pty; couleurs/disposition pas vues à l'œil.
- `3d.fs` (+ `3d_tests.fs`) et `gfx_3d.fs`: mathématiques, objets,
  rotation, perspective; dessin SDL2 vérifié seulement avec le pilote
  vidéo factice (comptes de pixels), pas sur un vrai écran.
- `inference/` (inference.fs, weather.fs, lamp_diagnosis.fs, tests,
  Makefile, READMEs): faits, règles, `infer`.
- `gfx_2048.fs`: interface SDL2 de 2048.fs (chiffres en bitmap,
  flèches/wasd/glissement à la souris, animation de glissement+pop,
  `--no-anim`); vérifié avec le pilote factice (couleurs de pixels des
  tuiles et des chiffres), pas à l'œil. Pourrait être ajouté à la liste
  Examples d'Android. Idées restantes: plus d'exemples d'inférence tirés
  de ses docs (`INFERENCE/docs/*.odt`), extras
  `3Dmodelisation`/`dumpTool`, autres projets en amont (lotto, 6809,
  flog, BMP, HTTP). Vérifié dans une vraie fenêtre X (affichage :0) pour
  gfx_2048: voir [PITFALLS.md](PITFALLS_fr.md), derniers points. Pièges
  rencontrés: `include` est relatif au fichier qui inclut; `if` au
  niveau supérieur ne marche pas (enveloppez-le dans un mot); `<# #s #>`
  prend un double, comme dans le standard (depuis 2026.10.27.2; avant,
  une cellule comme dans eForth): `n 0 <# #s #>`; `.r`, `random` ( n --
  r ) sont dans `ansi_compat.fs`, `ms-ticks 1 or rand-seed !`
  l'initialise.

## Mots ANS ajoutés à common/ansi_compat.fs - 2026-10-27

Les mots de Forth 2012 qui manquaient encore, vérifiés ensemble de mots
par ensemble de mots d'après les listes du standard avec `[defined]`:
CORE EXT, DOUBLE, LOCALS, SEARCH, STRING, MEMORY et EXCEPTION sont
maintenant complets; FACILITY n'a pas TIME&DATE (une horloge par port),
FLOATING seulement REPRESENT et les mots en double précision (les
flottants ici sont simples, F@ est SF@), TOOLS seulement CODE ;CODE
ASSEMBLER, FILE cinq mots qui vont avec les fichiers (READ-LINE
WRITE-LINE INCLUDE-FILE FILE-STATUS REQUIRE). Choix: `TO` est redéfini
en dernier, pour ranger aussi dans un 2VALUE et un FVALUE (il les
reconnaît à leur code DOES>); `C"` est compilé comme un `s"` dont le
texte commence par l'octet de comptage, donc `see` le lit;
SEARCH-WORDLIST met la liste de mots seule dans l'ordre de recherche
autour de FIND; TRAVERSE-WORDLIST saute les builtins du C (forks) et les
mots masqués (smudged); `{: a b | c -- d :}` est construit sur
`(local)`, les locales `|` commencent à 0. Tests dans
`ansi_compat_tests.fs` (posix); sur la version web ils ont été vérifiés
à la main (le cadre de test n'y tourne pas). La suite de tests forth2012
de Gerry Jackson n'a pas encore été lancée. La sortie numérique mise en
forme prend un double depuis 2026.10.27.2 (`#` divise par la base avec
trois u/mod sur des demi-cellules, `pad` fait 160 octets au-dessus de
`here` pour les 128 chiffres d'un double binaire).

## DO LOOP en C - 2026-10-27

`DO ?DO LOOP +LOOP` étaient des mots Forth (`common/boot.fs`, une
vingtaine de primitives par tour pour `LOOP`). Ce sont maintenant des
opcodes du niveau 2 (`(DO) (?DO) (LOOP) (+LOOP)` dans `internals`,
`common/tier2_opcodes.h`); `opcode-as (DO) DO` dans boot.fs donne au
`DO` Forth le champ de code de l'opcode, comme `BEGIN` et `IF` sont
faits, donc `see` et les marques de utils.fs trouvent toujours un seul
mot. La pile de retour est disposée comme avant (limite, puis indice:
`I` est `R@`, `J`, `K`, `LEAVE`, `UNLOOP` inchangés). La version web n'a
pas de niveau 2 et garde les versions Forth. `see` (2026.10.27.3): un
mot dont le champ de code est celui d'un builtin, comme `DO` ou `I`,
montre `C code: DO runs the opcode (DO)` au lieu de `Unsupported`
(`opcode-of` dans utils.fs), et le xt littéral que `postpone` compile
avant `,` s'affiche `POSTPONE DO` (`known-xt?` parcourt les vocabulaires
et les builtins). `see DO` trouve le mot de compilation `do` (la casse
est ignorée); le `DO` d'exécution est dans le vocabulaire
`internalized`: `internals internalized see DO forth`. 1e7 tours de
`0 10000000 0 do i + loop` sous posix: 554 ms avant, 71 ms après. Mêmes
résultats que gforth pour `+LOOP` avec des pas négatifs et `LEAVE`.

## Jeux d'après r3forth (examples/games/gfx_tetris.fs, gfx_snake.fs, gfx_minesweeper.fs) - fait le 2026-10-19

r3forth (https://github.com/phreda4/r3, Pablo Reda, MIT, crédité dans
NOTICE) a sa propre syntaxe (`cond? ( ... )`, `:mot`, virgule fixe
48.16): son code ne se charge pas ici, les jeux sont réécrits en gardant
ses idées. Chiffres du bitmap 3x5 du casse-briques, pas de polices.
`games_tests.fs` (dans `make test-ports`) les charge avec
`tetris-no-run` etc. définis, ce qui saute la boucle de jeu
(`[defined] x [if] [else] ... [then]` à la fin de chaque fichier).
tools/efi_qemu_test.py les joue depuis la clé (vérifications par copie
d'écran). Pas vus sur le Pi sous QEMU (même sdl2.fs). `color` remet
l'alpha à 255: fixez `alpha` après `color`. Fronts des touches: `just`
garde le dernier état par touche. `gfx_mastermind.fs` (le nôtre, pas de
r3) suit le même schéma: `mastermind-no-run`, tests dans games_tests.fs,
test efi. Autres candidats de r3: bdash (Boulder Dash, sprites propres
nécessaires), sokoban (niveaux de licence peu claire), raycaster, démos
de terminal (feu, matrix, tore), util/penner.r3 (assouplissements),
lib/sdl2poly.r3 (remplissage de polygones).

## Digger (examples/games/gfx_digger.fs) - fait le 2026-11-02

Un jeu façon Digger (le jeu PC de 1983), écrit d'après les règles telles
qu'elles sont largement décrites; les niveaux sont tirés au hasard et
les images sont des rectangles. POSIX et web
(`terminal.html?run=gfx_spr.fs,gfx_digger_sprites.fs,gfx_digger.fs`);
il tourne aussi sur le port DOS (signalé par le mainteneur, le
2026-11-04).

- Le terrain fait 15 x 10 cellules de 20 x 18 pixels. La terre est
  gardée en sous-cellules d'une demi-cellule (30 x 20), si bien qu'un
  tunnel avance d'une demi-cellule et que les monstres peuvent le
  suivre: un lien entre deux cellules est ouvert quand les quatre
  sous-cellules des deux sont libres (`link?`). Les positions sont en
  pixels, 2 par pas (3 pour un sac qui tombe), un pas toutes les 16 ms
  avec la boucle à pas fixe de `gfx_pacman.fs`; un personnage ne choisit
  sa route que lorsqu'il est aligné sur une cellule.
- Les nobbins suivent une carte de distances en largeur jusqu'au digger
  (`bfs`), les hobbins vont droit sur le digger en creusant. Un sac
  au-dessus d'une cellule qui se libère vacille, tombe, se brise en or
  après plus d'une cellule, et peut être poussé le long d'un tunnel (pas
  vers le haut ni le bas). Un digger arrivé juste sous un sac et qui
  continue de pousser vers le haut le retient : il tremble mais ne tombe
  pas tant que le digger part ou lâche la touche, puis tremble encore 10
  pas (`held?`, `bag-wobble`) pour lui laisser le temps de partir. Tirer
  en poussant vers le haut contre un sac envoie le tir vers le bas.
- Les mots sont préfixés `dg-` ou ont des noms peu courants (`bag.st`,
  `mon.t`...): un nom comme celui d'un mot `sdl2` est masqué sur le web
  et sous DOS (voir PITFALLS). `walk-to ( d -- )` et `fire!` portent la
  logique pour que les tests les pilotent sans touches.
- Tests: la section « Digger » de `examples/games/games_tests.fs`
  (niveaux, tunnels, repas, série de huit, sacs, écrasement, poussée,
  carte des distances, choix des monstres, boule de feu, repas du bonus,
  apparition, vies, niveau fini). Regardé dans une vraie fenêtre X
  (Xvfb + `xdotool` + `import`) et dans Chromium sans écran par CDP
  (l'écran se dessine, les monstres sortent, le digger creuse et meurt).
- Les images ne sont plus du code: elles sont dans
  `gfx_digger_sprites.fs`, faites avec `gfx_spr.fs` et dessinées avec
  `gfx_sprited.fs` (section suivante). Le corps du digger est le
  caractère `R`: le jeu change sa couleur (`spr-pal`) pour le
  clignotement et la mort.
- Laissé de côté par rapport à l'original: la musique du mode bonus, le
  second joueur, les niveaux dessinés à la main (les 8 cartes), le
  détail des vies gagnées avec les cerises; les sons sont des bips.

## Sprites et éditeur de sprites - fait le 2026-11-02

`examples/graphics/gfx_spr.fs` (POSIX et web): un sprite est une grille
de caractères (jusqu'à 63 x 63), un par pixel; `.` est transparent, tout
autre caractère est une couleur d'une palette commune
(`$rrggbb caractère spr-pal`, lue à chaque dessin: une couleur peut
changer). Un fichier de sprites est du Forth ordinaire:

    $b89a58 98 spr-pal              \ la couleur de 'b'
    16 16 sprite: coin
      s" ....bbbb........" spr-row   ...
    ;sprite
    coin 10 20 spr-draw             \ aussi spr-drawn ( spr x y échelle -- )

`;sprite` découpe la grille en une courte liste de rectangles
(gloutonne, une couleur chacun, `spr-build`): un sprite coûte quelques
`box`, pas un par pixel; `spr-source` réécrit le texte. Les sprites sont
listés dans `spr-all` / `spr-count`. Testé dans la partie "Digger" de
`examples/games/games_tests.fs` (découpage, aller-retour du source).

`examples/graphics/gfx_sprited.fs` (POSIX, SDL2) est l'éditeur:
`lugforth examples/graphics/gfx_sprited.fs` ouvre
`examples/games/gfx_digger_sprites.fs` (un autre fichier: voir l'en-tête
de l'éditeur). Souris: le bouton gauche peint, le droit efface, le
milieu prend la couleur; un clic sur une couleur ou sur un nom la
choisit. Touches: `s` sauvegarde (les lignes de commentaire du début
sont gardées), `u` annule (un pas), `c` efface, `h` `v` retournent, `r`
`e` tournent d'un quart (dans le sens des aiguilles, ou à l'inverse; un
sprite qui n'est pas carré tourne au milieu de sa propre taille et ce
qui dépasse est coupé), les flèches décalent (en rebouclant), `y` copie
le sprite et `p` le colle sur un autre sprite existant choisi ensuite
(depuis le coin haut gauche, ce qui manque est transparent), `d`
duplique le sprite, `n` une nouvelle couleur, `1 2` `3 4` `5 6` rouge,
vert, bleu en moins et en plus, Tab sprite suivant, Échap quitte sans
sauvegarder. `o` (ou le bouton OPEN en haut à droite) ouvre un autre
fichier: une liste des dossiers et des fichiers qui ne contiennent que
des sprites (chaque ligne d'un candidat est lue d'abord, car inclure un
autre fichier l'exécuterait), flèches et Entrée ou un clic, Échap
annule, `..` remonte; avec des changements non sauvegardés, `o` demande
deux fois. Si le fichier ne peut pas être chargé, l'ancien revient. La fenêtre montre le sprite sur la couleur de la terre, à
taille réelle et agrandi, avec le nombre de boîtes. Essayé dans une
vraie fenêtre X (Xvfb): les sprites se chargent, un pixel est peint, le
fichier est écrit et se recharge à l'identique. Pour importer une image,
rien encore: un PNG pourrait être lu avec `load-image` et un moyen de
lire les pixels (pas regardé). Pas fait: créer un sprite d'une taille
donnée depuis l'éditeur (dupliquer un sprite, ou commencer le fichier à
la main), une version web de l'éditeur (il faut écrire des fichiers).

Pièges rencontrés en route (aussi dans PITFALLS): sur le web `color` et
`box` ne sont les vrais qu'après `sdl2`; une bibliothèque chargée en
premier (`run=` liste les fichiers dans l'ordre) recevait les versions
du graphisme de la console, qui font `evaluate` à chaque appel et
mouraient après une cinquantaine d'appels avec
`objects[op] is not a function`. Mettre `sdl2` en tête d'une
bibliothèque. Dans une bibliothèque partagée par des jeux, ne pas
prendre de noms courts qu'un jeu peut définir (`cell`, `bx`): les tests
des jeux en chargeaient un qui changeait `cell` au milieu de
`spr-build`.

## Corridor, un jeu de tir sur rails en fil de fer (examples/games/gfx_corridor.fs) - fait le 2026-11-02

Un jeu de tir dans l'esprit de Star Fox sur un écran vectoriel, d'après
une maquette de l'utilisateur (lignes cyan, soleil rouge, tours des deux
côtés, grille au sol, un petit vaisseau blanc). POSIX et web
(`terminal.html?run=gfx_corridor.fs`); il tourne aussi sur le port DOS
(signalé par le mainteneur, le 2026-11-04). Que des
lignes, pas d'élimination des parties cachées.

- La 3D tient en une division de perspective: un point (x y z), z étant
  la distance à la caméra, va à la colonne `cx + (x - camx) * f / z` et
  à la ligne `hy - (y - camy) * f / z` (f = 120). `cr-wline` coupe
  d'abord le segment au plan proche (z = 20, interpolation entière: cela
  marche avec des cellules de 32 bits), puis le trace avec `line`. La
  caméra suit les 3/4 du x du vaisseau et la moitié de son y: le
  vaisseau reste bas et le monde glisse.
- Le monde vient vers la caméra: chaque chose a un `z` qui diminue de la
  vitesse à chaque pas (la même boucle à pas fixe de 16 ms que le
  digger). Les choses: tours (deux rangées, créées au loin quand la
  dernière défile), piliers (hauts ou bas) et barres flottantes à
  éviter, chasseurs (un octaèdre qui tourne et se balance, 12 arêtes)
  qui tirent un projectile visé sur le vaisseau, nos lasers (deux, aux
  ailes), explosions (huit rayons qui grandissent). Les emplacements
  sont des tableaux de drapeaux (`bd.on`, `ob.on`, `en.on`...) et
  `cr-free` en trouve un.
- Coups: un laser tue un chasseur (100) ou brise un pilier ou une barre
  (50); toucher un obstacle, un chasseur ou un projectile coûte un
  bouclier sur six et donne 50 pas d'invulnérabilité (le vaisseau
  clignote); plus de bouclier, fin de partie. La vitesse et le rythme
  des choses montent avec le niveau (un niveau tous les 12000 unités de
  trajet).
- La barre rouge du bas est la jauge de bouclier. Le vaisseau s'incline
  (ses ailes sont décalées par `cr-bank`, un cran par pas) quand il
  tourne.
- Touches: flèches ou wasd, espace ou `f` tire (maintenu: une rafale),
  `p` pause, Échap quitte, `r`, espace ou entrée après la fin.
- Les mots sont préfixés `cr-` (un nom partagé comme `cell` ou `just` a
  une fois heurté un autre jeu dans les tests). Le soleil est un disque
  de lignes d'un pixel avec deux bandes noires.
- Tests: la partie "Corridor" de `examples/games/games_tests.fs`
  (projection, appels de coupe, tours, laser et chasseur, chocs avec
  pilier et barre, invulnérabilité, fin de partie, un vol de 3000 pas
  qui laisse la pile propre, projectiles visés). Regardé dans une vraie
  fenêtre X (Xvfb) avec les touches maintenues.

## Lode Runner (examples/games/gfx_loderunner.fs) - fait le 2026-11-04

Un Lode Runner (le jeu de plateformes de 1983), écrit d'après les règles
telles qu'on les décrit; les cinq niveaux sont les nôtres, aucun ne
vient de l'original. POSIX et web
(`terminal.html?run=gfx_spr.fs,gfx_loderunner_sprites.fs,gfx_loderunner.fs`).
Les images sont 17 sprites de 10x10 pixels
(`gfx_loderunner_sprites.fs`, modifiables avec `gfx_sprited.fs`)
dessinés par `gfx_spr.fs`; la couleur du costume est la lettre de
palette `B`, que le jeu règle avant chaque corps (coureur blanc, gardes
rouges).

- Le terrain fait 28x16 cases de 10 pixels sur un écran de 320x200
  (zoom 3), gardé à raison d'un octet par case dans `lr-map`: espace,
  `#` brique (peut être creusée), `@` plein, `H` échelle, `-` corde, `$`
  or, `S` une échelle qui n'apparaît que quand tout l'or est pris, `&` le
  coureur et `0` un garde (ces deux-là sont retirés de la carte au
  chargement d'un niveau). La rangée 0 est la sortie: y monter par
  l'échelle. Un niveau est fait de 16 chaînes de 28 caractères
  (`lr-row`).
- Les corps (tableaux `le.*`, 0 est le coureur, 1 à 6 les gardes) ont une
  position en pixels, pas en cases. Un pas dure 16 ms: le coureur avance
  d'un pixel, un garde trois pas sur quatre, une chute fait deux pixels.
  `lr-go` est l'unique règle de déplacement, pour tout le monde: la case
  sous le centre du corps décide (`lr-support?`: une échelle ou une corde
  ici, un mur ou une échelle dessous, ou un garde coincé dans un trou
  dessous); au début d'une montée le corps est d'abord ramené au milieu
  de la case; les pas de côté n'ont lieu que sur une rangée entière.
  `lr-moves` donne les mêmes règles sous forme de masque (1 gauche,
  2 droite, 4 haut, 8 bas) pour une case entière, pour les gardes et les
  tests.
- Un garde ne décide que lorsqu'il est exactement sur une case: `lr-bfs`
  est un parcours en largeur sur `lr-moves` de sa case à celle du
  coureur et donne le premier pas. On ne lui dit pas comment grimper: la
  recherche trouve les échelles et les cordes. Un garde ramasse l'or sur
  lequel il passe et le repose plus tard (`le.c` est le temps restant),
  ou tout de suite s'il tombe dans un trou. Les gardes partent 100 pas
  après le début du niveau.
- Creuser (`z` ou `q` à gauche, `x` ou `e` à droite): la brique sous la
  case voisine disparaît, le coureur est occupé 12 pas, le trou dure 300
  pas et ses briques clignotent pendant les 40 derniers. Un garde qui y
  tombe est coincé 140 pas (75 points), puis il sort et fait un pas de
  côté vers le coureur; si le trou se referme avant, il est écrasé et
  revient en haut. Le coureur dans un trou qui se ferme meurt. Un sol
  mince (une brique avec du vide dessous) laisse tout le monde tomber à
  travers: un trou ne piège que là où le sol a deux briques d'épaisseur
  ou repose sur la rangée du bas.
- Score: 250 par or, 75 par garde piégé, 1500 par niveau. Cinq vies;
  toucher un garde libre en coûte une (`k` en abandonne une quand on est
  coincé). Les niveaux recommencent après le cinquième.
- Touches: flèches ou wasd, `z` `x` creusent, `p` pause, `k` abandonne
  une vie, Échap quitte, `r`, espace ou entrée après la fin. Les mots
  sont préfixés `lr-`.
- Tests: la partie « Lode Runner » de `examples/games/games_tests.fs`:
  un remplissage avec `lr-moves` vérifie que chaque niveau peut être
  fini sans creuser (tout l'or et la sortie atteignables), la marche, les
  murs, la gravité, les échelles, l'or, le trou (creusé, rebouché), un
  garde qui y tombe, est coincé et écrasé, le contact d'un garde, une
  partie de 3000 pas qui laisse la pile propre. Regardé dans une vraie
  fenêtre X (Xvfb) et dans Chromium sans écran (la page web tourne et se
  joue).

## Forth is Fun (tortue)

Forth is Fun (https://gitlab.com/garvalf/forth-is-fun, Eric Forgeot,
MIT/Apache): `examples/graphics/gfx_turtle.fs` est écrit d'après son
forthtoise.fs (mêmes noms de mots, anglais et français; traits gardés
dans une liste et redessinés à chaque image), tests dans
`turtle_tests.fs` (`make -C examples test-ports`). L'exécuter lance un
shell (`logo`): les lignes tapées dans une invite dessinée sous l'image
(une police 8x8 faite de `box`, donc sans fichier de police; `typed` et
`unpress` de sdl2.fs lisent les touches; `type` est redirigé vers le
journal avec `['] type >body !`) s'exécutent tout de suite et sont
gardées comme un programme si elles marchent; `go` / `play` le
redessinent (compilé comme un mot `logo-prog`; `safe-eval` restaure le
source après une exception, `evaluate` non). Le shell marche sur POSIX
(Xvfb, saisie par xdotool) et sur le web (Chrome headless, touches par
CDP), non automatisé. Pièges rencontrés: un `)` dans un commentaire
`( ... )` le ferme (le reste s'exécutait: STACK UNDERFLOW au
chargement); deux `{ }` dans une définition cassent; le web n'a pas
`fold-case`. Reste à emprunter: bresenham, le CYOA (livre dont vous êtes
le héros, aussi en français), les motifs glitch. Piège rencontré:
`fdup f>s s>f f< if` laissait une mauvaise valeur ici; arrondissez avec
`fdup f0< if 0.5e f- else 0.5e f+ then f>s`.

## Dessiner à l'invite (`graphic`) - fait le 2026-10-20

`graphic` (charge aussi sdl2) fait marcher `0 0 100 100 line`,
`red color ... box`, `n ink`, `cls` à l'invite de la console, dessinés
sous le texte, comme le BASIC d'un MO5. Matériel nu (ports/baremetal,
rpi + efi): `gfx_ensure` dans screen.h fait une couche 640x480 dans
`fb_back` (`fb_gfx`), chaque fonction C de dessin l'appelle; `con_sync`
(console.h) la montre à travers les pixels de cellule valant 0 (fond par
défaut, mis à l'échelle par `con_zoom`); `fb_flip` ne fait alors que
synchroniser; `fb_open` / `fb_close` y mettent fin. Forth: `gfx-on`,
`graphic`, `gfx-mode`, `graphic-size` (GFX-SIZE, `gfx_size` dans
screen.h; `con_compose` associe pixels d'écran et pixels de la couche)
et un `cls` qui efface en noir dans sdl2.fs. Console posix
(sdl2_console.fs): une texture cible `gfx-tex`, `con-draw` la rend entre
le fond et les cellules (crayon sauvegardé autour du redessin), `cls` et
`flip` sont redéfinis, `con-key` marque la fenêtre sale pour qu'un
dessin fait entre deux touches s'affiche. Testé: test efi QEMU (9
vérifications), la console posix à la main sous Xvfb (xdotool;
`python3 tools/gui_graphic_test.py out/posix/lugforth` l'automatise),
pas rpi (pas de chaîne d'outils aarch64), pas l'application Android.
Console web (ports/web, 2026-10-22): `graphic` met la zone de dessin
au-dessus du texte de terminal.html (canevas fixe + cale, S.layer dans
sdl2.fs), vérifié dans Chromium sans écran par CDP sur ordinateur et
téléphone émulé.

## `play` et les ondes par leur nom - fait le 2026-10-20

`play ( a n -- )` (MML, numérotation d'octaves du MSX, avec p n & v w;
analyseur `p-step` et consorts, sortie par le `p-out` différé, `beep`
par défaut) est écrit dans les trois sdl2.fs: ports/posix (tests dans
sdl2_tests.fs), ports/baremetal, ports/web: gardez les copies égales.
`wave`, `beep-volume`, `pitch-drop` sont des réglages (`: setting` =
`create , does> !`: `1 wave` et `1 to wave` rangent tous deux; lecture
avec `wave@` etc.), comme `color` prend sa valeur;
`square triangle saw noise sine` fixent `wave`. Le sinus est l'onde 4
(table dans sdl2.fs posix, `snd_wave` dans ports/rpi/sound.h,
oscillateur 'sine' sur le web). Un silence est un bip de fréquence 0
(muet). Pas entendu sur les cartes ni dans le navigateur, seul l'analyse
est testée (posix).

## Gestionnaire de fichiers (`filer`) et bureau (`desk`) - premières versions 2026-10-24

`examples/utils/filer.fs` (SDL2 + SDL2_ttf, chemin de DejaVuSans dans
`font-file`): liste du dossier courant (dossiers d'abord, en jaune),
clic ou flèches pour sélectionner, double clic ou Entrée pour ouvrir
(dossier: chdir; `.fs`: `included` après la fermeture de la fenêtre;
autre fichier: `ed`), Retour arrière = parent, `D` puis `Y` supprime un
fichier, la molette fait défiler. La liste se dessine dans un rectangle
(`fl-w` `fl-h`, origine 0 0) et reçoit les événements sous forme
d'appels (`fl-click`, `fl-key`, `fl-wheel`, `fl-open` est un defer),
donc un bureau peut l'héberger.

`examples/utils/desktop.fs` (`lugforth examples/utils/desktop.fs`, ou
`desk`): 800x600, icônes en deux colonnes (`icon`: genre 0 fenêtre du
gestionnaire de fichiers, 1 une commande dans une fenêtre de terminal, 2
un programme Forth), un double clic en lance une; fenêtres avec barre de
titre, glisser, amener devant, x; barre des tâches avec les fenêtres et
Quit. Une fenêtre de texte est un petit émulateur de terminal (mots
`t-*`: grille de points de code + attributs, UTF-8, CSI H J K A B C D G
s u m avec 16/256 couleurs, `?25l/h`): `ed` et `writhdeck` y tournent
sans changement. Comment: `type`, `key`, `key?` et `form-hook` sont
redirigés pendant que l'éditeur tourne (`run-in-terminal`), et
`key`/`key?` exécutent une image du bureau (événements, dessin, flip)
chaque fois que la file de touches est vide. Donc un seul terminal à la
fois (un deuxième lancement le dit dans la barre des tâches), les autres
fenêtres restent vivantes. Touches: `on-key` pour les spéciales et
Ctrl+lettre, SDL_TEXTINPUT pour le reste (AZERTY, accents). Le x d'un
terminal envoie Ctrl+Q (un texte non enregistré demande deux fois). Un
programme Forth (`.fs` depuis une icône ou depuis le gestionnaire de
fichiers) ferme le bureau, s'exécute avec `argc` fixé à 1 (donc
`script-bye` ne quitte pas) et le bureau revient; le programme est lancé
avec les mots du bureau hors de l'ordre de recherche (tous sont dans le
vocabulaire `fm`, `desk` et `filer` sont dans forth). Testé sous Xvfb
avec xdotool (mousedown/mouseup avec une pause, keydown/keyup): icônes,
fenêtre du gestionnaire de fichiers, glisser, writhdeck dans une fenêtre
(nouveau fichier, saisie avec accents, Ctrl+S, x), ed, README.md ouvert
depuis le gestionnaire de fichiers, Snake, 2048 et la démo lancés et
quittés avec Échap, Quit. CPU au repos environ 5 pour cent (une image
n'est dessinée que quand quelque chose a changé). L'icône Forth (`repl`)
exécute l'éditeur de ligne de la console (`edit-accept` avec `le-force`,
historique, flèches, Tab) et `evaluate` dans une fenêtre de terminal; le
x tape `bye`; `screen` ou `graphic` tapés là remplaceraient la fenêtre
du bureau (non géré). Non testé: Windows, la version web, l'application
Android, un deuxième terminal, l'usage de la souris dans ed/writhdeck,
les caractères larges (un caractère fait une cellule). Pièges
rencontrés: un mot nommé `scan`, `top`, `step`, `frame` ou `key-down`
remplace le mot du système ou de sdl2 de même nom, même pour les
programmes chargés plus tard (les jeux utilisent `key-down`);
`{ locales }` dans un corps de boucle; `!` écrit 8 octets (utilisez `l!`
et `ul@` pour des cellules de 32 bits d'un tableau); `0 { x }` est la
façon d'ajouter des locales après les arguments; `catch` ne restaure pas
les locales; un programme qui touche au vrai tty (editor-raw) est arrêté
quand il est lancé dans un pipeline: utilisez `timeout --foreground` et
`< /dev/null` dans les tests. Réglages (2026-10-24): les icônes se
déplacent et leurs places, la couleur de fond (8 échantillons) et la
taille du bureau (4 tailles, l'écran est rouvert) sont enregistrées sous
forme de lignes Forth dans `$HOME/.config/lugforth/desktop.ini`
(dossiers créés s'ils manquent, place pour d'autres réglages plus tard)
(`dk-bg!`, `dk-size!`, `icon@`, chargé avec `included`). Une ligne par
icône: `genre s" label" s" commande" x y icon@`; genres 0 gestionnaire
de fichiers, 1 commande dans une fenêtre de terminal (`ed`, `writhdeck`,
`repl`), 2 programme Forth (chemin depuis le dossier du projet, absolu
s'il commence par `/`), 3 réglages, 4 console Forth SDL (le bureau se
ferme, la console s'ouvre comme avec `--gui`, `desk` ramène le bureau);
le fichier peut être édité à la main. Le dossier du projet est trouvé en
cherchant `examples/utils/desktop.fs` dans `./`, `../`, `../../`,
`../../../`, donc `desk` démarre depuis examples/utils comme depuis la
racine. Testé sous Xvfb (glisser, échantillon, taille, redémarrage
depuis la racine, Snake). Étapes suivantes: renommer, copier, nouveau
dossier dans le gestionnaire de fichiers (demande un champ de texte),
sélection à la souris dans les fenêtres de terminal.

## Unicode dans la fenêtre console (`lugforth --gui`)

Une cellule de la grille contient un point de code Unicode (4 octets:
`grid` a un pas de 4, `gfill` vide les cellules, lecture avec `sl@`,
écriture avec `l!`). `con-putc` décode l'UTF-8 que les programmes `type`
(`u-byte`; une mauvaise séquence montre `?`). La police TrueType
(`sdl2_console_ttf.fs`) fait les glyphes 33 à 126 d'un coup et toute
autre lettre à sa première apparition (`ttf-other`: une table de 1024
textures indexées par point de code modulo 1021, un conflit remplace);
la couleur du texte est donnée à un glyphe quand il est dessiné
(`ttf-tint` ne fait que la ranger). Une lettre absente de la police
montre la boîte vide de la police (DejaVu Sans Mono n'a ni CJK ni
emoji); les lettres larges prennent encore une cellule. La police bitmap
n'a que l'ASCII et dessine la lettre simple pour le Latin-1 et 20 signes
courants (`bm-base`, `base-map`, `extra-cp`), sinon `?`. Une sélection
est copiée en UTF-8 (`sel-cell`). Les lettres tapées arrivent en octets
UTF-8 (saisie de texte SDL). Testé dans `sdl2_console_tests.fs` (sans
écran) et par capture sous Xvfb (latin, grec, cyrillique, symboles; avec
la police bitmap par `gui-load sdl2 console`).

Éditeur de ligne à l'invite (`common/lineedit.fs`): les déplacements,
retour arrière et suppression agissent sur une lettre, pas un octet
(`le-prev`, `le-next`), et les séquences de curseur comptent des
colonnes, c'est-à-dire des lettres (`le-cols`); c'était aussi faux sur
un vrai terminal. Testé dans `lineedit_tests.fs`.

Le curseur de la console clignote depuis `blink-tick`, appelé par
`con-key` et par `con-key?`: un programme qui interroge avec `key?`
(writhdeck) avait une phase figée, souvent l'invisible, donc pas de
curseur.

## Histoires Inform 6

Les histoires vivent dans `examples/games/zmachine/` (source `.inf`,
`.z3`/`.z5` compilés, les deux commités). Les compilateurs et
bibliothèques ne sont PAS dans le dépôt.

### Ce qui est installé ici (vérifié le 2026-10-26)

- `/usr/bin/inform` = Inform 6.31 (2006), bibliothèque 6/11 dans
  `/usr/share/inform/6.31/module`. Sources en Latin-1 seulement, pas de
  `-Cu`. Convient pour un test rapide en anglais
  (`inform -v5 +/usr/share/inform/6.31/module x.inf x.z5`), mais la 6/11
  n'a pas de « (first opening the chest) » automatique: `cave.inf` a été
  construit avec la 6.12 et `zmachine_tests.fs` attend ses messages.
- Rien d'autre. Procurez-vous ce qu'il faut dans un dossier de travail
  (jamais dans le dépôt):
  - compilateur:
    `git clone --depth 1 https://github.com/DavidKinder/Inform6.git && make`
    (6.45, a `-Cu`)
  - bibliothèque anglaise 6.12.x + fichiers français:
    `git clone https://gitlab.com/auraes/inform6lib_fr` et décompressez
    son `libI6_Windows.zip` (donne `inform6lib/` = anglais 6.12.6,
    `inform6lib_fr/` = `french.h`, `frenchg.h`, `translation.h`, et
    l'exemple `cqvdle.inf`, un bon deuxième test de l'interpréteur).

### Compiler

    inform6 cave.inf cave.z5 -v5 +/chemin/inform6lib/ # anglais, version
    5 (la bibliothèque ne tient pas en v3)
    inform6 cave_fr.inf cave_fr.z5 +/chemin/inform6lib/
    ++/chemin/inform6lib_fr/ # français (demande 6.35+ pour l'en-tête !%
    -Cu)

Le source français commence par `!% -Cu`, `!% -~S`,
`!% +Language_name=french` et est en UTF-8. Conventions de la
bibliothèque française: verbes à l'infinitif (`prendre`, `ouvrir`,
`allumer`, `regarder`, pas `regarde`), les accents doivent être tapés,
genres par `has male` / `has female` (la bibliothèque gère elle-même
élisions et accords, pas besoin d'`articles`),
`Include "parser"; Include "verblib";` puis `Include "frenchg";` en
dernier. La bibliothèque française est CC BY-SA 4.0: ne la copiez pas
dans le dépôt (si jamais: NOTICE avec son texte).

### Tester

    out/posix/lugforth examples/games/zmachine_tests.fs        # finit par PASS (49 vérifications)
    gforth examples/games/zmachine_tests.fs -e bye # pareil, sous gforth
    printf 'regarder\ninventaire\n' | out/posix/lugforth
    examples/games/zmachine.fs story.z5 # à la main; l'EOF de stdin
    affiche des déchets à la fin, ignorez

`zmachine_tests.fs`: `s" cmd" iline` met une commande en file, `play`
l'exécute, `has?` cherche dans la capture (`ocap`, 16 Ko), le tampon
d'entrée `icap` fait 600 octets. Une vérification sur un texte que
l'interpréteur replie à 78 colonnes peut échouer (le saut de ligne
remplace une espace): testez un court fragment. Le compte de tours
exclut les méta-commandes comme `score`.

### Pièges

- Les histoires `-Cu` d'Inform apportent leur propre table d'alphabet
  (mot d'en-tête $34) et table Unicode (mot 3 de l'extension d'en-tête,
  via $36); `zmachine.fs` lit les deux dans `zsetup` (`zuni`, `zun`,
  `zalpha`, `afind`, `ez-char`). Des accents faux (« Apr7s ») = ce code
  est cassé.
- Les messages purement affichés dans une routine `before` de
  `LibraryMessages` doivent finir par `rtrue`, sinon le message anglais
  est affiché après.
- Les constantes de la bibliothèque comme `AND__TX` ne peuvent pas être
  redéfinies sans fichier de langue: utilisez une vraie bibliothèque de
  langue plutôt que de rapiécer l'anglaise (le premier `cave_fr.inf` le
  faisait, avec `BeforeParsing` associant des mots français à des mots
  anglais: abandonné).
- Reconstruisez les disques EFI/Pi après avoir ajouté un `.z5`:
  `ports/efi/Makefile` prend `examples/games/zmachine/*.z3|*.z5` par
  joker.
