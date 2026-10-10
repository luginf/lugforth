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

# Applies the ueforth -> lugforth renaming to the text files of the tree.
# Use it after taking files from upstream (git checkout upstream/main -- file)
# or after a merge. Upstream URLs and credits are left alone.
# Renamed paths (org/lugforth, infix_lugforth.fs, ports/windows/lugforth.rc) are not
# handled here.
import re,subprocess,os
files=subprocess.check_output(['git','ls-files']).decode().split('\n')
skip_prefix=('attic/','docs/site/')
skip_files={'NOTICE','LICENSE','README.md'}
prot=[r'github\.com/flagxor/ueforth', r'/src/ueforth\b', r'upstream uEforth', r'uEforth by', r'uEforth \(https', r'of uEforth', r'Fork of uEforth',r'ueforth\.git']
changed=0
for f in files:
    if not f or f.startswith(skip_prefix) or f in skip_files or not os.path.isfile(f): continue
    try: s=open(f,encoding='utf-8').read()
    except Exception: continue
    if not re.search('ueforth',s,re.I): continue
    keep=[]
    def hide(m):
        keep.append(m.group(0)); return '\x00%d\x00'%(len(keep)-1)
    for p in prot: s=re.sub(p,hide,s)
    s=s.replace('UEFORTH','LUGFORTH').replace('ueforth','lugforth').replace('uEforth','Lugforth').replace('uEForth','Lugforth').replace('UEforth','Lugforth')
    s=re.sub(r'\x00(\d+)\x00',lambda m:keep[int(m.group(1))],s)
    open(f,'w',encoding='utf-8').write(s); changed+=1
print(changed)
