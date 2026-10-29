---
name: regen-docs
description: Regenerate the generated docs (WORDS.md, PDF, website) after adding words or editing docs; use whenever a word is added or docs/*.md changes
---

- New or changed words: `ninja posix` then `python3 tools/words_reference.py` (rewrites `docs/en/WORDS.md` from the built
  interpreters; descriptions come from `tools/words_notes.py` and `attic/oldsite/`). Add a note in `words_notes.py` for
  a word that has no description.
- Docs edited: `python3 tools/make_pdf.py` (pandoc + xelatex) rebuilds `docs/lugforth_manual.pdf`.
- Website: `ninja posix web`, then `python3 tools/make_pages.py` (pandoc) builds `out/pages`.
- The English docs live in `docs/en/`; the French translation lives in `docs/fr/` (hand written, same file names with the suffix `_fr`: `MANUAL_fr.md`, so that they can share a flat folder, a stick): when an English doc changes,
  change its French twin in the same change set.
- Then bump the version (skill `bump-version`).
