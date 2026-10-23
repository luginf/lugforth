#!/bin/sh
# Copies examples/ into $1 for a release archive, without what is not for the user
# (benchmarks, Makefile, ESP32 files, Python helpers).
set -e
mkdir -p "$1"
cp -R examples/. "$1"
(cd "$1" && rm -rf bench Makefile system/*xtensa* system/espnow.fs games/*.py)
