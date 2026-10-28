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

"""Runs the Forth 2012 test suite (John Hayes, Gerry Jackson) on Lugforth.

    python3 tools/forth2012_test.py [out/posix/lugforth] [suite-dir]

The suite is not part of this repository. It is taken from the directory given as the
second argument, or from $F2012_DIR, or cloned (depth 1) into out/forth2012-suite from
https://github.com/gerryjackson/forth2012-test-suite (needs the network the first time).
The test files `include` each other by relative name and the driver written in
out/tests/forth2012_driver.fth includes them by absolute path, in the order of the suite's
runtests.fth. core.fr asks for a line of text, which is given on the standard input.

The run passes when every word set of the error report has 0 errors and no file was
aborted. The floating point sets (fp/) are not run. Known failures can be listed in
tools/forth2012_known.txt, one word set name per line as the report prints it (a set listed
there may fail); there are none today.
"""

import os, re, subprocess, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__))) + '/'
URL = 'https://github.com/gerryjackson/forth2012-test-suite'
# The order of runtests.fth. coreexttest.fth comes before filetest.fth, which uses its SI_INC.
FILES = ['prelimtest.fth', 'tester.fr', 'core.fr', 'coreplustest.fth', 'utilities.fth',
         'errorreport.fth', 'coreexttest.fth', 'blocktest.fth', 'doubletest.fth',
         'exceptiontest.fth', 'facilitytest.fth', 'filetest.fth', 'localstest.fth',
         'memorytest.fth', 'toolstest.fth', 'searchordertest.fth', 'stringtest.fth']


def find_suite(arg):
    for d in (arg, os.environ.get('F2012_DIR'), ROOT + 'out/forth2012-suite'):
        if d and os.path.exists(os.path.join(d, 'src', 'core.fr')):
            return os.path.abspath(d)
    d = ROOT + 'out/forth2012-suite'
    r = subprocess.run(['git', 'clone', '--depth', '1', URL, d], capture_output=True, text=True)
    if r.returncode or not os.path.exists(d + '/src/core.fr'):
        sys.exit('forth2012: no suite: give its directory or set F2012_DIR, or allow the network '
                 '(git clone %s): %s' % (URL, r.stderr.strip()))
    return d


def main():
    args = [a for a in sys.argv[1:] if not a.startswith('-')]
    forth = os.path.abspath(args[0] if args else ROOT + 'out/posix/lugforth')
    suite = find_suite(args[1] if len(args) > 1 else None)
    src = suite + '/src/'
    known = set()
    if os.path.exists(ROOT + 'tools/forth2012_known.txt'):
        known = set(l.strip() for l in open(ROOT + 'tools/forth2012_known.txt') if l.strip())
    os.makedirs(ROOT + 'out/tests', exist_ok=True)
    driver = ROOT + 'out/tests/forth2012_driver.fth'
    with open(driver, 'w') as f:
        for name in FILES:
            f.write('S" %s%s" INCLUDED\n' % (src, name))
        f.write('REPORT-ERRORS\nbye\n')
    try:
        r = subprocess.run([forth, driver], cwd=src, input='hello world\n', capture_output=True,
                           text=True, timeout=600)
    except subprocess.TimeoutExpired:
        sys.exit('forth2012: timeout')
    out = (r.stdout + r.stderr).replace('\r', '')
    open(ROOT + 'out/tests/forth2012_test.out', 'w').write(out)
    rows = re.findall(r'^([A-Za-z][A-Za-z -]*?)\s+(\d+)\s*$', out.split('Error Report')[-1], re.M) \
        if 'Error Report' in out else []
    failed = False
    for name, n in rows:
        if name != 'Total' and int(n) and name not in known:
            print('forth2012: %s: %s errors' % (name, n))
            failed = True
    for l in out.split('\n'):
        if l.startswith(('INCORRECT RESULT', 'WRONG NUMBER OF RESULTS', 'Error including')):
            print('forth2012: ' + l[:150])
    if not rows or 'Error including' in out:
        failed = True
        if not rows:
            print('forth2012: no error report (the run stopped early)')
    if failed:
        print('forth2012: FAILED, see out/tests/forth2012_test.out')
        sys.exit(1)
    print('forth2012: all the word sets pass (%d sets, suite %s)' % (len(rows) - 1, suite))


main()
