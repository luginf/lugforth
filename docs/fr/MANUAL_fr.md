# Manuel de Lugforth

*Traduction française de [docs/en/MANUAL.md](../en/MANUAL.md) (la
version anglaise fait foi). La liste des mots,
[WORDS.md](../en/WORDS.md), reste en anglais: elle est générée à partir
des interpréteurs.*

## Objectif

Lugforth est un terrain de jeu pour apprendre et expérimenter Forth, et
pour y construire des applications: virtuelles, sur PC (Linux, Windows,
navigateur web) et sur Android, et réelles, sur des cartes comme le
Raspberry Pi Pico et l'ESP32 (et directement sur du matériel Raspberry
Pi et PC, sans système). Le même langage et les mêmes mots fonctionnent
partout: on essaie une idée à l'invite, on la garde dans un fichier, et
on l'emporte sur une autre cible.

Deux parties. **Apprendre** est un tutoriel: commencez au chapitre 1 et
tapez chaque exemple. **Référence** détaille Lugforth lui-même
(binaires, éditeurs, exemples, ports, compilation). Référence des mots:
[docs/en/WORDS.md](../en/WORDS.md). Pièges:
[docs/en/dev/PITFALLS.md](dev/PITFALLS_fr.md).

**Apprendre** [1 Premiers pas](#1-premiers-pas) - [2 La
pile](#2-la-pile) - [3 Nouveaux mots](#3-nouveaux-mots) - [4
Décisions](#4-décisions) - [5 Boucles](#5-boucles) - [6
Mémoire](#6-mémoire-variables-constantes-tableaux) - [7 Texte et
nombres](#7-texte-et-nombres) - [8 Fichiers et
éditeurs](#8-fichiers-et-éditeurs) - [9 Quand ça tourne
mal](#9-quand-ça-tourne-mal) - [10 Trois petits
programmes](#10-trois-petits-programmes) -
[Réponses](#réponses-aux-exercices)

**Référence** [Arborescence](#arborescence) - [Binaires](#binaires) -
[Fenêtre console](#fenêtre-console) - [Version mini](#version-mini) -
[Édition de ligne](#édition-de-ligne) - [Pagineur](#pagineur) -
[ed](#ed) - [writhdeck](#writhdeck) - [Blocs](#blocs) -
[Exemples](#exemples) -
[Varvara](#varvara-exécuter-des-roms-uxn) - [Matériel
nu](#matériel-nu) - [Compilation](#compilation)

# Apprendre

Conventions: ce que vous tapez suit l'invite `>`; la réponse de Lugforth
est sur la ligne suivante ou après votre saisie. La casse n'a pas
d'importance: `DUP`, `dup` et `Dup` sont le même mot. Les mots sont
séparés par des espaces, et presque tout ce qui n'est pas un espace peut
faire partie d'un nom de mot.

## 1. Premiers pas

Lancez `lugforth` (voir [Binaires](#binaires), ou compilez-le avec
`ninja posix`). Vous obtenez une bannière et l'invite:

```
Lugforth v2026.10.31.1
:; ===========>/>
type 'info' for help
 ok
>
```

Tapez une ligne et appuyez sur Entrée. Lugforth lit chaque mot,
l'exécute, et dit ` ok` quand la ligne est terminée.

```
> 2 3 + .
5  ok
```

Forth s'écrit en **notation postfixée**: les opérandes d'abord, puis
l'opérateur. `2 3 +` veut dire « pose 2, pose 3, additionne ». Le mot
`.` affiche le résultat. Sans lui, le résultat reste invisible:

```
> 2 3 +
 ok
> .
5  ok
```

Le 5 a attendu entre les deux lignes. Où? Sur la **pile**, sujet du
chapitre suivant. D'abord l'arithmétique:

```
> 10 3 - .
7  ok
> 7 6 * .
42  ok
> 17 5 / .
3  ok
> 17 5 mod .
2  ok
> -7 abs .
7  ok
> 3 9 max .
9  ok
```

Tous les nombres sont entiers (64 bits). `/` supprime la fraction en
arrondissant vers moins l'infini: `-7 2 /` donne `-4`.

Une ligne peut contenir beaucoup de mots: `1 2 + 3 * .` affiche 9,
c'est-à-dire `(1 + 2) * 3`. Il n'y a ni parenthèses ni priorités:
l'ordre des mots est l'ordre du travail.

`info` (ou `help`) montre la révision et la mémoire, et `bye` quitte.

## 2. La pile

Les nombres attendent sur la **pile**. Un nombre tapé est empilé; un mot
comme `+` dépile ce qu'il lui faut et empile son résultat. `.s` montre
la pile sans la modifier. Le nombre d'éléments est entre chevrons, le
sommet de la pile est à droite:

```
> 1 2 3 .s
<3> 1 2 3  ok
> + .s
<2> 1 5  ok
> . .
5 1  ok
```

Chaque `.` dépile le nombre du sommet et l'affiche, donc les nombres
sortent dans l'ordre inverse.

Les mots qui réarrangent la pile. Le commentaire entre parenthèses est
un **effet de pile**: ce qui est pris avant `--`, et ce qui reste après.
`( a b -- b a )` pour `swap` veut dire « a sous b devient b sous a ».

| Mot | Effet de pile | Action |
|---|---|---|
| `dup` | `( a -- a a )` | copie le sommet |
| `drop` | `( a -- )` | jette le sommet |
| `swap` | `( a b -- b a )` | échange les deux du dessus |
| `over` | `( a b -- a b a )` | copie le deuxième au sommet |
| `rot` | `( a b c -- b c a )` | amène le troisième au sommet |
| `nip` | `( a b -- b )` | jette le deuxième |
| `tuck` | `( a b -- b a b )` | copie le sommet sous le deuxième |
| `2dup` | `( a b -- a b a b )` | copie la paire du dessus |
| `2drop` | `( a b -- )` | jette la paire du dessus |

Essayez chacun avec `.s`:

```
> 1 2 swap .s
<2> 2 1  ok
> 2drop 5 dup * .
25  ok
```

`dup *` met un nombre au carré. Voilà toute l'idée de Forth: de petits
mots qui travaillent sur la pile, collés ensemble. Gardez un crayon sous
la main et notez la pile après chaque mot, jusqu'à ce que ça devienne
naturel.

Pour vider la pile, utilisez `drop` jusqu'à ce que `.s` montre `<0>`.
`depth` donne le nombre d'éléments.

**Exercices**

1. Qu'affiche `3 4 swap - .` ?
2. Que contient la pile après `1 2 3 rot` ?
3. Avec seulement les mots ci-dessus, calculez `(5 + 3) * (5 - 3)` en ne
   tapant 5 qu'une seule fois.

## 3. Nouveaux mots

Une **définition** commence par `:` (deux-points), le nom, le corps, et
se termine par `;` (point-virgule). À partir de là, le nouveau mot fait
partie du langage.

```
> : square  dup * ;
 ok
> 5 square .
25  ok
```

Écrivez toujours l'effet de pile juste après le nom; le compilateur
l'ignore, mais pas vous:

```
: square ( n -- n*n )  dup * ;
: cube ( n -- n*n*n )  dup square * ;
```

Un mot peut utiliser des mots définis avant lui. C'est la
**factorisation**: quand une phrase apparaît deux fois, donnez-lui un
nom. Les mots sont minuscules et testables un par un:

```
> 3 cube .
27  ok
: celsius>f ( c -- f )  9 * 5 / 32 + ;
> 100 celsius>f .
212  ok
```

Deux outils pour regarder autour de soi:

```
> see square
: square 
    DUP * 
; 
 ok
> words
```

`see` décompile un mot. `words` liste tous les mots du vocabulaire
courant, le plus récent d'abord (votre `square` vient en premier). `\`
commence un commentaire jusqu'à la fin de la ligne, et `( ... )` est un
commentaire à l'intérieur d'une ligne.

Taper une définition à l'invite est possible, mais rien n'est gardé
après `bye`. Le chapitre 8 montre comment garder vos mots dans un
fichier.

**Exercices**

1. Définissez `double-sum ( a b -- n )` qui donne le double de `a + b`.
2. Définissez `area ( w h -- a )` et servez-vous-en pour définir
   `volume ( w h d -- v )`.
3. Définissez `fahrenheit>c ( f -- c )`, l'inverse de `celsius>f`.

## 4. Décisions

Une comparaison laisse un **indicateur** (flag): `-1` pour vrai, `0`
pour faux.

```
> 3 4 < .
-1  ok
> 4 3 < .
0  ok
> 3 3 = .
-1  ok
> 5 0= .
0  ok
```

Les comparaisons sont `=`, `<>`, `<`, `>`, `<=`, `>=`, et `0=`, `0<`,
`0>` pour une comparaison avec zéro. `and`, `or`, `xor` et `invert`
combinent des indicateurs.

`if ... then` exécute son corps quand l'indicateur est vrai. `else`
ajoute l'autre cas:

```
: sign ( n -- )
  dup 0< if drop ." negative" else
  0= if ." zero" else ." positive" then then ;
> -5 sign
negative ok
> 0 sign
zero ok
> 8 sign
positive ok
```

`if` prend son indicateur sur la pile et le consomme, d'où le `dup`
avant le premier test et le `drop` dans la branche qui n'a pas besoin du
nombre.

Pour plusieurs valeurs d'un même nombre, `case`:

```
: kind ( n -- )
  case
    1 of ." one" endof
    2 of ." two" endof
    ." many"
  endcase ;
> 2 kind
two ok
```

> **Les mots de contrôle hors définition.** `if`, `do`, `begin`, `case`,
> `for` et leurs partenaires fonctionnent aussi tapés à l'invite, au
> niveau supérieur d'un fichier ou dans une chaîne passée à
> `evaluate`: `3 0 do i . loop` affiche `0 1 2`, `1 if ." yes" then` affiche `yes`. La structure est
> compilée dans un mot anonyme temporaire, exécutée dès qu'elle est
> fermée (`loop`, `then`, `until`...), puis oubliée. Elle peut s'étendre
> sur plusieurs lignes, et les éléments laissés sur la pile avant elle
> restent disponibles. Une structure encore ouverte quand une erreur
> survient est abandonnée; la suivante repart de zéro.
> (`common/interpret_control.fs`.)

**Exercices**

1. Définissez `abs2 ( n -- |n| )` avec `if` et `negate`.
2. Définissez `between? ( n lo hi -- flag )`, vrai quand
   `lo <= n <= hi`.

## 5. Boucles

`do ... loop` répète avec une limite et un départ:
`limite départ do ... loop`. À l'intérieur, `i` est le compteur.

```
: stars ( n -- )  0 ?do 42 emit loop ;
> 5 stars
***** ok
: table ( n -- )  11 1 do dup i * . loop drop ;
> 7 table
7 14 21 28 35 42 49 56 63 70  ok
```

`42 emit` affiche le caractère de code 42 (`*`). `?do` saute la boucle
quand la limite égale le départ; `do` la ferait tourner un tour complet
de la droite des nombres. Ne donnez pas non plus à `?do` un départ
supérieur à la limite.

* `+loop` ajoute un pas au lieu de 1: `: t 10 0 do i . 3 +loop ;`
  affiche `0 3 6 9`. Un pas négatif compte à rebours:
  `: t 0 5 do i . -1 +loop ;` affiche `5 4 3 2 1 0`.
* `leave` quitte la boucle aussitôt.
* `j` est le compteur de la boucle englobante dans des boucles
  imbriquées.

Quand le nombre de tours n'est pas connu d'avance, `begin ... until`
boucle jusqu'à ce que l'indicateur soit vrai, et
`begin ... while ... repeat` teste au début:

```
: countdown ( n -- )  begin dup . 1- dup 0= until drop ;
> 5 countdown
5 4 3 2 1  ok
: gcd ( a b -- g )  begin dup while tuck mod repeat drop ;
> 48 18 gcd .
6  ok
```

Un mot peut s'appeler lui-même avec `recurse`:

```
: fact ( n -- n! )  dup 1 > if dup 1- recurse * then ;
> 10 fact .
3628800  ok
```

**Deux mots nommés `do`.** `do`, `loop`, `if`, `begin` et les autres
mots de contrôle agissent à deux moments. Pendant la compilation d'une
définition, le **mot de compilation** `do` s'exécute aussitôt (il est
immédiat): il écrit dans la définition un appel au **mot d'exécution**
`DO` et retient où la boucle commence, pour `loop`. À chaque exécution
de la définition, le `DO` d'exécution met la limite et le compteur sur
la pile de retour. Seuls les mots d'exécution restent dans le code
compilé, et `see` les montre:

```
: compte  5 0 do i . loop ;
> see compte
: compte
    5 0 DO
        I .
    LOOP
;
```

Les deux ont le même nom (la casse est ignorée), comme dans uEforth: le
mot de compilation, défini en dernier, est celui qu'on trouve quand on
tape `do`, et le mot d'exécution est rangé dans le vocabulaire
`internalized`. Donc `see do` et `see DO` montrent le mot de
compilation; pour voir celui d'exécution, mettez `internalized` dans
l'ordre de recherche:

```
> see do
: do
    leaving( POSTPONE DO here
; IMMEDIATE
> internals internalized see DO forth
C code: DO runs the opcode (DO)
```

`DO ?DO LOOP +LOOP` sont écrits en C sur la plupart des versions; sur la
version web ce sont des mots Forth, et la même ligne montre leur
définition (`: DO SWAP R> -rot >R >R >R ;`). gforth fait pareil avec
d'autres noms: son mot d'exécution est `(do)`, et son `see` le réécrit
`DO`.

**Exercices**

1. Définissez `sum-to ( n -- sum )` qui donne `1 + 2 + ... + n`, avec
   une boucle.
2. Définissez `triangle ( n -- )` qui affiche un triangle de signes `#`
   de n lignes (`[char] #` donne le code de `#` dans une définition).
3. Affichez la table de multiplication de 1 à 9 sous forme de grille,
   avec deux boucles imbriquées.

## 6. Mémoire: variables, constantes, tableaux

Une **variable** est une cellule de mémoire nommée. Son nom empile son
*adresse*; `!` range et `@` lit.

```
> variable score
> 10 score !
> score @ .
10  ok
> 5 score +!
> score @ .
15  ok
```

`?` est le raccourci de `@ .`. Une **constante** est un nom pour une
valeur qui ne change pas:

```
> 42 constant answer
> answer .
42  ok
```

Une **valeur** (`value`) se lit comme une constante et se change avec
`to`; pratique pour un compteur qu'on lit souvent:

```
> 0 value lives
> 3 to lives
> lives .
3  ok
```

Un **tableau** est un bloc de mémoire créé avec `create`. Une cellule
fait 8 octets, donc utilisez `cells` pour indexer:

```
create primes  2 , 3 , 5 , 7 , 11 ,      \ , range un nombre et avance
> primes 3 cells + @ .
7  ok
create buf 10 allot                       \ 10 octets, remplis de ce qui s'y trouvait
```

Les octets se lisent avec `c@` et s'écrivent avec `c!`. La mémoire n'est
pas mise à zéro pour vous: utilisez `buf 10 erase` ou `buf 10 1 fill`.

Les **locales** donnent des noms aux éléments de pile à l'intérieur
d'une définition:

```
: hyp2 { a b -- r }  a a *  b b * + ;
> 3 4 hyp2 .
25  ok
```

Déclarez-les tout en haut du mot, jamais dans un `if` ou dans le corps
d'une boucle.

**Exercices**

1. Faites une variable `count`, un mot `tick` qui lui ajoute 1, et
   `.count` qui l'affiche.
2. Remplissez un tableau de 5 cellules avec les carrés `0 1 4 9 16` dans
   une boucle, puis affichez-les dans une autre.

## 7. Texte et nombres

```
> : hello ." Hello, world!" ;
> hello
Hello, world! ok
```

`."` affiche le texte jusqu'au guillemet fermant; à utiliser dans les
définitions. `s"` donne une chaîne sous forme d'adresse et de longueur,
à utiliser avec `type`:

```
> : greet ( a n -- )  ." Hello, " type ." !" ;
> s" Forth" greet
Hello, Forth! ok
```

Mots de sortie: `cr` (nouvelle ligne), `space`, `n spaces`, `n emit` (un
code de caractère), `type`, et `.r` pour aligner un nombre à droite dans
un champ: `5 3 .r 7 3 .r` affiche `  5  7`.

Les nombres utilisent la **base** courante. `hex` et `decimal` la
changent:

```
> 255 hex . decimal
FF  ok
```

Autres opérateurs: `1+ 1- 2* 2/ negate`, `and or xor`, `lshift rshift`,
`/mod` (reste et quotient), `*/` (multiplie, puis divise, sans perdre de
précision: `2 3 4 */` est `2 * 3 / 4`). `u.` affiche un nombre non
signé: `-1 u.` affiche `18446744073709551615`.

Les flottants existent aussi, avec leur propre pile et un `e` final sur
les littéraux:

```
> 1.5e 2.5e f+ f.
4.000000  ok
```

Nombres aléatoires: `n random` donne un nombre de 0 à n-1. Initialisez
d'abord la graine, sinon vous obtenez la même suite à chaque exécution:
`ms-ticks 1 or rand-seed !`.

## 8. Fichiers et éditeurs

Écrivez vos mots dans un fichier avec n'importe quel éditeur, ou avec
celui de Lugforth: `ed mygame.fs` (détails dans [ed](#ed)). Enregistrez
avec Ctrl+S, quittez avec Ctrl+Q. Un fichier est simplement le texte que
vous taperiez:

```
\ squares.fs
: square ( n -- n*n )  dup * ;
: squares ( n -- )  1+ 1 ?do i square . loop ;
```

Chargez-le, puis servez-vous des mots:

```
> include squares.fs
> 5 squares
1 4 9 16 25  ok
```

`include` est relatif au fichier qui le contient. `needs nom` charge une
bibliothèque une seule fois. Pour lancer directement un programme:
`lugforth squares.fs`. Ajoutez `bye` en dernière ligne pour quitter
ensuite.

L'ancienne façon de Forth, les sources gardées dans des blocs
numérotés de 1024 caractères et éditées avec `blocks-editor`, marche
aussi: voir [Blocs](#blocs).

Si un fichier produit `Error including: file` et rien d'autre, une
erreur a été levée pendant le chargement: procédez par dichotomie en
mettant des lignes `.` ou `.s` dans le fichier pour trouver où il
s'arrête.

## 9. Quand ça tourne mal

| Message | Signification |
|---|---|
| `ERROR: foo NOT FOUND!` | le mot `foo` n'est pas défini (faute de frappe, ou pas encore chargé) |
| `STACK UNDERFLOW` | un mot voulait plus de nombres que la pile n'en contenait |
| `Error including: file` | une erreur a interrompu le chargement de ce fichier |

Après une erreur, Lugforth vide la pile et rend l'invite. Quelques
habitudes font gagner des heures:

* Écrivez d'abord l'effet de pile et vérifiez-le avec `.s` après chaque
  essai.
* Testez chaque mot seul avant de l'utiliser dans un autre.
* `see mot` montre ce qui a réellement été compilé.
* Un mot qui laisse la pile modifiée, ou qui prend plus qu'il ne
  devrait, est la cause de la plupart des bogues.
* Plus de pièges dans [docs/en/dev/PITFALLS.md](dev/PITFALLS_fr.md).

## 10. Trois petits programmes

**FizzBuzz.** Les nombres de 1 à 15; les multiples de 3 donnent Fizz,
ceux de 5 Buzz, ceux des deux FizzBuzz.

```
: fizzbuzz ( -- )
  16 1 do
    i 15 mod 0= if ." FizzBuzz " else
    i 3 mod 0= if ." Fizz " else
    i 5 mod 0= if ." Buzz " else
    i . then then then
  loop ;
> fizzbuzz
1 2 Fizz 4 Buzz Fizz 7 8 Fizz Buzz 11 Fizz 13 14 FizzBuzz  ok
```

**Un crible d'Ératosthène.** Un tableau d'indicateurs, un octet par
nombre; on raye les multiples de chaque nombre premier trouvé. Remarquez
le test avant `?do`: quand `i * i` dépasse la limite, il n'y a rien à
rayer et `?do` ne doit pas être entré.

```
50 constant limit
create flags limit allot
: sieve ( -- )
  flags limit 1 fill
  limit 2 do
    flags i + c@ if
      i .
      i i * limit < if
        limit i i * do 0 flags i + c! j +loop
      then
    then
  loop ;
> sieve
2 3 5 7 11 13 17 19 23 29 31 37 41 43 47  ok
```

**Un mot qui prend un mot.** `'` (tick) donne l'adresse d'un mot,
`execute` l'exécute. C'est ainsi que Forth passe un comportement en
argument:

```
: .name ( -- ) ." Lugforth" ;
: rep ( n xt -- )  swap 0 ?do dup execute space loop drop ;
> 3 ' .name rep
Lugforth Lugforth Lugforth  ok
```

**La suite.** Les jeux avec graphismes et sons utilisent les mots SDL2
([SDL2.md](SDL2_fr.md) est la visite guidée,
[SDL2-words.md](SDL2-words_fr.md) la liste de tous les mots,
[SDL2-web.md](SDL2-web_fr.md) la version web): lisez
`examples/games/gfx_snake.fs`, le plus court, puis `gfx_tetris.fs` et
`gfx_breakout.fs` ([Exemples](#exemples)). `ed` et `writhdeck` sont
aussi écrits en Forth, dans `ports/posix/`: les lire montre les mêmes
mots à plus grande échelle. Pour le reste du langage,
[docs/en/WORDS.md](../en/WORDS.md) liste chaque mot avec une courte
description. Pour aller plus loin, les livres classiques sont *Starting
Forth* et *Thinking Forth* de Leo Brodie, tous deux libres en ligne.

## Réponses aux exercices

**2.** (1) `3 4 swap -` donne `4 3 -`, soit 1. (2) `2 3 1`. (3)
`5 dup 3 + swap 3 - * .` affiche 16. Vérifiez avec `.s`.

**3.** (1) `: double-sum ( a b -- n ) + 2* ;` (2)
`: area ( w h -- a ) * ;` puis `: volume ( w h d -- v ) >r area r> * ;`,
ou avec `rot`: `: volume ( w h d -- v ) -rot * * ;` (3)
`: fahrenheit>c ( f -- c ) 32 - 5 * 9 / ;`

**4.** (1) `: abs2 ( n -- |n| ) dup 0< if negate then ;` (2)
`: between? ( n lo hi -- flag ) rot tuck >= -rot <= and ;`

**5.** (1) `: sum-to ( n -- sum ) 0 swap 1+ 1 do i + loop ;`,
`100 sum-to .` affiche 5050. (2)
`: triangle ( n -- ) 1+ 1 ?do i 0 ?do [char] # emit loop cr loop ;` (3)
`: grid ( -- ) 10 1 do 10 1 do i j * 3 .r loop cr loop ;`

**6.** (1) `variable count`, `: tick 1 count +! ;`,
`: .count count @ . ;` (2) `create sq 5 cells allot`,
`: fill5 5 0 do i i * sq i cells + ! loop ;`,
`: show5 5 0 do sq i cells + @ . loop ;`

# Référence

## Arborescence

```
common/      noyau partagé (sources Forth et en-têtes C)
ports/       un dossier par plateforme: posix, web, windows, esp32, pico,
             pico-ice, gba, android, rpi (matériel nu: QEMU, Raspberry Pi 3/4), efi (PC x86-64 nu)
examples/    programmes d'exemple et bancs d'essai
tools/       scripts de compilation et de conversion
docs/        MANUAL.md, WORDS.md, fr/ (traduction française), dev/ (notes pour les développeurs), pages/ (site web), site/ (pages uEforth), images/
lib/         bibliothèques Forth partagées (hachage)
attic/       anciennes expériences
```

## Binaires

Pousser une étiquette comme `v7.0.8.1` (ou lancer
`.github/workflows/release.yml` à la main) les compile et les publie.

| Plateforme | Notes |
|---|---|
| Linux | Archive tar et AppImage, compilés sur Debian 10 (glibc 2.27 ou plus récente). L'AppImage contient les bibliothèques SDL2, les exemples et une police. Depuis une console elle donne l'interface terminal; un clic sur le bureau ouvre la fenêtre de `lugforth --gui` (un terminal si elle ne peut pas s'ouvrir). Les exemples sont copiés au premier lancement dans `~/.local/share/lugforth/examples` (`LUGFORTH_HOME` change le dossier), jamais écrasés. |
| Android | `lugforth-VERSION-android.apk` (arm64), compilé à la main avec `ports/android/build_apk.sh` et joint à la publication. Signé avec une clé de débogage gardée dans `out/android/debug.keystore`: conservez ce fichier pour pouvoir mettre à jour une copie installée. |
| Windows x64 | `lugforth-<version>-windows-x64.zip`: `lugforth.exe`, `SDL2.dll` et `SDL2_ttf.dll` (licences zlib et FreeType), les liaisons Forth dans `lib/` et `examples/`. `sdl2` et `sdl2-ttf` les chargent, et `lugforth.exe --gui`, le `lugforth-gui.bat` du zip (un double clic) ou `gui` à l'invite ouvrent la même fenêtre console que sous Linux, avec son menu, ses couleurs, ses polices (Consolas) et le menu Examples; ses réglages sont dans `%APPDATA%\lugforth`. Testé en CI avec le pilote vidéo factice, pas encore par le mainteneur sur un vrai écran. Lancez lugforth depuis son dossier. `ls`, `dir`, `cd`, `pwd`, l'éditeur de ligne et `/` ou `\` dans les chemins fonctionnent. `ed`, `writhdeck` et le son (`play`) ne sont que sous POSIX. |
| macOS | `lugforth-<version>-macos-universal.zip` (Apple silicon et Intel, macOS 11 ou plus récent). Non signé: clic droit, Ouvrir, ou `xattr -d com.apple.quarantine lugforth`. Testé en CI sur Apple silicon: l'invite, les tests, la complétion par Tab et les mots SDL2 (38 tests, sans écran) après `brew install sdl2 sdl2_ttf` (les bibliothèques sont cherchées dans le système et dans `/opt/homebrew/lib`, `/usr/local/lib`); la fenêtre console, les éditeurs et les sockets n'ont pas été essayés. |

Les binaires statiques ne sont pas possibles: Lugforth atteint libc,
SDL2 et X11 par `dlopen`/`dlsym`.

## Fenêtre console

`lugforth -e mots...` (POSIX) exécute le reste de la ligne de commande
comme une ligne de Forth, comme `gforth -e`:
`lugforth -e writhdeck notes.txt` ouvre l'éditeur,
`lugforth -e "1 2 + . cr bye"` calcule et quitte (sans `bye`
l'interpréteur reste ouvert). `-e` doit être le premier argument; chaque
`-e` suivant commence une nouvelle ligne, exécutée dans l'ordre
(`lugforth -e ": t 7 . ;" -e "t cr bye"`).

`lugforth --gui [fichier.fs]` (toute version Linux avec SDL2) ouvre une
fenêtre console avec une police TrueType, la sélection et le collage à
la souris, et le défilement arrière. Le menu est caché jusqu'à ce qu'on
utilise le bouton en haut à droite (ou F10): exemples, taille du texte,
couleurs (avec un sélecteur), polices du système, aide. Le menu Examples
n'existe que si le dossier courant (ou `LUGFORTH_EXAMPLES`) contient des
fichiers `.fs`. Un clic droit sur le bouton le cache, un clic droit à sa
place le montre de nouveau; F10 ouvre toujours le menu. Un clic du
milieu colle la sélection X11, comme dans les autres programmes Linux:
le texte qui vient d'être sélectionné dans cette fenêtre ou dans un
autre programme (pas le presse-papiers, que Ctrl+V colle; SDL2 2.26 ou
plus récent, avec une version plus ancienne le clic du milieu colle le
presse-papiers). Le texte sélectionné ici à la souris est proposé de la
même façon aux autres programmes. Ctrl + et Ctrl - changent la taille du
texte, Ctrl 0 la remet à zéro. Alt+Entrée met en plein écran.

Les réglages sont gardés dans `~/.local/share/lugforth/lugforth.ini`
(`LUGFORTH_HOME` change le dossier):

| Clé | Valeur |
|---|---|
| `size=` | taille du texte |
| `margin=` | marge |
| `fg=`, `bg=` | couleurs, RRGGBB en hexadécimal |
| `font=` | un fichier TrueType |
| `fullscreen=` | la touche, `Alt+Enter` par défaut |

## Dessiner à l'invite

Les mots de cette section et ceux du son plus bas sont les mots courts
SDL2, décrits un par un dans [SDL2-words.md](SDL2-words_fr.md) (visite
guidée: [SDL2.md](SDL2_fr.md), version web:
[SDL2-web.md](SDL2-web_fr.md)); les autres noms d'un même mot, comme
`plot` pour `dot`, y sont listés sous « Aliases ».

Tapez `graphic` une fois, puis dessinez comme en BASIC sur un micro 8
bits: le dessin apparaît aussitôt, sous le texte, sans `screen` ni
`flip`.

    graphic
    0 0 100 100 line
    red color 200 100 50 30 box
    5 ink 320 240 60 circle \ une couleur par son numéro
    cls \ efface le dessin, `page` efface le texte

La zone de dessin fait 640x480 par défaut, quelle que soit la taille de
la console; `w h graphic-size` la change (plus elle est grande, plus
c'est fin: `1280 960 graphic-size` sur une console de 1280x960 donne un
pixel de dessin par pixel d'écran, et sur le bureau
`1024 640 graphic-size` remplit une fenêtre de cette taille); `dot`,
`line`, `box`, `frame`, `disc`, `circle` (et `text` sur les cartes) sont
ceux des mots `sdl2`. `ink` prend 0 noir, 1 rouge, 2 vert, 3 jaune, 4
bleu, 5 magenta, 6 cyan, 7 blanc, 8 à 15 gris, orange, rose, marron,
violet et couleurs claires; `$rrggbb color` et `r g b rgb` donnent
n'importe quelle autre. Fonctionne dans la fenêtre console
(`lugforth --gui`, et l'application Android) et sur les écrans du Pi et
du PC. Un programme qui ouvre son propre `screen` remplace la couche.
Dans un terminal il n'y a pas de fenêtre: `graphic` le dit.

## Le son à l'invite

Dans la fenêtre console (`lugforth --gui`) tapez `sound` une fois;
ailleurs tapez `sdl2` (rien à ouvrir, pas besoin de fenêtre sous Linux,
le son sort par les haut-parleurs):

    440 300 beep                          \ une note: fréquence en Hz, durée en ms
    sine \ la forme: square triangle saw noise sine
    s" t140 o4 l8 c d e c e4 g4 r4 >c2" play

`play` prend des notes écrites en MML (Music Macro Language, le `PLAY`
du MSX et du GW-BASIC): les lettres `c d e f g a b`, `#` dièse, `-`
bémol, une durée après la note (4 noire, 8 croche, un point rallonge de
moitié), `r` ou `p` un silence, `o4` l'octave (`<` et `>` descendent et
montent; `o4 c` est le do du milieu), `l8` la durée par défaut, `t120`
le tempo, `n49` une note par son numéro (n49 est le do du milieu, n0 un
silence), `&` lie deux notes de même hauteur (`c4&c8`), `v8` le volume
(0 à 15), `w1` l'onde. La table complète est dans [SDL2.md](SDL2_fr.md).

**Plusieurs voix.** Une virgule dans la chaîne commence une autre voix,
et toutes les voix démarrent ensemble (4 au plus, 512 notes chacune; le
son dure autant que la plus longue):

    s" t120 o4 l8 e d c d e e e4, o3 l4 c g c g c2" play

Exemples (chacun tient sur une seule ligne à taper après `sdl2`):

    \ une mélodie sur une basse: la basse a son octave, son onde et son volume
    s" t140 o4 l8 w2 v10 c d e f g4 g4, w0 v6 o3 l4 c g c g, w1 v8 o2 l2
    c c" play

    \ un canon: la deuxième voix attend une ronde (r1) et joue les mêmes quatre notes
    s" t120 o4 l4 c d e c, r1 c d e c" play

    \ un accord de trois voix, une note chacune: do, mi et sol
    s" l1 o4 c, o4 e, o4 g" play

    \ une note tenue pendant qu'une autre bouge: le son dure autant que la voix la plus longue
    s" t120 l8 o4 v12 w4 e g b >e4 <b4, v7 w1 o3 l1 e" play

Chaque voix commence avec l'octave, la durée, le tempo, l'onde et le
volume avec lesquels `play` a démarré; répétez donc `t` ou `w` dans les
voix qui en ont besoin. Après le `play`, les réglages sont ceux de la
dernière voix. Sur le web, sur le Pi et sur le PC, les notes de toutes
les voix sont mises en file d'un coup; avec `sdl2-mixer`, `play` est un
autre mot (voir [SDL2.md](SDL2_fr.md)).

**Enveloppe.** `adsr ( attack decay sustain release -- )` façonne chaque
note de `beep` et `play`: l'attaque et la décroissance sont en ms, le
maintien est un niveau en pourcentage du pic, le relâchement en ms est
la fin de la note (il ne la rallonge pas). `10 80 60 120 adsr` est un
son pincé, `300 0 100 300 adsr` une lente houle. Exemples:

    10 80 60 120 adsr                      \ pincé: attaque rapide, retombe à 60 %
    s" t160 o4 l8 c e g >c4" play

    300 0 100 300 adsr                     \ houle: attaque lente, relâchement lent
    s" t80 o3 l2 c g" play

    5 0 100 40 adsr  sine                  \ orgue doux: pas de clic, relâchement court
    s" t120 o4 l4 e g e c2" play

    20 150 40 400 adsr                     \ comme une cloche, avec des voix ensemble
    s" t90 o5 l2 e c, o3 l2 c g" play

    adsr-off                               \ retour à la note simple

`adsr@` relit les quatre valeurs et `adsr-off` revient à la note simple
(par défaut: une note plate avec un très court fondu à sa fin). Les
coups (`drum-hit`) et les airs ABC ne sont pas touchés.

`wave`, `beep-volume` et `pitch-drop` prennent leur valeur sur la pile,
comme `color`: `1 wave`, `5000 beep-volume` (relisez-les avec `wave@`,
`beep-volume@`, `pitch-drop@`; `to wave` marche toujours). `silence`
arrête. Airs en notation ABC: `sdl2-abc`, `abc-play`.

## Version mini

`lugforth-mini` (dans l'archive tar, aussi seul) est la version POSIX
sans les éditeurs, SDL2, X11, la fenêtre console et les serveurs web et
telnet: environ 110 Ko contre 260 Ko. `ninja posix` compile les deux
(`out/posix/lugforth-mini`); `--gui` dit qu'il n'y a pas de fenêtre. Une
partie peut encore être chargée depuis une copie des sources:
`needs ports/posix/sdl2.fs`, puis `sdl2` (SDL2 lui-même doit être
installé).

## Édition de ligne

Disponible dans un terminal, dans le terminal web, dans la console SDL
de l'application Android et sur le Pi et le PC en matériel nu
(`common/lineedit.fs`).

* Touches de curseur, Début, Fin, Suppr, Ctrl-A/E/B/F/D/K/U/L.
* Historique de 64 lignes (haut, bas), non conservé entre les sessions.
* Tab complète les mots. Après `include`, `needs`, `required`,
  `included`, `s"`, `z"`, `r|` et `r~` il complète plutôt les noms de
  fichiers (pas sur le web).
* L'entrée redirigée et les autres ports utilisent le simple `accept`.

## Pagineur

Sur un terminal POSIX et sur les consoles en matériel nu
(`common/pager.fs`), la sortie d'une ligne tapée à l'invite s'arrête à
chaque écran avec `-- more --`: espace pour la page suivante, Entrée
pour une ligne, `q` ou Échap pour arrêter.

* Actif par défaut. `pager-off` et `pager-on` le basculent; mettez
  `pager-off` dans `autoexec.fs` pour le garder éteint.
* Inactif sur un tube, pendant l'inclusion d'un fichier, dans un
  programme qui lit les touches, ou dans la console SDL.
* Les lignes plus larges que l'écran comptent pour une seule. La fin
  d'une session telnet remet le `type` ordinaire.

## ed

`ed fichier.fs` (version POSIX, dans un terminal ou dans la fenêtre
`--gui`) ouvre ou crée un fichier texte. Code: `editors/editor.fs`,
compilé au premier usage; tests: `editors/editor_tests.fs`.

| Touches | Action |
|---|---|
| flèches, Début, Fin, Page préc., Page suiv., Suppr | déplacement et suppression |
| Ctrl+flèches, Ctrl+Début/Fin | par mot, début/fin du texte |
| Tab, Entrée | deux espaces; saut de ligne en gardant l'indentation |
| Maj+flèches, Ctrl+K | sélectionner (Ctrl+K commence et arrête une sélection) |
| Ctrl+C, Ctrl+X, Ctrl+V | copier, couper, coller |
| Ctrl+Z, Ctrl+Y | annuler, rétablir |
| Ctrl+F, Ctrl+N ou F3 | chercher, suivant |
| Ctrl+G | aller à la ligne |
| Ctrl+S, Ctrl+Q | enregistrer, quitter (deux fois si le texte a changé) |
| Ctrl+T | thème de couleurs suivant |

Les fichiers `.fs`, `.fth` et `.4th` sont colorés (commentaires,
chaînes, mots du langage, nombres, le nom après `:`) dans un terminal
avec couleurs. Thèmes: `default`, `colorforth` (définitions en rouge,
mots en vert), `light`, `solarized`, `mono`. Le thème est gardé dans
`editor.ini` à côté de `lugforth.ini`, au format des fichiers ini de
WrithDeck (voir [writhdeck.ini](#writhdeckini)); les clés qu'il contient
aussi (`limit_width`, `word_goal`, `wrap`) sont celles de `writhdeck`,
qui les lit une fois s'il n'a pas encore de `writhdeck.ini`. Sur les
cartes (Pi, PC) il est à la racine du disque.

## writhdeck

`writhdeck fichier.t2t` est le même éditeur pour écrire, d'après
[WrithDeck](https://github.com/luginf/writhdeck)
(`editors/writhdeck.fs`, construit sur `editor.fs`: les deux fichiers
sont nécessaires). Les lignes sont repliées dans une colonne centrée;
txt2tags et Markdown sont colorés (titres, listes, `**gras**`,
`//italique//`, `__souligné__`, commentaires `%`). La barre d'état
montre les mots, les mots de la session et la progression vers
l'objectif du jour.

Menu Échap:

| Touche | Action |
|---|---|
| `w` | repli des lignes |
| `f` | focus: atténue tous les paragraphes sauf le courant, la ligne reste au milieu |
| `h` | Hemingway: ni suppression ni retour en arrière |
| `t` | minuteur, chronomètre ou compte à rebours |
| `o` | plan (table des matières) des titres |
| `v` | occurrences des mots, les plus utilisés d'abord |
| `s` | statistiques |
| `r` | tout remplacer |
| `g` | objectif du jour |
| `a` | enregistrement automatique |
| `+` `-` | largeur |
| `c` | thème de couleurs |
| `q` | quitter |
| `?` | aide |

Ctrl+Y rétablit. Ctrl+O enregistre et retourne au navigateur de
fichiers, où `writhdeck` seul démarre: Entrée ouvre, `n` nouveau, `/`
filtre, `r` renomme, `d` supprime, `b` sauvegarde.

### writhdeck.ini

Les réglages sont dans `writhdeck.ini`, au format de
[WrithDeck](https://github.com/luginf/writhdeck) (le programme Tcl):
lignes `clé = valeur`, titres `[editor]`, `[behaviour]` et `[misc]`
(ignorés à la lecture), `#` ou `%` pour les commentaires (seuls sur une
ligne, ou après un blanc), `yes`/`no` pour les booléens. Seules les clés
que cet éditeur utilise sont écrites. Le fichier est lu à chaque
ouverture d'un texte et réécrit quand un réglage change dans le menu
Échap. Ce n'est **pas** le fichier du programme Tcl
(`~/Documents/writhdeck/writhdeck.ini`): il se trouve à côté de
`lugforth.ini` (`~/.local/share/lugforth`, ou `LUGFORTH_HOME`), et à la
racine du disque sur le Pi et le PC. Comme dans WrithDeck, un fichier
nommé `writhdec.ini`, `writhd.ini`, `wrid.ini` ou `writhdeck.ini` dans
le dossier courant l'emporte (le premier trouvé, dans cet ordre). À
défaut, l'`editor.ini` des versions plus anciennes est lu une fois.

| Clé | Défaut | Signification |
|---|---|---|
| `scheme` | `default` | `default`, `colorforth`, `light`, `solarized`, `mono` (propres à cet éditeur, pas ceux de Tcl) |
| `limit_width` | `72` | colonnes de la colonne de texte centrée (30 à 300) |
| `wrap` | `yes` | lignes repliées en douceur dans cette colonne (cet éditeur seulement) |
| `hemingway_mode` | `no` | démarrer en mode Hemingway |
| `word_goal` | `500` | objectif du jour en mots, `0` pour aucun |
| `bar` | `yes` | barre d'état; `no` la cache, un message s'y affiche quand même |
| `help_bar` | `Esc: menu` | texte du jeton `help_bar` quand il n'y a pas de message |
| `status_left`, `status_center`, `status_right` | voir plus bas | les jetons de chaque zone de la barre d'état |
| `autosave_enabled` | `no` | enregistrement automatique (le défaut de Tcl est `yes`) |
| `autosave_interval` | `1` | minutes entre deux enregistrements automatiques (1 à 60) |

La barre d'état a trois zones, chacune une liste de jetons séparés par
des blancs. Le défaut est
`status_left = filename dirty words session goal timer modes help_bar`,
les deux autres vides. `filename` `dirty` `words` `goal` `timer`
`help_bar` et `space` sont les jetons de WrithDeck. `session` (mots
écrits depuis l'ouverture du texte) et `modes` (`[focus]`, `[hem]`,
`[auto]`) sont propres à cet éditeur; les autres jetons de WrithDeck
(`ln`, `col`, `chars`, `clock`...) ne sont pas implémentés, et tout mot
inconnu est affiché tel quel. Pour cacher l'objectif, retirez `goal` de
la liste, ou mettez `word_goal = 0`. Un message (`Wrap on`,
`Autosaved`...) s'affiche à la place de `help_bar`, ou après la zone de
droite s'il n'y en a pas. Exemple, le nom à gauche, les mots au milieu,
l'objectif à droite:

```
status_left   = filename dirty
status_center = words
status_right  = goal help_bar
```

## Blocs

Avant les fichiers, Forth gardait ses sources dans des **blocs**: 1024
caractères, montrés en 16 lignes de 64. Lugforth les a sur toutes les
versions qui ont des fichiers (PC, Windows, Raspberry Pi, PC UEFI,
ESP32; code `common/blocks.fs`, venu d'uEforth). Un fichier de blocs
est un fichier ordinaire de blocs de 1024 octets numérotés à partir de
0. Dans Lugforth le 64e caractère de chaque ligne est un saut de ligne:
le fichier se lit aussi comme un texte, 63 caractères par ligne.

| Mot | Action |
|---|---|
| `use nom` | choisit le fichier de blocs (créé s'il manque); sans lui, le premier mot de bloc ouvre `blocks.fb` dans le répertoire courant |
| `block ( n -- a )` | adresse des 1024 caractères du bloc n, lus dans le fichier |
| `buffer ( n -- a )` | pareil sans lire, pour le remplir en entier |
| `update` | le bloc en mémoire a changé: il est écrit avant d'en lire un autre |
| `save-buffers`, `flush` | l'écrit tout de suite; `flush` l'oublie aussi |
| `empty-buffers` | l'oublie sans l'écrire |
| `load ( n -- )`, `thru ( a b -- )` | interprète le bloc n, ou les blocs a à b, comme s'ils étaient tapés |
| `copy ( de vers -- )` | copie un bloc sur un autre |
| `list ( n -- )` | montre un bloc; `scr` garde son numéro |

Un seul bloc est en mémoire: une adresse donnée par `block` vaut
jusqu'à la lecture d'un autre bloc. Un bloc ne peut donc pas en
charger un autre par `load` (le texte en cours de lecture est
remplacé): lancez `thru` à l'invite ou depuis un fichier. Lire un bloc
au-delà de la fin allonge le fichier.

L'éditeur de lignes d'uEforth travaille sur le dernier bloc listé,
après `editor` (`forth` en sort): `l` le liste à nouveau, `n` / `p`
passent au suivant / précédent, `3 r texte` remplace la ligne 3,
`3 a texte` insère une ligne à cet endroit, `3 d` la supprime, `3 e`
la vide, `wipe` vide le bloc.

```
> use squares.fb  1 list
> editor
> 0 r : square ( n -- n*n )  dup * ;
> 1 r : squares ( n -- )  1+ 1 ?do i square . loop ;
> flush forth
> 1 load  5 squares
1 4 9 16 25  ok
```

### blocks-editor

`blocks-editor` édite le dernier bloc listé, `blocks-editor 5` le bloc
5 du fichier choisi avec `use`, en plein écran: une ligne de titre
(numéro du bloc, blocs du fichier, insertion ou remplacement,
`modified`), les 16 lignes numérotées, une ligne de message et un
rappel des touches. Code: `common/blocks_editor.fs`, compilé au premier
usage; test: `python3 tools/blocks_editor_pty_test.py
out/posix/lugforth`.

| Touches | Action |
|---|---|
| flèches, Début, Fin (Ctrl+A, Ctrl+E), Tab | déplacement; Fin va après le dernier caractère de la ligne, Tab quatre places à droite |
| Entrée | début de la ligne suivante |
| Retour arrière, Suppr (Ctrl+D) | efface avant ou sous le curseur, la fin de la ligne revient à gauche |
| Inser, Ctrl+T | insertion ou remplacement (par défaut); insérer pousse la ligne à droite, son dernier caractère est perdu |
| Page préc., Page suiv. | bloc précédent, suivant |
| Ctrl+G | aller à un bloc: tapez son numéro, Entrée (Échap annule) |
| Ctrl+X, Ctrl+C, Ctrl+V | couper, copier la ligne; la coller au-dessus du curseur |
| Ctrl+N | insérer une ligne vide (la dernière ligne du bloc est perdue) |
| Ctrl+U | annuler les changements depuis la dernière écriture du bloc |
| Ctrl+S | enregistrer |
| Ctrl+L | enregistrer et charger le bloc (`load`), montrer ce qu'il affiche jusqu'à une touche |
| Ctrl+Q | enregistrer et quitter |

Les changements sont écrits par Ctrl+S, Ctrl+L, Ctrl+Q, ou quand un
autre bloc est affiché. Les mots définis par Ctrl+L restent après la
sortie. Les caractères sous l'espace s'affichent en espaces, ceux
au-delà de `~` en points; seul l'ASCII simple peut être tapé. Testé
dans un terminal (test pty) et sur le PC UEFI sous QEMU
(`make -C ports/efi test`); pas essayé dans la fenêtre `--gui`, sur le
Raspberry Pi, Windows ni l'ESP32.

## Exemples

`examples/` contient des jeux, des bancs d'essai et des démos; voir
[examples/README.md](../../examples/README.md).

| Programme | Quoi |
|---|---|
| `gfx_breakout.fs` | casse-briques |
| `gfx_roguelike.fs` | roguelike |
| `gfx_tetris.fs`, `gfx_snake.fs`, `gfx_minesweeper.fs` | réécrits d'après les jeux de r3forth |
| `gfx_mastermind.fs` | mastermind: quatre couleurs sur six en dix essais, clavier ou souris |
| `gfx_3d.fs` | fils de fer qui tournent |
| `gfx_2048.fs` | tuiles coulissantes; `--no-anim` coupe l'animation |
| `2048.fs` | 2048 dans le terminal |
| `zmachine.fs` | interpréteur de Z-machine en un seul fichier: joue les histoires Infocom et Inform (`.z3 .z4 .z5 .z8`), `zplay story.z5` (essayez `lugforth examples/games/zmachine.fs examples/games/zmachine/cave.z5`, ou `cave_fr.z5` en français); aussi sous gforth (`ports/gforth/zmachine-gforth.fs`) |
| `fm/gfx_fm.fs` | synthétiseur FM, clavier MIDI sous Linux et dans le navigateur |
| `inference/` | petit moteur d'inférence à règles, README en anglais et en français, Makefile avec des scénarios |

Licences: 2048, 3d et le moteur d'inférence sont des portages de projets
de Marc Petremann ([eForth
Windows](https://github.com/MPETREMANN11/eForth-Windows), GPL); tetris,
snake et minesweeper suivent [r3forth](https://github.com/phreda4/r3) de
Pablo Hugo Reda (MIT, le code de Lugforth est réécrit). Le reste du
dépôt est sous Apache 2.0. Voir [NOTICE](../../NOTICE).

Tests: `make -C examples test-ports` lance les tests de logique,
`make -C examples smoke` charge chaque exemple SDL2 sans écran.

## Varvara: exécuter des ROMs Uxn

`examples/system/varvara/` est une machine virtuelle
[Uxn/Varvara](https://100r.co/site/uxn.html) écrite en Lugforth.

* `lugforth examples/system/varvara/varvara.fs programme.rom` exécute
  une ROM dans une fenêtre SDL2, comme `uxnemu` (Screen, Controller,
  Mouse, Datetime, Audio, File, entrée Console). F1 zoom, F3 capture
  d'écran, F4 redémarrage; on peut déposer une ROM sur la fenêtre. Sans
  ROM, elle montre un écran d'utilisation.
* `uxncli.fs` exécute une ROM dans le terminal, comme `uxncli`.
* `launch.fs` est un lanceur avec options: `-c` terminal, `-z` zoom,
  `-s LxH` taille de l'écran, `--no-mouse`, `-q`, et des jetons pour
  writhdeck-uxn.

Le test des opcodes de la version d'origine (`opctest.rom`) passe. C'est
environ 60 fois plus lent que l'uxn en C, donc cela convient aux ROMs
qui attendent surtout des événements. Voir
`examples/system/varvara/README.md`.

## Plusieurs terminaux (Alt+F1 à Alt+F4)

Sur les ports en matériel nu (rpi et efi, pas le bureau) `Alt+F2` ouvre
un deuxième terminal: un autre Forth, avec son propre dictionnaire, ses
piles et son écran, pendant que le premier continue de tourner. `Alt+F1`
revient au premier, `Alt+F3` et `Alt+F4` ouvrent un troisième et un
quatrième. `n term` fait la même chose au clavier (Alt+Fn peut être pris
par l'hôte ou le micrologiciel), `terminal` donne le numéro de celui-ci
et `terminals` combien sont ouverts. `bye` dans un terminal autre que le
premier le ferme (dans le premier il éteint).

Un programme peut donc tourner dans un terminal pendant que le manuel
(`cat MANUAL.md`) ou un éditeur est ouvert dans un autre. Les terminaux
se relaient à chaque `pause` (attente d'une touche, `ms` et l'attente
d'image des jeux le font): un calcul qui ne fait jamais de pause bloque
les autres. Un programme qui a l'écran le garde pendant qu'un autre
terminal est montré et le retrouve, redessiné, au retour. Le clavier, la
souris et la ligne série sont ceux du terminal affiché; les fichiers et
le son sont partagés. Un terminal après le premier a un tas de 32 Mo, et
n'exécute pas `autoexec.fs`.

## Matériel nu

### Raspberry Pi et QEMU (`ports/rpi`)

Lugforth tourne sans système d'exploitation sur ARM 64 bits: QEMU `virt`
et `raspi3b` (qui tournent sur un PC Linux x86_64), un Raspberry Pi 3,
et un Pi 4 (écrit à l'aveugle). La console est la ligne série. Les mots
`sdl2` sont là: écran, texte, images, clavier et souris, bips et airs
ABC. Les fichiers sont sur la carte SD (FAT12/16/32): `include`, `ls`,
et un `autoexec.fs` exécuté au démarrage.
`examples/games/gfx_breakout.fs` et `gfx_roguelike.fs` tournent sans
changement.

```
sudo apt install gcc-aarch64-linux-gnu picolibc-aarch64-linux-gnu qemu-system-arm
make -C ports/rpi run                   # l'invite, sous QEMU virt
make -C ports/rpi roguelike BOARD=virt  # une fenêtre QEMU, avec le son
make -C ports/rpi test                  # démarre les deux machines et vérifie 121 choses
```

Ce qui marche où, ce qui est testé, ce qui demande une vraie carte:
[ports/rpi/README.md](../../ports/rpi/README.md).

### PC x86-64, UEFI (`ports/efi`)

Une application UEFI, `EFI/BOOT/BOOTX64.EFI` sur une clé USB FAT32,
lancée par le micrologiciel (Secure Boot désactivé, puis le menu de
démarrage ponctuel du micrologiciel, F12 sur beaucoup de PC). Elle
utilise l'écran, le clavier et les fichiers du micrologiciel: console à
l'écran, mots `sdl2`, `include` et éditeurs depuis la clé. Son par Intel
HD Audio (essayé seulement sous QEMU), pas de souris.

```
sudo apt install gnu-efi ovmf qemu-system-x86 dosfstools mtools
make -C ports/efi usb     # out/efi/usb.img, à copier avec dd sur une clé
make -C ports/efi run     # la même image sous QEMU avec OVMF
make -C ports/efi test    # 124 vérifications sous QEMU
```

Plusieurs clés: `volumes` liste les volumes FAT (`*` marque celui en
usage), `n mount` en fait le disque de `include`, `ls`, `cd`; une clé
branchée après le démarrage est trouvée. Voir le README du port.

Autres systèmes de fichiers (exFAT, ext2/3/4, NTFS...): le micrologiciel
ne lit que le FAT, mais des pilotes UEFI de systèmes de fichiers peuvent
être chargés depuis la clé. Copiez les pilotes x64 d'
[EfiFs](https://github.com/pbatard/efifs/releases) (GPL, ne fait pas
partie de lugforth) dans `EFI/drivers/` sur la clé: `exfat_x64.efi` pour
exFAT, `ext2_x64.efi` pour ext2, ext3 et ext4. Puis:

```
include drivers.fs   \ charge chaque .efi de /EFI/drivers/, puis montre les volumes
2 mount ls           \ un volume ext4 ou exFAT, maintenant dans la liste
0 mount              \ retour à la clé
```

`drivers.fs` (sur la clé faite par `make usb`) n'est pas exécuté au
démarrage: ajoutez la ligne `include drivers.fs` à `autoexec.fs` pour
avoir les pilotes à chaque fois. Ils ne font que lire: `include`, `cat`,
`ls` marchent sur ces volumes, écrire un fichier échoue. Un pilote à la
main: `s" /EFI/drivers/ext2_x64.efi" pc load-driver . forth` (0 chargé,
-1 fichier non lu, -2 refusé par le micrologiciel: pas une image UEFI
x64 ou Secure Boot actif, -3 son démarrage a échoué). La clé elle-même
reste en FAT32.

Voir [ports/efi/README.md](../../ports/efi/README.md).

## Compilation

```
sudo apt install ninja-build gcc-arm-none-eabi
git clone git@github.com:luginf/lugforth.git
cd lugforth
./configure.py
ninja
```

La sortie va dans `out/`: `posix`, `web`, `windows`, `esp32`,
`pico-ice`, `pages` (le site GitHub Pages, `tools/make_pages.py`), plus
des versions `*-sim` utilisées pour les tests. Une seule plateforme:
`ninja posix`, `ninja web`, `ninja win64`...

* `./configure.py -f` saute les composants les plus lents (web,
  windows).
* `ninja install` installe la version posix dans `/usr/bin`.
* ESP32: `ninja esp32-flash` (aussi `esp32s2-flash`, `esp32s3-flash`,
  `esp32c3-flash`, `esp32cam-flash`); mettez `PORT=com3` etc. pour
  choisir le port.
