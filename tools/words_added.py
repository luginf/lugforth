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

"""Words that Lugforth has and the original uEforth does not.

  tools/words_added.py --snapshot /path/to/upstream/out/posix/ueforth
      records the words of an upstream binary in docs/pages/upstream_words.json
      (done once, from the commit where the fork started)
  tools/words_added.py
      prints the words of out/posix/lugforth missing from that snapshot

Used by tools/make_pages.py for the "New words" page.
"""

import json, os, subprocess, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__))) + '/'
SNAPSHOT = ROOT + 'docs/pages/upstream_words.json'
VOCABS = ['forth', 'internals', 'posix', 'structures', 'streams', 'tasks',
          'ansi', 'editor', 'sockets', 'x11', 'graphics', 'httpd',
          'telnetd', 'web-interface', 'termios', 'visual', 'recognizers']


def words_of(binary):
  result = {}
  for vocab in VOCABS:
    out = subprocess.run([binary], input='." @@" %s words ." @@" cr\nbye\n' % vocab,
                         capture_output=True, text=True, timeout=60,
                         cwd='/tmp').stdout.replace('\r', '')
    parts = out.split('@@')
    if len(parts) >= 4:
      result[vocab] = sorted(set(w.lower() for w in parts[-2].split()))
  return result


def added(binary=None):
  """{vocabulary: [new words]}, in the order of the vocabularies."""
  now = words_of(binary or ROOT + 'out/posix/lugforth')
  old = json.load(open(SNAPSHOT))
  everywhere = set(w for ws in old.values() for w in ws)
  forth_now = set(now.get('forth', []))
  result = {}
  for vocab, ws in now.items():
    new = [w for w in ws if w not in old.get(vocab, []) and w not in everywhere
           and (vocab == 'forth' or w not in forth_now)]
    if vocab == 'forth':
      new = [w for w in ws if w not in old.get('forth', [])]
    if new:
      result[vocab] = new
  return result


if __name__ == '__main__':
  if len(sys.argv) == 3 and sys.argv[1] == '--snapshot':
    data = words_of(sys.argv[2])
    json.dump(data, open(SNAPSHOT, 'w'), indent=0, sort_keys=True)
    print('wrote', SNAPSHOT, sum(len(v) for v in data.values()), 'words')
  else:
    for vocab, ws in added().items():
      print('%s (%d): %s' % (vocab, len(ws), ' '.join(ws)))
