#!/usr/bin/env python3
"""docs/lugforth_manual.pdf: every Markdown file of docs/ (not docs/pages, which is the website)
in one PDF, with pandoc and xelatex.  Usage: python3 tools/make_pdf.py [output.pdf]"""
import os
import re
import subprocess
import time
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOCS = os.path.join(ROOT, 'docs')
ORDER = ['MANUAL.md', 'SDL2.md', 'SDL2-words.md', 'SDL2-web.md', 'WORDS.md']
DEV = ['README.md', 'ARCHITECTURE.md', 'FEATURES.md', 'PORTS.md', 'PITFALLS.md', 'IDEAS.md']


def files():
  out = [os.path.join(DOCS, f) for f in ORDER]
  known = set(DEV)
  out += [os.path.join(DOCS, 'dev', f) for f in DEV]
  out += [os.path.join(DOCS, 'dev', f) for f in sorted(os.listdir(os.path.join(DOCS, 'dev')))
          if f.endswith('.md') and f not in known]
  return [f for f in out if os.path.exists(f)]


def main():
  out = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else os.path.join(DOCS, 'lugforth_manual.pdf')
  version = re.search(r"version='([^']+)'", open(os.path.join(ROOT, 'BUILD')).read()).group(1)
  cmd = ['pandoc', '-f', 'gfm', '--pdf-engine=xelatex', '--toc', '--toc-depth=2', '-V', 'geometry:margin=2cm',
         '-V', 'fontsize=10pt', '-V', 'mainfont=DejaVu Sans', '-V', 'monofont=DejaVu Sans Mono',
         '-V', 'colorlinks=true', '-M', 'title=Lugforth manual', '-M', 'subtitle=version ' + version,
         '-M', 'date=' + time.strftime('%Y-%m-%d'),
         '--resource-path=' + DOCS, '--lua-filter=' + os.path.join(ROOT, 'tools', 'pdf_tables.lua'),
         '-H', os.path.join(ROOT, 'tools', 'pdf_header.tex'), '-o', out] + files()
  subprocess.run(cmd, check=True, cwd=DOCS)
  print('wrote', out)


main()
