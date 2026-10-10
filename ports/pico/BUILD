# Copyright 2024 Bradley D. Nelson
# Modifications Copyright 2026 Luginfo10
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

# Plain Raspberry Pi Pico (RP2040) port, adapted by Luginfo10 from ports/pico-ice/BUILD.
# This port is not part of upstream uEforth and is not endorsed by its author.
# Shares the pico-sdk submodule (and its checkout rules) with pico-ice.

if not PICO_ICE_ENABLED:
  Return()

PICO_FILES = []
PICO_ZIP_FILES = []

# Main files.
PICO_ZIP_FILES += [
  Importation('$dst/pico/lugforth-pico/README.txt',
              '$src/ports/pico/README.txt'),
  Copy('$dst/pico/lugforth-pico/lugforth-pico.uf2',
       '$dst/pico/lugforth_pico.uf2'),
  Copy('$dst/pico/lugforth-pico/LICENSE', '$src/LICENSE'),
  Copy('$dst/pico/lugforth-pico/pico-sdk-LICENSE.TXT',
       '$src/ports/pico-ice/pico-sdk/LICENSE.TXT'),
]

# Build the uf2.
Command(' '.join(['$dst/pico/lugforth_pico.uf2',
                  '$dst/pico/lugforth_pico.err']),
        ' '.join(['$dst/pico/build.ninja',
                  '$dst/gen/pico_boot.h']),
        'ninja -C $dst/pico lugforth_pico >$dst/pico/lugforth_pico.err 2>&1')
Command(' '.join(['$dst/pico/build.ninja',
                  '$dst/pico/cmake.err']),
        ' '.join(['$src/ports/pico-ice/pico-sdk/README.md',
                  '$src/ports/pico-ice/pico-sdk/lib/tinyusb/README.rst']),
        'cmake $dst/pico -G Ninja -S ports/pico -B $dst/pico >$dst/pico/cmake.err 2>&1')

# Zip it.
PICO_FILES += [
  Zip('$dst/pico/lugforth-pico.zip', PICO_ZIP_FILES,
      base='$dst/pico/lugforth-pico'),
]

# Simulator.
PICO_FILES += [
  CompileSim('$dst/pico-sim/lugforth_pico_sim', '$src/ports/pico/main.c',
             implicit=['$dst/gen/pico_boot.h'])
]
Importation('$dst/gen/pico_boot.h', '$src/ports/pico/pico_boot.fs', name='boot')

# Main Alias.
Alias('rpi-pico', ' '.join(PICO_FILES))
if not FAST:
  Default('rpi-pico')
