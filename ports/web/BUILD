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
  Copy('$dst/web/gfx_breakout.html', '$src/ports/web/gfx_breakout.html'),
  Copy('$dst/web/gfx_breakout.fs', '$src/examples/games/gfx_breakout.fs'),
  Copy('$dst/web/gfx_drums.html', '$src/ports/web/gfx_drums.html'),
  Copy('$dst/web/gfx_drums.fs', '$src/examples/music/gfx_drums.fs'),
  Copy('$dst/web/gfx_fm.html', '$src/examples/music/fm/gfx_fm.html'),
  Copy('$dst/web/gfx_fm.fs', '$src/examples/music/fm/gfx_fm.fs'),
  Copy('$dst/web/fm_midi_parser.fs', '$src/examples/music/fm/midi_parser.fs'),
  Copy('$dst/web/fm_engine_web.fs', '$src/examples/music/fm/engine_web.fs'),
  Copy('$dst/web/gfx_raycaster.html', '$src/ports/web/gfx_raycaster.html'),
  Copy('$dst/web/gfx_raycaster.fs', '$src/examples/games/gfx_raycaster.fs'),
  Copy('$dst/web/gfx_roguelike.html', '$src/ports/web/gfx_roguelike.html'),
  Copy('$dst/web/gfx_roguelike.fs', '$src/examples/games/gfx_roguelike.fs'),
  Copy('$dst/web/gfx_tiles.png', '$src/examples/games/gfx_tiles.png'),
  Copy('$dst/web/gfx_pinball.html', '$src/ports/web/gfx_pinball.html'),
  Copy('$dst/web/pinball_common.fs', '$src/examples/games/pinball/common.fs'),
  Copy('$dst/web/pinball_raid.fs', '$src/examples/games/pinball/raid.fs'),
  Copy('$dst/web/pinball_night.fs', '$src/examples/games/pinball/night.fs'),
  Copy('$dst/web/pinball_menu.fs', '$src/examples/games/pinball/pinball.fs'),
]

# Other games and demos: no page of their own, they run with
# terminal.html?run=<file>.fs (the pages of the site link them like that).
for _name, _dir in [
    ('gfx_2048', 'games'), ('2048', 'games'), ('gfx_snake', 'games'),
    ('gfx_tetris', 'games'), ('gfx_minesweeper', 'games'),
    ('gfx_mastermind', 'games'), ('gfx_invaders', 'games'),
    ('gfx_magnoquill', 'graphics'), ('gfx_demo', 'graphics'),
    ('gfx_turtle', 'graphics'), ('gfx_3d', 'graphics'), ('3d', 'graphics')]:
  WEB_ITEMS += [Copy('$dst/web/%s.fs' % _name, '$src/examples/%s/%s.fs' % (_dir, _name))]

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
