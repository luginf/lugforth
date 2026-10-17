# Lugforth sur Raspberry Pi / QEMU : manuel

Ce manuel décrit comment utiliser la version « bare-metal » de Lugforth
(`ports/rpi`) : sans système d'exploitation, sous QEMU ou sur un Raspberry Pi 3.
Pour les détails techniques (registres, DMA, tests), voir `README.md` (en anglais).

Les commandes `make` ci-dessous se tapent dans `ports/rpi`. Depuis la racine du
dépôt, ajouter `-C ports/rpi` (par exemple `make -C ports/rpi gui`).

## 1. Prérequis

```sh
sudo apt install gcc-aarch64-linux-gnu picolibc-aarch64-linux-gnu qemu-system-arm
sudo apt install dosfstools mtools        # pour fabriquer l'image de la carte SD
```

## 2. Lancer

| commande | effet |
|---|---|
| `make gui` | fenêtre QEMU (raspi3b) : la console Forth est **dans la fenêtre**, le clavier est celui de la fenêtre |
| `make gui BOARD=virt` | pareil sur la carte QEMU `virt` (écran `ramfb`) |
| `make gui DISK=../../out/rpi/disk.img` | avec une carte SD (voir §4) |
| `make run` | QEMU `virt`, console dans le terminal (Ctrl-A puis X pour quitter) |
| `make run-pi3` | QEMU raspi3b, console dans le terminal |
| `make screen` | fenêtre pour les graphiques, console dans le terminal |
| `make breakout` | idem, avec le jeu `examples/sdl2_breakout.fs` |
| `make test` | démarre les cartes sous QEMU et vérifie tout (écran, clavier, fichiers, son, Varvara) |

Pour quitter la fenêtre de `make gui` : fermer la fenêtre QEMU.

## 3. La console à l'écran

Quand la carte a un écran (la fenêtre QEMU, ou un écran HDMI), la console Forth
y est dessinée en plus de la liaison série : 80×30 caractères, curseur, défilement,
couleurs ANSI, historique avec les flèches.

### Taille

Par défaut 1280×960 (zoom 2). Au choix :

- à la compilation : `make gui ZOOM=1` (640×480) ou `ZOOM=2` ;
- au prompt : `1 console-zoom`, `2 console-zoom` ou `3 console-zoom` (1920×1440).
  L'écran est effacé.

### Clavier

Au départ le clavier est US (QWERTY) tant qu'aucun réglage n'est fait ; avec
`make disk` (§4) l'AZERTY est mis par défaut.

- `azerty` : disposition française. `qwerty` : retour à la disposition US.
- AltGr (Alt droit) donne `~ # { [ | ` \ ^ @ ] }`. Les lettres `é è ç à ù` et
  `² ° £ µ §` fonctionnent. La touche `<` `>` (à côté de Maj gauche) aussi.
- QEMU envoie les positions des touches : c'est la carte qui décide de la
  disposition, quelle que soit celle de la machine hôte.

### Quand un programme prend l'écran

Les programmes graphiques (`sdl2`, jeux, Varvara) prennent l'écran à la console.
Quand ils se terminent (`close-screen`, ou F12 pour Varvara), la console revient,
effacée.

## 4. La carte SD (fichiers)

Sans carte, les mots fichiers (`ls`, `include`, `open-file`...) répondent `ERROR`.

```sh
make disk                                   # crée out/rpi/disk.img
make gui DISK=../../out/rpi/disk.img
```

`make disk` fabrique une image (partition MBR, FAT32, 64 Mo) avec :

- les exemples `examples/*.fs` à la racine ;
- le dossier `examples/varvara/` (programmes et ROM Uxn) sous `varvara/` ;
- `disk/autoexec.fs` sous le nom `autoexec.fs` (voir ci-dessous).

Dans la console : `ls` (racine), `ls varvara`, `include 2048.fs`, `cat fichier`...

Limites de la FAT : un fichier **créé** doit avoir un nom 8.3 (pas de nom long) ;
la lecture des noms longs fonctionne.

### autoexec.fs : ce qui s'exécute au démarrage

Si la carte contient `autoexec.fs` à la racine, il est exécuté après la bannière
et avant le premier prompt. Celui de `make disk` contient une seule ligne :

```forth
azerty
```

Pour le changer, **éditer `ports/rpi/disk/autoexec.fs`** (n'importe quel éditeur de
l'hôte) puis refaire `make disk`. On peut y mettre ce qu'on veut, par exemple :

```forth
azerty
1 console-zoom
include varvara/varvara_rpi.fs
s" varvara/xo5.rom" varvara
```

Sans refaire toute l'image, on peut aussi remplacer le fichier directement :

```sh
mcopy -o -i out/rpi/disk.img@@1M mon_autoexec.fs ::autoexec.fs
```

(`@@1M` : la partition commence à 1 Mo. QEMU doit être arrêté.)

Depuis la console Forth, on peut écrire le fichier sans éditeur :

```forth
s" autoexec.fs" w/o create-file throw
dup s" azerty" rot write-file throw
close-file throw
```

### Éditeurs

`ed` et `writhdeck` (en Forth, `ports/posix`) **ne fonctionnent pas encore sur la
carte** : ils utilisent `termios` et la libc de Linux. Un portage reste à faire.
En attendant, éditer les fichiers sur l'hôte.

## 5. Graphiques : les mots `sdl2`

Les mêmes mots courts que sous Linux et sur le web (`screen`, `color`, `box`,
`line`, `text`, `flip`, `events`, `pressed?`, `beep`...). `examples/sdl2_breakout.fs`
tourne sans changement.

```forth
sdl2  2 to zoom  160 120 screen
$101830 color cls  red color 10 10 50 30 box  white color 120 20 15 circle  flip
```

Détails (images intégrées au noyau, police 8×8, sons, ABC) : `README.md`.

## 6. Varvara : lancer des ROM Uxn

Varvara/Uxn est une petite machine virtuelle ([100r.co](https://100r.co/site/uxn.html)).
Ici elle est écrite en Forth (`examples/varvara/`) et tourne sur la carte avec tous
ses périphériques : écran, clavier, souris, console, fichiers, son, date.

Avec la carte SD de `make disk` :

```forth
include varvara/varvara_rpi.fs
s" varvara/boot.rom" varvara              \ la ROM de démonstration
s" varvara/xo5.rom" varvara               \ un morpion 5x5 : cliquer dans la grille
s" notes.txt" s" ma.rom" varvara-open     \ une ROM à qui l'on donne un fichier
```

### Touches pendant l'exécution

| touche | rôle |
|---|---|
| Ctrl | bouton A |
| Alt | bouton B |
| Maj | Select |
| Origine (Home) | Start |
| flèches | croix directionnelle |
| texte tapé | envoyé à la ROM |
| F1 | change le zoom (1, 2, 3) |
| F2 | affiche les piles (sur la liaison série) |
| F3 | enregistre une capture dans `varvara.bmp` |
| F4 | recharge la ROM (remise à zéro) |
| F12 | quitte, retour à la console Forth |

La souris est celle de la fenêtre QEMU (cliquer d'abord dans la fenêtre).

### Réglages

- Zoom : `3 to varvara-zoom` avant de lancer (par défaut 2).
- Arguments donnés à la ROM : `s" texte" varvara-arg` (autant que voulu,
  `varvara-args-clear` pour les effacer). Les touches reçues par la liaison série
  vont à l'entrée de la console de la ROM.
- Heure : la carte n'a pas de pile d'horloge. Elle démarre à la date de
  compilation du noyau (ou à l'heure de l'hôte sous QEMU `virt`), en UTC.
  `also rpi 1800000000 unix-time! previous` règle l'heure (secondes Unix),
  `60 tz!` donne UTC+1 (minutes de décalage).
- Fichiers : la racine de la carte est le répertoire de travail ; un nom contenant
  `..` est refusé ; un nouveau fichier doit avoir un nom 8.3.
- Son : QEMU `virt` (virtio-sound) joue le son. Sur un Pi 3 il passe par la prise
  jack (PWM), écrit sans carte et **non testé**. QEMU raspi3b est muet.

### Lenteur

Sous QEMU, un Pi 3 est émulé en logiciel : redessiner un grand écran (par exemple
l'éditeur writhdeck-uxn en 960×576) prend environ une demi-seconde par touche. Un
vrai Pi devrait être nettement plus rapide.

## 7. Ce qui reste à faire

- Un éditeur de texte (`ed`, `writhdeck`) sur la carte.
- Tester sur un vrai Pi 3 (écran HDMI, clavier USB, jack, carte SD) ; le Pi 4 est
  écrit sans carte pour l'essai.
- Varvara : déposer un fichier sur la fenêtre (glisser-déposer) n'existe pas.
