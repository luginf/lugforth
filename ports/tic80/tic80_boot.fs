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

( Boot of the tic80 port: the kernel words, no file, task, terminal or )
( foreign-call words. The loop of platform.fs waits for the commands of TIC-80. )

needs ../../common/phase1.fs
needs allocation.fs
needs ../../common/phase2.fs
needs ../../common/interpret_control.fs
needs platform.fs
needs autoboot.fs
needs ../../common/fini.fs
