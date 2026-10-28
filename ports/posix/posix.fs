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

vocabulary posix   posix definitions

( Shared Library Handling )
1 constant RTLD_LAZY
2 constant RTLD_NOW
0 z" dlopen" dlsym constant 'dlopen
: dlopen ( z n -- a ) 'dlopen [ internals ] call2 [ posix ] ;
create calls
internals
 ' call0 ,  ' call1 ,  ' call2 ,  ' call3 ,  ' call4 ,
 ' call5 ,  ' call6 ,  ' call7 ,  ' call8 ,  ' call9 ,
' call10 , ' call11 , ' call12 , ' call13 , ' call14 , ' call15 ,
posix
: sofunc ( z n a "name" -- )
   swap >r swap dlsym dup 0= -38 and throw create , r> cells calls + @ ,
   does> dup @ swap cell+ @ execute ;
: sysfunc ( z n "name" -- ) 0 sofunc ;
: shared-library ( z "name" -- )
   RTLD_NOW dlopen dup 0= -38 and throw create , does> @ sofunc ;
: sign-extend ( n -- n ) >r rp@ sl@ rdrop ;

( Major Syscalls )
z" open" 3 sysfunc open
z" creat" 2 sysfunc creat
z" close" 1 sysfunc close
z" read" 3 sysfunc read
z" write" 3 sysfunc write
z" lseek" 3 sysfunc lseek
z" ftruncate" 2 sysfunc ftruncate
z" fsync" 1 sysfunc fsync
z" exit" 1 sysfunc sysexit
z" mmap" 6 sysfunc mmap
z" munmap" 2 sysfunc munmap
z" mprotect" 3 sysfunc mprotect
z" unlink" 1 sysfunc unlink
z" rename" 2 sysfunc rename
z" malloc" 1 sysfunc malloc
z" free" 1 sysfunc sysfree
z" realloc" 2 sysfunc realloc
z" usleep" 1 sysfunc usleep
z" signal" 2 sysfunc signal
z" isatty" 1 sysfunc isatty

( Processes )
z" fork" 0 sysfunc fork
z" wait" 1 sysfunc wait
z" waitpid" 3 sysfunc waitpid
z" execvp" 2 sysfunc execvp
z" dup2" 2 sysfunc dup2
z" pipe" 1 sysfunc pipe

( Directories )
z" chdir" 1 sysfunc chdir
z" mkdir" 2 sysfunc mkdir
z" rmdir" 1 sysfunc rmdir
z" getcwd" 2 sysfunc getcwd
z" opendir" 1 sysfunc opendir
z" closedir" 1 sysfunc closedir
z" readdir" 1 sysfunc readdir

( Errno )
also internals
: errno ( -- n ) errno ;
previous


( The system: values that macOS numbers differently from Linux )
also internals host-mac? previous constant on-mac?
: os ( linux darwin -- n ) on-mac? if swap drop else drop then ;   \ the value for this system: the second on macOS, else the first
( Library file names: the SDL2 bindings ask for them by these words, as on Windows )
: lib-try ( file-a file-n prefix-a prefix-n -- z or 0 )   ( the name made at here, when that library opens )
  dup >r here swap cmove                 ( file-a file-n )
  swap here r@ + rot dup >r cmove        ( the file name after the prefix )
  0 here r> r> + + c!
  here dup RTLD_NOW dlopen 0= if drop 0 then ;
: mac-lib ( a n -- z )   ( a library of macOS: in the system, else in the Homebrew folders )
  2dup s" " lib-try ?dup if nip nip exit then
  2dup s" /opt/homebrew/lib/" lib-try ?dup if nip nip exit then
  2dup s" /usr/local/lib/" lib-try ?dup if nip nip exit then
  s>z ;
: lib-name ( z-linux a-mac n-mac -- z ) on-mac? if mac-lib nip else 2drop then ;   \ the file name of a shared library on this system
: sdl2-lib ( -- z ) z" libSDL2-2.0.so.0" s" libSDL2-2.0.0.dylib" lib-name ;   \ the file name of libSDL2 on this system
: sdl2-ttf-lib ( -- z ) z" libSDL2_ttf-2.0.so.0" s" libSDL2_ttf-2.0.0.dylib" lib-name ;   \ the file name of libSDL2_ttf on this system
: sdl2-image-lib ( -- z ) z" libSDL2_image-2.0.so.0" s" libSDL2_image-2.0.0.dylib" lib-name ;   \ the file name of libSDL2_image on this system
: sdl2-mixer-lib ( -- z ) z" libSDL2_mixer-2.0.so.0" s" libSDL2_mixer-2.0.0.dylib" lib-name ;   \ the file name of libSDL2_mixer on this system
: .d_type ( a -- n ) 18 20 os + c@ ;
: .d_name ( a -- z ) 19 21 os + ;

( Default Pipes )
0 constant stdin
1 constant stdout
2 constant stderr

( Seek )
0 constant SEEK_SET
1 constant SEEK_CUR
2 constant SEEK_END

( mmap )
0 constant PROT_NONE
1 constant PROT_READ
2 constant PROT_WRITE
4 constant PROT_EXEC
$2 constant MAP_PRIVATE
$10 constant MAP_FIXED
$20 $1000 os constant MAP_ANONYMOUS

( open )
octal
0 constant O_RDONLY
1 constant O_WRONLY
2 constant O_RDWR
decimal
$40 $200 os constant O_CREAT
$80 $800 os constant O_EXCL
$200 $400 os constant O_TRUNC
$400 $8 os constant O_APPEND
$800 $4 os constant O_NONBLOCK

( Hookup I/O )
: stdout-write ( a n -- ) stdout -rot write drop ;
: stdin-key ( -- n ) 0 >r stdin rp@ 1 read 0= if rdrop -1 exit then r> ;

also forth definitions
: default-type stdout-write ;
: default-key stdin-key ;
only posix definitions
' default-type is type
' default-key is key
' sysexit is terminate

( I/O Error Helpers )
: 0<ior ( n -- ior ) 0< if errno else 0 then ;
: 0=ior ( n -- ior ) 0= if errno else 0 then ;
: d0<ior ( n -- n ior ) dup 0<ior ;
: d0=ior ( n -- n ior ) dup 0=ior ;

( errno.h )
11 35 os constant EAGAIN
32 constant EPIPE

( Signal Handling )
0 constant SIG_DFL
1 constant SIG_IGN

( Signals )
1 constant SIGHUP
2 constant SIGINT
9 constant SIGKILL
7 10 os constant SIGBUS
13 constant SIGPIPE

( Ignore SIGPIPE )
SIGPIPE SIG_IGN signal drop

( Modes )
octal 777 constant 0777 decimal

( Clock )
z" clock_gettime" 2 sysfunc clock_gettime
: timespec ( "name" ) create 0 , 0 , ;
0 constant CLOCK_REALTIME
1 constant CLOCK_MONOTONIC
2 constant CLOCK_PROCESS_CPUTIME_ID
3 constant CLOCK_THREAD_CPUTIME_ID
4 constant CLOCK_MONOTONIC_RAW
5 constant CLOCK_REALTIME_COARSE
6 constant CLOCK_MONOTONIC_COARSE
7 constant CLOCK_BOOTTIME
8 constant CLOCK_REALTIME_ALARM
9 constant CLOCK_BOOTTIME_ALARM

( File control )
z" fcntl" 3 sysfunc fcntl
4 constant F_SETFL
2048 4 os constant FNDELAY ( O_NONBLOCK )

forth definitions posix

( Generic Files )
O_RDONLY constant R/O
O_WRONLY constant W/O
O_RDWR constant R/W
: BIN ( fh -- fh ) ;

: CLOSE-FILE ( fh -- ior ) close sign-extend 0<ior ;
: FLUSH-FILE ( fh -- ior ) fsync sign-extend 0<ior ;
: OPEN-FILE ( a n fam -- fh ior ) >r s>zt r> 0777 open sign-extend d0<ior ;
: CREATE-FILE ( a n fam -- fh ior )
   >r s>zt r> O_CREAT O_TRUNC or or 0777 open sign-extend d0<ior ;
: DELETE-FILE ( a n -- ior ) s>zt unlink sign-extend 0<ior ;
: RENAME-FILE ( a n a n -- ior )
   here >r  s>z -rot s>zt swap rename sign-extend 0<ior  r> here - allot ;
: WRITE-FILE ( a n fh -- ior ) -rot dup >r write r> = 0=ior ;
: READ-FILE ( a n fh -- n ior ) -rot read d0<ior ;
: FILE-POSITION ( fh -- n ior ) 0 SEEK_CUR lseek d0<ior ;
: REPOSITION-FILE ( n fh -- ior ) swap SEEK_SET lseek 0<ior ;
: RESIZE-FILE ( n fh -- ior ) swap ftruncate sign-extend 0<ior ;
: FILE-SIZE ( fh -- n ior )
   dup 0 SEEK_CUR lseek >r
   dup 0 SEEK_END lseek r> swap >r
         SEEK_SET lseek drop r> d0<ior ;
( Non-standard )
: NON-BLOCK ( fh -- ior ) F_SETFL FNDELAY fcntl ;

( Directories )
: OPEN-DIR ( a n -- dh ior ) s>z opendir d0=ior ;
: CLOSE-DIR ( dh -- ior ) closedir sign-extend 0<ior ;
: READ-DIR ( dh -- a n ) readdir dup if .d_name z>s else 0 then ;

( Other Utils )
: ms ( n -- ) 1000 * usleep drop ;
: ms-ticks ( -- n )
   0 >r 0 >r CLOCK_MONOTONIC_RAW rp@ cell - clock_gettime throw
   r> 1000000 / r> 1000 * + ;

( Shell ops )
: cd ( "path" -- ) bl parse s>z chdir throw ;
: mkdir ( "path" -- ) bl parse s>z 0777 mkdir throw ;
: rmdir ( "path" -- ) bl parse s>z rmdir throw ;
: pwd   here 1024 getcwd drop here z>s type cr ;

forth

( Setup entry )
internals : ok   ." Lugforth" raw-ok ; forth
