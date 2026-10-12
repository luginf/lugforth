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

Importation('$dst/gen/posix_boot.h', '$src/ports/posix/posix_boot.fs', name='boot')
Compile('$dst/posix/lugforth', '$src/ports/posix/main.c',
        implicit=['$dst/gen/posix_boot.h'])

# lugforth-mini: the same without the editors, SDL2, X11, the console window and the servers
Importation('$dst/gen/posix_mini_boot.h', '$src/ports/posix/posix_mini_boot.fs', name='boot')
CompileMini('$dst/posix/lugforth-mini', '$src/ports/posix/main.c',
            implicit=['$dst/gen/posix_mini_boot.h'])

Alias('posix', ' '.join([
  ForthTest('$dst/tests/posix_all_tests.out',
            '$dst/posix/lugforth', '$src/common/all_tests.fs'),
  ForthTest('$dst/tests/posix_mini_all_tests.out',
            '$dst/posix/lugforth-mini', '$src/common/all_tests.fs'),
  ForthTest('$dst/tests/posix_lineedit_tests.out',
            '$dst/posix/lugforth', '$src/ports/posix/lineedit_tests.fs'),
  ForthTest('$dst/tests/posix_editor_tests.out',
            '$dst/posix/lugforth', '$src/ports/posix/editor_tests.fs'),
  ForthTest('$dst/tests/posix_writhdeck_tests.out',
            '$dst/posix/lugforth', '$src/ports/posix/writhdeck_tests.fs'),
  TestCommand('$dst/tests/posix_see_all_test.out',
              '$dst/posix/lugforth',
              'echo internals see-all bye | $dst/posix/lugforth '
              '>$dst/tests/posix_see_all_test.out 2>&1'),
  TestCommand(' '.join(['$dst/tests/posix_save_restore_test.out',
                        '$dst/tests/posix_save_restore.bin']),
              '$dst/posix/lugforth',
              'echo ": foosquare dup * ; save $dst/tests/posix_save_restore.bin bye" | '
              '$dst/posix/lugforth > $dst/tests/posix_save_restore_test.out && '
              'echo "restore $dst/tests/posix_save_restore.bin 4 foosquare 16 <> posix sysexit" | '
              '$dst/posix/lugforth >> $dst/tests/posix_save_restore_test.out'),
]))

OneShot('install',
        '$dst/posix/lugforth',
        'sudo cp $dst/posix/lugforth /usr/bin/lugforth',
        pool='console')

Default('posix')
