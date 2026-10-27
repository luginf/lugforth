# Lugforth

*Traduction française de [docs/pages/index.md](../pages/index.md), la
page d'accueil du site (le site lui-même est en anglais).*

Un petit interpréteur Forth: un noyau C plus du code de démarrage en
Forth, dans l'esprit d'(u)eForth.

**Télécharger.** [Le manuel
(PDF)](https://github.com/luginf/lugforth/releases/latest/download/lugforth-manual.pdf).
Les versions de la [dernière
publication](https://github.com/luginf/lugforth/releases/latest): Linux
(archive, AppImage), Windows (zip, compilé avec MSVC ou MinGW, avec les
DLL SDL2, les exemples et la doc), macOS (zip), Android (APK), images
Raspberry Pi et PC UEFI. Les zips contiennent `examples/` et `docs/`.

**Essayer.** La [version
web](https://luginf.github.io/lugforth/demo/terminal.html), avec le
casse-briques, les percussions, un roguelike, un labyrinthe 3D (le
raycaster, dirigé à la souris) et un flipper (boutons tactiles sur
téléphone). Tout fichier de jeu tourne aussi avec
`terminal.html?run=sdl2_roguelike.fs`. Du dessin depuis des boutons
HTML: un mot Forth appelé par un bouton, des lettres vectorielles, des
lettres en contour, du string art
([sources](https://github.com/luginf/lugforth/tree/main/examples/web)).

**Mots.** [Tous les mots](https://luginf.github.io/lugforth/words.html),
les mots ajoutés par Lugforth, [graphismes et son SDL2](SDL2_fr.md).

**Plateformes.** Linux, macOS, Windows, web, Android, Raspberry Pi, PC
(UEFI), ESP32, Raspberry Pi Pico, pico-ice, Game Boy Advance. Voir les
[ports](ports_fr.md).

## Ajouts à uEforth

* Environ 70 mots ANS manquants (`pick`, `roll`, `compare`, `>number`,
  doubles...).
* Des liaisons SDL2: fenêtres, dessin, entrées, images, texte, son,
  musique ABC, aussi sur le web avec les mêmes mots.
* Un éditeur de ligne de terminal: historique, touches de curseur,
  complétion par Tab des mots et des noms de fichiers. Un pagineur
  (`common/pager.fs`) arrête les longues sorties à chaque écran sur un
  terminal et sur les consoles en matériel nu: espace, Entrée, `q`;
  `pager-off`.
* De nouveaux ports: Android, Game Boy Advance, Raspberry Pi Pico,
  Raspberry Pi et PC UEFI (matériel nu), SDL2 sur Windows et macOS, une
  fenêtre console en SDL2 (`lugforth --gui`).
* Deux éditeurs de texte, `ed` et `writhdeck` (Linux, macOS, Windows;
  aussi sous gforth).
* Des exemples: jeux (casse-briques, roguelike, 2048 avec animation, un
  interpréteur de Z-machine pour la fiction interactive), fils de fer 3D
  qui tournent, un moteur d'inférence à règles, des bancs d'essai, un
  convertisseur txt2tags.

## Démarrage rapide

```
git clone https://github.com/luginf/lugforth.git
cd lugforth
sudo apt install ninja-build
./configure.py
ninja
out/posix/lugforth
```

```
> : square dup * ;
> 12 square .
144  ok
```
