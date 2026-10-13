# Moteur d'inférence

`../inference.fs` est un petit moteur d'inférence à chaînage avant : il applique des règles
« si... alors... » à des faits connus, jusqu'à ce qu'il n'y ait plus rien de nouveau à déduire. C'est
un portage du projet INFERENCE de Marc Petremann
([eForth Windows](https://github.com/MPETREMANN11/eForth-Windows), licence GNU GPL) : les faits et
la définition des règles sont de lui, la boucle `infer` est nouvelle.

Fichiers (dans `examples/`) : `inference.fs` (le moteur), `weather.fs` et `lamp_diagnosis.fs`
(exemples), `inference_tests.fs` et `lamp_diagnosis_tests.fs` (tests, `make test-ports`).
Les noms des mots et des faits restent en anglais.

## Les faits

Un fait est une valeur nommée, vrai (`-1`) ou faux (`0`). `FACT:` le définit, `to` le change.

```forth
FALSE FACT: rain
TRUE  FACT: cold
-1 to rain
rain .             \ -1
```

Les faits sont dans le vocabulaire `facts` : tape `also facts` pour les utiliser à l'invite.

## Les règles

Une règle est une condition (un mot qui laisse un drapeau) et une action (un mot qui change des
faits). `:CONDITION` et `:ACTION` ouvrent deux mots anonymes, `RULE:` nomme le couple et le range
dans le vocabulaire `rules`.

```forth
:CONDITION  rain cold and ;      \ si il pleut ET il fait froid
:ACTION     TRUE to ice ;        \ alors il y a de la glace
RULE: r-ice
```

## La boucle `infer`

1. Toutes les règles sont marquées « pas encore exécutée ».
2. On essaie les règles l'une après l'autre. Une règle qui n'a pas encore tourné et dont la condition
   est vraie exécute son action et est marquée exécutée.
3. Si au moins une règle a tourné pendant ce parcours, on retourne au point 2.
4. Sinon on s'arrête.

Les règles se **chaînent** donc : l'action d'une règle peut rendre vraie la condition d'une autre,
même définie avant elle, et l'ordre de définition n'a pas d'importance. Une règle ne tourne qu'une
fois par `infer`, ce qui garantit que la boucle se termine.

Autres mots : `.facts` affiche chaque fait et sa valeur, `clear-facts` les met tous à faux, `.rules`
compte les règles, `clear-rules` les oublie, `reset-rules` les marque toutes « pas exécutée ».

## Exemple 1 : la météo (`weather.fs`)

Faits : `cold` (froid), `rain` (pluie), `ice` (glace), `snow` (neige), `umbrella` (parapluie),
`slippery` (glissant).

| Règle | Condition | Action |
|---|---|---|
| `r-ice` | pluie ET froid | `ice` |
| `r-snow` | pluie ET froid ET pas de glace | `snow` |
| `r-slippery` | glace | `slippery` |
| `r-umbrella` | pluie ET PAS froid | `umbrella` |

```forth
include examples/inference.fs
include examples/weather.fs
also facts
-1 to rain  -1 to cold
infer
.facts
```

Résultat : `ice = -1`, `slippery = -1`, `snow = 0`, `umbrella = 0`.
Premier passage : `r-ice` s'exécute et la glace apparaît ; `r-snow` est essayée après, voit la glace,
donc `snow` reste faux ; `r-slippery` voit la glace et s'exécute. Deuxième passage : rien ne tourne,
le moteur s'arrête. Avec seulement `-1 to rain`, seule `r-umbrella` s'exécute.

## Exemple 2 : une lampe qui ne s'allume pas (`lamp_diagnosis.fs`)

Faits observés : `no-light` (pas de lumière), `socket-ok` (la prise marche). Conclusions :
`change-bulb` (changer l'ampoule), `mains-failure` (panne de secteur).

```forth
include examples/inference.fs
include examples/lamp_diagnosis.fs
also facts
-1 to no-light  -1 to socket-ok
infer  .facts
```

`r-bulb` (pas de lumière ET la prise marche) conclut `change-bulb = -1`. Avec la prise hors service
(seulement `-1 to no-light`), `r-mains` conclut `mains-failure = -1`. Pour un nouveau cas, appelle
`clear-facts` avant de poser les nouveaux faits.

## Essayer : `make`

`make` dans ce dossier lance sept scénarios concrets (météo, lampe, chaîne de règles), affiche les faits après `infer` et vérifie les conclusions attendues (`-> OK`), puis les tests unitaires. `make weather-rain-cold`, `make lamp-bulb`... en lancent un seul.

## Écrire les tiens

```forth
include examples/inference.fs
FALSE FACT: a   FALSE FACT: b   FALSE FACT: c
:CONDITION a ;   :ACTION TRUE to b ;   RULE: a-gives-b
:CONDITION b ;   :ACTION TRUE to c ;   RULE: b-gives-c
also facts  -1 to a  infer  .facts      \ a, b et c sont vrais : une chaîne de deux règles
```

## Limites

* Le moteur est à **chaînage avant** : il va des faits vers leurs conséquences. Il ne sait pas répondre
  à « que faudrait-il pour obtenir X ? ».
* Les faits sont vrai ou faux ; un fait peut contenir un nombre, mais le moteur n'en fait rien de
  particulier.
* Au plus 64 faits et 64 règles (`MAXFACTS`, `MAXRULES` dans `inference.fs`).
