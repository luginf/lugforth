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

if not WEB_ENABLED:
  Return()

Importation('$dst/gen/web_boot.js', '$src/ports/web/web_boot.fs', header_mode='web', name='boot')
Compile('$dst/gen/dump_web_opcodes', '$src/ports/web/dump_web_opcodes.c')
GenRun('$dst/gen/web_cases.js', '$dst/gen/dump_web_opcodes', 'cases', [])
GenRun('$dst/gen/web_dict.js', '$dst/gen/dump_web_opcodes', 'dict', [])
GenRun('$dst/gen/web_sys.js', '$dst/gen/dump_web_opcodes', 'sys', [])

WEB_ITEMS = [
  GenRun('$dst/web/lugforth.js', '$src/ports/web/fuse_web.js', '', [
      '$src/ports/web/web.template.js',
      '$dst/gen/web_boot.js',
      '$dst/gen/web_dict.js',
      '$dst/gen/web_cases.js',
      '$dst/gen/web_sys.js',
  ]),
  Copy('$dst/web/terminal.html', '$src/ports/web/terminal.html'),
  Copy('$dst/web/lazy_terminal.html', '$src/ports/web/lazy_terminal.html'),
  Copy('$dst/web/script_lite_test.html', '$src/ports/web/script_lite_test.html'),
  Copy('$dst/web/script_test.html', '$src/ports/web/script_test.html'),
  Copy('$dst/web/script_test.fs', '$src/ports/web/script_test.fs'),
  Copy('$dst/web/serial.html', '$src/ports/web/serial.html'),
  Copy('$dst/web/sdl2_breakout.html', '$src/ports/web/sdl2_breakout.html'),
  Copy('$dst/web/sdl2_breakout.fs', '$src/examples/games/sdl2_breakout.fs'),
  Copy('$dst/web/sdl2_drums.html', '$src/ports/web/sdl2_drums.html'),
  Copy('$dst/web/sdl2_drums.fs', '$src/examples/music/sdl2_drums.fs'),
  Copy('$dst/web/sdl2_fm.html', '$src/examples/music/fm/sdl2_fm.html'),
  Copy('$dst/web/sdl2_fm.fs', '$src/examples/music/fm/sdl2_fm.fs'),
  Copy('$dst/web/fm_midi_parser.fs', '$src/examples/music/fm/midi_parser.fs'),
  Copy('$dst/web/fm_engine_web.fs', '$src/examples/music/fm/engine_web.fs'),
  Copy('$dst/web/sdl2_raycaster.html', '$src/ports/web/sdl2_raycaster.html'),
  Copy('$dst/web/sdl2_raycaster.fs', '$src/examples/games/sdl2_raycaster.fs'),
  Copy('$dst/web/sdl2_roguelike.html', '$src/ports/web/sdl2_roguelike.html'),
  Copy('$dst/web/sdl2_roguelike.fs', '$src/examples/games/sdl2_roguelike.fs'),
  Copy('$dst/web/sdl2_tiles.png', '$src/examples/games/sdl2_tiles.png'),
  Copy('$dst/web/sdl2_pinball.html', '$src/ports/web/sdl2_pinball.html'),
  Copy('$dst/web/pinball_common.fs', '$src/examples/games/pinball/common.fs'),
  Copy('$dst/web/pinball_raid.fs', '$src/examples/games/pinball/raid.fs'),
  Copy('$dst/web/pinball_night.fs', '$src/examples/games/pinball/night.fs'),
  Copy('$dst/web/pinball_menu.fs', '$src/examples/games/pinball/pinball.fs'),
]

if D8_AVAILABLE:
  WEB_ITEMS += [
    TestCommand('$dst/tests/web_sanity_test.out',
                ' '.join([
                    '$dst/web/lugforth.js',
                    '$src/tools/check_web_sanity.py',
                ]),
                'echo "120 3 + . cr bye" | ' + D8 + ' $dst/web/lugforth.js | '
                '$src/tools/check_web_sanity.py ' +
                '$dst/tests/web_sanity_test.out'),
  ]

Alias('web', ' '.join(WEB_ITEMS))
Default('web')

if D8_AVAILABLE:
  OneShot('d8',
          '$dst/web/lugforth.js',
          D8 + ' $dst/web/lugforth.js $in',
          pool='console')
