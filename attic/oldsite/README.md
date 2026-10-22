# attic/oldsite: the documentation website inherited from uEforth (archived)

This folder is not the website of Lugforth. It holds the HTML pages of the original uEforth / ESP32forth site
(Brad Nelson), with its Google App Engine files (`app.yaml`, `eforth.go`, `static/`), kept because:

- the pages describe the words of the standard vocabularies, and `tools/words_reference.py` still reads them
  (with `tools/words_notes.py`) to write the descriptions of [docs/WORDS.md](../../docs/WORDS.md);
- `attic/oldsite/BUILD` could build the deployable copy of the original site (`out/deploy`): the pages are
  templates run through `tools/importation.py` (`head.html`, `menu.html`, `common.html`... are the parts).

They are left as upstream wrote them (and skipped by `tools/rename_upstream.py`): the text still says uEforth and
ESP32forth. Do not edit them to document Lugforth.

The website of Lugforth (https://luginf.github.io/lugforth/) is built from [docs/pages](../../docs/pages) and the
Markdown files of the repository by `tools/make_pages.py`, and published by `.github/workflows/pages.yml`.
