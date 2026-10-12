\ Copyright 2021 Bradley D. Nelson
\ Modifications Copyright 2026 Luginfo10
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

internals definitions
( TODO: Figure out why this has to happen so late. )
transfer internals-builtins
forth definitions internals
( Bring a forth to the top of the vocabulary. )
transfer forth
( Move heap to save point, with a gap. )
setup-saving-base
( From here on r" r| r~ copy their text into the definition. Before, in the boot, they )
( keep its address, which saves memory on the small boards: the boot text never goes away. )
( To go back to addresses only, remove this line. See docs/SKILLS.md, "r| copies". )
-1 r-copy !
forth
( assumes an xt for autoboot is on the dstack )
execute
ok
