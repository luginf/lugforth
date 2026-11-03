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

# Builds the terminal Lugforth inside Termux (Android) and puts it in
# $PREFIX/bin, so that `lugforth` runs from any folder. From a git clone of
# the repository (the build reads the revision with git):
#
#   pkg install git && git clone https://github.com/luginf/lugforth
#   sh lugforth/ports/termux/install.sh          # build and install
#   sh lugforth/ports/termux/install.sh --test   # also run the tests (ninja posix)
#
# It installs what is missing (clang, python, ninja), runs configure.py, which
# sees Termux and builds with clang as a position independent executable, the
# only kind Android runs. See ports/termux/README.md.

set -e

case "$TERMUX_VERSION$PREFIX" in
  *com.termux*|[0-9]*) ;;
  *) echo "This is for Termux (Android); elsewhere: python3 configure.py && ninja posix" >&2
     exit 1 ;;
esac

case "$(uname -m)" in
  aarch64|x86_64) ;;
  *) echo "Warning: $(uname -m) has 32-bit cells; only aarch64 and x86_64 were looked at." >&2 ;;
esac

need=
for tool in clang:clang python3:python ninja:ninja git:git; do
  command -v "${tool%%:*}" >/dev/null || need="$need ${tool#*:}"
done
if [ -n "$need" ]; then
  echo "Installing$need"
  pkg install -y $need
fi

cd "$(dirname "$0")/../.."
[ -d .git ] || { echo "Run this from a git clone of lugforth (the build reads the revision with git)" >&2; exit 1; }

python3 configure.py -q
if [ "$1" = "--test" ]; then
  ninja posix
else
  ninja out/posix/lugforth out/posix/lugforth-mini
fi
install -m 755 out/posix/lugforth "$PREFIX/bin/lugforth"
install -m 755 out/posix/lugforth-mini "$PREFIX/bin/lugforth-mini"
echo "Installed: $PREFIX/bin/lugforth (and lugforth-mini). Type lugforth."
