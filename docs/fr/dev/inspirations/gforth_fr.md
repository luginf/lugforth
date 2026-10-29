# gforth: points forts, points faibles, ce qu'on peut en retirer

*Note en français seulement.*

Notes du 2026-10-29, prises sur le gforth installé ici (0.7.9, version
du 2023-10-12, `/usr/local/share/gforth`) et dans son manuel
(`/usr/local/share/info/gforth.info`). Les mesures ci-dessous sont les
nôtres, sur ce PC, avec un petit banc (fib 32 récursif + une boucle
`do ... loop` de 20 millions d'itérations).

## Chiffres relevés

| | durée du banc | mémoire au démarrage | démarrage |
|---|---|---|---|
| `gforth-fast` | 0,09 s | 10,7 Mo | 0,02 s |
| `gforth` | 0,20 s | 8,5 Mo | 0,02 s |
| lugforth (POSIX) | 0,40 s | 2,3 Mo | 0,03 s |
| pforth | 0,48 s | 1,8 Mo | immédiat |

Installation de gforth: 7,9 Mo (`share`) + 16 Mo (`lib`), plusieurs
moteurs (`gforth`, `gforth-fast`, `gforth-itc`, `gforth-ditc`).

## Points forts

- Le Forth « de référence » de bureau sous licence libre (GPL): conforme
  Forth 2012, très complet, bien documenté (manuel Info avec index des
  mots).
- Rapide: `gforth-fast` est environ 4 fois plus rapide que lugforth sur
  notre banc (et 2 fois pour le `gforth` normal).
- Messages d'erreur utiles: type d'erreur et trace de la pile de retour
  (`backtrac.fs`), `assert.fs`, `debug.fs`, `coverage.fs`.
- Bibliothèques fournies (`/usr/local/share/gforth/.../*.fs`):
  fermetures (`closures.fs`), structures et objets, `complex.fs`,
  `fft.fs`, `csv.fs`, `date.fs`, `hash.fs`, analyseur Gray (`gray.fs`),
  serveur HTTP (`httpd.fs`), tables, locales `{: :}`.
- Interface C par `c-library` (`libcc.fs`): le C est compilé au
  chargement, avec flottants et structures possibles.
- Outils de plateformes: compilateur croisé (`cross.fs`), version « ec »
  pour l'embarqué, support Android (`jni*.fs`).
- Sensible à la casse par défaut, mais des listes de mots insensibles
  existent (`cs-wordlist`).

## Points faibles

- Pas de SDL fourni (nos notes `IDEAS.md` citent des liaisons tierces).
  `c-library` exige gcc et les en-têtes de développement à chaque
  machine; chez nous, `dlopen` ne demande que la bibliothèque `.so`.
- Plus gros et plus lent à installer: autotools, 25 Mo, mémoire 4 à 5
  fois plus grande que lugforth au démarrage.
- Sensible à la casse: `SDL_Quit` et `SDL_QUIT` entrent en conflit (déjà
  noté dans `IDEAS.md`).
- Pas de port sans système d'exploitation (ni EFI, ni Raspberry Pi nu,
  ni Game Boy Advance) ni de version web officielle.
- Pas d'éditeur ni de jeux intégrés: c'est un Forth de développement en
  ligne de commande.
- Licence GPL: on ne peut pas en copier du code dans un projet sous une
  autre licence sans en tenir compte.

## Ce qu'on pourrait en retirer

1. Les **traces d'erreur** (`backtrac.fs`): chez nous une erreur affiche
   seulement `ERROR`; savoir quel mot a échoué et par quel chemin serait
   un vrai progrès.
2. `assert.fs`, `debug.fs`, `coverage.fs`: idées pour les tests.
3. Les **fermetures** (`closures.fs`) et le mot `{: :}` partout.
4. `ans-report.fs` / `2012words.fs`: la liste des mots du standard avec
   ce qui manque, utile pour mesurer notre conformité.
5. `gforth` sert déjà de banc comparatif (`ports/gforth/` fait tourner
   `writhdeck`, `zmachine.fs` et ses tests): garder cette double
   exécution comme garde-fou de portabilité.
6. À comparer, pas à copier (GPL): le modèle d'objets et le compilateur
   croisé.
