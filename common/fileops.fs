\ Copyright 2023 Bradley D. Nelson
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


( The ports give a file size and position as a single cell: the standard has a double cell
  number, which is what these four words take and give. A 64 bit cell holds any file size,
  the high cell is only there for the standard. )

[defined] file-size [if]
: file-size ( fh -- ud ior ) file-size >r s>d r> ;
[then]
[defined] file-position [if]
: file-position ( fh -- ud ior ) file-position >r s>d r> ;
[then]
[defined] reposition-file [if]
: reposition-file ( ud fh -- ior ) >r d>s r> reposition-file ;
[then]
[defined] resize-file [if]
: resize-file ( ud fh -- ior ) >r d>s r> resize-file ;
[then]
