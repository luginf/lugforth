\ Inference engine: facts, rules, and `infer`.
\
\ Port of the INFERENCE project of Marc Petremann (eForth Windows,
\ https://github.com/MPETREMANN11/eForth-Windows), GNU General Public License.
\ His part: the facts (`FACT:`, in the `facts` vocabulary), the rules (`RULE:`, in
\ the `rules` vocabulary), `:CONDITION`, `:ACTION`, the languages of config.fs.
\ New here: the tables that list facts and rules, and `infer`, the engine loop.
\
\     include examples/inference.fs
\     include examples/weather.fs        \ an example
\     also facts  -1 to rain  -1 to cold  infer  .facts
\
\ A fact is a value (`to` changes it). A rule is a condition ( -- flag ) and an
\ action, both anonymous words. `infer` tries all the rules again and again until no
\ rule has anything to do; a rule runs at most once per `infer`.

0 constant FALSE
-1 constant TRUE

vocabulary facts    \ new facts are defined here
vocabulary rules    \ new rules are defined here

: in-facts ( -- )   also facts definitions ;
: in-rules ( -- )   also rules definitions ;
: in-forth ( -- )   previous definitions ;

\ ---- Languages (config.fs of the original) ----
0 value DEUTSCH   0 value ENGLISH   0 value FRANCAIS
: reset-languages ( -- )   0 to DEUTSCH  0 to ENGLISH  0 to FRANCAIS ;
: in-english ( -- )   reset-languages -1 to ENGLISH ;
: en-francais ( -- )   reset-languages -1 to FRANCAIS ;
: auf-deutsch ( -- )   reset-languages -1 to DEUTSCH ;

\ ---- Facts ----
64 constant MAXFACTS
create fact-table MAXFACTS cells allot
variable #facts

\ FACT: ( n|flag "name" -- )   and then  name ( -- n )   `to name` changes it
: FACT: ( n|fl comp: -- <word> | exec: -- n )
  #facts @ MAXFACTS < 0= if ." too many facts" cr abort then
  in-facts  value  in-forth
  latestxt fact-table #facts @ cells + !  1 #facts +! ;

\ the fact number n: its word, its value, and all of them shown
: fact-xt ( n -- xt )   cells fact-table + @ ;
: fact@ ( n -- v )   fact-xt execute ;
: clear-facts ( -- )   #facts @ 0 ?do  0 i fact-xt >body !  loop ;
: .facts ( -- )
  #facts @ 0 ?do  i fact-xt >name type  ."  = "  i fact@ . cr  loop ;

\ ---- Rules ----
64 constant MAXRULES
create rule-table MAXRULES cells allot
variable #rules
variable MODIFIED_RULE   \ true when a rule ran in this pass

\ a rule is three cells: condition xt, action xt, flag (true once executed)
: ->condition ( a -- a' ) ;
: ->action ( a -- a' )   cell+ ;
: ->execFlag ( a -- a' )   2 cells + ;

: exec-condition ( a -- fl )   ->condition @ execute ;
: exec-action ( a -- )   ->action @ execute ;
: set-flag ( a -- )   TRUE swap ->execFlag !  TRUE MODIFIED_RULE ! ;

\ run a rule that has not run yet, if its condition holds
: try-rule ( a -- )
  dup ->execFlag @ if drop exit then
  dup exec-condition if  dup exec-action  set-flag  else drop then ;

: (RULE:) ( xt-condition xt-action "name" -- )
  #rules @ MAXRULES < 0= if ." too many rules" cr abort then
  create  here rule-table #rules @ cells + !  1 #rules +!
          swap , , 0 ,
  does>  try-rule ;

\ RULE: ( xt-cond xt-action "name" -- )   defined in the rules vocabulary
: RULE: ( -- )   in-rules (RULE:) in-forth ;

: :CONDITION ( -- xt )   :noname ;
: :ACTION ( -- xt )   :noname ;

\ ---- The engine ----
: reset-rules ( -- )   #rules @ 0 ?do  0 rule-table i cells + @ ->execFlag !  loop ;
: pass ( -- )
  0 MODIFIED_RULE !
  #rules @ 0 ?do  rule-table i cells + @ try-rule  loop ;
\ run the rules until none has anything left to do
: infer ( -- )   reset-rules  begin pass MODIFIED_RULE @ 0= until ;
: clear-rules ( -- )   0 #rules ! ;
: .rules ( -- )   #rules @ . ." rules" cr ;

also facts
