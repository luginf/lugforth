\ Tests of lamp_diagnosis.fs:  lugforth examples/utils/inference/lamp_diagnosis_tests.fs   (the output ends with PASS)

include inference.fs
include lamp_diagnosis.fs

variable tn   variable tfail
: t= ( actual expected -- )
  1 tn +!  2dup <> if
    ." FAIL test " tn @ . ." got " over . ." expected " dup . cr  1 tfail +!
  then 2drop ;

clear-facts  TRUE to no-light  TRUE to socket-ok  infer
change-bulb 0<> -1 t=  mains-failure 0<> 0 t=
clear-facts  TRUE to no-light  infer
change-bulb 0<> 0 t=  mains-failure 0<> -1 t=
clear-facts  infer
change-bulb 0<> 0 t=  mains-failure 0<> 0 t=
depth 0 t=
: report ( -- )   tfail @ 0= if ." PASS " tn @ . cr else ." FAILED " tfail @ . cr then ;
report
bye
