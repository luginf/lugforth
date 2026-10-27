# Notes pour les développeurs (et pour Claude)

*Traduction française de [docs/dev/README.md](README_fr.md). L'anglais
fait foi: ces notes sont des notes de travail qui bougent à chaque
session.*

Ce qu'une personne ou un agent qui travaille sur Lugforth doit lire
avant de toucher au code. Les règles de travail (branches, commits,
version) sont dans [CLAUDE.md](../../../CLAUDE.md) à la racine du dépôt.

| fichier | quoi |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE_fr.md) | système de compilation (`configure.py`, `ninja`), tests, noyau C et démarrage Forth, arbre des plateformes |
| [FEATURES.md](FEATURES_fr.md) | ce qui existe et comment ça a été testé: mots SDL2, éditeurs (`ed`, `writhdeck`), jeux et portages d'exemples, `graphic`, `play` |
| [PORTS.md](PORTS_fr.md) | Android, Raspberry Pi, PC UEFI, terminaux, version et console web, GBA, le disque en matériel nu |
| [PITFALLS.md](PITFALLS_fr.md) | pièges rencontrés: Forth, chargeurs, SDL2, web, chaînes de démarrage de nouvelles plateformes, astuces de test |
| [IDEAS.md](IDEAS_fr.md) | questions ouvertes, idées restantes, autres Forth |

Pour les utilisateurs: [MANUAL.md](../MANUAL_fr.md) (tutoriel et
référence), [WORDS.md](../../WORDS.md) (tous les mots, en anglais),
[docs/pages](../../pages) (le site web),
[attic/oldsite](../../../attic/oldsite/README.md) (les pages héritées
d'uEforth).
