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

( A launcher for Uxn ROMs: window or terminal, resolution, mouse, sound... )
\
\   lugforth examples/system/varvara/launch.fs [options] [rom [file-or-arguments...]]
\
\ Modeled on the writhdeck-uxn launcher. Without a ROM it shows boot.rom.
\ Window mode (default) is varvara.fs, terminal mode (-c) is uxncli.fs with the
\ terminal put in raw mode and restored at the end. Run it with -h for the list
\ of options.

0 constant launching   ( tells varvara.fs and uxncli.fs not to start by themselves )
needs varvara.fs
needs uxncli.fs
also uxn

also posix
z" chdir" 1 sysfunc libc-chdir
z" cfmakeraw" 1 sysfunc libc-cfmakeraw
z" tcgetattr" 2 sysfunc libc-tcgetattr
z" tcsetattr" 3 sysfunc libc-tcsetattr
previous

( ---- what the options chose ---- )
0 value o-console    ( -c : run in the terminal )
0 value o-writhdeck  ( -w : writhdeck-uxn conventions )
0 value o-size       ( -s given )
0 value o-light
0 value o-mouse
0 value o-fonta   0 value o-fontn
0 value o-help
0 value o-first      ( index in argv of the ROM, 0 = none )
create tokens 256 allot
0 value ntok
create xtokens 256 allot     ( tokens given by -a )
0 value xn
0 value size-a   0 value size-n
0 value fnt-a    0 value fnt-n

: =s ( a n a2 n2 -- f ) compare 0= ;
: usage ( -- )
  ." usage: lugforth launch.fs [options] [rom [file or arguments...]]" cr
  cr
  ." window or terminal" cr
  ." -c, --console     terminal mode (uxncli): the terminal is put in raw mode" cr
  ." -z, --zoom N      zoom of the window, 1 to 8 (default 2)" cr
  ." -s, --size WxH    screen size at the start, for example 1280x800" cr
  ."                   (default 512x320; a ROM can resize it, and receives it as a" cr
  ."                   token with -w)" cr
  ." --no-mouse        ignore the mouse events" cr
  ." -q, --quiet       no sound" cr
  cr
  ." tokens for the second argument of the ROM, as writhdeck-uxn takes them" cr
  ." -w, --writhdeck   writhdeck-uxn: the ROM is writhdeck.rom (writhdeck-cli.rom with -c)" cr
  ."                   of the directory $WRITHDECK_UXN (default ~/src/writerdeck/writhdeck-uxn)" cr
  ."                   when none is given; the file is opened from its own directory;" cr
  ."                   the size, -l, -f and -m become the second argument" cr
  ." -l, --light       token light (start in the light theme)" cr
  ." -f, --font NAME   font: vga, cream, prop or creamprop (tokens f1 f2 f3)" cr
  ." -m, --mouse       token mouse (the ROM enables the mouse)" cr
  ." -a, --token WORD  any other token" cr
  ." -n, --new         writhdeck: empty draft, no file" cr
  ." -h, --help        this text" cr
  cr
  ." keys of the window: F1 zoom, F2 stacks, F3 screenshot, F4 reboot;" cr
  ." drop a ROM on the window to load it." cr ;

: die ( a n -- ) type cr 2 terminate ;

( ---- small parsers ---- )
0 value dv
: dec ( a n -- u f )   ( decimal digits only )
  0 to dv  dup 0= if 2drop 0 0 exit then
  0 ?do
    dup i + c@ dup [char] 0 < over [char] 9 > or if 2drop 0 0 unloop exit then
    [char] 0 - dv 10 * + to dv
  loop drop dv -1 ;
: wxh ( a n -- w h f )
  { a n }
  a n [char] x scan { b bn }
  bn 2 < if 0 0 0 exit then
  a b a - dec 0= if drop 0 0 0 exit then
  b 1+ bn 1- dec 0= if 2drop 0 0 0 exit then
  -1 ;

: tok+ ( a n -- )   ( append a token to the second argument )
  ntok if 32 tokens ntok + c! 1 +to ntok then
  dup >r  tokens ntok + swap cmove  r> +to ntok ;
: xtok+ ( a n -- )   ( a token of -a )
  xn 240 + 256 > if 2drop exit then
  xn if 32 xtokens xn + c! 1 +to xn then
  dup >r  xtokens xn + swap cmove  r> +to xn ;
: make-tokens ( -- )   ( in the order of the writhdeck-uxn launcher )
  0 to ntok
  o-size o-writhdeck and if size-a size-n tok+ then
  o-light if s" light" tok+ then
  fnt-n if fnt-a fnt-n tok+ then
  o-mouse if s" mouse" tok+ then
  xn if xtokens xn tok+ then ;

: env-or ( z a n -- a n )   ( the value of an environment variable, else a n )
  2>r libc-getenv ?dup if dup zlen 2r> 2drop else 2r> then ;

( ---- the writhdeck directory ---- )
create wd-path 1100 allot
0 value wn
: wd-add ( a n -- ) dup >r wd-path wn + swap cmove r> +to wn ;
: wd-rom ( -- a n )
  0 to wn
  z" WRITHDECK_UXN" libc-getenv ?dup if
    dup zlen 500 min wd-add
  else
    z" HOME" libc-getenv ?dup if dup zlen 500 min wd-add then
    s" /src/writerdeck/writhdeck-uxn" wd-add
  then
  o-console if s" /bin/writhdeck-cli.rom" else s" /bin/writhdeck.rom" then wd-add
  wd-path wn ;

( ---- the options ---- )
: need-arg ( i -- a n )   ( the argument after option i )
  1+ dup argc < 0= if drop s" missing value for the last option" die then argv ;

: font-token ( a n -- )
  2dup s" vga" =s if 2drop exit then
  2dup s" cream" =s if 2drop s" f1" to fnt-n to fnt-a exit then
  2dup s" prop" =s if 2drop s" f2" to fnt-n to fnt-a exit then
  2dup s" creamprop" =s if 2drop s" f3" to fnt-n to fnt-a exit then
  s" invalid font (vga, cream, prop or creamprop)" die ;

0 value gi
0 value stop
: opt-zoom ( -- )
  gi need-arg dec 0= if drop s" invalid zoom (1 to 8)" die then
  dup 1 < over 8 > or if drop s" invalid zoom (1 to 8)" die then
  to scale -1 to zoom-set ;
: opt-size ( -- )
  gi need-arg { a n }
  a n wxh 0= if 2drop s" invalid size (expected WIDTHxHEIGHT)" die then
  { w h }
  w 16 < w 4096 > or h 16 < or h 4096 > or if s" size out of range (16 to 4096)" die then
  w to init-w  h to init-h  -1 to o-size  a to size-a  n to size-n ;

: option ( -- )   ( the option at argv index gi, advances gi )
  gi argv { a n }
  a n s" -c" =s a n s" --console" =s or if -1 to o-console 1 +to gi exit then
  a n s" -w" =s a n s" --writhdeck" =s or if -1 to o-writhdeck 1 +to gi exit then
  a n s" -h" =s a n s" --help" =s or if usage 0 terminate then
  a n s" -l" =s a n s" --light" =s or if -1 to o-light 1 +to gi exit then
  a n s" -m" =s a n s" --mouse" =s or if -1 to o-mouse 1 +to gi exit then
  a n s" -n" =s a n s" --new" =s or if 1 +to gi exit then
  a n s" -q" =s a n s" --quiet" =s or if -1 to no-audio 1 +to gi exit then
  a n s" --no-mouse" =s if -1 to no-mouse 1 +to gi exit then
  a n s" -z" =s a n s" --zoom" =s or if opt-zoom 2 +to gi exit then
  a n s" -s" =s a n s" --size" =s or if opt-size 2 +to gi exit then
  a n s" -f" =s a n s" --font" =s or if gi need-arg font-token 2 +to gi exit then
  a n s" -a" =s a n s" --token" =s or if gi need-arg xtok+ 2 +to gi exit then
  a n s" --" =s if 1 +to gi gi argc < if gi to o-first then -1 to stop exit then
  s" unknown option: " type a n type cr usage 2 terminate ;

: parse-options ( -- )
  2 to gi  0 to o-first  0 to stop
  begin stop 0= gi argc < and while
    gi argv drop c@ [char] - = if option else gi to o-first -1 to stop then
  repeat ;

( ---- the ROM and its arguments ---- )
create file-dir 1100 allot   create file-base 1100 allot
0 value sp-a  0 value sp-n  0 value sp-i
: split-path ( a n -- )   ( the directory and the name of a path, in file-dir and file-base )
  to sp-n to sp-a
  sp-n to sp-i
  begin sp-i 0> while
    sp-i 1- to sp-i
    sp-i sp-a + c@ [char] / = if
      sp-a file-dir sp-i cmove  0 file-dir sp-i + c!
      sp-a sp-i 1+ + file-base sp-n sp-i - 1- cmove
      0 file-base sp-n sp-i - 1- + c!
      exit
    then
  repeat ;

: build-args ( r -- )   ( r : argv index before the first argument of the ROM )
  args-clear
  { r }
  r 1+ argc < if
    r 1+ argv
    o-writhdeck if
      ( the file is opened from its own directory: the File device refuses other paths )
      2dup [char] / scan nip if
        split-path file-dir libc-chdir drop  file-base dup zlen
      then
    then
    arg-add
  else
    ntok if s" " arg-add then
  then
  ntok if tokens ntok arg-add then
  r 2 + argc < if argc r 2 + ?do i argv arg-add loop then ;

: ends-rom? ( a n -- f ) dup 4 < if 2drop 0 exit then 4 - + 4 s" .rom" =s ;

create abs-in 1100 allot
create abs-out 4400 allot
: absolute ( a n -- a n )   ( the full path, because the directory may change )
  dup 1000 > if exit then
  { a n }
  a abs-in n cmove  0 abs-in n + c!
  abs-in abs-out libc-realpath if abs-out dup zlen else a n then ;

( ---- terminal mode ---- )
create tio-old 128 allot
create tio-new 128 allot
0 value raw?
: raw-on ( -- )
  0 tio-old libc-tcgetattr 0= if
    tio-old tio-new 128 cmove  tio-new libc-cfmakeraw drop
    tio-new 4 + dup ul@ 5 or swap l!   ( keep the translation of newlines )
    0 0 tio-new libc-tcsetattr drop  -1 to raw?
  then ;
: raw-off ( -- ) raw? if 0 0 tio-old libc-tcsetattr drop then ;

: run-console ( a n -- code )
  raw-on
  ['] uxncli-run catch
  raw-off
  ?dup if ." error " . cr 2drop 1 then ;

0 value l-r   0 value l-rom?
: launch ( -- )
  parse-options
  make-tokens
  o-first 0> if o-first argv ends-rom? o-writhdeck 0= or else 0 then to l-rom?
  l-rom? if o-first else o-first 0> if o-first 1- else argc 1- then then to l-r
  l-rom? 0= o-writhdeck 0= and if
    ( no ROM: boot.rom )
    o-console if s" no ROM to run" die then
    args-clear
    boot-rom varvara exit-code terminate
  then
  l-rom? if o-first argv else wd-rom then absolute { ra rn }
  l-r build-args
  o-console if
    o-size if s" launch: the size is ignored in terminal mode" type cr then
    ra rn run-console terminate
  then
  ra rn varvara exit-code terminate ;
launch
