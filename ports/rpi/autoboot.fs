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

( autoexec.fs at the root of the SD card, if there is one, runs at boot )
\ After the banner, before the first prompt. An error in it prints and
\ leaves the prompt, as an error typed there would.
internals definitions
: autoexec
  s" autoexec.fs" r/o open-file if drop exit then close-file drop
  s" autoexec.fs" ['] included catch if 2drop ." autoexec.fs: error" cr then ;
' autoexec boot-prompt !
forth definitions
' ok
