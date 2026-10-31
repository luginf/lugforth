# Lugforth

*Français - [English](README.md)*

Un petit Forth 64 bits, insensible à la casse, construit sur un noyau C minimal. Il tourne sous Linux, dans le
navigateur, sur Android, sur des microcontrôleurs (ESP32, Pico, GBA), sur du matériel nu (Raspberry Pi, PC
x86-64 via UEFI) et sous DOS (386 et plus, l'invite en mode texte pour l'instant).

Site: [luginf.github.io/lugforth](https://luginf.github.io/lugforth/) -
[Manuel](docs/fr/MANUAL_fr.md) - [Référence des mots](docs/en/WORDS.md) (en anglais)

> **Conformité au standard:** Lugforth passe toute la
> [suite de tests Forth 2012](https://github.com/gerryjackson/forth2012-test-suite)
> (John Hayes, Gerry Jackson) sur la version POSIX, sans aucun échec dans ses 12 jeux de mots: Core
> (avec les tests Core supplémentaires), Core Extension, Block, Double-Number, Exception, Facility,
> File-Access, Locals, Memory-Allocation, Programming-Tools, Search-Order et String. Le jeu en virgule
> flottante de la suite n'est pas exécuté. À vérifier avec `ninja forth2012` (réseau requis la première
> fois).

## Démarrage rapide

```
sudo apt install ninja-build
./configure.py
ninja posix
out/posix/lugforth
```

À l'invite, `info` (ou `help`) montre la révision, la mémoire et quelques mots pour commencer.

Les binaires (archive Linux et AppImage, APK Android) sont joints à chaque publication. Les compilations pour les
autres cibles sont dans le [manuel](docs/fr/MANUAL_fr.md#compilation).

## Ce qu'il contient

* **Interpréteur**: édition de ligne avec historique et complétion par Tab, un pagineur pour les longues
  sorties, une fenêtre console avec police TrueType (`lugforth --gui`).
* **Éditeurs**: `ed`, un éditeur de texte en Forth avec coloration syntaxique, et `writhdeck`, un éditeur
  d'écriture (mode focus, Hemingway, objectif du jour, plan).
* **Mots SDL2**: graphismes, images, son, airs ABC, MIDI et un synthétiseur FM.
* **Exemples**: casse-briques, roguelike, tetris, snake, démineur, mastermind, 2048, fils de fer en 3D, un
  interpréteur de Z-machine pour la fiction interactive (histoires Infocom et Inform), un moteur d'inférence,
  une machine virtuelle Uxn/Varvara qui exécute des fichiers `.rom`.
* **Matériel nu**: la même invite, le même écran, clavier et fichiers sur un Raspberry Pi 3/4 et sur un PC
  démarré depuis une clé USB, sans système d'exploitation.

Le détail de chacun est dans le [manuel](docs/fr/MANUAL_fr.md).

## Documentation

* [docs/fr/MANUAL_fr.md](docs/fr/MANUAL_fr.md): un tutoriel dans l'esprit de Starting Forth, puis l'usage, les
  éditeurs, les exemples, les ports, la compilation
* [docs/en/WORDS.md](docs/en/WORDS.md): référence des mots (en anglais, générée)
* [docs/fr/](docs/fr/README_fr.md): toute la documentation en français
* [docs/en/dev/](docs/fr/dev/README_fr.md): notes pour les développeurs (architecture, fonctionnalités, ports, pièges,
  idées), [en français](docs/fr/dev/README_fr.md)
* [examples/README.md](examples/README.md), [ports/rpi/README.md](ports/rpi/README.md),
  [ports/efi/README.md](ports/efi/README.md), [ports/dos/README.md](ports/dos/README.md) (en anglais)

## Inspiration

* [uEforth](https://github.com/flagxor/ueforth) de Bradley D. Nelson: Lugforth est parti d'un fork de ce projet.
  Il n'est pas officiel et n'est pas approuvé par lui.
* [eForth Windows](https://github.com/MPETREMANN11/eForth-Windows) de Marc Petremann: 2048, la bibliothèque 3D
  et le moteur d'inférence sont des portages de son travail.
* [Forth is Fun](https://gitlab.com/garvalf/forth-is-fun) : forthtoise, la tortue
  graphique (`examples/graphics/gfx_turtle.fs`)
* [r3forth](https://github.com/phreda4/r3) de Pablo Hugo Reda (phreda): tetris, snake et démineur sont
  réécrits d'après ses jeux.
* [WrithDeck](https://github.com/luginf/writhdeck): le modèle de `writhdeck`.

Crédits et licences du code ajouté: [NOTICE](NOTICE).
