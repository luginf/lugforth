\ Copyright 2026 luginf
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

needs ../common/phase1.fs

\ Same shape as pico-ice/pico_ice_boot.fs, minus ice40.fs (no FPGA here).
\ streams.fs is kept even though nothing here uses a byte ring buffer yet:
\ dropping it was tried and something later in the chain silently crashed
\ without it (not chased further -- the real bug in this file turned out to
\ be elsewhere, see platform.fs's comment on `forth definitions internals`).
\ This comment itself has to live AFTER `needs phase1.fs`, not before: this
\ Copyright header is stripped automatically (every file has one), but this
\ paragraph is not, so it becomes literal boot text -- and phase1.fs is what
\ defines `\` and `(` in the first place. Any comment placed before the
\ first `needs` line in this specific file is that same silent-crash trap
\ one more time, since nothing can be commented out yet to explain it.

needs allocation.fs
needs ../common/phase2.fs
needs ../common/tasks.fs
needs ../common/streams.fs
needs platform.fs
needs autoboot.fs
needs ../common/fini.fs
