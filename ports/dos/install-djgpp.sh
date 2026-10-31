#!/bin/sh
# Copyright 2026 Luginfo10
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

# Installs the DJGPP cross compiler (i586-pc-msdosdjgpp-gcc) that ports/dos
# needs. Debian and Ubuntu have no package for it (only binutils-djgpp, the
# assembler and linker, without gcc or DJGPP's libc), so this takes the
# prebuilt one of https://github.com/andrewwutw/build-djgpp (gcc 12.2, for
# Linux x86-64), checks it and unpacks it.
#
#   ports/dos/install-djgpp.sh          into /opt/djgpp-x if it can write
#                                       there, else ~/.local/djgpp-x
#   ports/dos/install-djgpp.sh DIR      into DIR (the compiler in DIR/djgpp)
#
# The Makefile looks in both default places; for another DIR:
#   make -C ports/dos DJGPP=DIR/djgpp
# To run the result: apt install dosbox (or dosbox-x), then make -C ports/dos run.

set -e

URL=https://github.com/andrewwutw/build-djgpp/releases/download/v3.4/djgpp-linux64-gcc1220.tar.bz2
SHA256=8464f17017d6ab1b2bb2df4ed82357b5bf692e6e2b7fee37e315638f3d505f00

if [ -n "$1" ]; then
  dir=$1
elif mkdir -p /opt/djgpp-x 2>/dev/null && [ -w /opt/djgpp-x ]; then
  dir=/opt/djgpp-x
else
  dir=$HOME/.local/djgpp-x
fi

if [ -x "$dir/djgpp/bin/i586-pc-msdosdjgpp-gcc" ]; then
  echo "DJGPP is already in $dir/djgpp"
  "$dir/djgpp/bin/i586-pc-msdosdjgpp-gcc" --version | head -1
  exit 0
fi

case "$(uname -s)-$(uname -m)" in
  Linux-x86_64) ;;
  *) echo "This prebuilt DJGPP is for Linux x86-64; elsewhere build it with build-djgpp.sh of" \
          "https://github.com/andrewwutw/build-djgpp" >&2
     exit 1 ;;
esac

for tool in curl tar bzip2 sha256sum; do
  command -v $tool >/dev/null || { echo "$tool is needed (apt install $tool)" >&2; exit 1; }
done

tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
echo "Downloading $URL"
curl -fL --progress-bar -o "$tmp/djgpp.tar.bz2" "$URL"
echo "$SHA256  $tmp/djgpp.tar.bz2" | sha256sum -c --quiet - || {
  echo "The archive is not the one expected (checksum): nothing installed" >&2; exit 1; }
mkdir -p "$dir"
tar -xjf "$tmp/djgpp.tar.bz2" -C "$dir"
"$dir/djgpp/bin/i586-pc-msdosdjgpp-gcc" --version | head -1
echo "DJGPP installed in $dir/djgpp"
case "$dir" in
  /opt/djgpp-x|"$HOME/.local/djgpp-x") echo "Next: make -C ports/dos" ;;
  *) echo "Next: make -C ports/dos DJGPP=$dir/djgpp" ;;
esac
