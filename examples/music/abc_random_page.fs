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

( The random tune of abc_random.fs for the page abc_random.html )
\
\ The page loads abc_random.fs, then this file. Its buttons type "N web-tune" at the
\ prompt: the tune of the seed N is made, its ABC text goes to the page (which draws
\ the score and shows the text) and it plays in a loop, until the next tune.

also web
JSWORD: js-abc-show { a n }
  if (window.abcShow) { window.abcShow(GetString(a, n)); }
~
JSWORD: js-abc-stop { }
  if (context.sdl && context.sdl.tuneStop) { context.sdl.tuneStop(); }
~
previous
: web-tune ( seed -- ) rand-seed !  new-tune 2dup js-abc-show abc-loop ;
: web-stop ( -- ) js-abc-stop ;
