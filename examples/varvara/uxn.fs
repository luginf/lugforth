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

0 value pc
0 value running
0 value kp      ( in keep mode, the stack pointer the pops go through )

: ram@ ( a -- b ) $ffff and ram + c@ ;
: ram! ( b a -- ) $ffff and ram + c! ;
: ram2@ ( a -- v ) dup ram@ 8 lshift swap 1+ ram@ or ;
: ram2! ( v a -- ) over 8 rshift over ram! 1+ ram! ;
: zram2@ ( a -- v ) dup ram@ 8 lshift swap 1+ $ff and ram@ or ;   ( zero page wraps )
: zram2! ( v a -- ) over 8 rshift over ram! 1+ $ff and ram! ;
: sx8 ( b -- n ) $80 xor $80 - ;
: pc! ( a -- ) $ffff and to pc ;
: kp! ( n -- ) to kp ;

( ---- stacks ---- )

: spush ( b s -- ) dup >r @ r@ cell+ + c! r@ @ 1+ $ff and r> ! ;
: spop8 ( s -- b ) dup >r @ 1- $ff and dup r@ ! r> cell+ + c@ ;
: kpop8 ( s -- b ) kp 1- $ff and dup to kp + cell+ c@ ;

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
: dei2 ( p -- v ) dup dei-byte 8 lshift swap 1+ dei-byte or ;
: deo2 ( v p -- ) over 8 rshift over deo-byte 1+ deo-byte ;

( ---- the instructions, compiled once for each of the 8 modes ---- )
\ An opcode is 3 mode bits (k r 2) and 5 bits of operation. The words below
\ are macros: compiled into an instruction, each picks the code of the mode
\ being built (c-mode), so an instruction never tests its mode when it runs.

create optab $100 cells allot
0 value c-mode
: c-short ( -- f ) c-mode 1 and ;
: c-stk ( -- s ) c-mode 2 and if rst else wst then ;
: c-oth ( -- s ) c-mode 2 and if wst else rst then ;
: c-keep ( -- f ) c-mode 4 and ;
: lit, ( n -- ) postpone literal ;

: pre ( -- ) c-keep if c-stk lit, postpone @ postpone kp! then ; immediate
: pop8 c-keep if c-stk lit, postpone kpop8 exit then   ( inline: S @ 1- $ff and dup S ! S+cell + c@ )
  c-stk lit, postpone @ postpone 1- $ff lit, postpone and postpone dup c-stk lit, postpone !
  c-stk cell+ lit, postpone + postpone c@ ; immediate
: pop16 postpone pop8 postpone pop8 8 lit, postpone lshift postpone or ; immediate
: pop c-short if postpone pop16 else postpone pop8 then ; immediate
: push8 ( inline: S @ S+cell + c! S @ 1+ $ff and S ! )
  c-stk lit, postpone @ c-stk cell+ lit, postpone + postpone c!
  c-stk lit, postpone @ postpone 1+ $ff lit, postpone and c-stk lit, postpone ! ; immediate
: push c-short if postpone dup 8 lit, postpone rshift postpone push8 then
  postpone push8 ; immediate
: opush c-short if postpone dup 8 lit, postpone rshift c-oth lit, postpone spush then
  c-oth lit, postpone spush ; immediate
: opush16 postpone dup 8 lit, postpone rshift c-oth lit, postpone spush
  c-oth lit, postpone spush ; immediate
: flag 1 lit, postpone and postpone push8 ; immediate
: jump c-short 0= if postpone sx8 postpone pc postpone + then postpone pc! ; immediate
: ram@, ( -- ) $ffff lit, postpone and ram lit, postpone + postpone c@ ;   ( ram@ inline )
: ld c-short if postpone ram2@ else ram@, then ; immediate
: st c-short if postpone ram2! else postpone ram! then ; immediate
: zld c-short if postpone zram2@ else ram lit, postpone + postpone c@ then ; immediate
: zst c-short if postpone zram2! else postpone ram! then ; immediate
: dei c-short if postpone dei2 else postpone dei-byte then ; immediate
: deo c-short if postpone deo2 else postpone deo-byte then ; immediate
: fetch c-short if postpone pc postpone ram2@ 2 lit, else postpone pc ram@, 1 lit, then
  postpone pc postpone + postpone pc! ; immediate

: op! ( xt n -- ) c-mode 5 lshift + cells optab + ! ;
: lit! ( xt -- ) c-keep if 0 op! else drop then ;   ( LIT is the k form of BRK )

: instructions r~
: op-inc pre pop 1+ push ;                  ' op-inc $01 op!
: op-pop pre pop drop ;                     ' op-pop $02 op!
: op-nip pre pop pop drop push ;            ' op-nip $03 op!
: op-swp pre pop pop swap push push ;       ' op-swp $04 op!
: op-rot pre pop pop pop >r push push r> push ;  ' op-rot $05 op!
: op-dup pre pop dup push push ;            ' op-dup $06 op!
: op-ovr pre pop pop dup >r push push r> push ;  ' op-ovr $07 op!
: op-equ pre pop pop = flag ;               ' op-equ $08 op!
: op-neq pre pop pop <> flag ;              ' op-neq $09 op!
: op-gth pre pop pop < flag ;               ' op-gth $0a op!
: op-lth pre pop pop > flag ;               ' op-lth $0b op!
: op-jmp pre pop jump ;                     ' op-jmp $0c op!
: op-jcn pre pop pop8 if jump else drop then ;  ' op-jcn $0d op!
: op-jsr pre pop pc opush16 jump ;          ' op-jsr $0e op!
: op-sth pre pop opush ;                    ' op-sth $0f op!
: op-ldz pre pop8 zld push ;                ' op-ldz $10 op!
: op-stz pre pop8 pop swap zst ;            ' op-stz $11 op!
: op-ldr pre pop8 sx8 pc + ld push ;        ' op-ldr $12 op!
: op-str pre pop8 sx8 pc + pop swap st ;    ' op-str $13 op!
: op-lda pre pop16 ld push ;                ' op-lda $14 op!
: op-sta pre pop16 pop swap st ;            ' op-sta $15 op!
: op-dei pre pop8 dei push ;                ' op-dei $16 op!
: op-deo pre pop8 pop swap deo ;            ' op-deo $17 op!
: op-add pre pop pop + push ;               ' op-add $18 op!
: op-sub pre pop pop swap - push ;          ' op-sub $19 op!
: op-mul pre pop pop * push ;               ' op-mul $1a op!
: op-div pre pop pop swap dup if / else 2drop 0 then push ;  ' op-div $1b op!
: op-and pre pop pop and push ;             ' op-and $1c op!
: op-ora pre pop pop or push ;              ' op-ora $1d op!
: op-eor pre pop pop xor push ;             ' op-eor $1e op!
: op-sft pre pop8 pop swap dup >r $f and rshift r> 4 rshift lshift push ;  ' op-sft $1f op!
: op-lit fetch push ;                       ' op-lit lit!
~ ;
: build ( mode -- ) to c-mode instructions evaluate ;
0 build 1 build 2 build 3 build 4 build 5 build 6 build 7 build

: rel16 ( -- addr ) pc ram2@ pc 2 + + $ffff and ;
: op-brk ( -- ) 0 to running ;
: op-jci ( -- ) wst spop8 if rel16 else pc 2 + then pc! ;
: op-jmi ( -- ) rel16 pc! ;
: op-jsi ( -- ) pc 2 + dup 8 rshift rst spush rst spush rel16 pc! ;
' op-brk $00 cells optab + !
' op-jci $20 cells optab + !
' op-jmi $40 cells optab + !
' op-jsi $60 cells optab + !

: step ( -- ) pc ram + c@  pc 1+ $ffff and to pc  cells optab + @ execute ;

: halted? ( -- f ) $0f dev@ 0<> ;
: exit-code ( -- n ) $0f dev@ $7f and ;
: eval ( addr -- )
  dup 0= halted? or if drop exit then
  to pc -1 to running
  begin pc ram + c@  pc 1+ $ffff and to pc  cells optab + @ execute
  running 0= until ;

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
    $0f of halted? if 0 to running then endof
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
