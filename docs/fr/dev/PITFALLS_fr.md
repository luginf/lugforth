# Pièges appris à la dure

*Traduction française de
[docs/en/dev/PITFALLS.md](../../en/dev/PITFALLS.md) (l'anglais fait
foi).*

À lire avant de toucher aux sources Forth, aux chargeurs, à la version
web ou à une nouvelle plateforme. Fait partie de
[README.md](README_fr.md).

## Forth, chargeurs et SDL2

- Dans un chargeur (une définition en deux-points compilée au
  démarrage), un nom comme `sdl2` ou `sdl2-mixer` écrit hors de la
  chaîne évaluée est lié au chargeur qui existait alors: l'exécuter de
  nouveau recharge tout et crée un nouveau vocabulaire vide. Mettez ces
  noms dans une chaîne exécutée par `evaluate`.
- Une structure de contrôle (`if`, `do`, `begin`) hors définition marche
  depuis 7.0.11.11 (`common/interpret_control.fs`), mais pas sur les
  démarrages esp32, pico, pico-ice, gba. `2swap`, `0>` et `within`
  existent (`common/ansi_compat.fs`).
- Des locales déclarées avec `{ }` dans une branche `if` corrompent la
  pile de retour quand la branche est sautée (un -9 silencieux);
  déclarez-les en haut du mot ou utilisez des valeurs.
- `dump-file` prend les données d'abord et le nom du fichier en dernier.
- Un `( ... )` imbriqué dans un commentaire de pile ferme le commentaire
  au premier `)`: `( optional xt ( n -- ): text )` laisse `: text )` à
  compiler, ce qui commençait une définition et donnait « n NOT FOUND »
  au chargement de `sdl2` (trouvé en ajoutant `render-hook`). Écrivez
  `( xt, n -- : text )`. `ninja posix` ne charge pas `sdl2`, donc lancez
  les tests SDL après avoir touché à `ports/posix/sdl2.fs`.
- Un `render-hook` (voir `examples/music/fm`) ajoute dans `mix`, qui
  contient 4096 cellules: ne lui demandez jamais plus d'échantillons
  (`latency` vaut 2205, et un test qui appelle `fm-render` directement
  reste sous 4096 sinon il écrase la mémoire après `mix` et échoue dans
  des tests sans rapport).

- **r| copie** (changé le 2026-10-13). `r"`, `r|` et `r~` gardaient,
  dans une définition, l'adresse du texte d'où ils avaient été lus, pas
  une copie. C'est bien tant que le texte reste: le texte de démarrage
  vit dans le binaire. C'est faux pour un fichier lu avec `include` ou
  `needs` (libéré après le chargement: un chargeur paresseux comme
  `: sdl2 r| ... | evaluate ;` trouvait des déchets plus tard, vus comme
  « he NOT FOUND » dans lugforth-mini), pour une ligne tapée à l'invite
  (écrasée par la ligne suivante) et pour une chaîne évaluée puis
  libérée. Maintenant `r-copy` (une variable de `internals`, mise à -1
  par `common/fini.fs` à la fin du démarrage) les fait copier leur texte
  exactement comme `s"`; pendant le démarrage ils gardent encore des
  adresses, donc le démarrage ne coûte rien de plus, et sur ESP32 ou
  Pico seul ce que l'utilisateur définit ensuite est copié.
  `include-file` garde aussi vivant le texte d'un fichier quand une
  adresse a été prise (`src-ref`), ce qui n'arrive que si `r-copy`
  vaut 0. Test: `test-r-copy` dans `common/ansi_compat_tests.fs`. **Pour
  revenir** aux seules adresses: supprimez la ligne `-1 r-copy !` dans
  `common/fini.fs` (le reste devient inerte; `include-file` protège
  toujours les fichiers inclus). Effets de bord possibles à surveiller:
  un programme qui comptait sur `r|` donnant une adresse dans son propre
  source (aucun dans ce dépôt), et l'utilisation du dictionnaire par les
  chaînes définies après le démarrage (un `r|` de 60 Ko est copié une
  fois).
- `ms` (`common/tasks.fs`) gardait un processeur à 100 % (une boucle sur
  `ms-ticks` avec `pause`); sous POSIX il dort maintenant 1 ms entre
  deux `pause`. Les autres ports attendent encore activement. Dans un
  programme qui interroge (`key?` dans une boucle, un jeu ou un éditeur
  qui attend une touche), dormez plutôt avec `usleep`, qui est dans le
  vocabulaire `posix`: `50000 usleep drop` pour 50 ms (voir `wd-wait`
  dans `editors/writhdeck.fs`). Aussi, la fenêtre console SDL ne se
  redessine que dans `key` et dans `key?`, donc une boucle qui n'appelle
  ni l'un ni l'autre ne montre rien.
- lugforth ignore la casse. `SDL_Quit` (fonction) et `SDL_QUIT`
  (constante d'événement) sont le même nom, donc la fonction est liée
  sous le nom `sdl-shutdown`. Vérifiez les nouvelles liaisons pour de
  tels conflits.
- Le FFI (`shared-library` + `sofunc`) ne passe que des cellules: pas de
  flottant ni de structure par valeur, pas de rappels de C vers Forth
  (l'audio utilise `SDL_QueueAudio`, pas un rappel).
- `beep` met en file une tonalité entière, donc les bips ne se
  chevauchent jamais. `drum-hit` (percussif, 8 voix, `pitch-drop`) est
  mélangé en Forth par `pump`, que `drum-hit`, `flip` et `delay`
  appellent par `audio-hook` et qui garde environ 50 ms en file: une
  boucle qui n'appelle jamais `flip` ni `delay` n'obtient aucun son des
  coups. Sur le web `drum-hit` est simplement un nœud WebAudio démarré
  tout de suite.
- Un `|` n'importe où dans un bloc `r|` (même dans un commentaire de
  pile comme `( -- img | 0 )`, ou un `||` JS) termine la chaîne trop tôt
  et bloque le démarrage ou la page (« ERROR: | NOT FOUND! »).
- Des locales déclarées avec `{ }` dans un corps de boucle cassent («
  Error including » silencieux): déclarez-les en haut du mot.
  `SDL_Color` marche parce qu'il tient dans un registre.
- Tout appel C empile un résultat, même les `void`: faites-lui `drop`.
  Les résultats `int` demandent `sign-extend`.
- Les chargeurs paresseux sont `: nom r| ...source... | evaluate ;`,
  donc le source ne doit pas contenir `|`. Ils finissent par
  `sdl2 definitions` pour que les mots de l'utilisateur atterrissent
  dans le vocabulaire `sdl2` et ne soient pas masqués par des noms de la
  bibliothèque (`px`...).
- `do` tourne ~65k fois quand le compte est 0: utilisez `?do`. `roll`,
  `0>`, `2constant`, `within` n'existent pas. Mais `?do` ne saute
  proprement que quand `start = limit` (un compte de 0); `start > limit`
  n'est pas un saut sûr et peut s'emballer (vu avec
  `limit start dup * ?do`, où `start` est une valeur au carré qui peut
  dépasser `limit`) ou corrompre la mémoire au lieu de ne rien faire:
  gardez explicitement (`start limit < if ... then`) chaque fois que
  start peut légitimement dépasser limit, ne comptez pas sur `?do` pour
  ce cas.
- Une locale `{ }` portant le même nom qu'un mot intégré (par exemple
  `{ pat trk anchor from to }`) le masque silencieusement pour le reste
  de ce mot: `from 1+ to et-c` empile alors simplement deux nombres au
  lieu d'affecter, sans aucune erreur (`et-c` garde tranquillement son
  ancienne valeur). Trouvé dans `examples/music/sdl2_pianoroll.fs` (une
  locale appelée `to` cassait son propre `to et-c`, si bien qu'un geste
  de glisser pour allonger une note tenue l'allongeait toujours jusqu'à
  la colonne 0). Évitez les noms de mots courants (`to`, `over`, `n`,
  ...) pour des locales; quand un mot se comporte mal sans aucune
  erreur, comparez d'abord sa propre liste de locales avec la liste des
  mots.
- Web: les corps de `JSWORD:` vivent aussi dans un bloc `r|`, donc pas
  de `||` dans le JS. `flip` doit faire `yield` (il attend
  requestAnimationFrame). `bye` sur la version web lève
  `objects[op] is not a function` (déjà vrai pour le `terminal.html`
  d'origine), donc la page est inerte ensuite: la page du casse-briques
  montre un bouton « Play again » qui recharge.
- Les vrais événements de souris sont convertis en pixels logiques par
  `SDL_RenderSetLogicalSize`; les injectés (`SDL_PushEvent`) ne le sont
  pas. `pressed?` lit l'état une fois par `events`, donc un toucher plus
  court qu'une image est manqué sous POSIX (la version web l'attrape
  aussi).
- Un mot de votre cru nommé comme un mot sdl2 (`draw`, `text`, `box`,
  `line`, `step`...) le masque pour le code défini après, et `sdl2`
  vient en premier dans l'ordre de recherche, donc vos appels ultérieurs
  au nom exécutent celui de sdl2: sur les démarrages en matériel nu
  `draw` (images, `img x y --`) donnait « stack underflow » dans
  sdl2_mouse_test.fs alors que POSIX allait bien. Utilisez `paint`,
  `render`... pour les vôtres.
- Astuces de test: `SDL_RenderReadPixels` pour vérifier des pixels,
  `SDL_PushEvent` pour simuler des entrées, Xvfb + `xdotool` pour un
  vrai pointeur, Chrome sans écran par CDP pour la page web (il faut
  http, pas `file://`: `cd out/web && python3 -m http.server`).
- `[char]` n'existe qu'en compilation: bien dans une définition `:`,
  mais utilisé directement (par exemple dans un
  `create ... [char] C , ...` de niveau supérieur) il lève un
  sous-dépassement de pile silencieux sans message. Utilisez le simple
  `char` hors définition.
- Une fois que `sdl2` (et `sdl2-mixer`, ...) ont fini de se charger,
  `posix` n'est plus dans l'ordre de recherche du code qui suit (les
  `previous` du chargeur le retirent): un exemple simple qui veut
  `sysfunc`/`sofunc` directement (par exemple pour son propre
  `mkstemps`) a besoin de son propre `also posix` d'abord, comme le fait
  en interne le chargeur de `sdl2_abc.fs`. **Danger**: ce `also posix`
  reste en vigueur pour le reste du fichier et peut décaler l'ordre de
  recherche assez pour qu'un mot ultérieur de votre cru portant un nom
  courant (`remember`, ...) soit masqué par un mot sans rapport déjà
  défini dans `forth`/`internals` (par exemple `common/filetools.fs` a
  son propre `remember`, pour `rm`/`reset`). Appeler le mauvais mot
  corrompt silencieusement l'état au lieu de lever une erreur normale,
  et le symptôme est un simple « Error including » sans message, depuis
  l'endroit où la pile ou la mémoire corrompue est touchée ensuite, pas
  depuis le vrai site d'appel (`ports/posix/faults.h` convertit aussi un
  vrai SIGSEGV/SIGBUS/SIGFPE/SIGINT en la même exception silencieuse,
  donc vérifiez d'abord avec `gdb -batch -ex run -ex bt`: une sortie
  propre du processus, pas un signal attrapé, désigne exactement ce
  cas). Trouvé une fois dans `examples/music/sdl2_pianoroll.fs` (son
  propre `remember` entrait ainsi en collision, visible seulement quand
  l'application était lancée depuis un dossier où l'ordre d'évaluation
  l'exposait), la correction fut de renommer le mot (`key-remember`).
  Donnez à tout mot défini après `also posix` un nom qui a peu de
  chances d'exister déjà (ou vérifiez avec `see`/un vocabulaire neuf).
- Tester avec Xvfb + `xdotool`: `xdotool mousemove --window <id> x y`
  est relatif à la fenêtre et insensible à sa position à l'écran,
  contrairement aux coordonnées de la racine avec `import -window root`
  (une fenêtre n'est pas toujours en 0,0: vérifiez avec
  `xdotool getwindowgeometry`). Un `click` (appui+relâchement en un
  appel) peut être plus rapide qu'une image et manqué; un
  `mousedown`/`sleep 0.2`/`mouseup` séparés, avec le pointeur déjà posé
  un instant avant, est fiable. Une capture prise juste après un clic
  peut montrer l'image d'avant le clic (retard de redessin): refaites la
  capture, ou vérifiez les effets de bord (un fichier audio enregistré
  qui grossit, un fichier de test écrit) au lieu de croire une seule
  image.

## Autres pièges et astuces de test

- Trouvé en portant eForth Windows et les éditeurs (2026-10): un `if`,
  `begin` ou `do` au niveau supérieur (hors définition) ne marche pas,
  sans erreur: `s" --x" option? if false to flag then` exécutait sa
  partie `then` à chaque fois, ce qui coupait silencieusement
  l'animation de `sdl2_2048.fs`. Mettez-le dans un mot. `<# #s #>` prend
  un double, comme en ANS depuis 2026.10.27.2 (il prenait une cellule
  avant, comme dans eForth): écrivez `n 0 <# #s #>`, ou `s>d` pour un
  nombre signé. `include` est relatif au fichier qui inclut, pas au
  dossier de travail. `b@` et `b!` dans `ed`/`writhdeck` prennent une
  position dans le texte, pas une adresse. `ms-ticks 1 or rand-seed !`
  initialise `random` ( n -- r ); `rnd` n'existe pas.
- Regardez la vraie chose: cette machine peut avoir un affichage X
  (`echo $DISPLAY`). Lancez le programme, `xdotool windowfocus`, appuyez
  sur une touche avec `keydown`, un court `sleep`, `keyup`
  (`xdotool key` est plus rapide qu'une image et manqué par `pressed?`),
  et `import -window $(xdotool search --name TITRE | head -1) shot.png`.
  Une copie ralentie d'une animation (3 s au lieu de 0,2 s) la rend
  visible sur les captures. Ne faites pas `pkill -f` d'un motif qui
  apparaît dans votre propre ligne de commande.
- Un Makefile qui vérifie les résultats:
  `examples/utils/inference/Makefile` lance des scénarios, affiche les
  faits et cherche avec grep ceux attendus (`-> OK` ou `-> FAIL`).
  Retirez les `\r` de la sortie de lugforth avant de faire le grep.

- Pièges du web (ports/web) rencontrés le 2026-10-22, plus les suivants
  (clavier d'un téléphone): un corps de JSWORD ne doit pas déclarer une
  variable nommée `sp` (elle masque le pointeur de pile: le mot
  s'exécute mais ne dépile pas ses arguments);
  `[defined] x [if] ... [then]` à l'intérieur d'une définition en
  deux-points casse l'interpréteur web (enveloppez des définitions
  entières au niveau supérieur); un source JS avec un caractère non
  ASCII littéral est lu en Windows-1252 quand la page n'a pas de
  `<meta charset>` et que le serveur n'en envoie pas (utilisez `'​'`);
  un gestionnaire `keypress` qui appelle preventDefault sur un téléphone
  empêche le champ de texte caché de recevoir le caractère. Testez la
  page web avec Chromium sans écran par CDP
  (`chromium --headless=new --remote-debugging-port`, `websockets` en
  python, `Input.dispatchKeyEvent`,
  `Emulation.setDeviceMetricsOverride mobile`), après `ninja web` et
  depuis `out/web`.

- Une page sans `<meta charset>` servie sans encodage lit un source JS
  en Windows-1252: aucun caractère non ASCII littéral dans les sources
  web.
- Sur un téléphone, un clavier qui recouvre la page laisse la dernière
  ligne inaccessible s'il n'y a rien en dessous: ajoutez un remplissage
  sous le texte (`context.KeepInView`).
- Ne faites pas `pkill -f` d'un motif qui apparaît dans votre propre
  ligne de commande (cela tue le shell); listez les processus avec `ps`
  et tuez par PID.

## Nouveaux ports de plateformes (ports/gba): pièges de la chaîne de démarrage

Détail complet dans `ports/gba/README.md`; en bref, puisque cela vaut
pour le `builtins.h`/`platform.fs`/fichier `.fs` de la chaîne de
démarrage de toute nouvelle plateforme, pas seulement GBA:

- Un mot compilé dans `internals` (par
  `also internals definitions ... previous`) au lieu de `forth` (par
  `forth definitions internals ... forth`, un nom de vocabulaire nu
  étant un changement d'ordre de recherche, pas un changement de cible
  de compilation) disparaît quand `internals` sort de l'ordre de
  recherche: `' default-type` (ou autre) ne trouve alors silencieusement
  rien, et l'erreur « not found » est elle-même impossible à afficher à
  ce moment-là, puisque `type` est justement le mot pas encore lié.
  `ports/pico-ice/platform.fs` le fait bien; copiez son idiome exact
  pour la mise en place des E/S d'une nouvelle plateforme.
- Un commentaire `\` placé *avant* la première ligne `needs` d'un
  fichier **d'entrée** de la chaîne de démarrage n'est pas sûr: `\` et
  `(` sont eux-mêmes définis par `phase1.fs`, pas disponibles avant son
  `needs`, et (contrairement à l'en-tête `\ Copyright ...` de tous les
  autres fichiers) un commentaire autre que Copyright n'y est pas retiré
  par `tools/importation.py`, donc il atterrit dans le texte de
  démarrage final avant même que la gestion des commentaires existe.
- Chaque plateforme doit définir `ok` elle-même
  (`internals : ok ." <bannière>" raw-ok ; forth`, comme
  `ports/posix/posix.fs`/`ports/pico-ice/platform.fs`); il n'y a aucun
  défaut dans `common/*.fs`. Le dernier acte de `common/fini.fs` est
  d'`execute` le xt que `autoboot.fs` laisse sur la pile, puis d'évaluer
  le mot nu `ok` — oublier de le définir veut dire que `' ok` dans un
  `autoboot.fs` minimal ne trouve rien, un autre plantage silencieux.
- Les trois symptômes ci-dessus sont le *même* `SIGSEGV` silencieux,
  sans texte d'erreur, parce que ce qui manque est, transitivement,
  `type`. Si une chaîne de démarrage écrite de zéro plante sans aucune
  sortie, soupçonnez l'un d'eux avant toute autre chose;
  `gdb -batch -ex run -ex bt` montrera juste un cadre générique
  `forth_run`/`TIER0_OPCODE_LIST` dans les deux cas, pas la vraie cause.
- **Celui qui a vraiment cassé du vrai matériel** (les quatre ci-dessus
  n'ont touché que le simulateur POSIX): un `crt0.s` écrit à la main
  pour une nouvelle cible en matériel nu doit copier `.data` de son
  image ROM/flash vers son adresse d'exécution en RAM, pas seulement
  mettre `.bss` à zéro. `.data` est l'endroit où peut vivre l'état
  interne d'une bibliothèque C — avec newlib, `_impure_ptr` et tout ce
  qu'il désigne (errno, la comptabilité de malloc) — et sauter la copie
  laisse ce pointeur désigner les déchets qui se trouvaient déjà en RAM.
  Rien ici n'appelle newlib directement, mais quelque chose dans la mise
  en place de `forth_init()` le fait apparemment, et le résultat fut une
  écriture sauvage par ce pointeur de déchets, atterrissant dans la RAM
  vidéo: un vrai écran GBA/mgba restait uniformément blanc sans aucune
  sortie d'erreur, reproduit avec `forth_init()` seul, avant que le
  moindre texte Forth ne soit interprété. Correction standard, manquante
  ici jusqu'à sa découverte: ajouter les symboles
  `__data_load`/`__data_start`/`__data_end` au script d'édition de liens
  et une boucle de copie dans `crt0.s`, avant la mise à zéro de `.bss`.
  La section « Fixed, and confirmed booting » de `ports/gba/README.md` a
  toute la piste de la dichotomie. Séparément: un émulateur qui tourne
  bien plus lentement que le vrai matériel (ici, mgba dans un bac à
  sable partagé sans GPU) peut ressembler exactement à ce même bogue sur
  une capture prise trop tôt — laissez-lui du vrai temps d'horloge avant
  de conclure que l'écran est réellement bloqué.
- **Trouvé en portant `examples/games/sdl2_roguelike.fs` vers
  `ports/gba/roguelike.fs`** (les locales `{ local }` et les chaînes
  booléennes de ce Forth ont deux autres arêtes vives, aucune propre à
  la GBA):
  - Une chaîne booléenne `and`/`or` de plus de deux termes a besoin d'un
    combinateur *par paire*, pas un par terme supplémentaire: combiner
    quatre indicateurs demande trois `and`/`or` en tout (par exemple
    `a b or  c d or  or`), pas deux. En écrire un de moins laisse un
    indicateur égaré sur la pile pour toujours — pas de plantage au site
    d'appel lui-même, juste une lente fuite de profondeur de pile qui
    déborde plus tard, arbitrairement loin, d'apparence sans rapport.
    Attrapé deux fois dans le même fichier (deux vérifications de bornes
    de la fenêtre, chacune un `and` de deux `or`), la pire des deux
    étant appelée sur chaque case visible de chaque image, débordant en
    quelques dizaines d'images sous la forme d'une erreur de bus.
  - Une `{ local }` déclarée **dans** un corps `do ... loop` n'est pas
    sûre: `common/locals.fs` implémente les locales avec `>r` sur la
    pile de retour, déroulée une seule fois, au `;` du *mot englobant*,
    pas une fois par itération — donc un corps de boucle qui en déclare
    une fuit une cellule de la pile de retour à chaque tour, corrompant
    silencieusement l'indice et la limite de la boucle, qui vivent sur
    cette même pile. Le `draw-bar` d'origine de
    `examples/games/sdl2_roguelike.fs` l'évite en employant un mot
    `shade` à état au lieu d'une locale par itération dans sa boucle de
    cœurs; la correction en portant est soit la même (laisser la valeur
    sur la pile de données au lieu de la nommer), soit de factoriser le
    corps de boucle dans son propre mot, appelé une fois par itération,
    pour qu'il ait sa propre portée nettoyée à son propre `;`.
  - Aucun des deux bogues ne produit un plantage répétable dès le
    premier appel: il faut assez d'itérations pour faire déborder une
    pile, donc ils apparaissent comme un plantage après N images (N
    variant d'une exécution à l'autre, puisqu'il dépend d'un état
    accessoire comme la disposition de la carte) plutôt qu'immédiatement
    — trompeur si l'on s'attend à une reproduction déterministe. Une
    dichotomie avec des affichages `." CHECKPOINT" cr` entre les appels
    suspects (même technique que pour les cas de `SIGSEGV` silencieux
    ci-dessus) les trouve vite une fois qu'on sait chercher cette forme
    de bogue, pas seulement un mot manquant ou mal placé.
  - **Une fois correct, c'était injouablement lent: ~20 s par redessin
    sur un vrai mgba**, confirmé par l'utilisateur. Une boucle Forth par
    pixel (`gba-dot`/`tint`/`tile-pixel`, un aiguillage de mot
    interprété par pixel) convient pour une poignée de tuiles statiques
    (la démo de démarrage) mais pas pour plus de 30000 pixels par tour
    (toute la fenêtre de 30x18 tuiles du roguelike). Correction:
    déplacer seulement la boucle interne chaude — un blit de tuile, un
    remplissage de rectangle — dans du C compilé simple
    (`gba_blit_tile`/ `gba_fill_rect` dans `main.c`, deux nouveaux
    opcodes), en gardant tout ce qui est au-dessus (quoi dessiner,
    caméra, état du jeu) en Forth; `draw-tile-tinted`/`gba-box` de
    `platform.fs` ont gardé leurs noms et effets de pile exacts, donc
    rien de ce qui les appelait n'a changé. Leçon générale pour tout
    futur port sur un matériel aussi modeste: prévoyez que la boucle
    chaude de mélange de pixels/octets doive être du C, pas du Forth,
    dès qu'elle tourne par pixel à une résolution réelle — détail
    complet, avec les chiffres exacts, dans la section « Performance »
    de `ports/gba/README.md`.

## Pièges rencontrés avec writhdeck, la console et l'éditeur de ligne (2026-10)

- Un `|` n'importe où dans un bloc `r|` le termine, **un commentaire de
  pile aussi**: `( cp -- texture|0 )` dans `sdl2_console_ttf.fs` faisait
  bloquer tous les lugforth au démarrage (les tests de tous les ports se
  bloquaient, c'est le signe). Écrivez `texture or 0`. Pareil pour `)`
  dans un `( commentaire )`: le texte qui suit est compilé
  (`ERROR: ---- NOT FOUND!`).
- `>r ... r@` dans un mot qui a des `{ locales }` lit le cadre des
  locales: les résultats sont des déchets. Utilisez une locale ou une
  `value`.
- Des `{ locales }` dans un corps `if` / boucle cassent (écrites dans un
  conditionnel elles échouaient silencieusement: writhdeck
  n'enregistrait pas). Déclarez-les en haut du mot, même quand cela
  semble inoffensif.
- Il n'y a pas de `l@` (utilisez `sl@`, signé, ou `l!` pour écrire), pas
  de `defer@` (le xt d'un defer est `' nom >body @`), pas de `bytes`
  hors de `editor.fs`. `?do` avec start > limit s'emballe: gardez-le.
- Un mot tel que `: ttf-one ( i -- )` laisse une valeur sous son travail
  (l'original utilisait `dup`): remplacer sa première ligne doit garder
  la forme de la pile (un sous-dépassement -4 ne se voyait que comme
  `--gui: cannot open a window`).
- Les mots de `sdl2_console.fs` sont compilés par `eval-copy` à
  l'ouverture de la fenêtre, pas par la compilation: `ninja posix` ne
  les vérifie pas. Lancez
  `SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy out/posix/lugforth ports/posix/sdl2_console_tests.fs`
  après l'avoir modifié, et regardez une capture (Xvfb a besoin de
  `xdotool windowfocus` avant de taper).
- Le curseur de la console, une attente de touche qui interroge (`key?`)
  et tout état gardé seulement dans `con-key` se figent quand un
  programme interroge: gardez cet état dans un mot que `con-key` et
  `con-key?` appellent tous les deux (`blink-tick`).
- Python dans un heredoc: `\xc3` dans une chaîne normale devient le
  caractère U+00C3, pas un octet; écrivez les vrais caractères dans les
  sources Forth (UTF-8), et lisez et écrivez les fichiers avec
  `encoding='utf-8'`.
- `tools/words_reference.py` trouve les constantes avec une expression
  régulière sur la ligne de source: `11 35 os constant EAGAIN` (valeurs
  Linux et macOS) a son propre motif; une nouvelle façon d'écrire des
  constantes en demande un aussi, sinon les descriptions disparaissent
  silencieusement de `docs/en/WORDS.md`.

## Ajouter des mots

Une nouvelle fonction C cherchée par `sysfunc` va aussi dans la table de
`ports/posix/main.c` (version statique, voir PORTS.md).

Après avoir ajouté des mots à l'interpréteur, compilez
(`ninja posix web`) et lancez `python3 tools/words_reference.py`: il
interroge les vrais interpréteurs sur leurs mots et réécrit
`docs/en/WORDS.md` (descriptions tirées de `attic/oldsite/*.html`,
`tools/words_notes.py`, des commentaires de pile des sources; ajoutez
une note dans `words_notes.py` pour un mot qu'il laisse sans
description). `tools/words_added.py` liste ce qui diffère d'uEforth
d'origine pour la page « New words » du site.

Écrivez l'explication d'un nouveau mot dans le source:
`: nom ( pile ) ( ce qu'il fait )` ou `: nom ( pile )   \ ce qu'il fait`
sur la même ligne, ou des lignes `\` juste au-dessus de la définition.
`words_reference.py` les prend (pas depuis les fichiers `*sdl2*` ni
`_tests.fs`). Les aides qui ont un préfixe commun (`le-`, `pg-`, `hist`,
`pool`) reçoivent une note par famille (`PREFIX_NOTES` dans
`words_reference.py`). Les mots de `sdl2` sont écrits à la main dans
`docs/en/SDL2-words.md` (`docs/en/SDL2.md` est la visite guidée,
`docs/en/SDL2-web.md` la version web): ajoutez-y une ligne quand un mot
court est ajouté dans l'un des trois `sdl2.fs`.

## Pièges Forth rencontrés encore et encore

- `b@` dans l'éditeur prend une *position dans le texte*, pas une
  adresse.
- `{ locales }` dans un corps de conditionnelle ou de boucle cassent:
  déclarez-les en haut du mot.
- Un `|` dans un bloc `r|` le termine. `[char]` seulement dans les
  définitions. `?do` avec start > limit s'emballe.
- Un « Error including: file » silencieux est une exception quelque
  part: dichotomie avec un fichier brouillon contenant des affichages
  `.`.

- Un opcode utilisé par un fichier Forth partagé doit exister dans
  chaque `main*.c` qui le charge: l'application Android a sa propre
  liste (ports/android/app/native/main_android.cpp). `host-mac?`
  (utilisé par `os` dans ports/posix/posix.fs) y manquait dans la v0.5.0
  et la console SDL2 restait noire, sans message. Après avoir ajouté un
  opcode à ports/posix/main.c, ajoutez-le aussi à main_android.cpp, et
  essayez l'APK sur un téléphone (`adb logcat` ne montre rien du côté
  Forth).
