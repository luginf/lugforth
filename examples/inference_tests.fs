\ Tests of inference.fs and weather.fs:  lugforth examples/inference_tests.fs
\ (run from the repository root, the output must end with PASS)

include inference.fs
include weather.fs

variable tn   variable tfail
: t= ( actual expected -- )
  1 tn +!  2dup <> if
    ." FAIL test " tn @ . ." got " over . ." expected " dup . cr  1 tfail +!
  then 2drop ;
: yes ( f -- f' )   0<> ;

clear-facts infer
ice yes 0 t=  snow yes 0 t=  umbrella yes 0 t=

clear-facts  TRUE to rain  infer
umbrella yes -1 t=  ice yes 0 t=  slippery yes 0 t=

\ a chain: ice, then slippery
clear-facts  TRUE to rain  TRUE to cold  infer
ice yes -1 t=  slippery yes -1 t=  umbrella yes 0 t=  snow yes 0 t=

\ again: same result
infer  ice yes -1 t=  slippery yes -1 t=

\ the order of the rules does not matter
clear-facts  TRUE to ice  infer  slippery yes -1 t=

\ facts can be defined later
TRUE FACT: late
late yes -1 t=   clear-facts  late yes 0 t=
#facts @ 7 t=   #rules @ 4 t=
depth 0 t=
.facts
: report ( -- )   tfail @ 0= if ." PASS " tn @ . cr else ." FAILED " tfail @ . cr then ;
report
bye
