#!/usr/bin/env python3
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

"""Builds the GitHub Pages site into out/pages.

    ninja posix web          (the word lists and the web demo come from there)
    python3 tools/make_pages.py

Needs pandoc. The pages come from the Markdown files of the repository
(docs/WORDS.md, ports/posix/SDL2*.md, README files, NOTICE) plus
docs/pages/index.md and the "new words" page computed by tools/words_added.py.
"""

import html, os, re, shutil, subprocess, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__))) + '/'
sys.path.insert(0, ROOT + 'tools')
import words_added

OUT = ROOT + 'out/pages/'
REPO = 'https://github.com/luginf/lugforth/blob/main/'

NAV = [('index.html', 'Home'), ('demo/terminal.html', 'Try it'),
       ('words.html', 'Words'), ('new-words.html', 'New words'),
       ('sdl2.html', 'SDL2'), ('ports.html', 'Ports'),
       ('examples.html', 'Examples'), ('credits.html', 'Credits')]

# page file -> (title, source Markdown files; several are joined)
PAGES = {
  'index.html': ('Lugforth', ['docs/pages/index.md']),
  'words.html': ('Words', ['docs/WORDS.md']),
  'sdl2.html': ('SDL2 graphics and sound', ['ports/posix/SDL2.md', 'ports/posix/SDL2-words.md']),
  'ports.html': ('Ports', ['docs/pages/ports.md', 'ports/android/README.md', 'ports/gba/README.md']),
  'examples.html': ('Examples', ['examples/README.md']),
  'credits.html': ('Credits', ['NOTICE']),
}
SITE_PAGES = set(['index.html', 'words.html', 'new-words.html', 'sdl2.html', 'ports.html',
                  'examples.html', 'credits.html'])
# Markdown sources that have a page: links to them stay on the site
LINKS = {'docs/WORDS.md': 'words.html', 'WORDS.md': 'words.html',
         'ports/posix/SDL2-words.md': 'sdl2.html', 'ports/posix/SDL2.md': 'sdl2.html',
         'README.md': 'index.html', 'NOTICE': 'credits.html'}


def pandoc(md):
  return subprocess.run(['pandoc', '-f', 'gfm', '-t', 'html'], input=md, text=True,
                        capture_output=True, check=True).stdout


def fix_links(page, source, body):
  """Relative links go to the site when the target has a page, else to GitHub."""
  base = os.path.dirname(source)
  def repl(m):
    url = m.group(1)
    if re.match(r'^(https?:|mailto:|#)', url) or url.split('#')[0] in SITE_PAGES \
        or url.startswith('demo/'):
      return m.group(0)
    path, _, frag = url.partition('#')
    full = os.path.normpath(os.path.join(base, path))
    if full in LINKS:
      return 'href="%s%s"' % (LINKS[full], '#' + frag if frag else '')
    return 'href="%s%s"' % (REPO + full, '#' + frag if frag else '')
  return re.sub(r'href="([^"]*)"', repl, body)


def layout(page, title, body):
  nav = ''.join('<a href="%s"%s>%s</a>' % (h, ' class="here"' if h == page else '', t)
                for h, t in NAV)
  return """<!DOCTYPE html>
<html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>%s - Lugforth</title><script src="theme.js"></script><link rel="stylesheet" href="style.css"></head>
<body><header><a class="brand" href="index.html">Lugforth</a><nav>%s</nav><button id="theme-toggle" class="theme-toggle" type="button" hidden aria-label="Toggle light/dark theme" title="Toggle light/dark theme"></button></header>
<main>%s</main>
<footer>Unofficial fork of <a href="https://github.com/flagxor/ueforth">uEforth</a>
by Bradley D. Nelson. Source: <a href="https://github.com/luginf/lugforth">github.com/luginf/lugforth</a>.
Apache License 2.0.</footer></body></html>
""" % (html.escape(title), nav, body)


def new_words_md():
  """The page of the words Lugforth added, with the descriptions of WORDS.md."""
  desc = {}
  for line in open(ROOT + 'docs/WORDS.md', encoding='utf-8'):
    cells = [c.strip() for c in line.strip().strip('|').split('|')]
    if len(cells) >= 3 and cells[0].startswith('`') and cells[0].endswith('`'):
      desc.setdefault(cells[0].strip('`').lower(), (cells[1], cells[2]))
  md = ['# New words\n',
        'Words that Lugforth has and the original uEforth does not, found by '
        'comparing the two interpreters (`tools/words_added.py`). The ones '
        'loaded on demand (`sdl2`, `sdl2-image`, `sdl2-ttf`, `sdl2-mixer`, '
        '`sdl2-abc`) have their own [reference](sdl2.html).\n']
  for vocab, words in words_added.added().items():
    md.append('## Vocabulary `%s` (%d words)\n' % (vocab, len(words)))
    md.append('| Word | Stack | What it does |\n|------|-------|--------------|')
    for w in words:
      stack, what = desc.get(w, ('', ''))
      if what.strip() == 'ANS.':
        what = 'Standard ANS Forth word, see [forth-standard.org](https://forth-standard.org/standard/words).'
      md.append('| `%s` | %s | %s |' % (w, stack, what))
    md.append('')
  return '\n'.join(md)


def main():
  if os.path.isdir(OUT):
    shutil.rmtree(OUT)
  os.makedirs(OUT)
  shutil.copy(ROOT + 'docs/pages/style.css', OUT + 'style.css')
  shutil.copy(ROOT + 'docs/pages/theme.js', OUT + 'theme.js')
  jobs = dict(PAGES)
  for page, (title, sources) in jobs.items():
    parts = []
    for src in sources:
      path = ROOT + src
      if not os.path.exists(path):
        continue
      parts.append(fix_links(page, src, pandoc(open(path, encoding='utf-8').read())))
    open(OUT + page, 'w', encoding='utf-8').write(layout(page, title, '\n<hr>\n'.join(parts)))
  open(OUT + 'new-words.html', 'w', encoding='utf-8').write(
      layout('new-words.html', 'New words', fix_links('new-words.html', 'docs/WORDS.md',
                                                     pandoc(new_words_md()))))
  # the web version, with its games
  demo = ROOT + 'out/web/'
  if os.path.isdir(demo):
    os.makedirs(OUT + 'demo')
    for name in ('terminal.html', 'lugforth.js', 'sdl2_breakout.html', 'sdl2_breakout.fs',
                 'sdl2_drums.html', 'sdl2_drums.fs', 'sdl2_roguelike.html',
                 'sdl2_roguelike.fs', 'sdl2_tiles.png'):
      if os.path.exists(demo + name):
        shutil.copy(demo + name, OUT + 'demo/' + name)
  # the HTML examples of examples/web, which load lugforth.js from out/web in the repository
  exdir = ROOT + 'examples/web/'
  if os.path.isdir(OUT + 'demo') and os.path.isdir(exdir):
    for name in sorted(os.listdir(exdir)):
      if name.endswith('.html'):
        text = open(exdir + name, encoding='utf-8').read()
        text = text.replace('../../out/web/lugforth.js', 'lugforth.js')
        open(OUT + 'demo/' + name, 'w', encoding='utf-8').write(text)
  open(OUT + '.nojekyll', 'w').close()
  print('wrote', OUT, len(os.listdir(OUT)), 'files')


if __name__ == '__main__':
  main()
