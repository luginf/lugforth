# Lugforth: état, points forts et points faibles

*Note en français seulement.*

Un bilan fait le 2026-10-29, au regard de ce qui est dans le dépôt et de
comparaisons mesurées avec gforth et pforth (voir
[inspirations/](inspirations/)). Ce n'est pas une promesse: les faits
relevés sur du vrai matériel sont signalés comme tels.

## 32 ou 64 bits?

La cellule a la taille d'un pointeur de la cible. Elle fait 64 bits sur
les ports de bureau et de PC (Linux, Windows, Android, Raspberry Pi nu,
PC par UEFI); elle fait **32 bits** sur le web (WebAssembly), dans
TIC-80 et sur la Game Boy Advance (`docs/en/dev/PORTS.md`). Pour les
microcontrôleurs (ESP32, Pico) la documentation ne le dit pas; ce sont
des cibles 32 bits. Conséquence: un produit en virgule fixe 16.16 passe
par `*/` pour tourner partout (voir `sdl2_raycaster.fs`).

## Passer en 32 bits exclusivement?

Réflexion du 2026-10-29, faite à la lecture du code et de la
documentation, sans build 32 bits essayé. Conclusion: non.

Ce qu'on y gagnerait: un seul comportement partout (plus de piège entre
le web, la GBA, TIC-80 en 32 bits et le reste en 64, comme le produit
16.16 qui doit passer par `*/`), des tests à passer une seule fois, des
cellules deux fois plus petites.

Pourquoi non:

- **SDL2 par `dlopen` casserait**: un processus 32 bits ne peut pas
  charger les bibliothèques 64 bits du système (SDL2, SDL2_ttf,
  SDL2_mixer); il faudrait les versions 32 bits, que les distributions
  retirent peu à peu.
- **Les pointeurs ne tiendraient plus dans une cellule**: la cellule a
  la taille d'un pointeur (`cell_t`) et l'appel de fonctions C
  (`calls.h`) passe des pointeurs que SDL renvoie sur 64 bits. Des
  cellules de 32 bits dans un processus 64 bits ne marcheraient pas non
  plus.
- **Les plateformes actuelles sont en 64 bits**: Android l'impose pour
  les nouvelles applications; le PC en UEFI et le Raspberry Pi 3/4 nu
  tournent en 64 bits.
- **On perdrait les entiers de 64 bits**: `*/` à produit intermédiaire
  large, grands nombres, horodatages.
- **Aucun gain de vitesse attendu**: l'écart avec gforth vient du
  moteur, pas de la largeur des cellules.

À faire à la place: garder les deux largeurs (c'est un `typedef`, le
coût est déjà payé), ajouter une configuration de test à cellules de 32
bits sur POSIX ou un test de la version web pour attraper les bugs
propres au 32 bits avant la GBA, et écrire dans le manuel la règle « ne
pas supposer 64 bits dans les exemples portables ».

## Points forts

- **Portabilité exceptionnelle pour un Forth** : Linux, navigateur,
  Android, ESP32, Pico, Game Boy Advance, TIC-80, Windows, Raspberry Pi
  nu et PC en UEFI sans système, depuis un noyau C minimal.
- **Insensible à la casse** (comme pforth, au contraire de gforth), ce
  qui évite par exemple le conflit `SDL_Quit` / `SDL_QUIT`.
- **Mots SDL2 de haut niveau** (graphique, images, son, ABC, MIDI,
  synthé FM) chargés par `dlopen`: seule la `.so` est nécessaire.
- **Éditeurs et environnement fournis**: `ed`, `writhdeck`, console
  graphique, historique et complétion, pager.
- **Beaucoup d'exemples** (des dizaines de fichiers): jeux, 3D,
  Z-machine pour la fiction interactive, moteur d'inférence, machine
  Varvara/uxn.
- **Conformité testée**: la suite Forth 2012 de Hayes / Gerry Jackson
  passe pour tous les ensembles de mots (`ninja forth2012`), plus des
  tests Forth sur les jeux, les éditeurs, la Z-machine.
- **Mémoire très réduite**: 2,3 Mo au démarrage (gforth: 8,5 à 10,7 Mo),
  binaire POSIX de 350 Ko.
- **Documentation en deux langues**, notes de développement détaillées
  (pièges, ports, fonctionnalités).

## Points faibles

- **Vitesse**: environ 2 fois plus lent que `gforth` et 4 à 5 fois plus
  que `gforth-fast` sur notre banc (0,40 s contre 0,20 s et 0,09 s). Pas
  de compilation optimisée ni de code natif.
- **Messages d'erreur pauvres**: `ERROR` sans trace de pile ni nom de
  mot; un `include` qui échoue répond seulement « Error including:
  fichier » (voir `PITFALLS.md`). gforth fait beaucoup mieux.
- **Taille de cellule variable selon le port** (64 ou 32 bits): des
  programmes qui supposent 64 bits cassent sur le web ou la GBA.
- **Pièges de langage connus**: locales `{ }` à déclarer en tête de mot,
  `include` relatif au fichier courant (non standard), `[char]`
  seulement dans les définitions.
- **Peu de vérifications sur du vrai matériel**: l'EFI (ventilateur,
  touches maintenues), le Raspberry Pi nu et plusieurs jeux n'ont été
  essayés que sous QEMU. Le port Android n'est pas compilable sur le PC
  de développement (écrit à l'aveugle).
- **Tests EFI**: 3 échecs connus et antérieurs aux changements récents
  (second terminal, session, son WAV).
- **Maintenance**: projet porté par une personne avec des sessions
  d'assistance; documentation à tenir en double (anglais et français).
- **Outils de mise au point minimaux**: un `see` très sommaire, ni
  références croisées, ni points d'arrêt comme chez EditForthLin.
- **Communauté** réduite.

## Ce qui reste à faire, en vrac

- Traces d'erreur (idée de gforth, `backtrac.fs`).
- Port d'un diff, de macros, d'un émulateur 6502 (Dusk OS).
- OOP, traducteur Pascal, nombres longs (EditForthLin).
- Vérifier sur de vrais PC l'EFI et le Raspberry Pi nu.
- Comprendre l'écart de vitesse avec gforth (banc: fib 32 récursif plus
  une boucle de 20 millions d'itérations; lugforth 0,40 s, `gforth` 0,20
  s, `gforth-fast` 0,09 s). Les mots du banc sont déjà des primitives C,
  sauf `recurse` (littéral puis `execute`, trois dispatches). À faire:
  compter les opcodes exécutés sur le banc, et essayer un `recurse`
  compilé en appel direct. L'hypothèse à vérifier est que l'écart vient
  du moteur (sauts indirects, pile en mémoire) plutôt que des mots
  écrits en Forth.
- Ajouter une configuration de test à cellules de 32 bits (build POSIX
  `-m32` si les bibliothèques le permettent, ou lancer les tests Forth
  sur la version web) pour attraper les bugs propres au 32 bits.
- Écrire dans le manuel la règle sur la largeur des cellules (voir «
  Passer en 32 bits exclusivement? »).
