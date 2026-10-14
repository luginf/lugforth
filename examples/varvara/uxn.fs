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

( The Uxn CPU of the Varvara computer, in Forth )
\
\ Uxn (https://100r.co/site/uxn.html) is an 8-bit stack machine: 64 KB of
\ memory (here 16 banks of 64 KB, reached through the System expansion port),
\ two circular stacks of 256 bytes, 256 devices ports and 32 opcodes, each
\ with three mode bits: 2 (shorts), r (return stack) and k (keep).
\
\ This file is the CPU, the System device and the Console output. Every
\ device is a pair of handlers in dei-tab/deo-tab, one per 16 ports, which
\ uxncli.fs (and later a window) can replace. Words live in vocabulary uxn.
\
\   load-rom ( a n -- )   load-bytes ( a n -- )   eval ( addr -- )   halted? ( -- f )   exit-code ( -- n )

vocabulary uxn  also uxn definitions

$10 constant banks
create ram  banks $10000 * allot
create devs $100 allot
create wst 0 , $100 allot   ( pointer cell, then 256 bytes )
create rst 0 , $100 allot

variable pc
variable stk     ( the stack an instruction works on )
variable oth     ( the other one, for STH and JSR )
variable sp-a    ( the pointer pops go through: the stack's own, or kp in keep mode )
variable kp
variable short?
variable running

: ram@ ( a -- b ) $ffff and ram + c@ ;
: ram! ( b a -- ) $ffff and ram + c! ;
: ram2@ ( a -- v ) dup ram@ 8 lshift swap 1+ ram@ or ;
: ram2! ( v a -- ) over 8 rshift over ram! 1+ ram! ;
: sx8 ( b -- n ) $80 xor $80 - ;

( ---- stacks ---- )

: spush ( b s -- ) dup >r @ r@ cell+ + c! r@ @ 1+ $ff and r> ! ;
: pop8 ( -- b ) sp-a @ dup @ 1- $ff and tuck swap ! stk @ cell+ + c@ ;
: pop16 ( -- v ) pop8 pop8 8 lshift or ;
: pop ( -- v ) short? @ if pop16 else pop8 then ;
: push8 ( b -- ) stk @ spush ;
: push16 ( v -- ) dup 8 rshift push8 push8 ;
: push ( v -- ) short? @ if push16 else push8 then ;
: opush ( v -- ) short? @ if dup 8 rshift oth @ spush then oth @ spush ;
: flag ( f -- ) 1 and push8 ;

: jump ( addr -- ) short? @ 0= if sx8 pc @ + then $ffff and pc ! ;
: ld ( a -- v ) short? @ if ram2@ else ram@ then ;
: st ( v a -- ) short? @ if ram2! else ram! then ;
: zld ( a -- v ) short? @ if dup ram@ 8 lshift swap 1+ $ff and ram@ or else ram@ then ;
: zst ( v a -- ) short? @ if over 8 rshift over ram! 1+ $ff and then ram! ;

( ---- devices ---- )

create dei-tab $10 cells allot
create deo-tab $10 cells allot
: dev@ ( p -- b ) $ff and devs + c@ ;
: dev2@ ( p -- v ) dup dev@ 8 lshift swap 1+ dev@ or ;
: dev! ( b p -- ) $ff and devs + c! ;
: plain-dei ( p -- b ) dev@ ;
: plain-deo ( p -- ) drop ;
: device! ( dei-xt deo-xt n -- ) cells tuck deo-tab + ! dei-tab + ! ;
: dei-byte ( p -- b ) $ff and dup 4 rshift cells dei-tab + @ execute ;
: deo-byte ( v p -- ) $ff and tuck dev! dup 4 rshift cells deo-tab + @ execute ;

( ---- the 32 instructions ---- )

: op-inc pop 1+ push ;
: op-pop pop drop ;
: op-nip pop pop drop push ;
: op-swp pop pop swap push push ;
: op-rot pop pop pop >r push push r> push ;
: op-dup pop dup push push ;
: op-ovr pop pop dup >r push push r> push ;
: op-equ pop pop = flag ;
: op-neq pop pop <> flag ;
: op-gth pop pop < flag ;
: op-lth pop pop > flag ;
: op-jmp pop jump ;
: op-jcn pop pop8 if jump else drop then ;
: op-jsr pop pc @ dup 8 rshift oth @ spush oth @ spush jump ;
: op-sth pop opush ;
: op-ldz pop8 zld push ;
: op-stz pop8 pop swap zst ;
: op-ldr pop8 sx8 pc @ + ld push ;
: op-str pop8 sx8 pc @ + pop swap st ;
: op-lda pop16 ld push ;
: op-sta pop16 pop swap st ;
: op-dei pop8 short? @ if dup dei-byte 8 lshift swap 1+ dei-byte or else dei-byte then push ;
: op-deo pop8 pop swap short? @ if over 8 rshift over deo-byte 1+ then deo-byte ;
: op-add pop pop + push ;
: op-sub pop pop swap - push ;
: op-mul pop pop * push ;
: op-div pop pop swap dup if / else 2drop 0 then push ;
: op-and pop pop and push ;
: op-ora pop pop or push ;
: op-eor pop pop xor push ;
: op-sft pop8 pop swap dup >r $f and rshift r> 4 rshift lshift push ;

create ops
  0 , ' op-inc , ' op-pop , ' op-nip , ' op-swp , ' op-rot , ' op-dup , ' op-ovr ,
  ' op-equ , ' op-neq , ' op-gth , ' op-lth , ' op-jmp , ' op-jcn , ' op-jsr , ' op-sth ,
  ' op-ldz , ' op-stz , ' op-ldr , ' op-str , ' op-lda , ' op-sta , ' op-dei , ' op-deo ,
  ' op-add , ' op-sub , ' op-mul , ' op-div , ' op-and , ' op-ora , ' op-eor , ' op-sft ,

: mode ( ins -- )
  dup $20 and 0<> short? !
  dup $40 and if rst wst else wst rst then oth ! stk !
  $80 and if stk @ @ kp ! kp else stk @ then sp-a ! ;

: rel16 ( -- addr ) pc @ ram2@ pc @ 2 + + $ffff and ;
: skip2 ( -- ) pc @ 2 + $ffff and pc ! ;
: lit ( ins -- )
  mode short? @ if pc @ ram2@ 2 else pc @ ram@ 1 then
  pc @ + $ffff and pc ! push ;
: special ( ins -- )   ( the opcodes whose low five bits are 0 )
  dup $80 and if lit exit then
  0 mode
  case
    $00 of 0 running ! endof                                   ( BRK )
    $20 of pop8 if rel16 pc ! else skip2 then endof            ( JCI )
    $40 of rel16 pc ! endof                                    ( JMI )
    $60 of pc @ 2 + dup 8 rshift rst spush rst spush           ( JSI )
           rel16 pc ! endof
  endcase ;

: step ( -- )
  pc @ ram + c@  pc @ 1+ $ffff and pc !
  dup $1f and ?dup if cells ops + @ swap mode execute else special then ;

: halted? ( -- f ) $0f dev@ 0<> ;
: exit-code ( -- n ) $0f dev@ $7f and ;
: eval ( addr -- )
  dup 0= halted? or if drop exit then
  pc ! -1 running ! begin step running @ 0= until ;

( ---- System device: ports $00-$0f ---- )

: bank-addr ( bank addr -- a ) $ffff and swap banks 1- and $10000 * + ram + ;
: expansion ( -- )   ( a command in memory: fill, copy left or copy right )
  $02 dev2@ { cmd }
  cmd 1+ ram2@ { len }
  cmd ram@ case
    $00 of len 0 ?do
             cmd 7 + ram@  cmd 3 + ram2@ cmd 5 + ram2@ i + bank-addr c!
           loop endof
    $01 of len 0 ?do
             cmd 3 + ram2@ cmd 5 + ram2@ i + bank-addr c@
             cmd 7 + ram2@ cmd 9 + ram2@ i + bank-addr c!
           loop endof
    $02 of len 0 ?do
             cmd 3 + ram2@ cmd 5 + ram2@ len + i - 1- bank-addr c@
             cmd 7 + ram2@ cmd 9 + ram2@ len + i - 1- bank-addr c!
           loop endof
  endcase ;

also posix
create obuf $1000 allot
variable olen
: flush-out ( -- ) olen @ if stdout obuf olen @ write drop 0 olen ! then ;
: out-byte ( b -- ) olen @ obuf + c! 1 olen +! olen @ $1000 = if flush-out then ;
: err-byte ( b -- ) flush-out pad c! stderr pad 1 write drop ;
previous

: .hex2 ( b -- ) base @ >r hex <# # # #> type r> base ! ;
: .stack ( s -- ) dup @ 0 ?do dup cell+ i + c@ .hex2 space loop drop ;
: debug-stacks ( -- )
  flush-out ." WST " wst .stack cr ." RST " rst .stack cr ;
: system-dei ( p -- b )
  dup $0f and case
    $04 of drop wst @ endof
    $05 of drop rst @ endof
    drop dev@ 0
  endcase ;
: system-deo ( p -- )
  $0f and case
    $03 of expansion endof
    $04 of $04 dev@ wst ! endof
    $05 of $05 dev@ rst ! endof
    $0e of $0e dev@ if debug-stacks then endof
    $0f of halted? if 0 running ! then endof
  endcase ;

( ---- Console output: ports $10-$1f ---- )

: console-deo ( p -- )
  $0f and case
    $08 of $18 dev@ out-byte endof
    $09 of $19 dev@ err-byte endof
  endcase ;

: reset-uxn ( -- )
  ram banks $10000 * erase  devs $100 erase  wst $100 cell+ erase  rst $100 cell+ erase  0 olen !
  $10 0 do ['] plain-dei ['] plain-deo i device! loop
  ['] system-dei ['] system-deo 0 device!
  ['] plain-dei ['] console-deo 1 device! ;

: load-rom ( a n -- )
  reset-uxn
  r/o bin open-file throw >r
  ram $100 + banks $10000 * $100 - r@ read-file throw drop
  r> close-file throw ;

: load-bytes ( a n -- ) reset-uxn ram $100 + swap cmove ;

: console-input ( c type -- )   ( call the Console vector with one byte )
  $17 dev! $12 dev! $10 dev2@ eval ;

previous definitions
