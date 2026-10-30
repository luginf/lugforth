\ Copyright 2026 Luginfo10
\
\ Licensed under the Apache License, Version 2.0 (the "License");
\ you may not use this file except in compliance with the License.
\ You may obtain a copy of the License at
\
\     http://www.apache.org/licenses/LICENSE-2.0
\
\ Unless required by applicable law or agreed to in writing, software
\ distributed under the License is distributed on an "AS IS" BASIS,
\ WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
\ See the License for the specific language governing permissions and
\ limitations under the License.

needs ../../common/phase1.fs

\ Same shape as gba_boot.fs (see that file's comment for why this paragraph
\ has to live after the needs line above, not before): boots straight into
\ magnoquill.fs instead of the tile-sheet demo in autoboot.fs. `run`, at the
\ end of magnoquill.fs, loops forever, so fini.fs's own trailing `ok` is
\ never reached -- same as how autoboot.fs's `' ok` never returns either,
\ once `raw-ok` gets to `quit`.

needs allocation.fs
needs ../../common/phase2.fs
needs ../../common/tasks.fs
needs ../../common/streams.fs
needs platform.fs
needs magnoquill.fs
needs ../../common/fini.fs
