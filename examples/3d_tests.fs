\ Tests of 3d.fs:  lugforth examples/3d_tests.fs   (the output ends with PASS)

include 3d.fs

variable tn   variable tfail
: t= ( actual expected -- )
  1 tn +!  2dup <> if
    ." FAIL test " tn @ . ." got " over . ." expected " dup . cr  1 tfail +!
  then 2drop ;

0 get-sin 0 t=   30 get-sin 128 t=   90 get-sin 256 t=   120 get-sin 222 t=
180 get-sin 0 t=   210 get-sin -128 t=   270 get-sin -256 t=   330 get-sin -128 t=
-30 get-sin -128 t=   390 get-sin 128 t=   0 get-cos 256 t=   90 get-cos 0 t=   180 get-cos -256 t=
CUBE-POINTS getNbItemsInObject 8 t=   CUBE-EDGES getNbItemsInObject 12 t=
PYRAMID-POINTS getNbItemsInObject 5 t=   PYRAMID-EDGES getNbItemsInObject 8 t=
OCTA-POINTS getNbItemsInObject 6 t=   OCTA-EDGES getNbItemsInObject 12 t=
CUBE-POINTS 6 get3Dpoint 100 t= 100 t= 100 t=
CUBE-POINTS 1 get3Dpoint  -100 t= -100 t= 100 t=   \ z y x: the point 1 is (100 -100 -100)
1 2 3 CUBE-POINTS 0 set3Dpoint   CUBE-POINTS 0 get3Dpoint 3 t= 2 t= 1 t=
CUBE-EDGES 11 get3Dedge 7 t= 3 t=
0 value np   0 value ee
\ edges refer to existing points
: edges-ok? ( edges points -- f )
  getNbItemsInObject to np  to ee  -1
  ee getNbItemsInObject 0 ?do  ee i get3Dedge np u< swap np u< and and  loop ;
CUBE-EDGES CUBE-POINTS edges-ok? -1 t=   PYRAMID-EDGES PYRAMID-POINTS edges-ok? -1 t=
OCTA-EDGES OCTA-POINTS edges-ok? -1 t=
\ no rotation: the point is unchanged; 90 degrees around z: (x y) -> (-y x)
0 to ax 0 to ay 0 to az   10 20 30 rotate 30 t= 20 t= 10 t=
90 to az  100 0 5 rotate 5 t= 100 t= 0 t=
0 to az  90 to ay  100 0 0 rotate -100 t= 0 t= 0 t=
0 to ay  90 to ax  0 100 0 rotate 100 t= 0 t= 0 t=
\ projection: x * focal / z
300 FOCAL !  100 -50 600 project -25 t= 50 t=
100 100 0 project 30000 t= 30000 t=
depth 0 t=
: report ( -- )   tfail @ 0= if ." PASS " tn @ . cr else ." FAILED " tfail @ . cr then ;
report
bye
