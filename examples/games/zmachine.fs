\ A Z-machine interpreter: plays the interactive fiction story files of Infocom and of Inform
\ (.z3 .z4 .z5 .z8) in a terminal, the web terminal or the SDL console.
\
\     include examples/games/zmachine.fs
\     zplay story.z5                \ or:  s" story.z5" zstart
\ or from the shell:  lugforth examples/games/zmachine.fs story.z5
\
\ Written from the Z-Machine Standards Document 1.1 (Graham Nelson, https://inform-fiction.org/zmachine/standards/);
\ no code of another interpreter is used. Fortz (Brad Shepherdson, gforth, GPL) was the inspiration.
\ Versions 3, 4, 5 and 8 (the Inform default and most of Infocom); not 6 (pictures), not 1, 2 and 7.
\ Text only: no upper window (a story that draws a status line there shows nothing for it),
\ no colours, no sound, no undo. The status line of the version 3 stories is not shown unless
\ you say  -1 zm-status !  ( [location Score: s Moves: m] before each question ).
\ Save and restore use a file of our own (not Quetzal), asked on the line. Accents are UTF-8 on the screen
\ and on the keyboard. The input line has the history and the editing keys of the console.
\
\ The memory of the story is a buffer; the Z-machine stack (frames and evaluation stack) is one
\ array of cells. All the values are kept as 16 bits unsigned and signed with s16 when needed.

variable zm-status
[defined] ms-ticks 0= [if] : ms-ticks ( -- ms ) utime 1000 um/mod nip ; [then]   \ gforth
vocabulary zm   also zm definitions

\ ---- State ----
0 value zmem                  \ the story file in memory
0 value zsize
0 value zver
0 value zglobals   0 value zstatic   0 value zobjt   0 value zabbr
variable zpc   variable zrun   variable zlast
create zpath 256 allot   variable zpathn

: s16 ( u -- n ) $ffff and dup $8000 and if $10000 - then ;

: zb@ ( a -- b ) zmem + c@ ;
: zw@ ( a -- w ) dup zb@ 8 lshift swap 1+ zb@ or ;
: zb! ( b a -- ) zmem + c! ;
: zw! ( w a -- ) >r $ffff and dup 8 rshift r@ zb! r> 1+ zb! ;

\ ---- Output: lines are broken at the width, on the spaces ----
create lb 300 allot   variable ll   variable lj
defer ztype   ' type is ztype      \ where the text goes (a test captures it)
defer zcr   ' cr is zcr
78 value zwidth
: lb-flush ( -- ) lb ll @ ztype 0 ll ! ;
: lb-space ( -- i ) 0 ll @ 0 ?do lb i + c@ 32 = if drop i then loop ;
: lb-break ( -- )
  lb-space dup 0= if drop lb-flush zcr exit then
  dup lb swap ztype zcr  1+ lj !
  lb lj @ +  lb  ll @ lj @ -  cmove
  ll @ lj @ - ll ! ;
: lb-put ( c -- ) lb ll @ + c!  1 ll +! ;

create zuni0   \ ZSCII 155 to 223 as Unicode (the default table)
$e4 , $f6 , $fc , $c4 , $d6 , $dc , $df , $bb , $ab , $eb , $ef , $ff , $cb , $cf ,
$e1 , $e9 , $ed , $f3 , $fa , $fd , $c1 , $c9 , $cd , $d3 , $da , $dd ,
$e0 , $e8 , $ec , $f2 , $f9 , $c0 , $c8 , $cc , $d2 , $d9 ,
$e2 , $ea , $ee , $f4 , $fb , $c2 , $ca , $ce , $d4 , $db , $e5 , $c5 , $f8 , $d8 ,
$e3 , $f1 , $f5 , $c3 , $d1 , $d5 , $e6 , $c6 , $e7 , $c7 , $fe , $f0 , $de , $d0 ,
$a3 , $153 , $152 , $a1 , $bf ,

create zuni 97 cells allot   variable zun      \ the table of the story (its header extension) or the default one
: zuni-default ( -- ) 69 zun !  69 0 do zuni0 i cells + @ zuni i cells + ! loop ;
zuni-default
: zuni-find ( cp -- zscii ) 63 zun @ 0 ?do over zuni i cells + @ = if drop i 155 + leave then loop nip ;
: lb-utf8 ( cp -- ) dup 6 rshift $c0 or lb-put $3f and $80 or lb-put ;

: zscreen ( c -- )
  dup 13 = if drop lb-flush zcr exit then
  dup 155 zun @ 155 + within if zuni swap 155 - cells + @ lb-utf8 else
  dup 32 127 within 0= if drop exit then lb-put then
  ll @ zwidth >= if lb-break then ;

\ output streams: 3 is a table in memory
create mta 6 cells allot   create mtn 6 cells allot   variable mtop   variable zwin
: mt-a ( -- a ) mtop @ cells mta + ;
: mt-n ( -- a ) mtop @ cells mtn + ;
: zout ( c -- )
  mtop @ if mt-a @ mt-n @ + 2 + zb!  1 mt-n +! exit then
  zwin @ if drop exit then
  zscreen ;

: zhalt ( c-addr u -- )
  lb-flush cr ." [Z-machine: " type ."  at " zlast @ . ." ]" cr   0 zrun ! ;

\ ---- The Z-machine stack: frames and evaluation stack in one array ----
\ frame at zfp: +0 return pc, +1 previous frame, +2 variable to store the result (-1: none),
\ +3 number of arguments, +4 number of locals, +5.. the locals, then the evaluation stack
16384 constant zstk-n
0 value zstk
variable zsp   variable zfp
: zs@ ( i -- v ) cells zstk + @ ;
: zs! ( v i -- ) cells zstk + ! ;
: zpush ( v -- )
  zsp @ zstk-n 8 - > if s" stack overflow" zhalt drop exit then
  zsp @ zs!  1 zsp +! ;
: zpop ( -- v ) zsp @ zfp @ 5 + <= if s" stack underflow" zhalt 0 exit then -1 zsp +! zsp @ zs@ ;

: zlocal ( n -- i ) zfp @ 4 + + ;
: zglob ( n -- a ) 16 - 2* zglobals + ;
: zvar@ ( n -- v )
  dup 0= if drop zpop exit then
  dup 16 < if zlocal zs@ exit then
  zglob zw@ ;
: zvar! ( v n -- )
  dup 0= if drop zpush exit then
  dup 16 < if zlocal zs! exit then
  zglob zw! ;
: zvar@i ( n -- v ) dup 0= if drop zsp @ 1- zs@ exit then zvar@ ;   \ indirect: the stack is read in place
: zvar!i ( v n -- ) dup 0= if drop zsp @ 1- zs! exit then zvar! ;

\ ---- Instruction stream ----
: zf8 ( -- b ) zpc @ dup 1+ zpc ! zb@ ;
: zf16 ( -- w ) zf8 8 lshift zf8 or ;

create zop 8 cells allot   variable znop
: op@ ( n -- v ) cells zop + @ ;
: zadd ( v -- ) znop @ cells zop + !  1 znop +! ;
: zarg ( type -- v ) dup 0= if drop zf16 exit then  1 = if zf8 exit then  zf8 zvar@ ;
variable ztb   variable ztb2
: ztypes ( b -- )   \ up to 4 operands from the two-bit types
  ztb !
  4 0 do
    ztb @ 6 i 2* - rshift 3 and
    dup 3 = if drop leave then
    zarg zadd
  loop ;

: zstore ( v -- ) $ffff and zf8 zvar! ;

variable br-cond   variable br-off
: zret ( v -- )
  zfp @ 2 + zs@ >r
  zfp @ zsp !
  zfp @ zs@ zpc !
  zfp @ 1+ zs@ zfp !
  r> dup 0< if 2drop else zvar! then ;
: zbr ( f -- )   \ branch information follows
  zf8 dup $80 and 0<> br-cond !
  dup $40 and if $3f and else $3f and 8 lshift zf8 or  dup $2000 and if $4000 - then then
  br-off !
  0<> br-cond @ = if
    br-off @ 2 u< if br-off @ zret else zpc @ br-off @ + 2 - zpc ! then
  then ;

\ ---- Routines ----
: zunpack ( p -- a ) zver 4 < if 2* exit then zver 8 = if 8 * else 4 * then ;
variable zcn   variable zcl
: zcall ( store -- )
  0 op@ 0= if dup 0< if drop else 0 swap zvar! then exit then
  zsp @ zstk-n 64 - > if drop s" stack overflow" zhalt exit then
  >r
  0 op@ zunpack zcn !  zcn @ zb@ zcl !  1 zcn +!
  zpc @ zsp @ zs!
  zfp @ zsp @ 1+ zs!
  r> zsp @ 2 + zs!
  znop @ 1- zsp @ 3 + zs!
  zcl @ zsp @ 4 + zs!
  zsp @ zfp !   5 zsp +!
  zcl @ 0 ?do
    zver 5 < if zcn @ zw@ 2 zcn +! else 0 then
    zsp @ i + zs!
  loop
  znop @ 1- zcl @ min 0 ?do  i 1+ op@ zfp @ 5 + i + zs!  loop
  zcl @ zsp +!
  zcn @ zpc ! ;

\ ---- Text: Z-characters ----
create zalpha 80 allot   \ the 26 letters of each of the 3 alphabets, built without a string holding a quote
: zalpha-default ( -- )
  s" abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ  0123456789.,!?_#'" zalpha swap cmove
  34 zalpha 71 + c!   47 zalpha 72 + c!   92 zalpha 73 + c!
  s" -:()" zalpha 74 + swap cmove ;
zalpha-default
variable za   variable zab   variable zesc   variable zhi

: zscii-out ( c -- ) zout ;
: zch-esc ( z -- )
  zesc @ 1 = if 5 lshift zhi !  2 zesc !
  else zhi @ or zscii-out  0 zesc !  0 za ! then ;
defer zprint-str
: zch-abbr ( z -- )
  zab @ 1- 32 * + 2* zabbr + zw@ 2* zprint-str drop
  0 zab !  0 za !  0 zesc ! ;
: zch ( z -- )
  zesc @ if zch-esc exit then
  zab @ if zch-abbr exit then
  dup 1 4 within if zab ! exit then
  dup 4 = if drop 1 za ! exit then
  dup 5 = if drop 2 za ! exit then
  dup 0= if drop 32 zscii-out 0 za ! exit then
  za @ 2 = over 6 = and if drop 1 zesc ! exit then
  za @ 2 = over 7 = and if drop 13 zscii-out 0 za ! exit then
  6 - za @ 26 * + zalpha + c@ zscii-out  0 za ! ;
: zword-chars ( w -- ) dup 10 rshift 31 and zch  dup 5 rshift 31 and zch  31 and zch ;
:noname ( addr -- end )
  0 za !  0 zab !  0 zesc !
  begin dup zw@ >r 2 + r@ zword-chars r> $8000 and until ; is zprint-str

\ ---- Objects ----
: zdefs ( -- n ) zver 4 < if 31 else 63 then ;
: zosz ( -- n ) zver 4 < if 9 else 14 then ;
: zattrb ( -- n ) zver 4 < if 4 else 6 then ;
: zoa ( obj -- a ) 1- zosz * zobjt zdefs 2* + + ;
: zlink ( obj k -- a ) swap zoa zattrb +  swap zver 4 < if else 2* then + ;
: zl@ ( a -- n ) zver 4 < if zb@ else zw@ then ;
: zl! ( n a -- ) zver 4 < if zb! else zw! then ;
: zpar ( obj -- a ) 0 zlink ;
: zsib ( obj -- a ) 1 zlink ;
: zchi ( obj -- a ) 2 zlink ;
: zpt ( obj -- a ) zoa zattrb + zver 4 < if 3 else 6 then + zw@ ;   \ property table
: zpname-end ( obj -- a ) zpt dup zb@ 2* + 1+ ;                       \ first property

variable zo1
: zunlink ( obj -- )
  zo1 !
  zo1 @ zpar zl@ dup 0= if drop exit then
  dup zchi zl@ zo1 @ = if
    zo1 @ zsib zl@ swap zchi zl!
  else
    zchi zl@
    begin dup zsib zl@ zo1 @ <> while zsib zl@ repeat
    zo1 @ zsib zl@ swap zsib zl!
  then
  0 zo1 @ zpar zl!   0 zo1 @ zsib zl! ;

: zattr ( obj attr -- a mask )
  dup 7 and 7 swap - 1 swap lshift >r  3 rshift swap zoa + r> ;

: zpinfo ( a -- num size data )
  dup zb@
  zver 4 < if
    dup 31 and swap 5 rshift 1+ rot 1+
  else
    dup $80 and if
      63 and swap dup 1+ zb@ 63 and dup 0= if drop 64 then swap 2 +
    else
      dup 63 and swap $40 and if 2 else 1 then rot 1+
    then
  then ;

variable zpn
: zpfind ( obj prop -- data size )   \ data 0 if the object does not have it
  zpn !  zpname-end
  begin dup zb@ while
    dup zpinfo
    rot zpn @ = if swap rot drop exit then
    + nip
  repeat drop 0 0 ;

: zplen ( data -- size )
  1- zb@
  zver 4 < if 5 rshift 1+ exit then
  dup $80 and if 63 and dup 0= if drop 64 then exit then
  $40 and if 2 else 1 then ;

\ ---- Words of the input ----
create zline 260 allot   create zl 260 allot
variable zci   variable zco
: zconv ( n -- m )   \ zline (host bytes, UTF-8) to zl (ZSCII, lower case)
  0 zci !  0 zco !
  begin zci @ over < while
    zline zci @ + c@  1 zci +!
    dup $c0 and $c0 = if
      $1f and 6 lshift  zline zci @ + c@ $3f and or  1 zci +!  zuni-find
    else dup 65 91 within if 32 + then then
    zl zco @ + c!  1 zco +!
  repeat drop zco @ ;

\ The line editor: that of the console in Lugforth (without its "> " prompt); in gforth accept on a terminal,
\ else read-line, which sees the end of the input (ports/gforth/zmachine-gforth.fs).
[defined] gforth 0= [if]
also internals   arrow constant zarrow   ' edit-accept constant zacc   previous
: zedit-line ( -- n )
  zarrow @ >r  0 zarrow !
  zline 250 zacc execute
  r> zarrow ! ;
[else]
: zedit-line ( -- n )
  stdout flush-file drop
  stdin isatty if zline 250 accept cr exit then      \ gforth's own editing, and the echo of what is typed
  zline 250 stdin read-line throw 0= if drop 255 zline c! 1 then ;
[then]
defer zgetline   ' zedit-line is zgetline     \ ( -- n ) a line of n bytes in zline
: zread-line ( -- n )
  lb-flush
  zgetline
  dup 0> if zline c@ 255 = if drop 0  0 zrun ! then then   \ end of the input
  zconv ;

create eb 8 allot   create zq 12 allot   variable zqn
: ezmax ( -- n ) zver 4 < if 6 else 9 then ;
: ezq ( z -- ) zqn @ ezmax < if zq zqn @ + c!  1 zqn +! else drop then ;
: afind { c lo hi -- k }   \ where c is in zalpha between lo and hi, or -1
  -1  hi lo ?do c zalpha i + c@ = if drop i leave then loop ;
: ez-char ( c -- )
  dup 0 26 afind dup 0< 0= if nip 6 + ezq exit then drop
  dup 26 52 afind dup 0< 0= if nip 20 - 4 ezq ezq exit then drop
  dup 54 78 afind dup 0< 0= if nip 46 - 5 ezq ezq exit then drop
  5 ezq 6 ezq  dup 5 rshift ezq  31 and ezq ;
: zpack ( -- )
  ezmax 3 / 0 do
    zq i 3 * +  dup c@ 10 lshift  over 1+ c@ 5 lshift or  swap 2 + c@ or
    i ezmax 3 / 1- = if $8000 or then
    dup 8 rshift eb i 2* + c!   eb i 2* + 1+ c!
  loop ;
: zenc ( addr len -- )   \ the encoded word in eb
  0 zqn !  0 ?do dup i + zb@ ez-char loop drop
  begin zqn @ ezmax < while 5 ezq repeat
  zpack ;

variable dl   variable dn   variable da
: zdict-setup ( dict -- ) dup zb@ + 1+  dup zb@ dl !  1+ dup zw@ s16 dn !  2 + da ! ;
: zmatch? ( entry -- f )
  ezmax 3 / 2* 0 do  dup i + zb@ eb i + c@ <> if drop 0 unloop exit then loop drop -1 ;
: zlook ( -- entry )   \ 0 if the word is not in the dictionary
  dn @ abs 0 ?do  da @ dl @ i * +  dup zmatch? if unloop exit then drop loop 0 ;

variable tk-t   variable tk-p   variable tk-d   variable tk-skip
variable tk-i   variable tk-ws   variable tk-wl   variable tk-n
: tk-len ( -- n )
  zver 5 >= if tk-t @ 1+ zb@ exit then
  0 begin tk-t @ 1+ over + zb@ while 1+ repeat ;
: tk-base ( -- a ) tk-t @ zver 5 >= if 2 else 1 then + ;
: tk-char ( i -- c ) tk-base + zb@ ;
: tk-sep? ( c -- f )
  tk-d @ zb@ 0 ?do dup tk-d @ 1+ i + zb@ = if drop -1 unloop exit then loop drop 0 ;
: tk-skip-sp ( -- )
  begin tk-i @ tk-len < if tk-i @ tk-char 32 = else 0 then while 1 tk-i +! repeat ;
: tk-next ( -- )   \ the next word: tk-ws, tk-wl (0: none)
  tk-skip-sp  tk-i @ tk-ws !  0 tk-wl !
  tk-i @ tk-len >= if exit then
  tk-i @ tk-char tk-sep? if 1 tk-wl !  1 tk-i +! exit then
  begin
    tk-i @ tk-len < if tk-i @ tk-char dup 32 <> swap tk-sep? 0= and else 0 then
  while 1 tk-i +!  1 tk-wl +! repeat ;
: tk-store ( entry -- )
  tk-skip @ over 0= and if drop exit then
  tk-p @ 2 + tk-n @ 4 * +
  swap over zw!
  tk-wl @ over 2 + zb!
  tk-ws @ tk-base tk-t @ - + swap 3 + zb! ;
: tokenise ( text parse dict skip -- )
  tk-skip !  dup 0= if drop $08 zw@ then  tk-d !  tk-p !  tk-t !
  tk-d @ zdict-setup
  0 tk-i !  0 tk-n !
  begin
    tk-next  tk-wl @ 0<>  tk-n @ tk-p @ zb@ <  and
  while
    tk-base tk-ws @ + tk-wl @ zenc  zlook tk-store
    1 tk-n +!
  repeat
  tk-n @ tk-p @ 1+ zb! ;

\ ---- Opcodes ----
: s/ ( a b -- q ) 2dup xor 0< >r abs swap abs swap / r> if negate then ;
: smod ( a b -- r ) over >r abs swap abs swap mod r> 0< if negate then ;
: zdivzero ( -- ) s" division by zero" zhalt ;

: z-je 0 op@ 0  znop @ 1 ?do over i op@ = if drop -1 then loop nip zbr ;
: z-jl 0 op@ s16 1 op@ s16 < zbr ;
: z-jg 0 op@ s16 1 op@ s16 > zbr ;
: zadd-var ( var delta -- new )  >r dup zvar@i r> + $ffff and  tuck swap zvar!i ;
: z-dec_chk 0 op@ -1 zadd-var s16 1 op@ s16 < zbr ;
: z-inc_chk 0 op@ 1 zadd-var s16 1 op@ s16 > zbr ;
: z-jin 0 op@ zpar zl@ 1 op@ = zbr ;
: z-test 0 op@ 1 op@ tuck and = zbr ;
: z-or 0 op@ 1 op@ or zstore ;
: z-and 0 op@ 1 op@ and zstore ;
: z-test_attr 0 op@ 1 op@ zattr >r zb@ r> and 0<> zbr ;
: z-set_attr 0 op@ 1 op@ zattr >r dup zb@ r> or swap zb! ;
: z-clear_attr 0 op@ 1 op@ zattr invert >r dup zb@ r> and swap zb! ;
: z-store 1 op@ 0 op@ zvar!i ;
: z-insert_obj
  0 op@ zunlink
  1 op@ zchi zl@ 0 op@ zsib zl!
  0 op@ 1 op@ zchi zl!
  1 op@ 0 op@ zpar zl! ;
: z-loadw 0 op@ 1 op@ 2* + zw@ zstore ;
: z-loadb 0 op@ 1 op@ + zb@ zstore ;
: z-get_prop
  0 op@ 1 op@ zpfind
  over 0= if 2drop 1 op@ 1- 2* zobjt + zw@ zstore exit then
  1 = if zb@ else zw@ then zstore ;
: z-get_prop_addr 0 op@ 1 op@ zpfind drop zstore ;
: z-get_next_prop
  0 op@ 1 op@ ?dup if
    zpfind over 0= if 2drop 0 zstore exit then
    +
  else drop zpname-end then
  dup zb@ 0= if drop 0 else zpinfo 2drop then zstore ;
: z-add 0 op@ s16 1 op@ s16 + zstore ;
: z-sub 0 op@ s16 1 op@ s16 - zstore ;
: z-mul 0 op@ s16 1 op@ s16 * zstore ;
: z-div 1 op@ s16 0= if zdivzero exit then 0 op@ s16 1 op@ s16 s/ zstore ;
: z-mod 1 op@ s16 0= if zdivzero exit then 0 op@ s16 1 op@ s16 smod zstore ;
: z-call_vs zf8 zcall ;
: z-call_vn -1 zcall ;
: z-set_colour ;
: z-throw 0 op@ 1 op@ zfp !  zret ;

: z-jz 0 op@ 0= zbr ;
: z-get_sibling 0 op@ zsib zl@ dup zstore 0<> zbr ;
: z-get_child 0 op@ zchi zl@ dup zstore 0<> zbr ;
: z-get_parent 0 op@ zpar zl@ zstore ;
: z-get_prop_len 0 op@ dup 0= if else zplen then zstore ;
: z-inc 0 op@ 1 zadd-var drop ;
: z-dec 0 op@ -1 zadd-var drop ;
: z-print_addr 0 op@ zprint-str drop ;
: z-remove_obj 0 op@ zunlink ;
: z-print_obj 0 op@ zpt 1+ zprint-str drop ;
: z-ret 0 op@ zret ;
: z-jump 0 op@ s16 zpc @ + 2 - zpc ! ;
: z-print_paddr 0 op@ zunpack zprint-str drop ;
: z-load 0 op@ zvar@i zstore ;
: z-not 0 op@ invert zstore ;

: z-rtrue 1 zret ;
: z-rfalse 0 zret ;
: z-print zpc @ zprint-str zpc ! ;
: z-print_ret z-print 13 zout 1 zret ;
: z-nop ;
: z-ret_popped zpop zret ;
: z-pop/catch zver 5 < if zpop drop else zfp @ zstore then ;
: z-quit 0 zrun ! ;
: z-new_line 13 zout ;
: zpr-u ( u -- ) dup 9 > if dup 10 / recurse then 10 mod 48 + zout ;
: zpr-n ( n -- ) dup 0< if 45 zout negate then zpr-u ;

: z-show_status
  zm-status @ 0= if exit then
  91 zout 32 zout 16 zvar@ zpt 1+ zprint-str drop
  s"   Score: " 0 ?do dup i + c@ zout loop drop 17 zvar@ s16 zpr-n
  s"  Moves: " 0 ?do dup i + c@ zout loop drop 18 zvar@ s16 zpr-n  93 zout 13 zout ;
: z-verify -1 zbr ;
: z-piracy -1 zbr ;

: z-storew 2 op@ 0 op@ 1 op@ 2* + zw! ;
: z-storeb 2 op@ 0 op@ 1 op@ + zb! ;
: z-put_prop
  0 op@ 1 op@ zpfind
  over 0= if 2drop s" property missing" zhalt exit then
  1 = if 2 op@ swap zb! else 2 op@ swap zw! then ;

variable rd-t   variable rd-p
: z-read
  zver 4 < if z-show_status then
  0 op@ rd-t !  znop @ 1 > if 1 op@ else 0 then rd-p !
  zread-line
  rd-t @ zb@ zver 5 < if 1- then min
  zver 5 < if
    dup 0 ?do zl i + c@ rd-t @ 1+ i + zb! loop
    0 rd-t @ 1+ rot + zb!
  else
    dup rd-t @ 1+ zb!
    0 ?do zl i + c@ rd-t @ 2 + i + zb! loop
  then
  rd-p @ if rd-t @ rd-p @ 0 0 tokenise then
  zver 5 >= if 13 zstore then ;
: z-print_char 0 op@ zout ;
: z-print_num 0 op@ s16 zpr-n ;

variable zseed
: zrand ( -- u ) zseed @ dup 13 lshift xor $ffffffff and  dup 17 rshift xor  dup 5 lshift xor $ffffffff and  dup zseed ! ;
: z-random
  0 op@ s16 dup 0 > if zrand $7fffffff and swap mod 1+ zstore exit then
  dup 0= if drop ms-ticks 1 or zseed ! else negate zseed ! then
  0 zstore ;
: z-push 0 op@ zpush ;
: z-pull zpop 0 op@ zvar!i ;
: z-split_window ;
: z-set_window 0 op@ zwin ! ;
: z-erase_window ;
: z-erase_line ;
: z-set_cursor ;
: z-get_cursor 1 0 op@ zw!  1 0 op@ 2 + zw! ;
: z-set_text_style ;
: z-buffer_mode ;
: z-output_stream
  0 op@ s16 dup 3 = if
    drop mtop @ 4 < if 1 mtop +!  1 op@ mt-a !  0 mt-n ! then exit
  then
  -3 = if mtop @ if mt-n @ mt-a @ zw!  -1 mtop +! then then ;
: z-input_stream ;
: z-sound_effect ;
: z-read_char lb-flush key dup 10 = if drop 13 then zstore ;
variable sc-w   variable sc-e   variable sc-x   variable sc-a
: sc-get ( a -- v ) sc-w @ if zw@ else zb@ then ;
: z-scan_table
  0 op@ sc-x !  1 op@ sc-a !
  znop @ 3 > if 3 op@ else $82 then  dup $80 and 0<> sc-w !  $7f and sc-e !
  0
  2 op@ 0 ?do  sc-a @ sc-get sc-x @ = if drop sc-a @ leave then  sc-e @ sc-a +!  loop
  dup zstore  0<> zbr ;
variable ct-f   variable ct-s   variable ct-n
: ct-fwd ct-n @ 0 ?do ct-f @ i + zb@ ct-s @ i + zb! loop ;
: ct-bwd ct-n @ 0 ?do ct-f @ ct-n @ 1- i - + zb@  ct-s @ ct-n @ 1- i - + zb! loop ;
: z-copy_table
  0 op@ ct-f !  1 op@ ct-s !  2 op@ s16 ct-n !
  ct-s @ 0= if ct-n @ abs 0 ?do 0 ct-f @ i + zb! loop exit then
  ct-n @ 0< if ct-n @ negate ct-n !  ct-fwd exit then
  ct-f @ ct-s @ < if ct-bwd else ct-fwd then ;
: z-print_table
  1 op@  znop @ 2 > if 2 op@ else 1 then  0 ?do
    dup 0 ?do 0 op@ j 1 op@ * + i + zb@ zout loop
    13 zout
  loop drop ;
: z-tokenise 0 op@ 1 op@ znop @ 2 > if 2 op@ else 0 then znop @ 3 > if 3 op@ else 0 then tokenise ;
: z-encode_text
  0 op@ 2 op@ + 1 op@ zenc
  ezmax 3 / 2* 0 ?do eb i + c@ 3 op@ i + zb! loop ;
: z-check_arg_count 0 op@ zfp @ 3 + zs@ <= zbr ;
: z-log_shift 0 op@ 1 op@ s16 dup 0< if negate rshift else lshift then zstore ;
: z-art_shift 0 op@ s16 1 op@ s16 dup 0< if negate 0 ?do 2/ loop else lshift then zstore ;
: z-set_font 0 op@ 1 = if 1 else 0 then zstore ;
: z-print_unicode 0 op@ dup 127 < if zout else zuni-find zout then ;
: z-check_unicode 3 zstore ;
: z-save_undo $ffff zstore ;
: z-restore_undo 0 zstore ;

\ ---- Save, restore, restart ----
create i32 4 allot
: wr32 ( n fid -- ) swap dup i32 c!  dup 8 rshift i32 1+ c!  dup 16 rshift i32 2 + c!  24 rshift i32 3 + c!
  i32 4 rot write-file drop ;
: rd32 ( fid -- n ) >r i32 4 r> read-file drop drop
  i32 c@  i32 1+ c@ 8 lshift or  i32 2 + c@ 16 lshift or  i32 3 + c@ 24 lshift or
  dup $80000000 and if $100000000 - then ;
$5a534156 constant zmagic
create zfname 80 allot
variable zfl   variable zfid
: zname ( -- a n )
  lb-flush ." File name (empty: zstory.sav): "
  zread-line dup 0= if drop s" zstory.sav" else zl swap then ;
: zsave-file ( a n -- ok )
  w/o create-file if drop 0 exit then zfid !
  zmagic zfid @ wr32  zpc @ zfid @ wr32  zsp @ zfid @ wr32  zfp @ zfid @ wr32  zstatic zfid @ wr32
  zmem zstatic zfid @ write-file drop
  zsp @ 0 ?do i zs@ zfid @ wr32 loop
  zfid @ close-file drop -1 ;
create ztmp 32 allot
: zrestore-file ( a n -- ok )
  r/o open-file if drop 0 exit then zfid !
  zfid @ rd32 zmagic <> if zfid @ close-file drop 0 exit then
  zfid @ rd32 ztmp !  zfid @ rd32 ztmp 8 + !  zfid @ rd32 ztmp 16 + !
  zfid @ rd32 zstatic <> if zfid @ close-file drop 0 exit then
  zstatic allocate if drop zfid @ close-file drop 0 exit then
  dup zstatic zfid @ read-file drop drop
  dup 2 + c@ zmem 2 + c@ <>  over 3 + c@ zmem 3 + c@ <> or
  over $12 + c@ zmem $12 + c@ <> or  over $17 + c@ zmem $17 + c@ <> or
  if free drop zfid @ close-file drop 0 exit then
  dup zmem zstatic cmove free drop
  ztmp @ zpc !  ztmp 8 + @ zsp !  ztmp 16 + @ zfp !
  zsp @ 0 ?do zfid @ rd32 i zs! loop
  zfid @ close-file drop -1 ;

: zheader ( -- )   \ what this interpreter says about itself
  zver 4 < if 1 zb@ $10 or $9f and 1 zb! then
  zver 3 > if 0 1 zb!  6 $1e zb!  65 $1f zb!  255 $20 zb!  80 $21 zb! then
  zver 4 > if 80 $22 zw!  255 $24 zw!  $0101 $26 zw!  $0101 $32 zw! then
  $10 zb@ $fe and $10 zb!  $11 zb@ 3 and $11 zb! ;

: z-save
  zname zsave-file
  zver 4 < if zbr else if 1 else 0 then zstore then ;
: z-restore
  zname zrestore-file
  zheader
  zver 4 < if zbr else if 2 else 0 then zstore then ;
: z-ext_save z-save ;
: z-ext_restore z-restore ;

defer zrestart
: z-restart zrestart ;

\ ---- Tables of opcodes ----
: zbad ( -- ) s" unknown opcode" zhalt ;
: tab: create 32 0 do ['] zbad , loop ;
tab: t0   tab: t1   tab: t2   tab: tv   tab: te
: t! ( xt n table -- ) swap cells + ! ;
: t@x ( n table -- ) swap cells + @ execute ;

' z-je 1 t2 t!   ' z-jl 2 t2 t!   ' z-jg 3 t2 t!   ' z-dec_chk 4 t2 t!   ' z-inc_chk 5 t2 t!
' z-jin 6 t2 t!   ' z-test 7 t2 t!   ' z-or 8 t2 t!   ' z-and 9 t2 t!   ' z-test_attr 10 t2 t!
' z-set_attr 11 t2 t!   ' z-clear_attr 12 t2 t!   ' z-store 13 t2 t!   ' z-insert_obj 14 t2 t!
' z-loadw 15 t2 t!   ' z-loadb 16 t2 t!   ' z-get_prop 17 t2 t!   ' z-get_prop_addr 18 t2 t!
' z-get_next_prop 19 t2 t!   ' z-add 20 t2 t!   ' z-sub 21 t2 t!   ' z-mul 22 t2 t!
' z-div 23 t2 t!   ' z-mod 24 t2 t!   ' z-call_vs 25 t2 t!   ' z-call_vn 26 t2 t!
' z-set_colour 27 t2 t!   ' z-throw 28 t2 t!

' z-jz 0 t1 t!   ' z-get_sibling 1 t1 t!   ' z-get_child 2 t1 t!   ' z-get_parent 3 t1 t!
' z-get_prop_len 4 t1 t!   ' z-inc 5 t1 t!   ' z-dec 6 t1 t!   ' z-print_addr 7 t1 t!
' z-call_vs 8 t1 t!   ' z-remove_obj 9 t1 t!   ' z-print_obj 10 t1 t!   ' z-ret 11 t1 t!
' z-jump 12 t1 t!   ' z-print_paddr 13 t1 t!   ' z-load 14 t1 t!

' z-rtrue 0 t0 t!   ' z-rfalse 1 t0 t!   ' z-print 2 t0 t!   ' z-print_ret 3 t0 t!   ' z-nop 4 t0 t!
' z-save 5 t0 t!   ' z-restore 6 t0 t!   ' z-restart 7 t0 t!   ' z-ret_popped 8 t0 t!
' z-pop/catch 9 t0 t!   ' z-quit 10 t0 t!   ' z-new_line 11 t0 t!   ' z-show_status 12 t0 t!
' z-verify 13 t0 t!   ' z-piracy 15 t0 t!

' z-call_vs 0 tv t!   ' z-storew 1 tv t!   ' z-storeb 2 tv t!   ' z-put_prop 3 tv t!   ' z-read 4 tv t!
' z-print_char 5 tv t!   ' z-print_num 6 tv t!   ' z-random 7 tv t!   ' z-push 8 tv t!   ' z-pull 9 tv t!
' z-split_window 10 tv t!   ' z-set_window 11 tv t!   ' z-call_vs 12 tv t!   ' z-erase_window 13 tv t!
' z-erase_line 14 tv t!   ' z-set_cursor 15 tv t!   ' z-get_cursor 16 tv t!   ' z-set_text_style 17 tv t!
' z-buffer_mode 18 tv t!   ' z-output_stream 19 tv t!   ' z-input_stream 20 tv t!   ' z-sound_effect 21 tv t!
' z-read_char 22 tv t!   ' z-scan_table 23 tv t!   ' z-not 24 tv t!   ' z-call_vn 25 tv t!
' z-call_vn 26 tv t!   ' z-tokenise 27 tv t!   ' z-encode_text 28 tv t!   ' z-copy_table 29 tv t!
' z-print_table 30 tv t!   ' z-check_arg_count 31 tv t!

' z-ext_save 0 te t!   ' z-ext_restore 1 te t!   ' z-log_shift 2 te t!   ' z-art_shift 3 te t!
' z-set_font 4 te t!   ' z-save_undo 9 te t!   ' z-restore_undo 10 te t!
' z-print_unicode 11 te t!   ' z-check_unicode 12 te t!

\ version dependent places: 1OP 15 is not (v4 and before) or call_1n (v5), 0OP 9, the read store
: zfix ( -- )
  zver 5 < if ['] z-not else ['] z-call_vn then 15 t1 t! ;

\ ---- Decoding and the main loop ----
: zdo-long ( b -- )
  0 znop !
  dup $40 and if 2 else 1 then zarg zadd
  dup $20 and if 2 else 1 then zarg zadd
  $1f and t2 t@x ;
: zdo-short ( b -- )
  0 znop !
  dup 4 rshift 3 and dup 3 = if drop $0f and t0 t@x exit then
  zarg zadd  $0f and t1 t@x ;
: zdo-var ( b -- )
  0 znop !
  zf8
  over dup 236 = swap 250 = or if zf8 else 255 then ztb2 !
  ztypes
  znop @ 4 = if ztb2 @ ztypes then
  dup $20 and if $1f and tv else $1f and t2 then t@x ;
: zdo-ext ( -- )
  0 znop !
  zf8 zf8 ztypes te t@x ;
: zstep ( -- )
  zpc @ zlast !
  zf8
  dup $be = zver 5 >= and if drop zdo-ext exit then
  dup $80 and 0= if zdo-long exit then
  dup $c0 and $80 = if zdo-short exit then
  zdo-var ;
: zrunloop ( -- ) begin zrun @ while zstep repeat lb-flush ;

\ ---- Start ----
: zsetup ( -- )
  0 zb@ to zver
  $0c zw@ to zglobals   $0e zw@ to zstatic   $0a zw@ to zobjt   $18 zw@ to zabbr
  zalpha-default   zuni-default
  zver 5 >= if
    $34 zw@ ?dup if 78 0 do dup i + zb@ zalpha i + c! loop drop then            \ the alphabet of the story
    $36 zw@ ?dup if
      dup zw@ 3 >= if 6 + zw@ ?dup if                                          \ and its Unicode table
        dup zb@ 97 min dup zun !  swap 1+ swap 0 ?do dup i 2* + zw@ zuni i cells + ! loop drop
      then else drop then
    then
  then
  zfix  zheader
  $06 zw@ zpc !
  5 zsp !  0 zfp !
  5 0 do 0 i zs! loop
  0 mtop !  0 zwin !  0 ll !
  -1 zrun ! ;
: zfsize ( fid -- n ) file-size throw d>s ;
: zread-story ( a n -- ok )
  r/o open-file if drop 0 exit then >r
  r@ zfsize to zsize
  zmem if zmem free drop then
  zsize 65536 + allocate if r> close-file drop 0 exit then to zmem
  zmem zsize 65536 + erase
  zmem zsize r@ read-file throw drop
  r> close-file drop -1 ;
: zrestart-now ( -- )
  zpath zpathn @ zread-story 0= if s" cannot read the story" zhalt exit then
  zsetup ;
' zrestart-now is zrestart

previous also forth definitions previous
also zm
: zstart ( a n -- )
  dup 255 > if 2drop ." name too long" cr exit then
  dup zpathn !  zpath swap cmove
  zstk 0= if zstk-n cells allocate throw to zstk then
  zpath zpathn @ zread-story 0= if ." Cannot read the story file" cr exit then
  0 zb@ dup 3 < over 8 > or over 6 = or over 7 = or if
    ." Story version " . ." is not supported" cr exit
  then drop
  ms-ticks 1 or zseed !
  zsetup
  zrunloop cr ;
: zplay ( "story" -- ) bl parse zstart ;

\ lugforth examples/games/zmachine.fs story.z5     plays the story at once (the second argument)
[defined] gforth 0= [defined] argv and [if]   \ no argc / argv on the bare-metal boots
: zmain ( -- )
  argc 3 < if exit then
  1 argv dup 11 < if 2drop exit then  + 11 - 11 s" zmachine.fs" compare if exit then
  2 argv zstart bye ;
zmain
[then]
previous
