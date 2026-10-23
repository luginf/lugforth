#!/bin/sh
# Copies docs/ into $1 for a release archive (manual, word list, developer notes, images),
# without the sources of the website (docs/pages) and the generated PDF (published on its own).
set -e
mkdir -p "$1"
cp -R docs/. "$1"
(cd "$1" && rm -rf pages lugforth_manual.pdf)
