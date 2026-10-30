# Mots courts sdl2 pour le web

*Traduction française de [docs/en/SDL2-web.md](../en/SDL2-web.md) (la
version anglaise fait foi).*

La version web a les mêmes mots courts `sdl2` que la version Linux
([SDL2.md](SDL2_fr.md)), dessinés sur un canevas HTML. Un jeu écrit avec
eux tourne sans changement dans les deux:
`examples/games/gfx_breakout.fs` est chargé tel quel par
`ports/web/gfx_breakout.html`, `examples/music/gfx_drums.fs` par
`ports/web/gfx_drums.html`, `examples/music/fm/gfx_fm.fs` (un
synthétiseur FM avec entrée MIDI, voir `examples/music/fm/README.md`)
par `examples/music/fm/gfx_fm.html`, `examples/games/gfx_roguelike.fs`
(avec sa planche de tuiles `examples/games/gfx_tiles.png`) par
`ports/web/gfx_roguelike.html` et `examples/games/pinball/` par
`ports/web/gfx_pinball.html`, et `examples/games/gfx_raycaster.fs` (un
labyrinthe 3D, dirigé à la souris) par `ports/web/gfx_raycaster.html`.
Les cellules de la version web font 32 bits: un jeu qui calcule en
virgule fixe 16.16 doit multiplier par `*/` (voir le raycaster).

À l'invite de `terminal.html`, comme dans la fenêtre console sous Linux,
`graphic` ouvre une zone de dessin au-dessus du texte (640x480, réduite
à la moitié de la hauteur de la page au plus; `w h graphic-size` pour
une autre taille) et les mots de dessin marchent depuis l'invite, le
terminal restant utilisable, aussi sur un téléphone: `graphic`, puis
`10 10 150 140 line`, `red color 200 20 100 60 box`, `cls`. `sdl2` seul
donne aussi les mots de son à l'invite (`s" t200 o4 c d e" play`,
`440 300 beep`): la première touche pressée ou le premier toucher permet
à la page de jouer des sons.

```forth
sdl2                          \ se charge au premier usage, comme sous Linux
3 to zoom  320 240 screen     \ ouvre un canevas sur la page
: frame  events  black color cls  red color 10 10 50 50 box  flip ;
: run  begin frame quit? until close-screen ;
run
```

## Essayer les jeux

La page charge son fichier Forth avec un XMLHttpRequest, que les
navigateurs refusent sur `file://`, donc servez le dossier de
compilation en http:

```sh
ninja web
cd out/web && python3 -m http.server
# puis ouvrez http://localhost:8000/gfx_breakout.html
# ou            http://localhost:8000/gfx_drums.html
# ou            http://localhost:8000/gfx_fm.html
# ou            http://localhost:8000/gfx_roguelike.html
# ou            http://localhost:8000/gfx_pinball.html
```

Les flèches, la souris ou le toucher déplacent la raquette. Espace, clic
ou toucher lance la balle. Échap quitte, et la page propose alors « Play
again ».

Dans la boîte à rythmes, tout se fait à la souris: cliquer ou glisser
sur la grille, boutons play/stop, tempo
- et + (1 bpm avec le bouton gauche, 5 avec le droit, maintenir pour
  répéter), CLR et X (quitter), et les pads colorés à gauche jouent les
  percussions. Le clavier marche aussi: espace, haut/bas (5 bpm), 1 2 3
  4, c, Échap.

Dans le roguelike, les flèches ou w a s d déplacent, espace attend et un
clic avance vers la case cliquée.

Dans le flipper (Night Raid), z et m ou les flèches bougent les
palettes, bas maintenu puis relâché lance la balle, espace ou n secoue
et Entrée démarre une partie. Sur un écran tactile (ou avec `?touch`
dans l'adresse) la page montre des boutons sur le jeu: les deux palettes
en bas, LAUNCH, START et NUDGE. Chacun suit son propre doigt et fixe la
touche dans `context.sdl.down` (les mêmes codes que le clavier), donc
les deux palettes peuvent être tenues à la fois, ce que le pointeur
unique du canevas ne peut pas faire.

## Ce qui existe

Ces mots ont les mêmes noms et les mêmes effets de pile que sous Linux,
voir [SDL2-words.md](SDL2-words_fr.md) pour ce que fait chacun:

* Écran:
  `screen close-screen screen-open? zoom width height title fullscreen cursor`
* Couleurs: `color rgb rgba alpha` et les couleurs nommées
  `black white red green blue yellow cyan magenta orange purple pink brown gray`
* Dessin: `cls dot line box frame hline disc circle flip pixel@`
* Temps: `ticks delay dt`
* Entrées:
  `events quit? quit! pressed? scan? last-key mouse-x mouse-y wheel`,
  les constantes `key-`
  (`key-left key-right key-up key-down key-space key-enter key-esc key-tab key-backspace`)
  et `LEFT-BUTTON MIDDLE-BUTTON RIGHT-BUTTON`
* Images (les mots de `sdl2-image`, qui ne fait rien ici):
  `load-image free-image image-size draw draw-size draw-part image-alpha image-tint`
* Bips et coups:
  `beep drum-hit pitch-drop silence beeping? wave beep-volume audio-open audio-open? latency`

## Ce qui est différent

* **Non disponibles:** les mots bruts `SDL_`, `sdl2-ttf`, les fichiers
  de sons et de musique de `sdl2-mixer` (`load-sound`, `play`,
  `load-music`, `music`...), et quelques aides:
  `wait sdl-error window-handle renderer pen pen-r pen-g pen-b pen-a sample-rate`.
* **`screen`** met une couche noire avec son propre canevas sur toute la
  page, donc le terminal est caché pendant que le jeu tourne. Le canevas
  garde son rapport d'aspect, montré à `zoom` fois sa taille ou plus
  petit si la fenêtre est trop petite. `close-screen` le retire.
* **`flip`** attend l'image d'animation suivante, donc les jeux tournent
  à la fréquence de rafraîchissement de l'écran, et s'arrêtent quand
  l'onglet est caché.
* **Les touches** sont prises au terminal tant qu'un écran est ouvert:
  elles n'y sont pas tapées. Les combinaisons avec Ctrl, Alt ou Meta et
  les touches F ne sont pas interceptées (les combinaisons Ctrl restent
  traitées par le terminal, comme dans toute page de la version web).
* **`pressed?`** voit aussi une touche ou un bouton pressé et relâché
  entre deux `events`, ce que la version Linux manque.
* **Les positions de la souris et du toucher** sont en pixels logiques,
  comme sous Linux, et un toucher compte comme `LEFT-BUTTON`.
* **Sur un téléphone**, le clavier de l'appareil ne monte pas tant qu'un
  écran est ouvert: le terminal ne donne le focus à son champ de texte
  caché, sur un toucher, que lorsqu'aucun écran n'est ouvert, et
  `screen` le lui retire.
* **Les images** sont chargées par le navigateur, donc tout format qu'il
  lit convient. `load-image` attend le fichier (l'interpréteur cède la
  main entre-temps) et lève une exception s'il ne peut pas être chargé.
  Comme sous Linux, un nom avec des dossiers qui n'est pas trouvé est
  réessayé sans eux: `s" examples/games/gfx_tiles.png"` trouve
  `gfx_tiles.png` à côté de la page. `image-tint` multiplie les
  couleurs, comme dans SDL, avec une copie de l'image gardée pour chaque
  couleur.
* **Le son** démarre après la première touche pressée ou le premier
  clic, parce que les navigateurs refusent de jouer du son avant. Les
  bips d'avant sont perdus, et une file de plus de deux secondes n'est
  pas allongée. Un `drum-hit` joue aussitôt par-dessus les autres sons,
  avec 24 sons vivants au plus.
* **Les airs** (`sdl2-abc`, le lecteur de notation ABC, voir
  [SDL2.md](SDL2_fr.md)) ont les mêmes mots que sous Linux:
  `abc abc-play abc-loop tune-play tune-loop tune-stop tune-playing? tune-bpm tune-program tune-ms tune-notes`,
  mais il n'y a ni synthétiseur MIDI ni soundfont dans un navigateur.
  Chaque note est jouée par un oscillateur (l'onde triangle, ou celle
  choisie avec `wave`), donc l'instrument de `%%MIDI program` est
  ignoré, et `tune-midi` et `tune-save` n'existent pas. `soundfont?`
  répond toujours faux et `soundfont` ne fait rien, donc un programme
  qui nomme une soundfont tourne sans changement; `sdl2-mixer` n'existe
  que pour être tapé et donne `music-volume`. Un air en boucle attend
  que le son soit permis et démarre à sa répétition suivante s'il ne
  l'était pas encore. Le volume est `music-volume` (0 à 128, comme sous
  Linux).
* **`scan?`** prend la place d'une touche (`KeyboardEvent.code`), donc
  une rangée de touches de piano ne bouge pas sur un clavier AZERTY.
  Seuls les lettres, les chiffres, la virgule, le point, la barre
  oblique, le point-virgule, l'apostrophe, les crochets, le moins, le
  égal, l'espace, entrée, échap, tab, retour arrière et les flèches ont
  ici un code de balayage.
* **`line`** emploie l'algorithme de Bresenham et peut différer d'un
  pixel de celui de SDL.
* **`bye`** arrête l'interpréteur web. Un fichier de jeu qui se termine
  par `bye`, comme le casse-briques, laisse la page inerte quand `run`
  rend la main (la console du navigateur montre une erreur
  `objects[op] is not a function`, qu'un simple `bye` dans la page
  terminal donne aussi). Rechargez la page pour recommencer.

## Tests

Vérifié avec Chrome 150 sans écran, piloté par le protocole DevTools: la
page se charge sans erreur de console, le casse-briques est dessiné,
bouger le pointeur et tenir une touche déplacent la raquette, un très
court appui sur espace lance la balle, un robot qui suit la balle à la
souris casse des briques et programme du son, Échap ferme l'écran. La
page de la boîte à rythmes a été vérifiée de la même façon: un clic sur
la grille bascule un pas, les boutons démarrent et arrêtent le
séquenceur (des sons sont programmés, plusieurs à la fois), changent le
tempo, vident la grille et quittent, et un clic sur un pad joue sa
percussion. La page du roguelike aussi: la planche de tuiles se charge
(64x8), les touches et les clics jouent un tour sans atteindre le
terminal, les cases mémorisées sont plus sombres, les monstres
poursuivent et blessent, Échap ferme l'écran. Sa musique aussi: l'air
est lu (37 notes, 21,8 s à 88 bpm), toutes ses notes sont programmées et
répétées, `m` l'arrête et le relance, et Échap l'arrête. Firefox et
Safari ne sont pas testés.

## Jeux sans page à eux

`terminal.html?run=<fichier>.fs` lance tout fichier de jeu servi à côté
de la page (plusieurs fichiers, chargés dans cet ordre:
`?run=2048.fs,gfx_2048.fs`). Testé avec Chromium sans écran le
2026-10-30 (l'écran s'ouvre et se dessine): `gfx_2048` (avec `2048.fs`),
`gfx_snake`, `gfx_tetris`, `gfx_minesweeper`, `gfx_mastermind`,
`gfx_invaders`, `gfx_magnoquill`, `gfx_demo`, `gfx_3d` (avec `3d.fs`),
`gfx_turtle` (les commandes se tapent dans une invite sous le dessin).
`ports/web/BUILD` les copie dans `out/web` et la page d'accueil les
relie. Ne tournent pas sur le web: `gfx_mouse_test` (demande
`sdl2-ttf`), `gfx_pianoroll` (une soundfont; pas essayé). Un fichier qui
fait `include` demande la garde `[undefined] mot [if] include fichier.fs
[then]` et son fichier inclus placé en premier dans `run=`.