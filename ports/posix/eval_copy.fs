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

( eval-copy: evaluate a string that is kept in the dictionary, safely. )
( The code of the console window, of ed and of writhdeck is a string evaluated when )
( asked. A copy is evaluated: the words compiled would overwrite the string while it )
( is read. The input state is saved and restored, a throw skips the restore. )
( 'tib, #tib and >in are in the internals vocabulary. )

also internals
: eval-copy ( a n -- )
  { a n }
  'tib @ #tib @ >in @ { t1 t2 t3 }
  n allocate throw { buf }
  a buf n cmove
  buf n ['] evaluate catch { err }
  t3 >in !  t2 #tib !  t1 'tib !
  buf free drop  err throw ;
previous
