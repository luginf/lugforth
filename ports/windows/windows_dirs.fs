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

( Directories on Windows: open-dir read-dir close-dir as on the boards, ls dir cd pwd )

windows definitions
z" FindFirstFileA" 2 Kernel32 FindFirstFileA
z" FindNextFileA" 2 Kernel32 FindNextFileA
z" FindClose" 1 Kernel32 FindClose
z" SetCurrentDirectoryA" 1 Kernel32 SetCurrentDirectoryA
z" GetCurrentDirectoryA" 2 Kernel32 GetCurrentDirectoryA
forth definitions also windows

( A handle is 16 bytes then the WIN32_FIND_DATA: the find handle, a flag for the entry read by )
( FindFirstFile and not given yet, the attributes at 16, the name at 60 )
: dir-pattern { a n -- z }   \ path/* in a buffer at here
   n 0= if s" ." to n to a then
   a here n cmove
   a n + 1- c@ dup 47 <> swap 92 <> and if 47 here n + c! 1 +to n then
   42 here n + c!   0 here n + 1+ c!
   here ;
: open-dir ( a n -- dh ior )
   dir-pattern 340 allocate throw >r
   r@ 16 + FindFirstFileA dup -1 = if drop r> free drop 0 1 exit then
   r@ !  1 r@ cell+ !  r> 0 ;
: read-dir ( dh -- a n )   \ a name, ending with / for a directory, or n = 0 at the end
   >r
   r@ cell+ @ if 0 r@ cell+ !
   else r@ @ r@ 16 + FindNextFileA $ffffffff and 0= if rdrop 0 0 exit then then
   r@ 60 + z>s
   r> 16 + @ $10 and if 2dup + 47 swap c! 2dup + 1+ 0 swap c! 1+ then ;
: close-dir ( dh -- ior ) dup @ FindClose drop free drop 0 ;
: ls ( "path" -- )
   bl parse dup 0= if 2drop s" ." then
   open-dir throw >r
   begin r@ read-dir dup while type cr repeat 2drop
   r> close-dir drop ;
: dir ( "path" -- ) ls ;
: cd ( "path" -- ) bl parse s>z win-path SetCurrentDirectoryA $ffffffff and 0= throw ;
: pwd ( -- ) 1024 here GetCurrentDirectoryA here swap type cr ;
previous
