# EditForthLin: ce qu'on pourrait emprunter

*Note en français seulement (pas de version anglaise).*

Notes prises en lisant une copie locale d'EditForthLin
(`/tmp/EditForthLin`, version 2.5.6.2), le 2026-10-29. Rien n'a été
porté. C'est le portage Linux d'EditForth, de la famille Win32Forth
(Windows, macOS, Android, Linux). Le code est annoncé « domaine public »
dans le README, mais il n'y a pas de fichier de licence.

Le noyau est écrit en assembleur ARM64. Deux scripts Python
(`GrokTools/arm64_to_nasm_sysv.py` et `arm64_to_gas_linux.py`) le
traduisent en NASM x64 System V et en GNU as. L'interface est en Qt 6
(`Edit/`, environ 8000 lignes de C++), reliée au Forth par une socket
AF_UNIX (`--companion`). La bibliothèque Forth est dans
`Forth/Resources/Library`.

## Points forts

- Un seul noyau pour toutes les plateformes, traduit par script plutôt
  que réécrit à la main.
- L'« Emitter » (`Library/Emitter`) découpe dans l'image les seuls mots
  utilisés par un programme et en fait un exécutable autonome (fenêtre
  X11 sous Linux, `emit-lin.c`). Directives `EMIT-NO-PAUSE`,
  `EMIT-NO-WINDOW`, `EMIT-NO-WRAPPER` (`Docs/EMIT-AUTO.md`).
- Outils de navigation et de débogage en Forth (`Hyper/hyper.fth`, 1232
  lignes): `VIEW`, `LOCATE`, `SEE`, références croisées `REF`, `XREF`,
  `USEDIN`, `CALLS`, `ANYWORDS`; débogueur avec `BREAK`, `UNBREAK` et
  pas à pas (`Library/Debugger`).
- Bibliothèque variée, en domaine public : OOP à la Win32Forth
  (`Classes/`, McKewan et Zimmer), traducteur Tiny Pascal vers Forth
  (`Pascal/`, Zimmer, 930 lignes), entiers longs (`BigInteger/`), π par
  Chudnovsky (`PI/`), benchmarks classiques (`Benchmarks/`: tri à
  bulles, matrices PLDI '96, crible), textes du standard Forth 2012
  (`Reference/`).
- Tests de conformité ANS-VALIDATE (394 tests) et Hayes, lancés sur
  arm64 et x64 pour la 1.3.1, puis sur arm64 pour la 2.5.6.1.
- Assistant IA local « Ask Forth » (Ollama ou API compatible OpenAI,
  index de la bibliothèque pour répondre).
- Paquets `.deb` amd64 et arm64 prêts à installer.

## Points faibles

- Dépendances lourdes : Qt 6, CMake, NASM, Python. Rien de comparable à
  notre construction (`ninja`, un seul binaire).
- Dépôt encombré (environ 10 Mo): 5,4 Mo de noyaux générés versionnés,
  deux `.deb` (2,5 Mo), une photo JPEG, des fichiers Windows gardés «
  pour référence ».
- Validation inégale : les notes de version disent plusieurs fois «
  Hayes et ANS-VALIDATE n'ont pas été lancés » (2.5.6.0, 2.5.6.2), et la
  2.5.6.2 n'a pas été reconstruite sur arm64.
- Numérotation confuse : versions internes 0.1.x et 2.5.6.2 « marketing
  » en même temps; `REMAINING.md` s'arrête à la 1.3.1.
- Documentation très bavarde : `REMAINING.md` (15 Ko) mélange l'état
  courant et l'historique.
- Hérite de macOS : chemins `~/Documents/EditForthLin`, `.app`,
  `Contents/MacOS`, raccourci ⌥⌘A dans la documentation.
- Pas de graphisme SDL2, de jeux ni de ports embarqués (EFI, Raspberry
  Pi, web, GBA): c'est un environnement de bureau.

## Ce qu'on pourrait en retirer

1. `Classes/oop.fth` et `classes.fth`: une OOP simple en Forth 2012, qui
   manque à Lugforth.
2. `Pascal/PASCAL.fth`: un exemple original pour `examples/utils/`.
3. `BigInteger/big-int.fth` (391 lignes) et `PI/pi-chudnovsky.fth` (306
   lignes): exemples numériques et banc d'essai.
4. `Benchmarks/`: complète `examples/bench/`.
5. `Hyper/` et `Debugger/`: l'idée de `REF`, `USEDIN`, `CALLS` et de
   `BREAK` avec pas à pas dans `ed` ou `writhdeck`. Dépend de l'éditeur,
   donc pas un portage direct.
6. L'idée de l'Emitter (exécutable autonome qui ne contient que les mots
   utilisés): gros chantier, dépend d'un noyau en assembleur, donc idée
   seulement.
7. `Reference/` (textes Forth 2012): pour compléter `docs/` ou vérifier
   la conformité.
8. Une suite ANS-VALIDATE en plus de Hayes (nous passons déjà tout Hayes
   avec `ninja forth2012`).

À vérifier avant de reprendre du code : la licence de chaque fichier.
Dans `Classes` et `Pascal`, le domaine public vient de McKewan, Zimmer
et Win32Forth ; ailleurs, seul le README l'annonce. Tout emprunt va dans
NOTICE.
