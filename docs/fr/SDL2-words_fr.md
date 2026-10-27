# Référence des mots SDL2

*Traduction française de [SDL2-words.md](SDL2-words_fr.md) (la version
anglaise fait foi). Les noms de mots et les colonnes « Pile » sont ceux
de Forth et restent tels quels.*

Chaque mot du vocabulaire `sdl2`, pour la version POSIX
(`out/posix/lugforth`). Pour une visite guidée avec des exemples, lisez
d'abord [SDL2.md](SDL2_fr.md). Cette page est la liste complète.

Les mots viennent de cinq chargeurs, chacun chargeant sa source la
première fois qu'on le tape. Tous ajoutent au même vocabulaire `sdl2`:

| Chargeur | Bibliothèque | Section |
|--------|---------|---------|
| `sdl2` | `libSDL2-2.0.so.0` | [Noyau](#noyau-sdl2) |
| `sdl2-image` | `libSDL2_image-2.0.so.0` | [Images](#images-sdl2-image) |
| `sdl2-ttf` | `libSDL2_ttf-2.0.so.0` | [Texte](#texte-sdl2-ttf) |
| `sdl2-mixer` | `libSDL2_mixer-2.0.so.0` | [Son](#son-sdl2-mixer) |
| `sdl2-abc` | (charge `sdl2-mixer`) | [Airs](#airs-sdl2-abc) |

Notation de pile: `x y` sont des coordonnées en pixels, `w h` des
tailles, `a n` une chaîne Forth (adresse, longueur), `z` une chaîne C
terminée par zéro, `f` un indicateur, `$rrggbb` une couleur.
`img snd mus font` sont des poignées renvoyées par les mots `load-`. Les
valeurs se lisent par leur nom et se changent avec `to`: `3 to zoom`.

Les liaisons C brutes gardent le nom et l'ordre des arguments du C.
Chacune empile exactement un résultat. Quand le tableau dit `void`, la
fonction C ne renvoie rien mais une valeur est quand même empilée:
faites-lui `drop`. Les résultats entiers qui peuvent être négatifs (`-1`
pour une erreur) doivent passer par `sign-extend` du vocabulaire
`posix`.

Listes de mots: [index alphabétique](#index-alphabétique).

## Noyau (sdl2)


### Écran

| Mot | Pile | Ce qu'il fait |
|------|-------|--------------|
| `screen` | `( w h -- )` | Ouvre une fenêtre de w*`zoom` par h*`zoom` pixels dont la zone de dessin fait w par h pixels logiques (mise à l'échelle, rapport conservé). Ferme d'abord l'écran précédent. Utilise un moteur de rendu accéléré avec synchro verticale, ou le logiciel si cela échoue. Lève une exception avec le message de SDL en cas d'échec. |
| `close-screen` | `( -- )` | Détruit le moteur de rendu et la fenêtre, exécute `close-hook`, ferme le périphérique audio et appelle `sdl-shutdown` (la fonction C `SDL_Quit`). |
| `screen-open?` | `( -- f )` | Vrai tant qu'un écran est ouvert. |
| `zoom` | `( -- n )` | Valeur, 1 par défaut. Facteur d'échelle de la fenêtre utilisé par le prochain `screen`: `3 to zoom`. |
| `width` | `( -- n )` | Valeur. Largeur logique donnée au dernier `screen`. |
| `height` | `( -- n )` | Valeur. Hauteur logique donnée au dernier `screen`. |
| `title` | `( a n -- )` | Fixe le titre de la fenêtre. |
| `fullscreen` | `( f -- )` | Active (vrai) ou désactive (faux) le plein écran du bureau. |
| `cursor` | `( f -- )` | Montre (vrai) ou cache (faux) le curseur de la souris. |
| `window-handle` | `( -- a )` | Valeur. Le `SDL_Window*`, 0 quand aucun écran n'est ouvert. |
| `renderer` | `( -- a )` | Valeur. Le `SDL_Renderer*`, 0 quand aucun écran n'est ouvert. À passer aux appels bruts. |
| `sdl-error` | `( -- a n )` | Texte de la dernière erreur SDL. |

### Couleurs

| Mot | Pile | Ce qu'il fait |
|------|-------|--------------|
| `color` | `( $rrggbb -- )` | Fixe la couleur de dessin, opaque. `red color`, `$102030 color`. |
| `rgb` | `( r g b -- )` | Fixe la couleur de dessin à partir de composantes de 0 à 255, opaque. |
| `rgba` | `( r g b a -- )` | Fixe la couleur de dessin et l'alpha. Un alpha inférieur à 255 se mélange à ce qui est déjà dessiné. |
| `alpha` | `( a -- )` | Change seulement l'alpha de la couleur courante. |
| `black` `white` `red` `green` `blue` `yellow` `cyan` `magenta` `orange` `purple` `pink` `brown` `gray` | `( -- $rrggbb )` | Couleurs nommées, constantes: `$000000 $ffffff $ff0000 $00c000 $0000ff $ffff00 $00ffff $ff00ff $ff8000 $800080 $ff80c0 $804000 $808080`. À utiliser ainsi: `red color`. |
| `pen-r` `pen-g` `pen-b` `pen-a` | `( -- n )` | Valeurs. Composantes de la couleur courante. Lisez-les, fixez la couleur avec `rgba`. |
| `ink` | `( n -- )` | Fixe la couleur de dessin par son numéro, comme sur un micro 8 bits: 0 noir, 1 rouge, 2 vert, 3 jaune, 4 bleu, 5 magenta, 6 cyan, 7 blanc, 8 à 15 gris, orange, rose, marron, violet, vert clair, bleu clair, gris clair. `1 ink` est `red color`. Opaque. |
| `pen` | `( -- n )` | Valeur. Couleur courante empaquetée en `r + g<<8 + b<<16 + a<<24`, la disposition d'un `SDL_Color` passé par valeur (utilisé par `text`). |

### Dessin

| Mot | Pile | Ce qu'il fait |
|------|-------|--------------|
| `cls` | `( -- )` | Remplit tout l'écran avec la couleur courante. |
| `dot` | `( x y -- )` | Dessine un pixel. S'appelle aussi `plot` (voir [Alias](#alias)). |
| `line` | `( x1 y1 x2 y2 -- )` | Dessine une ligne, les deux extrémités comprises. |
| `box` | `( x y w h -- )` | Rectangle plein. |
| `frame` | `( x y w h -- )` | Contour de rectangle. |
| `hline` | `( x y w -- )` | Ligne horizontale de largeur w à partir de x y. |
| `disc` | `( cx cy r -- )` | Cercle plein de rayon r. |
| `circle` | `( cx cy r -- )` | Contour de cercle de rayon r. |
| `flip` | `( -- )` | Montre ce qui a été dessiné depuis le dernier `flip`. Attend la synchro verticale quand elle existe, et met à jour `dt`. Complète aussi le son de `drum-hit`. |
| `pixel@` | `( x y -- $rrggbb )` | Relit un pixel de l'écran. Utile pour les collisions par la couleur. |

### Temps

| Mot | Pile | Ce qu'il fait |
|------|-------|--------------|
| `ticks` | `( -- ms )` | Millisecondes depuis le démarrage de SDL. |
| `delay` | `( ms -- )` | Dort ms millisecondes, en complétant entre-temps le son de `drum-hit`. |
| `dt` | `( -- ms )` | Valeur. Millisecondes entre les deux derniers `flip`. |

### Entrées

| Mot | Pile | Ce qu'il fait |
|------|-------|--------------|
| `events` | `( -- )` | Traite tous les événements en attente: touches, souris, fermeture de la fenêtre. À appeler une fois par image, avant de lire les entrées. |
| `wait` | `( -- )` | Bloque jusqu'à l'arrivée d'un événement et le traite. |
| `quit?` | `( -- f )` | Vrai une fois la fenêtre fermée ou `quit!` appelé. |
| `quit!` | `( -- )` | Fixe l'indicateur de sortie, comme si la fenêtre était fermée. |
| `pressed?` | `( key -- f )` | Vrai tant que la touche ou le bouton de souris est tenu, d'après le dernier `events`. La touche est un caractère (`[char] a`, casse ignorée), une constante `key-`, ou une constante `-BUTTON`. Un appui relâché dans la même image n'est pas vu. |
| `scan?` | `( scancode -- f )` | Vrai tant que la touche à cette place du clavier est tenue (code de balayage SDL: 4 à 29 sont A à Z d'un QWERTY, 30 à 39 sont 1 à 9 et 0, 54 55 56 sont virgule, point, barre oblique), quelle que soit la disposition. |
| `last-key` | `( -- n )` | Valeur. Code de la dernière touche pressée. |
| `mouse-x` `mouse-y` | `( -- n )` | Valeurs. Dernière position de la souris vue dans les événements, en pixels logiques (la taille de `screen`), quel que soit le `zoom`. |
| `wheel` | `( -- n )` | Valeur. Mouvement de la molette traité par le dernier `events` (vers le haut est positif). |
| `key-left` `key-right` `key-up` `key-down` | `( -- n )` | Constantes. Flèches, pour `pressed?`. |
| `key-space` `key-enter` `key-esc` `key-tab` `key-backspace` | `( -- n )` | Constantes. `32 13 27 9 8`, pour `pressed?`. |
| `LEFT-BUTTON` `MIDDLE-BUTTON` `RIGHT-BUTTON` | `( -- n )` | Constantes `255 254 253`, pour `pressed?`. Mêmes valeurs que l'interface graphique générique. |

### Bips et coups

| Mot | Pile | Ce qu'il fait |
|------|-------|--------------|
| `beep` | `( freq ms -- )` | Met en file une tonalité de freq Hz durant ms millisecondes. Les tonalités jouent l'une après l'autre, donc une série de `beep` est une mélodie. Silencieux si aucun périphérique audio n'a pu être ouvert. |
| `drum-hit` | `( freq ms -- )` | Joue un son percussif tout de suite, par-dessus ce qui joue déjà: il s'éteint linéairement et sa hauteur peut tomber (`pitch-drop`). Jusqu'à 8 à la fois, un neuvième coupe le plus ancien. Utilise `wave` et `beep-volume`. Le mélange est mis en file environ 50 ms à l'avance par `drum-hit`, `flip` et `delay`, donc appelez régulièrement `flip` ou `delay`. Un `beep` attend la file alors qu'un coup non, ne comptez donc pas sur leur alignement. |
| `pitch-drop` | `( n -- )` | Réglage (`pitch-drop@` le lit), 0 par défaut. Pourcentage dont la hauteur des prochains coups tombe pendant qu'ils jouent, de 0 à 100. Le bruit n'a pas de hauteur. |
| `silence` | `( -- )` | Abandonne tous les bips en file et arrête tous les coups. |
| `latency` | `( -- n )` | Valeur, 2205 par défaut. Échantillons gardés en file par `flip`, `delay` et `drum-hit` (50 ms); baissez-la pour un instrument plus réactif. |
| `render-hook` `active-hook` | `( -- xt )` | Valeurs, 0 par défaut. Sources supplémentaires pour le mélange de `pump`: `render-hook` est exécuté comme `( n -- )` et ajoute n échantillons au tableau de cellules `mix`, `active-hook` comme `( -- f )` dit qu'il a du son à donner. `sdl2-fm` s'en sert. |
| `beeping?` | `( -- f )` | Vrai tant qu'il reste des bips ou des coups en file. |
| `wave` | `( n -- )` | Réglage. Forme des prochains bips: 0 carré, 1 triangle, 2 dent de scie, 3 bruit, 4 sinus (`wave@` le lit; `to wave` marche aussi). |
| `beep-volume` | `( n -- )` | Réglage (`beep-volume@` le lit), 3000 par défaut. Amplitude des bips, 0 à 32767. |
| `square` `triangle` `saw` `noise` `sine` | `( -- )` | Choisissent l'onde des prochains bips par leur nom: `0 wave`, `1 wave`, `2 wave`, `3 wave`, `4 wave`. |
| `play` | `( a n -- )` | Joue une mélodie écrite en texte (MML, comme sur le MSX), mise en file comme `beep`: `s" t140 o4 l8 c d e c  e4 g4 r4 >c2" play`. Notes `c d e f g a b`, `#` ou `+` dièse, `-` bémol, puis une durée (4 noire, 8 croche, un point rallonge de moitié); `r` ou `p` un silence; `o4` octave, `<` une octave plus bas, `>` une plus haut; `l8` durée par défaut; `t120` tempo en noires par minute; `w1` l'onde; `v8` le volume 0 à 15; `n49` une note par son numéro (`n0` un silence, 49 est le do du milieu); `c4&c8` lie deux notes de même hauteur. Les espaces sont libres; les réglages restent d'un `play` au suivant. Une virgule commence une autre voix, jouée avec les autres (4 voix, 512 notes chacune, chacune commence avec l'octave, la durée, le tempo, l'onde et le volume avec lesquels le `play` a démarré). Seulement dans les mots courts (`sdl2`), pas dans le mixer: avec `sdl2-mixer`, `play` est le mot qui joue un son. |
| `adsr` | `( a d s r -- )` | Enveloppe des bips et de `play`: attaque et décroissance en ms, maintien en pourcentage du pic, relâchement en ms (la fin de la note, il ne s'y ajoute pas). Les coups et les airs ABC ne sont pas touchés. Désactivée par défaut. |
| `adsr@` | `( -- a d s r )` | Les quatre valeurs de `adsr`. |
| `adsr-off` | `( -- )` | Retour à la note simple, sans enveloppe. |
| `setting` | `( n "name" -- )` | Définit un réglage, comme le sont `wave`, `beep-volume` et `pitch-drop`: `3000 beep-volume` range, `to beep-volume` aussi, et `beep-volume@` lit. |
| `audio-open` | `( -- )` | Ouvre le périphérique audio des bips tout de suite au lieu du premier `beep`. |
| `audio-open?` | `( -- f )` | Vrai quand le périphérique audio des bips est ouvert. |
| `sample-rate` | `( -- n )` | Constante 44100. Fréquence d'échantillonnage des bips. |
| `sdl` | `( z n "name" -- )` | Poignée de libSDL2 faite par `shared-library`. `z" SDL_Init" 1 sdl SDL_Init` définit un mot qui appelle cette fonction C avec n arguments. |

### Constantes (noyau)

| Constante | Valeur |
|----------|-------|
| `SDL_INIT_AUDIO` | `$00000010` |
| `SDL_INIT_VIDEO` | `$00000020` |
| `SDL_INIT_EVENTS` | `$00004000` |
| `SDL_WINDOW_SHOWN` | `$00000004` |
| `SDL_WINDOW_RESIZABLE` | `$00000020` |
| `SDL_WINDOW_FULLSCREEN_DESKTOP` | `$00001001` |
| `SDL_WINDOWPOS_CENTERED` | `$2FFF0000` |
| `SDL_RENDERER_SOFTWARE` | `$00000001` |
| `SDL_RENDERER_ACCELERATED` | `$00000002` |
| `SDL_RENDERER_PRESENTVSYNC` | `$00000004` |
| `SDL_BLENDMODE_NONE` | `0` |
| `SDL_BLENDMODE_BLEND` | `1` |
| `SDL_BLENDMODE_ADD` | `2` |
| `SDL_PIXELFORMAT_ARGB8888` | `$16362004` |
| `SDL_QUIT` | `$0100` |
| `SDL_KEYDOWN` | `$0300` |
| `SDL_KEYUP` | `$0301` |
| `SDL_MOUSEMOTION` | `$0400` |
| `SDL_MOUSEBUTTONDOWN` | `$0401` |
| `SDL_MOUSEBUTTONUP` | `$0402` |
| `SDL_MOUSEWHEEL` | `$0403` |
| `AUDIO_S16SYS` | `$8010` |
| `#voices` | `8` |

### Liaisons C brutes (noyau)

`sdl-shutdown` est la fonction C `SDL_Quit`: lugforth ignore la casse,
elle ne pouvait donc pas partager son nom avec la constante d'événement
`SDL_QUIT`.

| Mot | Pile | Args |
|------|-------|------|
| `SDL_Init` | `( n -- n )` | 1 |
| `SDL_InitSubSystem` | `( n -- n )` | 1 |
| `sdl-shutdown` | `( -- void )` | 0 |
| `SDL_GetError` | `( -- z )` | 0 |
| `SDL_SetHint` | `( z z -- n )` | 2 |
| `SDL_GetTicks` | `( -- n )` | 0 |
| `SDL_Delay` | `( n -- void )` | 1 |
| `SDL_CreateWindow` | `( z x y w h flags -- a )` | 6 |
| `SDL_DestroyWindow` | `( a -- void )` | 1 |
| `SDL_SetWindowTitle` | `( a z -- void )` | 2 |
| `SDL_SetWindowFullscreen` | `( a n -- n )` | 2 |
| `SDL_ShowCursor` | `( n -- n )` | 1 |
| `SDL_CreateRenderer` | `( a n n -- a )` | 3 |
| `SDL_DestroyRenderer` | `( a -- void )` | 1 |
| `SDL_RenderSetLogicalSize` | `( a n n -- n )` | 3 |
| `SDL_SetRenderDrawColor` | `( a r g b a -- n )` | 5 |
| `SDL_SetRenderDrawBlendMode` | `( a n -- n )` | 2 |
| `SDL_RenderClear` | `( a -- n )` | 1 |
| `SDL_RenderPresent` | `( a -- void )` | 1 |
| `SDL_RenderDrawPoint` | `( a x y -- n )` | 3 |
| `SDL_RenderDrawLine` | `( a x y x y -- n )` | 5 |
| `SDL_RenderDrawRect` | `( a a -- n )` | 2 |
| `SDL_RenderFillRect` | `( a a -- n )` | 2 |
| `SDL_RenderCopy` | `( a a a a -- n )` | 4 |
| `SDL_RWFromFile` | `( z z -- a )` | 2 |
| `SDL_RenderReadPixels` | `( a a n a n -- n )` | 5 |
| `SDL_CreateTexture` | `( a n n n n -- a )` | 5 |
| `SDL_CreateTextureFromSurface` | `( a a -- a )` | 2 |
| `SDL_UpdateTexture` | `( a a a n -- n )` | 4 |
| `SDL_QueryTexture` | `( a a a a a -- n )` | 5 |
| `SDL_SetTextureColorMod` | `( a r g b -- n )` | 4 |
| `SDL_SetTextureAlphaMod` | `( a n -- n )` | 2 |
| `SDL_SetTextureBlendMode` | `( a n -- n )` | 2 |
| `SDL_DestroyTexture` | `( a -- void )` | 1 |
| `SDL_FreeSurface` | `( a -- void )` | 1 |
| `SDL_PollEvent` | `( a -- n )` | 1 |
| `SDL_WaitEvent` | `( a -- n )` | 1 |
| `SDL_PushEvent` | `( a -- n )` | 1 |
| `SDL_GetMouseState` | `( a a -- n )` | 2 |
| `SDL_GetKeyboardState` | `( a -- a )` | 1 |
| `SDL_OpenAudioDevice` | `( z n a a n -- n )` | 5 |
| `SDL_CloseAudioDevice` | `( n -- void )` | 1 |
| `SDL_PauseAudioDevice` | `( n n -- void )` | 2 |
| `SDL_QueueAudio` | `( n a n -- n )` | 3 |
| `SDL_GetQueuedAudioSize` | `( n -- n )` | 1 |
| `SDL_ClearQueuedAudio` | `( n -- void )` | 1 |

### Mots internes (noyau)

Utilisés par les mots ci-dessus. Listés parce qu'ils sont dans le
vocabulaire et peuvent cacher vos propres noms ou être cachés par eux.

| Mot | Pile | Ce qu'il fait |
|------|-------|--------------|
| `8bit` | `( n -- n )` | Garde les 8 bits de poids faible. |
| `?sdl` | `( n -- )` | Lève une exception avec le texte d'erreur de SDL quand un résultat entier est négatif. |
| `ev` | `( -- a )` | Tampon contenant le `SDL_Event` courant, 64 octets. |
| `ev-type` | `( -- n )` | Type de l'événement dans `ev`. |
| `rect` | `( -- a )` | Tampon `SDL_Rect` partagé (quatre int32: x y w h). |
| `rect-at!` | `( x y w h a -- )` | Remplit un `SDL_Rect` à l'adresse a. |
| `rect!` | `( x y w h -- )` | Remplit `rect`. |
| `px` | `( -- a )` | Tampon de 4 octets utilisé par `pixel@`. |
| `span-x` | `( -- n )` | Valeur. Demi-largeur courante utilisée par `disc` et `circle`. |
| `span-step` | `( i r -- )` | Réduit `span-x` jusqu'à ce qu'il tienne dans le cercle de rayon r à la rangée i. |
| `dots4` | `( cx cy a b -- )` | Trace les quatre points symétriques cx+-a, cy+-b. |
| `key-index` | `( sym scancode -- n )` | Transforme une touche SDL en code de 0 à 255 utilisé par `pressed?`: ASCII tel quel, touches spéciales en 128 plus le code de balayage. |
| `key-event` | `( f -- )` | Range un appui (1) ou un relâchement (0) de touche d'après `ev`. |
| `button-event` | `( f -- )` | Range un appui ou un relâchement de bouton de souris d'après `ev`. |
| `handle-event` | `( -- )` | Aiguille l'événement de `ev` selon son type. |
| `keys` | `( -- a )` | Table de 256 octets des touches et boutons tenus. |
| `fold-case` | `( k -- k )` | Met en minuscule le code d'une lettre majuscule. |
| `quitting` | `( -- n )` | Valeur. Indicateur de sortie derrière `quit?`. |
| `close-hook` | `( -- xt )` | Valeur. xt facultatif exécuté par `close-screen`; le mixer s'en sert. |
| `last-flip` | `( -- ms )` | Valeur. Heure du dernier `flip`. |
| `make-renderer` | `( -- a )` | Crée le moteur de rendu, accéléré avec synchro verticale, sinon logiciel. |
| `audio-dev` | `( -- n )` | Valeur. Identifiant du périphérique audio des bips, 0 quand fermé. |
| `audio-spec` | `( -- a )` | Tampon `SDL_AudioSpec`, 32 octets. |
| `seed` | `( -- n )` | Valeur. État du générateur de bruit. |
| `noise-sample` | `( -- n )` | Échantillon de bruit suivant. |
| `wave-sample` | `( phase -- n )` | Échantillon de la `wave` courante à la phase 0 à 65535. |
| `fill-beep` | `( freq n buf -- )` | Écrit n échantillons d'une tonalité dans buf, avec un court fondu de sortie. |
| `base-name` | `( a n -- a' n' )` | Un nom de fichier sans ses dossiers. |
| `audio-hook` | `( -- xt )` | Valeur. xt facultatif exécuté par `flip` et `delay`; il est fixé à `pump`. |
| `run-audio-hook` | `( -- )` | Exécute `audio-hook` s'il est fixé. |
| `#voices` | `( -- 8 )` | Constante. Nombre de coups pouvant jouer à la fois. |
| `voice-size` | `( -- n )` | Constante. Octets d'une voix: 7 cellules (phase, fréquence, échantillons restants, échantillons au total, onde, chute de hauteur, volume). |
| `voices` | `( -- a )` | Table des voix. |
| `voice` | `( i -- a )` | Adresse de la voix i. |
| `clear-voices` | `( -- )` | Arrête tous les coups sans toucher à la file audio. |
| `left@` | `( i -- n )` | Échantillons restant à jouer dans la voix i, 0 quand elle est inactive. |
| `voices-active` | `( -- n )` | Nombre de coups en train de jouer. |
| `voice-free` | `( -- i )` | Une voix inactive, sinon celle qui est la plus proche de sa fin. |
| `start-voice` | `( freq ms -- )` | Démarre un coup dans une voix sans le mélanger encore (`drum-hit` est ceci plus `pump`). |
| `pitch-at` | `( freq drop elapsed total -- hz )` | Fréquence en cours de route d'un coup dont la hauteur tombe de drop pour cent. |
| `mix` | `( -- a )` | 4096 cellules où les voix sont additionnées. |
| `mixed` | `( -- a )` | 4096 échantillons de 16 bits, le mélange prêt à être mis en file. |
| `latency` | `( -- n )` | Constante 2205. Échantillons gardés en file à l'avance, 50 ms. |
| `mix-voice` | `( v n -- )` | Additionne jusqu'à n échantillons de la voix v dans `mix`. |
| `render` | `( n buf -- )` | Mélange les voix dans n échantillons de 16 bits de buf, écrêtés. |
| `pump` | `( -- )` | Complète la file audio jusqu'à `latency` échantillons avec le mélange des voix, sauf si aucune ne joue. |

## Images (sdl2-image)

Tapez `sdl2-image` une fois, puis chargez les images après `screen`.

| Mot | Pile | Ce qu'il fait |
|------|-------|--------------|
| `load-image` | `( a n -- img )` | Charge un fichier PNG, JPG, BMP... dans une texture. Un nom avec des dossiers qui n'est pas trouvé est réessayé sans eux, donc un programme retrouve ses fichiers à côté de lui. Lève une exception avec le message de SDL en cas d'échec. Ouvrez d'abord l'écran. |
| `free-image` | `( img -- )` | Détruit la texture. |
| `image-size` | `( img -- w h )` | Taille de l'image en pixels. |
| `draw` | `( img x y -- )` | Dessine l'image à sa taille naturelle, coin supérieur gauche en x y. |
| `draw-size` | `( img x y w h -- )` | Dessine l'image étirée à w par h. |
| `draw-part` | `( img sx sy sw sh x y w h -- )` | Dessine la partie sx sy sw sh de l'image dans x y w h. Pour les planches de sprites et les tuiles. |
| `image-alpha` | `( img a -- )` | Fixe l'opacité de l'image, 0 à 255. |
| `image-tint` | `( img $rrggbb -- )` | Multiplie les couleurs de l'image par une couleur. |
| `sdlimg` | `( z n "name" -- )` | Poignée de libSDL2_image faite par `shared-library`. |

### Liaisons C brutes (image)

| Mot | Pile | Args |
|------|-------|------|
| `IMG_LoadTexture` | `( a z -- a )` | 2 |

### Mots internes (image)

| Mot | Pile | Ce qu'il fait |
|------|-------|--------------|
| `try-image` | `( a n -- img, or 0 )` | Charge une texture depuis un nom de fichier, 0 si cela échoue. |
| `srect` | `( -- a )` | Tampon `SDL_Rect` source utilisé par `draw-part`. |
| `isize` | `( -- a )` | Tampon de 8 octets utilisé par `image-size`. |

## Texte (sdl2-ttf)

Tapez `sdl2-ttf` une fois. Le texte est dessiné dans la couleur
courante.

| Mot | Pile | Ce qu'il fait |
|------|-------|--------------|
| `load-font` | `( a n pt -- font )` | Ouvre un fichier TrueType à pt points, en initialisant SDL_ttf la première fois. Lève une exception en cas d'échec. |
| `font` | `( font -- )` | Choisit la police utilisée par `text` et `text-size`. |
| `free-font` | `( font -- )` | Ferme la police. |
| `text` | `( x y a n -- )` | Dessine la chaîne avec la police et la couleur courantes, coin supérieur gauche en x y. Construit une texture à chaque appel: bien pour les scores et les menus. |
| `text-size` | `( a n -- w h )` | Taille que prendrait la chaîne avec la police courante. |
| `sdlttf` | `( z n "name" -- )` | Poignée de libSDL2_ttf faite par `shared-library`. |

### Liaisons C brutes (texte)

L'argument `SDL_Color` de `TTF_RenderUTF8_Blended` est passé empaqueté,
comme `pen`.

| Mot | Pile | Args |
|------|-------|------|
| `TTF_Init` | `( -- n )` | 0 |
| `TTF_Quit` | `( -- void )` | 0 |
| `TTF_OpenFont` | `( z n -- a )` | 2 |
| `TTF_CloseFont` | `( a -- void )` | 1 |
| `TTF_SizeUTF8` | `( a z a a -- n )` | 4 |
| `TTF_RenderUTF8_Blended` | `( a z n -- a )` | 3 |

### Mots internes (texte)

| Mot | Pile | Ce qu'il fait |
|------|-------|--------------|
| `current-font` | `( -- font )` | Valeur. Police choisie par `font`. |
| `tsize` | `( -- a )` | Tampon de 8 octets utilisé pour les tailles. |

## Son (sdl2-mixer)

Tapez `sdl2-mixer` une fois. À ne pas confondre avec `beep`, qui est
dans le noyau.

| Mot | Pile | Ce qu'il fait |
|------|-------|--------------|
| `soundfont` | `( a n -- )` | Utilise un fichier soundfont (.sf2 ou .sf3) pour la musique MIDI. Le nom est essayé tel quel, puis sans ses dossiers dans le dossier courant, puis dans le dossier du script, donc une soundfont posée à côté du programme est trouvée de partout. Lève une exception si elle n'est pas trouvée. Sans elle FluidSynth utilise la soundfont du système. |
| `soundfont?` | `( a n -- f )` | Pareil, mais renvoie un indicateur au lieu de lever une exception: vrai quand le fichier a été trouvé et fixé. |
| `load-sound` | `( a n -- snd )` | Charge un effet WAV, OGG, MP3 ou FLAC. Ouvre le mixer la première fois. Lève une exception en cas d'échec. |
| `play` | `( snd -- )` | Joue le son sur un canal libre. Les sons se chevauchent et sont mélangés. |
| `free-sound` | `( snd -- )` | Libère le son. |
| `sound-volume` | `( n -- )` | Volume de tous les canaux, 0 à 128. |
| `stop-sounds` | `( -- )` | Arrête tous les sons en cours. |
| `sounds-playing` | `( -- n )` | Nombre de canaux en train de jouer. |
| `load-music` | `( a n -- mus )` | Charge un fichier de musique. Ouvre le mixer la première fois. Lève une exception en cas d'échec. |
| `music` | `( mus -- )` | Joue la musique, en boucle jusqu'à `stop-music`. |
| `music-once` | `( mus -- )` | Joue la musique une seule fois. |
| `stop-music` | `( -- )` | Arrête la musique. |
| `music-volume` | `( n -- )` | Volume de la musique, 0 à 128. |
| `music?` | `( -- f )` | Vrai tant que la musique joue. |
| `free-music` | `( mus -- )` | Libère la musique. |
| `sdlmix` | `( z n "name" -- )` | Poignée de libSDL2_mixer faite par `shared-library`. |

### Constantes (son)

| Constante | Valeur |
|----------|-------|
| `MIX_RATE` | `44100` |
| `MIX_CHANNELS` | `2` |

### Liaisons C brutes (son)

| Mot | Pile | Args |
|------|-------|------|
| `Mix_OpenAudio` | `( n n n n -- n )` | 4 |
| `Mix_CloseAudio` | `( -- void )` | 0 |
| `Mix_LoadWAV_RW` | `( a n -- a )` | 2 |
| `Mix_FreeChunk` | `( a -- void )` | 1 |
| `Mix_PlayChannelTimed` | `( n a n n -- n )` | 4 |
| `Mix_Volume` | `( n n -- n )` | 2 |
| `Mix_HaltChannel` | `( n -- n )` | 1 |
| `Mix_Playing` | `( n -- n )` | 1 |
| `Mix_LoadMUS` | `( z -- a )` | 1 |
| `Mix_FreeMusic` | `( a -- void )` | 1 |
| `Mix_PlayMusic` | `( a n -- n )` | 2 |
| `Mix_HaltMusic` | `( -- n )` | 0 |
| `Mix_VolumeMusic` | `( n -- n )` | 1 |
| `Mix_PlayingMusic` | `( -- n )` | 0 |
| `Mix_SetSoundFonts` | `( z -- n )` | 1 |

### Mots internes (son)

| Mot | Pile | Ce qu'il fait |
|------|-------|--------------|
| `path-buf` | `( -- a )` | Tampon de 512 octets contenant le nom trouvé à côté du script. |
| `beside-script` | `( a n -- a' n' f )` | Le nom de fichier dans le dossier du script en cours; f est vrai quand ce fichier existe. |
| `find-file` | `( a n -- a' n' f )` | Cherche un fichier tel que donné, puis par son seul nom dans le dossier courant, puis à côté du script. |
| `mixer-init` | `( -- )` | Ouvre le mixer à 44100 Hz, 16 bits, stéréo, s'il n'est pas déjà ouvert. |
| `mixer-open?` | `( -- f )` | Vrai quand le mixer est ouvert. |
| `mixer-open` | `( -- n )` | Valeur. Indicateur d'ouverture du mixer. |
| `mixer-close` | `( -- )` | Ferme le mixer. Installé dans `close-hook`. |

## Airs (sdl2-abc)

Tapez `sdl2-abc` une fois (il charge `sdl2-mixer` si besoin). Un air
écrit en notation ABC (voir abcnotation.com) est lu, transformé en
fichier MIDI dans un fichier temporaire (supprimé une fois chargé) et
joué par SDL2_mixer via FluidSynth, avec la soundfont donnée par
`soundfont` ou celle du système. Les mêmes mots existent dans la version
web, où de simples oscillateurs jouent les notes.

| Mot | Pile | Ce qu'il fait |
|------|-------|--------------|
| `abc` | `( a n -- )` | Lit un air écrit en notation ABC (une chaîne, plusieurs lignes). Compris: les champs K: (tonalité et mode), L:, M:, Q:, la ligne `%%MIDI program n`; les notes `A`-`G` et `a`-`g` avec `^ _ =` et `, '`, des durées comme `2`, `/2`, `3/2`; les silences `z x`; les accords `[CEG]`; les liaisons `-`; le rythme brisé `> <`; les nolets `(3`; les barres de mesure. Paroles, décorations, appoggiatures, reprises, voix et autres champs sont ignorés: une voix, une ligne mélodique. Fixe `tune-bpm` et `tune-program` d'après l'air. Utilisez `r~ ... ~` pour une chaîne multiligne, car l'ABC emploie la barre verticale. |
| `tune-play` | `( -- )` | Joue une fois l'air lu par `abc`, en MIDI par la soundfont. |
| `tune-loop` | `( -- )` | Le rejoue sans cesse jusqu'à `tune-stop`. |
| `tune-stop` | `( -- )` | Arrête l'air. |
| `tune-playing?` | `( -- f )` | Vrai tant que l'air joue. |
| `abc-play` | `( a n -- )` | `abc` puis `tune-play`. |
| `abc-loop` | `( a n -- )` | `abc` puis `tune-loop`. |
| `tune-notes` | `( -- n )` | Nombre de notes de l'air (chaque note d'un accord compte). 1024 au plus sont gardées. |
| `tune-ms` | `( -- ms )` | Durée de l'air en millisecondes à `tune-bpm`. |
| `tune-bpm` | `( -- n )` | Valeur. Noires par minute, fixé par `Q:` (120 par défaut). Changez-le après `abc` pour jouer plus vite ou plus lentement. |
| `tune-program` | `( -- n )` | Valeur. Instrument General MIDI 0 à 127, fixé par `%%MIDI program` (0 par défaut). Changez-le après `abc` pour en choisir un autre. |
| `tune-midi` | `( -- a n )` | L'air sous forme des octets d'un fichier MIDI standard (format 0, 480 ticks par noire). |
| `tune-save` | `( a n -- )` | Écrit l'air dans un fichier `.mid` de ce nom. |

### Mots internes (airs)

Le lecteur lui-même vit dans le vocabulaire `abc-int`, qui n'est pas
cherché par défaut.

| Mot | Pile | Ce qu'il fait |
|------|-------|--------------|
| `abc-int` | `( -- )` | Vocabulaire contenant le lecteur de notation ABC: environ 80 mots (`token`, `note`, `chord`, `read-key`, les tableaux `nt nd nk` des notes...). Gardé à part pour ne pas cacher vos propres noms; `also abc-int words` les liste. |
| `smf` | `( -- a )` | Tampon de 32 Ko où le fichier MIDI est construit. |
| `smf-n` | `( -- n )` | Valeur. Octets écrits dans `smf`. |
| `smf-t` | `( -- n )` | Valeur. Heure du dernier événement écrit, en ticks. |
| `smf-len` | `( -- n )` | Valeur. Endroit où la longueur de la piste est écrite. |
| `sb,` | `( byte -- )` | Ajoute un octet au fichier MIDI. |
| `sbe32,` | `( n -- )` | Ajoute un nombre de 32 bits, octet de poids fort d'abord. |
| `(vlq)` | `( n flag -- )` | Ajoute un nombre de longueur variable, groupes de poids fort d'abord. |
| `vlq,` | `( n -- )` | Ajoute un nombre de longueur variable MIDI. |
| `emit-group` | `( first -- next )` | Écrit les notes qui commencent ensemble: toutes allumées, puis toutes éteintes. |
| `build-smf` | `( -- )` | Construit dans `smf` le fichier MIDI de l'air. |
| `tune-music` | `( -- mus )` | Valeur. La musique en train de jouer, 0 pour aucune. |
| `tune-path` | `( -- a n )` | Crée un nouveau fichier vide avec un nom propre dans `/tmp` (avec `mkstemps`, donc sans conflit possible avec un autre utilisateur ou une autre exécution) et donne son nom. |
| `tune-load` | `( -- )` | Écrit le fichier MIDI dans un fichier temporaire, le charge comme musique et supprime le fichier. |
| `c-mkstemps` | `( z n -- n )` | Le `mkstemps` du C: fait un fichier unique à partir d'un modèle finissant par XXXXXX. |
| `c-close` | `( n -- n )` | Le `close` du C. |
| `tmpl` | `( -- a )` | Tampon de 64 octets pour le modèle et le nom du fichier temporaire. |
| `tune-code` | `( -- n )` | Valeur. Code d'erreur gardé pendant la suppression du fichier temporaire. |

## Dessiner à l'invite, et les cartes (rpi, efi, web)

`graphic` fait marcher les mots de dessin à l'invite de la console en
matériel nu (Raspberry Pi, PC UEFI), de la fenêtre console (`--gui`) et
du terminal web, sans `screen`: une zone de dessin est montrée sous
(web: au-dessus de) le texte, qui reste utilisable. `0 0 100 100 line`,
`red color`, `cls`. Les mots ci-dessous existent dans les mots courts de
ces versions; ils sont listés ici avec les autres.

### Zone de dessin

| Mot | Pile | Ce qu'il fait |
|------|-------|--------------|
| `graphic` | `( -- )` | Met `sdl2` dans l'ordre de recherche et ouvre la zone de dessin (`gfx-on`), 640 par 480 pixels logiques. |
| `gfx-on` | `( -- )` | Ouvre la zone de dessin sous le texte de la console (matériel nu) et fixe `width`, `height` et `gfx-mode`. |
| `graphic-size` | `( w h -- )` | Ouvre la zone de dessin avec une autre taille, jusqu'à 1920 par 1440. L'écran de la console lui-même fait 640 par 480 fois `console-zoom`. Web: `graphic-on` fait la même chose avec 640 par 480. |
| `gfx-mode` | `( -- f )` | Valeur. Vrai (-1) tant que les mots de dessin marchent sous la console. |
| `gfx-cls` | `( -- )` | À l'invite: efface la zone de dessin en noir, quel que soit le crayon. `cls` l'appelle. |
| `graphic-on` | `( -- )` | Web seulement: `640 480 graphic-size`. |

### Touches sur les cartes (interne)

La ligne série des cartes est lue comme un flot de caractères, sans
relâchements de touches (un clavier USB est lu par du code C, voir
`pressed?`): une touche compte comme tenue pendant `key-hold`
millisecondes après son dernier appui.

| Mot | Pile | Ce qu'il fait |
|------|-------|--------------|
| `key-hold` | `( -- n )` | Valeur, 120 par défaut. Durée pendant laquelle une touche compte comme tenue après son dernier appui, en ms. |
| `key-times` | `( -- a )` | Tableau de 256 cellules: l'heure (`ticks`) du dernier appui de chaque code de touche. |
| `key-hit` | `( k -- )` | Note l'appui de la touche k: fixe `last-key` et `key-times`. |
| `forget-keys` | `( -- )` | Marque toutes les touches comme non pressées. |
| `key-within` | `( ms -- k\|-1 )` | Attend au plus ms un octet de la ligne série: l'octet, ou -1 s'il n'en vient pas à temps. |
| `one-key` | `( c -- )` | Note un caractère c reçu sur la ligne série comme un appui de touche (`key-hit`): les lettres deviennent minuscules, 10 est `key-enter`, 127 `key-backspace`, ESC lance `escape`. |
| `escape` | `( -- c\|-1 )` | Après un ESC: lit ce qui suit, une flèche ou la touche Échap. |
| `arrow` | `( c -- )` | Note la flèche nommée par la lettre qui termine une séquence d'échappement: A `key-up`, B `key-down`, C `key-right`, D `key-left`. |
| `ticks>ms` | `( ticks -- ms )` | Convertit la durée d'une note en ticks en millisecondes, d'après `tune-bpm` (airs ABC). |
| `tune-level` | `( -- n )` | Valeur, 2500 par défaut. Amplitude des notes d'un air, 0 à 32767. |
| `tune-send` | `( loop -- )` | Envoie les notes de l'air courant au mixer, une fois (0) ou en boucle (-1). Utilisé par `tune-play` et `tune-loop`. |

### Le son sur le PC (efi)

| Mot | Pile | Ce qu'il fait |
|------|-------|--------------|
| `sound-off` | `( -- )` | Arrête le son (le contrôleur Intel HD Audio est réinitialisé) et écrit `sound = off` dans `lugforth.ini` à la racine de la clé: lu au démarrage. Pour un PC dont le codec fait du bruit. |
| `sound` `sound-on` | `( -- )` | Permet de nouveau le son, écrit `sound = on`. Le contrôleur démarre au prochain mot de son. |
| `snd-ok` | `( -- f )` | Démarre le son si besoin et dit si un contrôleur a répondu. |

## Alias

Autres noms d'un même mot, comme les appellent d'autres Forth et BASIC.
Un alias est une simple définition en deux-points qui appelle le mot,
donc il marche partout où `sdl2` marche (Linux, web, rpi, efi).
Ajoutez-en ici.

Où les changer: un alias tient en une ligne, `: plot ( x y -- ) dot ;`,
écrite juste après le mot qu'elle renomme dans chacune des trois sources
des mots courts, qui doivent rester égales:

| Version | Source |
|-------|--------|
| Linux (et la fenêtre console) | `ports/posix/sdl2.fs` |
| Raspberry Pi et PC (UEFI) | `ports/baremetal/sdl2.fs` |
| Web | `ports/web/sdl2.fs` (la version web a aussi une souche plus bas, `: plot graphic s" plot" evaluate ;`, pour que le mot marche à l'invite avant `graphic`) |

Ajoutez ensuite la ligne au tableau ci-dessus, et le mot à l'index
alphabétique à la fin de cette page.

D'où viennent les autres sections de cette page: ce fichier est écrit à
la main, section par section, d'après les sources: Noyau, Images, Texte,
Son et Airs (`ports/posix/sdl2.fs`, `sdl2_image.fs`, `sdl2_ttf.fs`,
`sdl2_mixer.fs`, `sdl2_abc.fs`); les mots des cartes et de la page web
(`ports/baremetal/sdl2.fs`, `ports/web/sdl2.fs`,
`ports/web/sdl2_abc.fs`); `sound-off` et `snd-ok`
(`ports/efi/sound_ini.fs`, `ports/efi/hda.h`). La liste générée des
autres mots est `docs/WORDS.md` (voir `docs/dev/PITFALLS.md`, « Adding
words »).

| Alias | Pareil que | Pourquoi |
|-------|---------|-----|
| `plot` | `dot` `( x y -- )` | Les programmes Forth et BASIC disent `plot` pour un pixel. |

## Index alphabétique

`abc` `abc-loop` `abc-play` `active-hook` `alpha` `arrow` `audio-open`
`audio-open?` `beep` `beep-volume` `beeping?` `black` `blue` `box`
`brown` `c-close` `c-mkstemps` `circle` `close-screen` `cls` `color`
`cursor` `cyan` `delay` `disc` `dot` `draw` `draw-part` `draw-size`
`drum-hit` `dt` `escape` `events` `flip` `font` `forget-keys` `frame`
`free-font` `free-image` `free-music` `free-sound` `fullscreen`
`gfx-cls` `gfx-mode` `gfx-on` `graphic` `graphic-on` `graphic-size`
`gray` `green` `height` `hline` `image-alpha` `image-size` `image-tint`
`IMG_LoadTexture` `ink` `key-backspace` `key-down` `key-enter` `key-esc`
`key-hit` `key-hold` `key-left` `key-right` `key-space` `key-tab`
`key-times` `key-up` `key-within` `last-key` `latency` `LEFT-BUTTON`
`line` `load-font` `load-image` `load-music` `load-sound` `magenta`
`MIDDLE-BUTTON` `Mix_CloseAudio` `Mix_FreeChunk` `Mix_FreeMusic`
`Mix_HaltChannel` `Mix_HaltMusic` `Mix_LoadMUS` `Mix_LoadWAV_RW`
`Mix_OpenAudio` `Mix_PlayChannelTimed` `Mix_Playing` `Mix_PlayingMusic`
`Mix_PlayMusic` `Mix_SetSoundFonts` `Mix_Volume` `Mix_VolumeMusic`
`mouse-x` `mouse-y` `music` `music-once` `music-volume` `music?` `noise`
`one-key` `orange` `pen` `pen-a` `pen-b` `pen-g` `pen-r` `pink`
`pitch-drop` `pixel@` `play` `plot` `pressed?` `purple` `quit!` `quit?`
`red` `render-hook` `renderer` `rgb` `rgba` `RIGHT-BUTTON` `sample-rate`
`saw` `scan?` `screen` `screen-open?` `sdl` `sdl-error` `sdl-shutdown`
`SDL_ClearQueuedAudio` `SDL_CloseAudioDevice` `SDL_CreateRenderer`
`SDL_CreateTexture` `SDL_CreateTextureFromSurface` `SDL_CreateWindow`
`SDL_Delay` `SDL_DestroyRenderer` `SDL_DestroyTexture`
`SDL_DestroyWindow` `SDL_FreeSurface` `SDL_GetError`
`SDL_GetKeyboardState` `SDL_GetMouseState` `SDL_GetQueuedAudioSize`
`SDL_GetTicks` `SDL_Init` `SDL_InitSubSystem` `SDL_OpenAudioDevice`
`SDL_PauseAudioDevice` `SDL_PollEvent` `SDL_PushEvent`
`SDL_QueryTexture` `SDL_QueueAudio` `SDL_RenderClear` `SDL_RenderCopy`
`SDL_RenderDrawLine` `SDL_RenderDrawPoint` `SDL_RenderDrawRect`
`SDL_RenderFillRect` `SDL_RenderPresent` `SDL_RenderReadPixels`
`SDL_RenderSetLogicalSize` `SDL_RWFromFile` `SDL_SetHint`
`SDL_SetRenderDrawBlendMode` `SDL_SetRenderDrawColor`
`SDL_SetTextureAlphaMod` `SDL_SetTextureBlendMode`
`SDL_SetTextureColorMod` `SDL_SetWindowFullscreen` `SDL_SetWindowTitle`
`SDL_ShowCursor` `SDL_UpdateTexture` `SDL_WaitEvent` `sdlimg` `sdlmix`
`sdlttf` `setting` `silence` `sine` `snd-ok` `sound` `sound-off`
`sound-on` `sound-volume` `soundfont` `soundfont?` `sounds-playing`
`square` `stop-music` `stop-sounds` `text` `text-size` `ticks`
`ticks>ms` `title` `triangle` `TTF_CloseFont` `TTF_Init` `TTF_OpenFont`
`TTF_Quit` `TTF_RenderUTF8_Blended` `TTF_SizeUTF8` `tune-bpm`
`tune-level` `tune-loop` `tune-midi` `tune-ms` `tune-notes` `tune-play`
`tune-playing?` `tune-program` `tune-save` `tune-send` `tune-stop`
`wait` `wave` `wheel` `white` `width` `window-handle` `yellow` `zoom`
