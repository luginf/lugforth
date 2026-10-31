# Ports et plateformes: état, limites, ce qui a été testé

*Traduction française de [docs/en/dev/PORTS.md](../../en/dev/PORTS.md)
(l'anglais fait foi).*

Android, Raspberry Pi, PC UEFI, la console web et la Game Boy Advance.
Fait partie de [README.md](README_fr.md).

## Android (ports/android, ne se compile pas ici)

`MenuBar.java` est la barre des deux consoles: Tab, flèches, Ctrl
(collant: la lettre suivante est un code de contrôle; envoie F9, que
`ports/posix/sdl2_console.fs` transforme en `ctrl-next`; la page web
fait de même par `lugkey('Ctrl')` dans `ports/web/platform.fs`), un
bouton « ⋮ » pour Examples/A-/A+/Help, et l'accueil. À vérifier une fois
compilé: le bouton Ctrl sur un vrai clavier logiciel (les lettres
peuvent arriver comme des événements de touche plutôt que comme saisie
de texte), et que rien n'indique que Ctrl est armé (un indicateur
aiderait).

## AArch64 en matériel nu (ports/rpi) et code partagé (ports/baremetal)

`ports/baremetal` contient ce que le Pi et le PC (ports/efi) partagent:
screen.h (framebuffer, mots `sdl2` en C), console.h, input.h, font8x8.h,
sdl2.fs, files.fs, allocation.fs, autoboot.fs; tools/baremetal_*.py
(images de disque, ressources, lanceur/test QEMU) servent aux deux. Les
fichiers propres au matériel restent dans ports/rpi (start.S, usb.h,
virtio.h, sound.h, storage.h, fat.h) et ports/efi.

`make -C ports/rpi test` construit
out/rpi/{virt/lugforth.elf,pi3/kernel8.img,pi4/kernel8.img} et démarre
les deux premiers sous QEMU (`virt`, `raspi3b`), en vérifiant les
réponses sur la ligne série (tools/baremetal_qemu_test.py). Demande
`gcc-aarch64-linux-gnu picolibc-aarch64-linux-gnu qemu-system-arm` (apt,
installable dans le conteneur du nuage). Pas de système, picolibc,
console PL011, table de pages MMU en identité (nécessaire: les accès non
alignés plantent sans elle), les fautes deviennent THROW. Pas essayé sur
un vrai Pi; Pi 4 écrit à l'aveugle (QEMU 8.2 n'a pas raspi4b). Voir
ports/rpi/README.md. Graphismes: mots courts `sdl2` sur le framebuffer
de la boîte aux lettres (ports/baremetal/sdl2.fs, screen.h), touches
venant de l'UART; `make -C ports/rpi breakout` joue
examples/games/gfx_breakout.fs dans une fenêtre QEMU raspi3b
(`BOARD=virt`: virt avec `-device ramfb`, configuré par fw_cfg),
`make test` vérifie les deux par copie d'écran. Texte (font8x8), images
(PNG intégrés par tools/baremetal_assets.py), clavier/souris
(virtio-input sur virt avec `-global virtio-mmio.force-legacy=false`,
USB HID DWC2 sur pi3; testé par input-send-event de QMP), son (mixer de
sound.h, virtio-sound sur virt vérifié depuis un fichier
`-audiodev wav`; PWM+DMA du Pi 3 écrit à l'aveugle), `paste` (un fichier
entier sur la ligne série, puis Ctrl-D: les chaînes multilignes
marchent). `make roguelike` lance examples/games/gfx_roguelike.fs. QEMU
lance d'un coup toute une chaîne DMA bcm2835: une chaîne qui boucle le
bloque (même par SIGTERM), donc n'en démarrez jamais une à l'aveugle.
Fichiers: storage.h (SDHCI/EMMC du Pi par scrutation, virtio-blk de
virt) + fat.h (FAT12/16/32, avec ou sans MBR, noms longs lus et écrits,
mkdir/rmdir/chdir, dates d'après l'horloge), les mots de fichiers
standard, donc include/ls/cat marchent; autoexec.fs s'exécute au
démarrage. tools/baremetal_mkdisk.py fabrique les images (demande
dosfstools mtools); make test les vérifie ensuite avec mtools et
fsck.fat.

## PC x86-64 en matériel nu, UEFI (ports/efi)

Décision (2026-10-18): pas de port x86 sans UEFI (démarrage propre,
framebuffer VBE/Multiboot2, PS/2, pilotes AHCI/NVMe et FAT dessus): trop
de travail pour une cible étroite. Si jamais voulu, Multiboot2 par GRUB
serait le raccourci. Le pointeur est gardé en vrais pixels d'écran
(`in_mouse_rx/ry`) et dessiné par `fb_cursor_flip` directement dans le
framebuffer après le `flip` zoomé, donc sous `3 to zoom` il avance d'un
pixel, pas de trois (`mouse-x` donne toujours des pixels logiques). Pas
encore vu sur une vraie souris. Limites connues: OVMF n'interroge jamais
les souris USB (pas de souris sous QEMU, seulement sur le matériel:
`n mouse-speed`, 20 par défaut = 0,20 pixel par pas); pas de relâchement
de touche venant d'UEFI, la répétition du micrologiciel commence 500 ms
après l'appui (le casse-briques saccade au clavier au début). La mémoire
vidéo est en écriture combinée: gardez le `sfence` dans `dcache_clean`
(screen.h). La console se synchronise au plus toutes les 100 ms pendant
la sortie (console.h), sinon à l'attente d'une touche: plus court montre
les redessins des éditeurs à moitié faits. `common/lineedit.fs`
(historique, Tab sur les mots et les fichiers) et `common/pager.fs`
(arrête la sortie à chaque écran, espace/Entrée/q, `pager-off`) sont
chargés par les deux démarrages en matériel nu et par les démarrages
posix. Le pagineur enveloppe `type`, `key`, `key?`, `prompt`; il n'agit
que tant que `'tib` est le tampon de la console (pas dans un fichier
inclus), sur un tty (posix) ou quand `con-rows` n'est pas 0; `q` est
`-28 throw`, silencieux dans `quit` (`ls` et `cat` ferment leur poignée
sur une exception). tools/pager_pty_test.py vérifie le côté posix. Les
opcodes d'écran, d'entrée, de son et de fichiers sont partagés dans
ports/baremetal/builtins.h (`BM_VOC` = rpi ou pc, `input_flush` défini
par chaque main.c). Duplication restante: la partie commune des deux
platform.fs (azerty, console-zoom, touches).

`make -C ports/efi test` construit out/efi/BOOTX64.EFI (gcc + gnu-efi)
et le démarre sous QEMU q35 + OVMF depuis une image de clé USB (MBR +
FAT32, tools/baremetal_mkdisk.py), 129 vérifications
(tools/efi_qemu_test.py, qui réutilise baremetal_qemu_test.py). Demande
`gnu-efi ovmf qemu-system-x86 dosfstools mtools`. Reste dans les
services de démarrage: écran GOP, clavier ConIn (pas de relâchements:
une touche est tenue 120 ms après son dernier appui; le test utilise
`-device usb-kbd`, PS/2 ne répète pas), le FAT de la clé par Simple File
System (ports/efi/files.h, même API que fat.h). Réutilise
ports/baremetal (screen.h, console.h, input.h, font8x8.h, sdl2.fs,
files.fs...), partagé avec ports/rpi; vocabulaire `pc` avec l'alias
`rpi`. La console et `fb_flip` (ports/baremetal) dessinent en RAM et
n'écrivent que les pixels changés, sans jamais lire la mémoire vidéo; la
souris est interrogée depuis les pointeurs du micrologiciel (intestable
sous OVMF, le pavé tactile peut ne pas être exposé). Vitesse sur du vrai
matériel non mesurée. efi_main est appelé en SysV par le crt0 de gnu-efi
(PAS EFIAPI: cela plantait); libc propre dans libc.h (mathématiques
x87), setjmp et souches de fautes dans start.S (copie corrigée de l'IDT
du micrologiciel). Son: pilote Intel HD Audio minimal (ports/efi/hda.h,
mixer partagé dans ports/baremetal/mixer.h avec rpi/sound.h), vérifié
par le test avec `-audiodev wav` et QEMU intel-hda + hda-duplex
(`make run` les a aussi, AUDIODEV=pa); `sound-off` / `sound` gardent le
choix dans lugforth.ini (ports/efi/sound_ini.fs); pas essayé sur un vrai
PC. Démarré depuis une vraie clé USB (l'invite, gfx_3d, roguelike
marchent); ports/efi/README.md donne les étapes (Secure Boot désactivé,
menu de démarrage ponctuel). Autres systèmes de fichiers: `load-driver`
(pc, files.h: LoadImage depuis un tampon, StartImage, connect_all) et
ports/efi/disk/drivers.fs (sur la clé, PAS exécuté au démarrage)
chargent les pilotes EfiFs depuis EFI/drivers/ (exfat_x64.efi,
ext2_x64.efi: lecture seule, GPL, pas dans le dépôt). EFIFS=dir make
test ajoute 7 vérifications (disques exFAT et ext4 faits avec mkfs.exfat
/ mkfs.ext4 -d; exfatprogs s'installe avec apt). `volumes` / `n mount`
(files.h, platform.fs) changent le volume FAT des mots de fichiers
(refusé tant qu'un fichier est ouvert, l'ancien volume vidé): testé avec
une deuxième clé et une clé branchée à chaud sous QEMU, pas sur le
matériel. Le test efi se compile ici avec gnu-efi et ovmf décompressés
d'un `apt download` (sans root):
`make EFIINC=<racine>/usr/include/efi EFILIB=<racine>/usr/lib OVMF=<racine>/usr/share/OVMF/OVMF_CODE_4M.fd test`.
Le test de la touche morte est instable.

## Terminaux (Alt+F1..F4) sur rpi et efi - fait le 2026-10-20

`ports/baremetal/term.h` (tôt: `term_cur`, `term_foc`, `TERM_FOCUSED()`)
et `term_run.h` (tard: l'ordonnanceur). Chaque terminal est un autre
`forth_init` sur son propre tas (32 Mo après le premier; tout l'état de
l'interpréteur est dans `g_sys` et ce tas) et a son propre état d'écran,
échangé par `term_save`/`term_load` (la liste `TERM_FIELDS`:
curseur/couleurs de la console/`con_shadow`, `fb_back`, `fb_w`...,
`images`). `forth_run` rend la main à la boucle principale à chaque
`pause`, où `term_next()` choisit le terminal suivant (coopératif: une
boucle qui ne fait jamais de pause bloque les autres). Partagés et
écrits seulement par le terminal qui a le focus: `con_front`,
`fb_front`, `fb_mem` (`con_sync`, `fb_flip`, `fb_hw_open` rendent la
main quand il n'a pas le focus), le clavier (`getchar_timeout_us`,
`console_extra_char`, `input_snapshot`...), la sortie série
(`RAW-TYPE`). Un changement de focus redessine tout l'écran
(`term_present`). Alt+Fn est attrapé dans `input_key` (input.h,
partagé); `term_poll` (dans `RAW-YIELD`) lit le clavier à chaque pause
pour qu'un programme qui ne lit jamais les touches puisse quand même
être quitté. Mots: `term`, `terminal`, `terminals`
(ports/baremetal/terms.fs, numérotés depuis 1). `bye` hors du premier
ferme le terminal (`term_bye`). `autoexec.fs` ne s'exécute que dans le
premier. Pièges: la macro d'opcode `X` entre en conflit avec une liste
X-macro (utilisez `F`); `also rpi` dans une définition en deux-points se
compile, il ne cherche pas (utilisez une aide définie après un
`also rpi` de niveau supérieur); un mot que l'interpréteur ne trouve pas
n'interrompt pas le reste de la ligne (`v @` avec un `v` inconnu plante
sur les déchets laissés). Testé: `terminals()` du test efi (10
vérifications) et à la main avec un jeu quitté puis retrouvé; rpi non
compilé (pas de chaîne d'outils aarch64). Pas fait: un indicateur à
l'écran du numéro du terminal, le partage de `cwd`, la fermeture du
premier tant que d'autres vivent, les terminaux F5+.

## Web (ports/web)

- Les cellules font 32 bits (les autres versions 64): un produit en
  virgule fixe 16.16 doit passer par `*/` (voir
  `examples/games/gfx_raycaster.fs`). Les pages sont copiées dans
  `out/web` par `ports/web/BUILD` et doivent être servies de là
  (`ninja web && cd out/web && python3 -m http.server`);
  `tools/make_pages.py` les copie dans `demo/` sur le site.
- Console web, 2026-10-22: `graphic` et les mots de dessin tapés à
  l'invite ouvrent une zone de dessin au-dessus du texte (`S.layer`,
  `S.need` dans `ports/web/sdl2.fs`; un canevas fixe et une cale); une
  touche pressée ou un toucher déverrouille le son; sur un téléphone un
  champ de texte caché prend les caractères (`ports/web/platform.fs`),
  l'invite est gardée au-dessus du clavier (`context.KeepInView`), et le
  curseur est redessiné après chaque code d'échappement. Vérifié dans
  Chromium sans écran par CDP, sur ordinateur et avec un téléphone
  émulé; pas avec un vrai clavier.
- Les pièges sont dans [PITFALLS.md](PITFALLS_fr.md) (le pointeur de
  pile `sp`, `[defined]` dans une définition, l'encodage, keypress).

## TIC-80 (ports/tic80) - commencé le 2026-10-27

Un module wasm32 pour la console fantaisie TIC-80. TIC-80 a retiré sa
liaison pForth de ses publications (nesbox/TIC-80#3133) parce que pForth
lit et écrit la mémoire de l'hôte; ici chaque adresse est un décalage
dans la mémoire linéaire du module, un accès au-delà provoque un piège,
et les seules sorties sont les imports du module `tic80` (`api`, `type`,
`ticks`, les fonctions mathématiques), dont l'hôte vérifie les adresses.
Pas de mots CALL, DLSYM, fichiers ni tâches.

- Compilation: `make -C ports/tic80` (clang avec la cible wasm32 et
  wasm-ld, sans sysroot: `include/` et `libc.h` sont les morceaux de
  bibliothèque C dont le noyau a besoin, avec un `malloc`
  premier-ajustement sur `memory.grow`). Donne
  `out/tic80/lugforth-tic80.wasm` (environ 75 Kio avec le texte de
  démarrage). `-mcpu=mvp`; la mémoire peut grandir jusqu'à 16 Mio
  (`--max-memory`), une limite dans le module. L'en-tête de démarrage
  est du C simple (`importation.py --header c`).
- TIC-80 n'interprète pas le module: `make -C ports/tic80 c` le traduit
  en C avec wasm2c (paquet wabt), et TIC-80 compile cela avec
  l'environnement d'exécution wasm2c en mode vérification des bornes
  (son `vendor/lugforth/`). D'abord essayé avec wasm3, l'interpréteur
  que TIC-80 a pour son langage wasm: un interpréteur qui exécute un
  interpréteur, 25 fois plus lent que Lugforth natif (1e6 DO LOOP: 1,3
  s; démarrage 90 ms). Avec wasm2c: 13,5 ms (Lua dans TIC-80: 10,5 ms),
  démarrage 11 ms.
- L'interpréteur interne aiguille sur l'opcode (`ports/tic80/interp.h`,
  comme la version MSVC): wasm n'a pas de computed goto. Une division
  par zéro lève -10 (`DIV0_CHECK`); un xt forgé ou un pointeur de pile
  placé n'importe où reste dans le module.
- Exports: `lf_init`, `lf_alloc`/`lf_free`, `lf_eval`, `lf_find`,
  `lf_call`, `lf_call1` (SCN, BDR, MENU prennent la rangée), `lf_depth`.
  Les commandes passent par `'host`, lu par `host-loop`
  (`ports/tic80/platform.fs`), qui rend la main à l'hôte après chacune;
  la pile est vidée avant chaque commande, donc un TIC qui laisse des
  cellules se voit dans `lf_depth`. Après un piège l'instance est à
  mi-chemin d'un mot: l'hôte la laisse tomber.
- `api.h` est la liste des 56 mots de l'API, avec les noms et effets de
  pile de la liaison pForth de TIC-80 (`src/api/forth.c` là-bas):
  EXITGAME et KEYPRESSED, pas EXIT et KEY. TIC-80 en garde une copie.
- Test: `make -C ports/tic80 test` (node: démarrage, commandes, effets
  de pile de l'API, pièges). Avec une cartouche en deuxième argument,
  `node ports/tic80/test_host.js out/tic80/lugforth-tic80.wasm cart.fth`
  la charge et appelle BOOT et TIC. Vérifié le 2026-10-27 avec
  `demos/forthdemo.fth`, `demos/bunny/forthmark.fth` et
  `tests/conformance/probes/forth.fth` de TIC-80 (334 appels d'API,
  aucune sonde n'a levé d'exception; dans TIC-80, aucune divergence avec
  Lua).
- Les cellules font 32 bits, comme sur le web.

## Game Boy Advance (ports/gba)

Un prototype avec un roguelike. Voir `ports/gba/README.md`; les pièges
de la chaîne de démarrage rencontrés sont dans
[PITFALLS.md](PITFALLS_fr.md).

Depuis 2026.10.30.7, une seconde cartouche, `gba_magnoquill_boot.fs`:
le nuage de points de `gfx_magnoquill.fs`, ses calculs en C en virgule
fixe (`GBA-MAGNO`), à environ 20 images par seconde pour 400 points sous
mgba (vérifié là, pas sur une console). Le script de liens a
maintenant une section IWRAM et aligne les images ROM que `crt0.s`
copie; le roguelike et la démo des tuiles démarrent toujours après cela
(captures sous mgba).

## Le disque en matériel nu et la clé USB

Les dossiers de `examples/` sont gardés (`include games/gfx_snake.fs`,
`system/varvara/varvara_rpi.fs`), avec `lib/` (éditeurs), `MANUAL.md` et
`WORDS.md` (docs/), `autoexec.fs`. Construit par les Makefile de
ports/rpi (`disk`) et ports/efi (`usb`, `stage`).

## SDL2 sur une console texte Linux (KMSDRM, sans X)

Signalé le 2026-10-29, testé par le mainteneur sur un tty (Ubuntu
24.04, SDL 2.30.0, pilote NVIDIA propriétaire 595): `out/posix/lugforth`
lancé depuis un tty, sans serveur X ni Wayland, ouvre ses fenêtres
`sdl2`. Le binaire est lié à la libSDL2 du système, qui a sous
Debian/Ubuntu le pilote vidéo KMSDRM (libdrm, libgbm): sans `DISPLAY`
ni `WAYLAND_DISPLAY`, SDL le choisit seul et dessine par DRM/KMS.
Clavier et souris viennent alors d'evdev: il faut être dans le groupe
`input` (ou root). Pour le forcer: `SDL_VIDEODRIVER=kmsdrm`.
`lugforth-static-sdl` ne peut pas: son SDL est compilé pour X11
seulement. Depuis 2026.10.29.5, `con-open` (`sdl2_console.fs`) ne
refuse plus d'ouvrir la fenêtre de la console quand `DISPLAY`,
`WAYLAND_DISPLAY` et `SDL_VIDEODRIVER` sont tous absents. Impossible à
tester depuis une session graphique (`kmsdrm not available` tant qu'un
autre client tient le maître DRM).

Ce qu'on a trouvé sur ce tty (2026-10-30):

- `gfx_magnoquill.fs`, `gfx_mouse_test.fs`, `gfx_demo.fs` et
  `desktop.fs` tournent, avec le son, sans `SDL_AUDIODRIVER` ni
  `AUDIODEV` (`alsa`, `pulseaudio` et `pipewire` marchent tous). Les
  premiers essais du démo et du bureau se sont figés; avec
  `SDL_AUDIODRIVER=dummy` non, et les essais suivants non plus: la
  cause n'a pas été trouvée. Ouvrir et jouer du son seul n'a jamais
  bloqué, quel que soit le pilote.
- SDL n'affiche **aucun curseur de souris** sous KMSDRM (les clics
  marchent). Depuis 2026.10.30.6, `flip` (`ports/posix/sdl2.fs`) dessine
  une petite flèche en `mouse-x mouse-y` quand `screen` a trouvé le
  pilote `KMSDRM` (`soft-cursor`; SDL écrit le nom en majuscules, la
  première version comparait avec `kmsdrm` et ne marchait jamais), sauf
  si le programme a demandé `0 cursor` (comme `gfx_mouse_test.fs`, qui
  dessine sa propre croix). Le bureau y montre aussi son image cinq
  fois par seconde. Pas dessinée par la console SDL (`sdl2_console.fs`)
  ni par `varvara.fs`, qui présentent l'image eux-mêmes.
- `Error including: ...gfx_demo.fs` après Échap est un `-9` intercepté
  (erreur de segmentation) dans `SDL_DestroyWindow`, et aussi dans
  `SDL_Quit` (qui détruit les fenêtres), une fois des images
  affichées (une fenêtre ouverte et refermée sans rien dessiner ne
  plante pas). Sans doute un défaut KMSDRM/GBM de SDL 2.30.0 avec le
  pilote NVIDIA (pas prouvé). Fermer l'audio d'abord, attendre 300 ms
  ou détruire la fenêtre avant le renderer n'a rien changé. Le message
  est sans gravité: le tty reste utilisable.
- **Ne pas sauter `SDL_DestroyWindow` ni `SDL_Quit` sous KMSDRM** pour
  éviter ce message (essayé en 2026.10.30.2, annulé): le démo s'est
  affiché, puis le tty était mort (plus de clavier; le mainteneur a dû
  tuer lightdm), très probablement parce que `SDL_Quit` rend la console
  et le clavier. Pareil après avoir tué un programme qui tient l'écran.
  Si le clavier est mort, depuis une autre console `sudo kbd_mode -u -C
  /dev/tty1` peut aider (non testé).
- Carte son: `AUDIODEV=plughw:0,0`, que `main.c` impose au binaire
  statique, serait faux sur ce PC (la carte 0 est le HDMI NVIDIA, sans
  périphérique 0; la carte son est la carte 1, `plughw:1,0`).
- `make -C examples smoke`: `gfx_magnoquill.fs` ne s'est pas arrêté
  sous `timeout 5` sur ce PC (pilotes factices, aussi avec l'ancien
  `sdl2.fs`), alors qu'il tourne bien à la main. Non compris.

## SDL2 sur Windows et macOS

Les liaisons de `ports/posix/sdl2*.fs` demandent leur bibliothèque par
un mot (`sdl2-lib`, `sdl2-ttf-lib`... dans `ports/posix/posix.fs`: noms
Linux `.so.0`, `.dylib` de macOS cherchés dans le système puis dans les
dossiers de Homebrew). Sous Windows `ports/windows/windows_sdl2.fs` fait
un petit vocabulaire `posix` (`shared-library` = `dll`, `sign-extend`,
`SDL2.dll`...) et `sdl2` / `sdl2-ttf` incluent `lib/sdl2.fs`,
`lib/sdl2_ttf.fs` (copies des fichiers posix, mises dans le zip par le
job `windows`: les sources sont trop grosses pour la chaîne MSVC du
démarrage). Ce job télécharge les DLL officielles de SDL2 et SDL2_ttf et
lance `ports/windows/sdl2_test.fs` (pilotes factices). Testé: Windows
(fenêtre, pixel, TTF) et macOS (`sdl2_tests.fs`, 38) en CI le
2026-10-23, version 2026.10.23.2. Non embarqués: SDL2_image, SDL2_mixer.
La fenêtre console (`--gui`, `gui`) est le `sdl2_console.fs` et le
`sdl2_console_ttf.fs` de posix, copiés dans `lib/`, avec plus de mots
dans la cale `posix` (`sysfunc` par msvcrt.dll, `dlsym`, `readdir`,
`.d_name`, `.d_type`, `isatty`, un `termios` avec `form-hook`) et `win?`
dans la console (OS=Windows_NT: pas de test DISPLAY,
`%APPDATA%\lugforth`, `C:/Windows/Fonts`). Le menu Examples parcourt
trois niveaux de dossiers sur toutes les plateformes. Dossiers Windows:
`ports/windows/windows_dirs.fs` (FindFirstFile); chemins: `/` devient
une barre oblique inverse dans `open-file`, `ends/` connaît les deux.
Pièges rencontrés en testant sous wine (`wine lugforth.exe t.fs` avec la
sortie écrite dans un fichier: la sortie de la console n'est pas sur
stdout): `[']` ne marche pas à l'invite (utilisez `'`), et (avant
2026.10.27.2) `<# #s #>` prenait une seule cellule.

## macOS (ports/posix)

Corrigé en 7.0.11.16 sans Mac sous la main, en lançant le job `macos` de
`.github/workflows/release.yml` (la branche `macos-fix` avait un job
`macos-test.yml` avec des étapes de traçage: `fs_usage`, `sample`, une
session pty). Le démarrage bouclait à cause d'Apple arm64:
- un appel par un pointeur `(...)` met chaque argument sur la pile, donc
  `common/calls.h` a, pour `__APPLE__` + `__aarch64__`, une conversion
  par nombre d'arguments; `open`, `fcntl` et `ioctl` sont variadiques,
  donc `DLSYM` (ports/posix/main.c) fournit pour eux des enveloppes à
  arguments fixes;
- arm64 ne piège pas une division entière par zéro: `DIV0_CHECK`
  (common/tier0_opcodes.h, défini pour aarch64 dans main.c) lève SIGFPE;
- les nombres Linux que Darwin change (options de `open`, `EAGAIN`,
  `MAP_ANONYMOUS`, `SIGBUS`, `FNDELAY`, la disposition et les options de
  `termios`, `TIOCGWINSZ`, les décalages de `dirent`) sont choisis avec
  `os ( linux darwin -- n )` (opcode `host-mac?`). Vérifié en CI sur
  macos-latest (arm64): tous les tests passent (229), et une session pty
  (invite, `1 2 + .`, complétion par Tab, `bye`). Non vérifié: la
  console SDL2, les éditeurs, les sockets (sockets.fs a les constantes
  Linux; `sockaddr` a un octet de longueur sous Darwin), x86_64 (seule
  la moitié arm64 du binaire universel a tourné), une vraie taille de
  terminal (`form` donnait 0 0 dans le pty de la CI). Le binaire n'est
  pas signé: `xattr -d com.apple.quarantine lugforth` ou clic droit,
  Ouvrir. Un commentaire `( ... )` ne doit contenir aucun `)` interne:
  il terminait un commentaire trop tôt et le démarrage plantait sans
  message.

## ed et writhdeck sous gforth

`gforth ports/gforth/writhdeck-gforth.fs` (puis `ed fichier`,
`writhdeck fichier` à l'invite; testé avec gforth 0.7.9). Les fichiers
de `ports/posix` ne sont pas changés: le chargeur découpe le texte Forth
entre `r|` et la barre fermante, l'écrit dans
`/tmp/lugforth-gforth-body.fs` et l'inclut. `ports/gforth/compat.fs`
fournit ce qui manque à gforth: les vocabulaires
`ansi internals posix termios`, `z"` et `z>s`, `bytes`, `sysfunc`
(associé à des mots `sf-*`: getenv, mkdir, chdir, getcwd, remove,
rename, time), `readdir` / `.d_name` / `.d_type` sur `read-dir`, les
mots ANSI de `common/ansi.fs`, `ms-ticks`, et `<#` prenant une cellule
comme dans ueForth (plus nécessaire depuis 2026.10.27.2: `<#` prend un
double, comme dans gforth). Le mode brut est `stty raw -echo`. Pièges:
gforth compile dans une liste de mots hors de `forth` au démarrage
(`forth-wordlist set-current` d'abord, sinon `only forth` perd les
vocabulaires); les `{ locales }` sont refusées hors d'une définition;
les chemins de `included` sont relatifs au fichier qui inclut.
`python3 tools/editors_pty_test.py ports/gforth/writhdeck-gforth.fs`
lance le même test pty que pour la version native (tous passent).

## Exécutable Windows compilé avec MinGW (sans Visual Studio)

`ninja windows-mingw` (demande `x86_64-w64-mingw32-gcc` et `windres`)
construit `out/windows-mingw/lugforth.exe` sous Linux à partir du même
`ports/windows/main.c`, puis lance `ports/windows/mingw_test.fs` sous
wine. Différences avec la version MSVC, toutes dans `main.c` /
`interp.h`: `*/` emploie `__int128` au lieu de `_mul128/_div128`; MinGW
n'a pas de `__try`, donc un gestionnaire d'exceptions vectorisé fait un
`longjmp` vers `forth_run` (la division par zéro donne -10, une adresse
invalide -9, comme avec MSVC); l'exe est un programme *console*
(`-DLUGFORTH_CONSOLE`, `main` appelle `WinMain`), donc stdout, les tubes
et la sortie sous wine marchent. La CI compile encore avec MSVC. Avec
cet exe, tout `ports/windows/sdl2_test.fs` de la CI passe sous wine
(SDL2.dll et SDL2_ttf.dll du paquet de la CI, un TTF quelconque copié
dans `drive_c/windows/Fonts/arial.ttf` et `consola.ttf`). L'exe MSVC
n'affiche rien sous wine (sous-système graphique).

## Exécutable statique, musl (ports/posix)

`ninja posix-static` -> `out/posix/lugforth-static` (environ 400 Ko,
aucune bibliothèque, tourne sur n'importe quel Linux quelle que soit sa
libc). Demande le paquet `musl-dev` (`x86_64-linux-musl-gcc`;
configure.py le dit quand il manque). Même `main.c` et même démarrage
que `lugforth`, compilé avec `-DLUGFORTH_STATIC`: le Forth du terminal,
`ed`, `writhdeck`, fichiers, sockets, `-e`.

- Un exécutable statique n'a pas de table de symboles dynamique, donc
  `dlsym(RTLD_DEFAULT, "open")` ne trouve rien et le premier `sysfunc`
  faisait boucler le démarrage. `main.c` contient une table (`lug_syms`)
  des fonctions C que les sources `.fs` cherchent par leur nom,
  consultée avant `dlsym`. **Un nouveau `z" nom" n sysfunc` demande son
  nom dans cette table** (les tests de `posix-static` échouent alors au
  chargement, erreur -38).
- `lugforth-static` n'a ni SDL2, ni son, ni fenêtre: ceux-ci font un
  `dlopen` de bibliothèques partagées, ce qu'un exécutable musl statique
  ne peut pas faire. Pour eux voir `lugforth-static-sdl` plus bas.
- La table de noms est générée, pas écrite à la main:
  `tools/static_syms.py` parcourt les `.fs` de `common/` et
  `ports/posix/` à la recherche de `z" nom" n sysfunc|sdl|sofunc` et
  `... dlsym` et écrit `out/gen/static_syms*.h` (déclarations extern par
  étiquette asm, donc pas de conflit d'en-têtes). Une nouvelle liaison
  n'a besoin de rien d'autre que d'être écrite dans un `.fs`.
- Testé: `common/all_tests.fs`, les tests de l'éditeur et de writhdeck,
  `tools/editors_pty_test.py out/posix/lugforth-static`. Pas encore dans
  le workflow de publication (le conteneur Debian 10 n'a pas
  `x86_64-linux-musl-gcc`).

### lugforth-static-sdl (SDL2 dans l'exécutable statique)

`ninja posix-static-sdl` -> `out/posix/lugforth-static-sdl` (environ 5,2
Mo): pareil, avec SDL2 2.30.0, SDL2_image 2.8.2, SDL2_ttf 2.22.0,
SDL2_mixer 2.8.0, freetype 2.13.2, alsa-lib 1.2.11, libX11, libXext,
libxcb et libXau liés dedans. `--gui` (console SDL, police TrueType),
les jeux et exemples graphiques `sdl2` marchent sur n'importe quel Linux
avec un serveur X, quelle que soit sa libc.

- `tools/build_musl_sdl.sh` (lancé par ninja, idempotent) télécharge les
  sources (sha256 vérifié) et les compile contre musl dans
  `out/musl-deps/` (ignoré par git): demande `curl`, `cmake`, `make`,
  `pkg-config`, le réseau la première fois, quelques minutes. X11,
  freetype et alsa-lib sont construits avec autotools
  (`--enable-static`), les bibliothèques SDL avec cmake.
- SDL est construit pour X11 seulement (lié, sans `dlopen`), moteur de
  rendu logiciel (`sdl2.fs` retombe déjà d'ACCELERATED), pilotes vidéo
  dummy et offscreen (donc `SDL_VIDEODRIVER=dummy` et les tests de fumée
  marchent). SDL2_image décode avec stb_image (PNG, JPEG, BMP, GIF...;
  pas de SVG, TIFF, WEBP). SDL2_mixer: WAV, MP3, FLAC, OGG (stb_vorbis)
  et MIDI avec son timidity intégré (demande des patchs d'instruments
  sur l'hôte, sinon silencieux). freetype n'a ni harfbuzz, ni png, ni
  bzip2, ni brotli, ni zlib. **Laissés de côté:** manette, Wayland,
  OpenGL, Vulkan, extensions X11 (xcursor, xrandr, xinput...),
  PulseAudio, PipeWire.
- **Son (alsa-lib):** le coût est d'environ 1 Mo. alsa-lib est LGPL 2.1
  (voir NOTICE: qui distribue le binaire doit permettre le réédition de
  liens, les fichiers objets et le script de compilation suffisent). Une
  alsa-lib statique ne peut pas charger de greffons (`--with-libdl=no`).
  Sur un bureau PipeWire/Pulse (Ubuntu 24.04) le périphérique `default`
  est le greffon pipewire, une bibliothèque partagée: il échoue («
  Cannot open shared library libasound_module_pcm_pipewire.so ») et SDL
  ne trouve aucun périphérique audio. `main.c` fixe donc
  `AUDIODEV=plughw:0,0` (carte 0, avec conversion de format) sauf si la
  variable est déjà fixée; **testé par le mainteneur sur un bureau
  PipeWire: le son marche**. Une autre carte: `AUDIODEV=plughw:1,0 ...`.
  Les lignes ALSA sur le greffon manquant sont réduites au silence; le «
  Couldn't open audio device » de SDL reste. Si PipeWire tenait un jour
  la carte (« busy »), la sortie serait le pilote `disk` de SDL envoyé
  dans `pw-play`. Pas entendu par moi (la session du PC de test n'a pas
  accès à la carte son).
- Le test cmake de Xlib ne peut pas lier une libX11 statique, donc deux
  résultats sont donnés à la main (`HAVE_XGENERICEVENT`,
  `SDL_VIDEO_DRIVER_X11_HAS_XKBKEYCODETOKEYSYM`); sans eux la
  compilation s'arrête dans `SDL_x11events.c` (`XGenericEventCookie` en
  conflit). Le `sdl2.pc` d'une version statique liste les bibliothèques
  comme `-l/chemin/libX11.a`, ce qui ne se lie pas: la ligne d'édition
  de liens est écrite dans `configure.py`.
- `main.c` (`LUGFORTH_STATIC_SDL`): `dlopen("libSDL2...")` (SDL2, image,
  ttf, mixer) renvoie une poignée qui veut dire « cet exécutable »,
  toute autre bibliothèque renvoie 0; `dlsym` avec une telle poignée ne
  cherche que dans la table. `tools/static_syms.py` écrit dans la table
  les noms `SDL_`, `IMG_`, `TTF_`, `Mix_` des mots `sdl`, `sdlimg`,
  `sdlttf`, `sdlmix` (seulement pour cette version).
- Testé ici: `all_tests`, `sdl2_console_tests`, `editors_pty_test.py`,
  `--gui` sous Xvfb (accent tapé avec xdotool, dessiné avec la police
  TrueType), le raycaster, `gfx_roguelike.fs` (planche d'images
  chargée, mixer ouvert) sous Xvfb, un programme C lié de la même façon.
  Non testé: une machine sans bibliothèques X (elle ne devrait pas en
  avoir besoin), les bureaux Wayland seul (XWayland nécessaire), le son
  audible, d'autres claviers que celui de xdotool.
- Pas dans le workflow de publication (le conteneur Debian 10 n'a pas de
  musl-gcc): décision du mainteneur.
