# Copyright 2023 Bradley D. Nelson
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

if not PICO_ICE_ENABLED:
  Return()

PICO_ICE_FILES = []
PICO_ICE_ZIP_FILES = []

# Main files.
PICO_ICE_ZIP_FILES += [
  Importation('$dst/pico-ice/lugforth-pico-ice/README.txt',
              '$src/ports/pico-ice/README.txt'),
  Copy('$dst/pico-ice/lugforth-pico-ice/lugforth-pico-ice.uf2',
       '$dst/pico-ice/lugforth_pico_ice.uf2'),
  Copy('$dst/pico-ice/lugforth-pico-ice/LICENSE', '$src/LICENSE'),
  Copy('$dst/pico-ice/lugforth-pico-ice/pico-ice-sdk-LICENSE.md',
       '$src/ports/pico-ice/pico-ice-sdk/LICENSE.md'),
  Copy('$dst/pico-ice/lugforth-pico-ice/pico-sdk-LICENSE.TXT',
       '$src/ports/pico-ice/pico-sdk/LICENSE.TXT'),
]

# Build the uf2.
Command(' '.join(['$dst/pico-ice/lugforth_pico_ice.uf2',
                  '$dst/pico-ice/lugforth_pico_ice.err']),
        ' '.join(['$dst/pico-ice/build.ninja',
                  '$dst/gen/pico_ice_boot.h']),
        'ninja -C $dst/pico-ice lugforth_pico_ice >$dst/pico-ice/lugforth_pico_ice.err 2>&1')
Command(' '.join(['$dst/pico-ice/build.ninja',
                  '$dst/pico-ice/cmake.err']),
        ' '.join(['$src/ports/pico-ice/pico-sdk/README.md',
                  '$src/ports/pico-ice/pico-ice-sdk/README.md',
                  '$src/ports/pico-ice/pico-sdk/lib/tinyusb/README.rst']),
        'cmake $dst/pico-ice -G Ninja -S ports/pico-ice -B $dst/pico-ice >$dst/pico-ice/cmake.err 2>&1')

# Check out submodules if needed.
Command(' '.join([
          '$src/ports/pico-ice/pico-sdk/README.md',
          '$src/ports/pico-ice/pico-sdk/LICENSE.TXT',
        ]),
        '', 'git submodule update --init ports/pico-ice/pico-sdk')
Command('$src/ports/pico-ice/pico-sdk/lib/tinyusb/README.rst',
        '$src/ports/pico-ice/pico-sdk/README.md',
	      'cd $src/ports/pico-ice/pico-sdk && git submodule update --init lib/tinyusb')
Command(' '.join([
          '$src/ports/pico-ice/pico-ice-sdk/README.md',
          '$src/ports/pico-ice/pico-ice-sdk/LICENSE.md',
        ]),
	      '', 'git submodule update --init ports/pico-ice/pico-ice-sdk')

# Zip it.
PICO_ICE_FILES += [
  Zip('$dst/pico-ice/lugforth-pico-ice.zip', PICO_ICE_ZIP_FILES,
      base='$dst/pico-ice/lugforth-pico-ice'),
]

# Simulator.
PICO_ICE_FILES += [
  CompileSim('$dst/pico-ice-sim/lugforth_pico_ice_sim', '$src/ports/pico-ice/main.c',
             implicit=['$dst/gen/pico_ice_boot.h'])
]
Importation('$dst/gen/pico_ice_boot.h', '$src/ports/pico-ice/pico_ice_boot.fs', name='boot')

# Main Alias.
Alias('pico', ' '.join(PICO_ICE_FILES))
if not FAST:
  Default('pico')
