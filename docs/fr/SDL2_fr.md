# SDL2 pour la version POSIX

*Traduction française de [docs/en/SDL2.md](../en/SDL2.md) (la version
anglaise fait foi).*

Fenêtres, dessin, clavier, souris, images, texte et son pour les jeux,
grâce aux bibliothèques partagées de SDL2. Rien n'est lié dans
`lugforth`: les bibliothèques sont ouvertes à l'exécution (`dlopen`) la
première fois qu'on s'en sert.

```forth
sdl2                          \ charge, sdl2 devient votre vocabulaire
3 to zoom  320 240 screen     \ 320x240 pixels logiques, montrés 3 fois plus gros
: frame  events  black color cls  red color 10 10 50 50 box  flip ;
: run  begin frame quit? until close-screen ;
run
```

Exemples: `sdl2_demo.fs` (visite des mots), `sdl2_breakout.fs` (un jeu,
environ 200 lignes), `sdl2_drums.fs` (boîte à rythmes de 16 pas),
`fm/sdl2_fm.fs` (synthétiseur FM à deux opérateurs, entrée MIDI, gardé
hors de l'interpréteur: voir `fm/README.md`), `sdl2_pianoroll.fs` (piano
roll avec lecture par soundfont et export en `.mid` et ABC, POSIX
seulement), `sdl2_roguelike.fs` (planche de tuiles, avec musique),
`sdl2_3d.fs` (cube, pyramide et octaèdre en fil de fer qui tournent),
`sdl2_2048.fs` (2048 avec une animation de glissement, `--no-anim` la
coupe; chiffres dessinés à partir d'un bitmap 3x5), `pinball/pinball.fs`
(un flipper dans le style de Night Mission, physique en virgule fixe).
Le casse-briques, les percussions, le synthétiseur FM et le roguelike
tournent aussi dans le navigateur.

Tous les mots: [SDL2-words.md](SDL2-words_fr.md). Les mots courts
existent aussi dans la version web, voir [SDL2-web.md](SDL2-web_fr.md).

## Paquets

| Chargeur      | Bibliothèque nécessaire | Paquet Debian/Ubuntu   | Ajoute                      |
|---------------|----------------------|------------------------|-----------------------------|
| `sdl2`        | libSDL2-2.0.so.0     | `libsdl2-2.0-0`        | fenêtre, dessin, entrées, bips |
| `sdl2-image`  | libSDL2_image-2.0.so.0 | `libsdl2-image-2.0-0` | sprites (PNG, JPG, BMP...)  |
| `sdl2-ttf`    | libSDL2_ttf-2.0.so.0 | `libsdl2-ttf-2.0-0`    | texte avec polices TrueType |
| `sdl2-mixer`  | libSDL2_mixer-2.0.so.0 | `libsdl2-mixer-2.0-0` | fichiers son, musique, mixage, soundfont |
| `sdl2-abc`    | (`sdl2-mixer`, et une soundfont pour le MIDI) | `fluid-soundfont-gm` ou votre propre `.sf2` | airs en notation ABC |

Retaper un chargeur remet `sdl2` au sommet. Tous les mots ci-dessous
vivent dans le vocabulaire `sdl2`, avec les noms C bruts (`SDL_Init`,
...) pour tout ce qui n'a pas de mot court. `sdl2` est aussi l'endroit
où vont vos définitions.

## Mots courts (`sdl2`)

Écran: `screen ( w h -- )`, `close-screen`, `zoom` (valeur, à fixer
avant `screen`), `title ( a n -- )`, `fullscreen ( f -- )`,
`cursor ( f -- )`, `width`, `height`.

Sur une console texte Linux (ni X ni Wayland), SDL2 dessine avec son
pilote KMSDRM; les fenêtres, le clavier et la souris marchent, et `flip`
dessine une flèche pour la souris. Il faut être dans le groupe `input`,
et `SDL_VIDEODRIVER` est choisi par SDL (ou mettre
`SDL_VIDEODRIVER=kmsdrm`). Détails et pièges: `docs/en/dev/PORTS.md`.

Couleurs: `color ( $rrggbb -- )`, `rgb ( r g b -- )`,
`rgba ( r g b a -- )`, `alpha ( a -- )`, et les couleurs nommées
`black white red green blue yellow cyan magenta orange purple pink brown gray`
(`red color`), et `ink ( n -- )`, une couleur par son numéro de 0 à 15
(0 noir, 1 rouge, 2 vert, 3 jaune, 4 bleu, 5 magenta, 6 cyan, 7
blanc...).

Dessin: `cls`, `dot ( x y -- )`, `line ( x1 y1 x2 y2 -- )`,
`box ( x y w h -- )`, `frame ( x y w h -- )`, `hline ( x y w -- )`,
`disc ( cx cy r -- )`, `circle ( cx cy r -- )`, `flip`,
`pixel@ ( x y -- $rrggbb )`.

Boucle d'image: `events` (à appeler une fois par image), `quit?`,
`quit!`, `wait`, `ticks ( -- ms )`, `delay ( ms -- )`, `dt` (ms prises
par la dernière image).

Entrées: `pressed? ( key -- f )` avec `[char] a`,
`key-left key-right key-up key-down key-space key-enter key-esc key-tab key-backspace`,
`LEFT-BUTTON MIDDLE-BUTTON RIGHT-BUTTON`, `mouse-x`, `mouse-y`,
`last-key`, `wheel`. Mêmes noms et même sens que l'interface graphique
générique. `scan? ( scancode -- f )` dit si une touche est tenue d'après
sa place sur le clavier (code de balayage SDL: 4 est la touche A d'un
QWERTY, 20 est Q...), quelle que soit la disposition: servez-vous-en
pour un piano ou une rangée de touches de type WASD qui ne doit pas
bouger sur un clavier AZERTY.

Bips (aucune bibliothèque au-delà de libSDL2): `beep ( freq ms -- )` met
une tonalité en file, donc plusieurs `beep` de suite jouent une mélodie.
`wave` fixe la forme (0 carré, 1 triangle, 2 dent de scie, 3 bruit, 4
sinus), comme `color` fixe la couleur: `1 wave`, ou les mots
`square triangle saw noise sine`. `beep-volume ( n -- )` et
`pitch-drop ( n -- )` fonctionnent de la même façon; relisez un réglage
avec `wave@`, `beep-volume@`, `pitch-drop@`. `to wave` marche toujours.
Aussi `silence`, `beeping?`.

`play ( a n -- )` met en file des notes écrites en MML (Music Macro
Language, le `PLAY` des BASIC du MSX, de GW-BASIC et de QBasic):
`s" t140 o4 l8 c d e c  e4 g4 r4 >c2" play`.

| Commande | Signification |
|---|---|
| `c d e f g a b` | une note; `#` ou `+` après elle la diésie, `-` la bémolise |
| un nombre après une note | sa durée: 4 noire, 8 croche, 1 ronde; un point ajoute la moitié (`c4.`) |
| `r` ou `p` | un silence (même syntaxe de durée) |
| `o4` | l'octave, 0 à 8; `o4 c` est le do du milieu (262 Hz) |
| `<` `>` | une octave plus bas, plus haut |
| `l8` | la durée par défaut |
| `t120` | le tempo, en noires par minute |
| `n49` | une note par son numéro: n1 est le do le plus grave (octave 0), n49 est le do du milieu; n0 est un silence |
| `&` | lie une note à la suivante de même hauteur: `c4&c8` |
| `v8` | le volume, 0 à 15 (il fixe `beep-volume`) |
| `w1` | l'onde, comme `1 to wave` (ne fait pas partie du MML habituel) |

Les espaces sont libres; les réglages restent d'un `play` au suivant.
Différences avec les dialectes: la numérotation des octaves est celle du
MSX (`o4 c` est le do du milieu; vérifiez un air écrit pour un autre
dialecte, ils diffèrent); pas d'accords, pas de modes `m` (legato,
staccato) et pas de `x` (jouer une variable chaîne). Cela marche à
l'invite après `sdl2` (la fenêtre console, les cartes, la page web).

Plusieurs voix: une virgule dans la chaîne commence une autre voix, et
les voix démarrent ensemble (`s" o4 e d c d e e e2, o3 c1 g1" play`). 4
voix au plus, 512 notes chacune; chaque voix commence avec l'octave, la
durée, le tempo, l'onde et le volume avec lesquels le `play` a démarré.
Sous Linux les voix sont mélangées dans un seul tampon et mises en file
entières (donc un air très long demande un instant de préparation); sur
les cartes chaque voix a sa propre file; sur la page web les notes de
chaque voix sont programmées à partir du même instant.

Enveloppe: `adsr ( attack decay sustain release -- )` (ms, ms,
pourcentage, ms) façonne chaque `beep` et chaque note de `play`; le
relâchement est la fin de la note, il ne s'y ajoute pas. `adsr@` lit les
valeurs, `adsr-off` supprime l'enveloppe (le défaut). Les coups et les
airs ABC ne sont pas touchés.

Coups: `drum-hit ( freq ms -- )` démarre aussitôt et joue par-dessus les
autres sons (8 à la fois), en s'éteignant; `pitch-drop` (pourcentage)
fait tomber la hauteur, assez pour une grosse caisse. Appelez `flip` ou
`delay` régulièrement, comme le fait une boucle de jeu. Utilisez `beep`
ou `drum-hit` dans un programme, pas les deux.

## Images (`sdl2-image`)

`load-image ( a n -- img )`, `free-image`, `image-size ( img -- w h )`,
`draw ( img x y -- )`, `draw-size ( img x y w h -- )`,
`draw-part ( img sx sy sw sh x y w h -- )` pour les planches de sprites,
`image-alpha`, `image-tint`. Chargez les images après `screen`. Un nom
avec des dossiers qui n'est pas trouvé est réessayé sans eux, donc
`s" examples/tiles.png" load-image` marche aussi depuis `examples/`.

## Texte (`sdl2-ttf`)

`load-font ( a n pt -- font )`, `font ( font -- )`, `free-font`,
`text ( x y a n -- )` dans la couleur courante,
`text-size ( a n -- w h )`.

## Son (`sdl2-mixer`)

`load-sound ( a n -- snd )`, `play ( snd -- )`, `free-sound`,
`sound-volume`, `stop-sounds`, `sounds-playing`,
`load-music ( a n -- mus )`, `music ( mus -- )` (en boucle),
`music-once`, `stop-music`, `music-volume`, `music?`, `free-music`. Les
sons qui se chevauchent sont mélangés.

Fichiers de musique: OGG, MP3, FLAC, Opus, WAV, MOD et MIDI, dans la
mesure où votre SDL2_mixer a les décodeurs. Le MIDI demande une
soundfont: `soundfont ( a n -- )` fixe un `.sf2` ou `.sf3` (`soundfont?`
renvoie un indicateur au lieu de lever une exception). Le nom est essayé
tel quel, puis dans le dossier courant, puis dans le dossier du script,
donc `s" mygame.sf2" soundfont` trouve un fichier à côté du programme.
Sans soundfont, FluidSynth utilise celle du système s'il y en a une.

## Airs (`sdl2-abc`)

Les airs écrits en [notation ABC](https://abcnotation.com/), un format
texte pour les mélodies, peuvent être joués avec la soundfont. Tapez
`sdl2-abc` une fois (il charge `sdl2-mixer` si besoin). Comme l'ABC
emploie la barre verticale, écrivez l'air avec `r~ ... ~`, une chaîne
multiligne qui se termine à un tilde:

```forth
sdl2-abc
s" gm.sf2" soundfont?  drop        \ facultatif, une soundfont à côté du programme
: tune r~ X:1
T:Scale
M:4/4
L:1/4
Q:1/4=120
K:C
%%MIDI program 0
C D E F | [CEG]2 z2 |
~ ;
tune abc-play                      \ une fois; abc-loop répète; tune-stop arrête
```

`abc ( a n -- )` lit l'air; `tune-play`, `tune-loop`, `tune-stop` et
`tune-playing?` le jouent; `abc-play` et `abc-loop` font les deux. Après
`abc` on peut changer `tune-bpm` et `tune-program` (instrument General
MIDI), lire `tune-ms` et `tune-notes`, ou écrire un fichier MIDI avec
`tune-save`.

Compris: `K:` `L:` `M:` `Q:` et `%%MIDI program n`; notes, altérations,
marques d'octave, durées, silences, accords, liaisons, rythme brisé,
nolets, barres de mesure. Ignorés: symboles d'accords, décorations,
appoggiatures, commentaires, paroles. Non gérés: reprises, plusieurs
voix, changements de tempo. 1024 notes au plus. La version web a les
mêmes mots avec un synthétiseur simple.

## Fenêtre console (`sdl2_console.fs`)

Dans la fenêtre console, `graphic` fait marcher les mots de dessin à
l'invite (`0 0 100 100 line`): une texture de 640x480 les reçoit,
dessinée sous le texte, centrée, en proportion; `cls` l'efface, `flip`
redessine la fenêtre. `w h graphic-size` fixe la taille de la texture
(16 à 4096), gardée en proportion dans la fenêtre. Un programme qui
appelle `screen` y met fin. Le crayon est sauvegardé et restauré autour
de chaque redessin.

`lugforth --gui [fichier.fs]` ouvre une fenêtre console (`type` et `key`
vont vers une fenêtre SDL). Le code fait partie du binaire: `gui-load`
le charge, puis `desktop console` le démarre (Android utilise
`gui-load smooth-font console`). Sans SDL2 ou sans écran, `--gui` le dit
et garde le terminal.

Le menu est caché: le bouton en haut à droite, ou F10, le montre (un
clic droit sur le menu ouvert le ferme; un clic droit sur le bouton
cache le bouton, et à sa place le montre de nouveau) (Examples,
seulement quand le dossier contient des fichiers `.fs`, View, Colors,
Font, Help). Examples liste les fichiers `.fs` de `LUGFORTH_EXAMPLES`
(par défaut: le dossier courant). Colors a un sélecteur de couleur pour
le texte et pour le fond (un carré de saturation et de luminosité, une
bande de teintes, la valeur hexadécimale) et quelques thèmes.
Alt+Entrée, ou View, bascule le plein écran. Font liste les polices
TrueType et OpenType à chasse fixe trouvées dans `/usr/share/fonts`,
`~/.local/share/fonts` et `~/.fonts` (la molette fait défiler un long
menu). View a aussi More margin et Less margin (`margin=` dans le
`.ini`, en pixels). Aussi: sélection à la souris (copiée), clic du
milieu ou Ctrl+V pour coller, molette ou Page préc./Page suiv. pour
remonter, Ctrl+molette, Ctrl +/-, Ctrl 0 pour la taille du texte. ASCII
seulement.

Les réglages sont gardés dans `lugforth.ini` dans
`~/.local/share/lugforth` (ou `LUGFORTH_HOME`):

```
size=15
fg=D2D2D2
bg=000000
font=/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf
margin=12
fullscreen=Alt+Enter
```

`fullscreen=` est la touche du plein écran: `Alt+Enter`, `F11`,
`Ctrl+Shift+F`...

`font=` l'emporte sur `LUGFORTH_FONT`, puis DejaVu Sans Mono et d'autres
polices courantes du système. `ports/posix/sdl2_console_tests.fs` teste
les réglages (pilotes factices, comme les autres tests SDL).

## Limites

* Les appels ne passent que des entiers et des pointeurs: pas de
  flottants ni de structures par valeur (`SDL_RenderCopyEx`,
  rotation...).
* Pas de rappels de C vers Forth: l'audio utilise des files (`beep`,
  `drum-hit`) ou le mixer.
* Les positions de la souris sont en pixels logiques, quel que soit le
  `zoom`.
* `pressed?` lit l'état tenu une fois par `events`; un appui plus court
  qu'une image est manqué.
* Les noms ignorent la casse, donc la fonction `SDL_Quit` est liée sous
  le nom `sdl-shutdown`; `SDL_QUIT` est la constante d'événement.

## Tests

Ils demandent les bibliothèques, donc `ninja posix` ne les lance pas:

```sh
SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy out/posix/lugforth ports/posix/sdl2_tests.fs
```

Pareil pour `sdl2_media_tests.fs` et `sdl2_abc_tests.fs`.
