# Musique ABC dans Lugforth: lecteurs, générateur aléatoire


*Note de travail, en français seulement. Elle décrit comment le texte
ABC est lu et joué sur chaque plateforme, et comment
`examples/music/abc_random.fs` fabrique ses morceaux (graine comprise).*

## Les pièces

```
texte ABC --> common/abc.fs (le lecteur, une chaîne Forth)
                 |  événements: clé MIDI, début, durée, voix
                 v
   PC        ports/posix/sdl2_abc.fs   fichier MIDI + SDL2_mixer
   web       ports/web/sdl2_abc.fs     oscillateurs WebAudio
   ESP32     ports/esp32/abc.fs        mélangeur logiciel + PWM
   Pico      ports/pico/abc.fs         mélangeur logiciel + PWM
```

Le lecteur ne comprend que le nécessaire: les champs `K:` `L:` `M:`
`Q:`, les notes avec altérations et marques d'octave, les durées, les
silences, les accords `[CEG]`, les liaisons, les rythmes pointés, les
triolets et, si `voices?` vaut -1, les voix `V:1` `V:2`... qui ont
chacune leur temps. Le reste (paroles, décorations, reprises) est sauté.
Les événements ne sont pas triés par temps quand il y a plusieurs voix:
chaque lecteur de carte ou de page suit les temps de début et de durée.

## abc_random.fs: la graine

Le hasard vient de `random` (`common/ansi_compat.fs`), un générateur
congruentiel linéaire: `graine = graine * 1103515245 + 12345`, puis on
prend les bits 16 à 30 et on fait `mod n`. La graine est la variable
`rand-seed`. Les bits de poids faible suivent la même suite avec des
cellules de 32 et de 64 bits, donc un même numéro donne le même morceau
sur le PC, le Pico, l'ESP32 et le web.

`new-tune` commence par garder les 31 bits de poids faible de
`rand-seed` (`$7fffffff and`): c'est le numéro du morceau, écrit dans
`T:Random <numéro>` et affiché par `random-music` sous la forme `tune
<numéro>`. Tous les tirages qui suivent partent de ce numéro, donc le
même numéro redonne exactement le même texte.

| commande | effet |
|---|---|
| `random-music` | ajoute `ms-ticks` à la graine: un morceau différent à chaque lancement, puis boucle |
| `N music-seed` | fixe la graine à N et joue ce morceau, puis d'autres après un appui sur le bouton |
| `N rand-seed ! new-tune type` | affiche le texte ABC du morceau N (à copier) |
| `N rand-seed ! save-tune t1.abc` | l'écrit dans un fichier (pas sur le web) |
| `N rand-seed ! new-tune abc-play` | le joue une fois |

Piège rencontré: fixer la graine puis appeler `random-music` ne rejoue
pas le morceau, parce que `random-music` mélange l'horloge à la graine.
Il faut `music-seed`.

## abc_random.fs: la construction d'un morceau

**Choix globaux**, tirés une fois au début de `new-tune`: la tonalité
(10 possibles: C, G, D, F, Bb, A, puis Am, Em, Dm, Gm en mineur
naturel), le tempo (80 à 132), deux progressions d'accords `p1` et `p2`
parmi 5, et un mode d'accompagnement parmi 3.

**Forme**: 12 mesures à 4/4, trois phrases de 4. Les phrases 0 et 2
suivent `p1`, la phrase 1 suit `p2`. La mesure 10 est forcée sur V et la
mesure 11 sur I (la cadence); cette dernière mesure est une note entière
à la mélodie et un accord plaqué à l'accompagnement.

**Notes**: ce sont des degrés de la gamme de do (0 = do, 7 = do de
l'octave au-dessus, -7 = do en dessous). La ligne `K:` fait mettre les
dièses et les bémols, donc le code n'écrit jamais d'altération et tout
reste dans la tonalité. `t+note` traduit un degré en lettre ABC avec ses
virgules ou ses apostrophes d'octave.

**Accords**: les progressions sont des tables de 4 degrés (par exemple 0
4 5 3 = I V vi IV). La fondamentale est ramenée dans l'octave de la
mélodie (`mel-root`, degrés 3 à 9) ou dans celle de la basse
(`bass-root`, degrés -6 à 0); la tierce et la quinte sont deux et quatre
degrés plus haut.

**Voix 1, la mélodie**: un arpège de l'accord de la mesure. L'accord est
le tableau fondamentale, tierce, quinte, octave; un indice monte et
descend dedans (`rt-next`): le sens s'inverse une fois sur 6, il y a un
saut de deux une fois sur 8, et les bords font rebondir. Chaque temps
est une noire (3 chances sur 10) ou deux croches; dans deux croches, la
seconde est un silence (1 sur 14) ou une note voisine plus basse (1 sur
7). La mesure commence sur la fondamentale, ou, une fois sur 4, sur la
quinte.

**Voix 2, l'accompagnement**: chaque mesure tire un style. 0 = un accord
entier, 1 = deux accords d'une demi-mesure, 2 = une basse fondamentale,
quinte, tierce, quinte en noires, 3 = un contrepoint de 4 noires pris
dans l'accord, qui va à l'inverse de la mélodie: pour chaque temps, on
regarde si le premier degré de la mélodie a monté ou descendu par
rapport au temps d'avant, et on cherche le degré de l'accord le plus
proche de la direction opposée (en évitant de répéter la note
précédente). Le mode du morceau limite les styles: 0 = accords seulement
(0 ou 1), 1 = basse ou contrepoint (2 ou 3), 2 = les quatre.

**Assemblage**: le texte de chaque voix va dans un tampon à part (1600
et 1000 octets). À la fin on écrit l'en-tête (`X:1`, `T:`, `M:`,
`L:1/8`, `Q:`, `K:`), la voix 1, puis `V:2` et la voix 2 collée à la
suite. Le lecteur ABC lit ensuite le résultat comme n'importe quel
morceau.

## Lecture en boucle et bouton

`music-loop` génère un morceau, l'annonce (`tune <numéro>`) et le joue
en boucle jusqu'à une demande. `rm-halt?` regarde le bouton
(`music-button`, un GPIO avec rappel au plus, appuyé à 0, sur le Pico et
l'ESP32) puis le clavier. Sur une carte, `abc-play` appelle ce mot par
le crochet `abc-halt-with` (`halt?` dans le mélangeur, toutes les 16
blocs, et dans l'arpège) et rend la main quand il rend vrai; une touche
termine alors `random-music`. Sur le PC et dans le navigateur, le
lecteur boucle seul (`abc-loop`) et une touche demande un autre morceau.
Un bouton laisse le tirage tomber sur le morceau suivant de la suite; un
`N music-seed` fixe le premier.

## Le rendu sonore sur le Pico

Le Pico n'a pas de convertisseur: le son sort d'un PWM de 8 bits
(compteur de 0 à 255, diviseur 1, environ 488 kHz à 125 MHz) dont le
niveau change à chaque échantillon. La fréquence du PWM est trop haute
pour le haut-parleur, qui n'entend que la moyenne: la somme de quatre
ondes carrées (créneaux de moitié pour la voix 1, d'un quart pour les
autres, le niveau moyen étant 128).

**Avant**: la boucle qui calcule un échantillon était en Forth
(`mix-sample`), et sa vitesse décidait de tout: `mix-rate!` mesurait
pendant 50 ms combien d'échantillons elle faisait et les hauteurs
étaient déduites de ce nombre. Elle était lente, donc grave en fréquence
d'échantillonnage (repliement des notes aiguës, grésillement), et ses
à-coups déplaçaient les notes.

**Maintenant** (`ports/pico/mixer.h`, 2026-11-02): les échantillons sont
faits par une interruption d'une alarme du minuteur, toutes les 32
microsecondes (31250 échantillons par seconde), quoi que fasse le Forth.
Une première version en C faisait des blocs de 512 échantillons, mais
entre deux blocs le Forth devait préparer le suivant et la sortie
restait figée: une coupure d'environ 1 ms, 60 fois par seconde, qui
s'entendait comme un trémolo. Avec l'interruption il n'y a plus de
coupure. Mots du vocabulaire `pico`: `mix_start ( pin -- )` (remet les
phases à zéro, règle la broche et lance l'interruption, de priorité plus
haute que l'USB), `mix_slot ( i inc amp thr -- )` (règle une onde: la
phase de 24 bits monte de `inc` à chaque échantillon, l'onde est haute
(`amp`) tant que la phase est sous `thr`, basse (`-amp`) après) et
`mix_stop ( -- )`. Si le Forth cesse d'appeler `mix_slot` (une erreur au
milieu d'un morceau), l'interruption s'arrête seule après 4000
échantillons (128 ms) et met la sortie à zéro, pour ne pas laisser une
note tenue. `mix_block ( n -- )`, qui fait `n` échantillons dans le
programme sans interruption, reste pour les tests. La boucle Forth
(`mix-play`) règle les ondes toutes les 4 ms, regarde `halt?` toutes les
8 tours (environ 32 ms) pour que le bouton réponde vite, et date les
notes avec `ms-ticks`. Les hauteurs sont exactes, sans calibrage (`rate`
vaut 31250). L'interruption est dans la RAM (`__not_in_flash_func`);
pendant l'écriture d'un fichier en flash les interruptions sont coupées
et le son s'arrête un instant.

**L'enveloppe**: chaque note s'éteint doucement pendant qu'elle sonne,
comme une corde pincée: son volume baisse de `env-depth/256` (90 sur
256, soit environ un tiers) entre son début et sa fin, toutes les 4 ms.
`hz+` garde pour cela le début et la fin de chaque note (`hst`, `hen`),
`slots-set` les range par onde (`sst`, `sen`) avec le volume de base
`amp0`, et `env-apply` calcule `amp` à chaque tour de 4 ms. Le dixième
final de silence entre deux notes répétées reste. `90 abc-decay` règle
la profondeur, `0 abc-decay` redonne le volume plat d'avant.

**Retour en arrière**: `0 to c-mix` (dans le vocabulaire caché `sdl2`,
`also internals also sdl2 also abc-int 0 to c-mix`) remet la boucle
Forth et ses 128 échantillons, au cas où le mélangeur en C ne plairait
pas.

**Vérifié**: dans `out/pico-sim` (où les niveaux vont dans un tampon
`mix_log` au lieu du PWM, sans attente), `mix_block` donne exactement
les mêmes 64 niveaux qu'une boucle Python qui refait le calcul;
l'enveloppe donne 100, 82, 64 au début, au milieu et à la fin d'une note
de volume 100. **Écouté sur une carte** (Maker Pi Pico V1, 2026-11-02):
le mélangeur en C par blocs et l'enveloppe sonnent mieux que la boucle
Forth; il restait un effet de trémolo, dû aux coupures entre deux blocs,
et le passage à l'interruption du minuteur l'a fait disparaître
(«beaucoup mieux»). Pas mesuré: la charge du processeur, ni le
comportement pendant l'écriture d'un fichier en flash.

## Tests

`examples/music/abc_random_tests.fs` (dans `make -C examples
test-ports`) lit 200 morceaux avec le lecteur de `common/abc.fs`: deux
voix de 12 mesures chacune (1 mesure = 4 x 480 ticks), plus de 100
notes, et la même graine donne le même texte. Le bouton a été essayé
dans `out/pico-sim` avec des bouchons gpio, pas sur une vraie carte; la
page web a été essayée dans Chromium sans écran (le morceau est annoncé,
une touche en donne un autre), le son WebAudio pas écouté.
