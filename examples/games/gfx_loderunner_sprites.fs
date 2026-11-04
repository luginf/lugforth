\ Sprites of gfx_loderunner.fs, drawn by gfx_sprited.fs: edit them there.
\ Colors: B is the suit of the runner and of the guards (the game sets it).

$FFFFFF 66 spr-pal   \ B
$40C0FF 76 spr-pal   \ L
$FFF0A0 89 spr-pal   \ Y
$404050 100 spr-pal   \ d
$FFC890 104 spr-pal   \ h
$601808 109 spr-pal   \ m
$D09040 111 spr-pal   \ o
$C8482C 114 spr-pal   \ r
$808090 115 spr-pal   \ s
$D0D0D0 119 spr-pal   \ w
$F0C020 121 spr-pal   \ y

10 10 sprite: lr-brick
  s" rrrrrmrrrr" spr-row
  s" rrrrrmrrrr" spr-row
  s" rrrrrmrrrr" spr-row
  s" mmmmmmmmmm" spr-row
  s" rrmrrrrrmr" spr-row
  s" rrmrrrrrmr" spr-row
  s" rrmrrrrrmr" spr-row
  s" mmmmmmmmmm" spr-row
  s" rrrrrmrrrr" spr-row
  s" rrrrrmrrrr" spr-row
;sprite

10 10 sprite: lr-brickfill
  s" .........." spr-row
  s" .........." spr-row
  s" .........." spr-row
  s" .........." spr-row
  s" .........." spr-row
  s" rrmrrrrrmr" spr-row
  s" rrmrrrrrmr" spr-row
  s" rrmrrrrrmr" spr-row
  s" mmmmmmmmmm" spr-row
  s" rrrrrmrrrr" spr-row
;sprite

10 10 sprite: lr-solid
  s" wwwwwwwwww" spr-row
  s" wssssssssd" spr-row
  s" wssssssssd" spr-row
  s" wssssssssd" spr-row
  s" wssssssssd" spr-row
  s" wssssssssd" spr-row
  s" wssssssssd" spr-row
  s" wssssssssd" spr-row
  s" wssssssssd" spr-row
  s" dddddddddd" spr-row
;sprite

10 10 sprite: lr-ladder
  s" .L......L." spr-row
  s" .L......L." spr-row
  s" .LLLLLLLL." spr-row
  s" .L......L." spr-row
  s" .L......L." spr-row
  s" .LLLLLLLL." spr-row
  s" .L......L." spr-row
  s" .L......L." spr-row
  s" .LLLLLLLL." spr-row
  s" .L......L." spr-row
;sprite

10 10 sprite: lr-rope
  s" .........." spr-row
  s" oooooooooo" spr-row
  s" oooooooooo" spr-row
  s" .........." spr-row
  s" .........." spr-row
  s" .........." spr-row
  s" .........." spr-row
  s" .........." spr-row
  s" .........." spr-row
  s" .........." spr-row
;sprite

10 10 sprite: lr-gold
  s" .........." spr-row
  s" .........." spr-row
  s" ...yyyy..." spr-row
  s" ..yYyyyy.." spr-row
  s" .yYyyyyyy." spr-row
  s" .yyyyyyyy." spr-row
  s" ..yyyyyy.." spr-row
  s" ...yyyy..." spr-row
  s" .........." spr-row
  s" .........." spr-row
;sprite

10 10 sprite: lr-run-r1
  s" ...hhh...." spr-row
  s" ...hhhh..." spr-row
  s" ...hhh...." spr-row
  s" ..BBBBB..." spr-row
  s" .BBBBBBB.." spr-row
  s" .B.BBB.B.." spr-row
  s" ...BBB...." spr-row
  s" ..BB.BB..." spr-row
  s" .BB...BB.." spr-row
  s" .B.....B.." spr-row
;sprite

10 10 sprite: lr-run-r2
  s" ...hhh...." spr-row
  s" ...hhhh..." spr-row
  s" ...hhh...." spr-row
  s" ..BBBBB..." spr-row
  s" .BBBBBBB.." spr-row
  s" .B.BBB.B.." spr-row
  s" ...BBB...." spr-row
  s" ...BBB...." spr-row
  s" ...B.B...." spr-row
  s" ..BB.BB..." spr-row
;sprite

10 10 sprite: lr-climb1
  s" .B.hhh.B.." spr-row
  s" .B.hhh.B.." spr-row
  s" .BBBBBBB.." spr-row
  s" ..BBBBB..." spr-row
  s" ..BBBBB..." spr-row
  s" ..BBBBB..." spr-row
  s" ...B.B...." spr-row
  s" ...B.B...." spr-row
  s" ...B.B...." spr-row
  s" ..BB.BB..." spr-row
;sprite

10 10 sprite: lr-climb2
  s" .B.hhh...." spr-row
  s" .B.hhh.B.." spr-row
  s" .BBBBBBB.." spr-row
  s" ..BBBBB..." spr-row
  s" ..BBBBB..." spr-row
  s" ..BBBBB..." spr-row
  s" ...B.B...." spr-row
  s" ..BB.B...." spr-row
  s" ..B..BB..." spr-row
  s" ..B...B..." spr-row
;sprite

10 10 sprite: lr-hang-r
  s" .........." spr-row
  s" .B..hhh.B." spr-row
  s" .B..hhh.B." spr-row
  s" .BBBBBBBB." spr-row
  s" ..BBBBB..." spr-row
  s" ...BBB...." spr-row
  s" ...B.B...." spr-row
  s" ..BB.BB..." spr-row
  s" ..B...B..." spr-row
  s" ..B...B..." spr-row
;sprite

10 10 sprite: lr-hang-r2
  s" .........." spr-row
  s" .B..hhh.B." spr-row
  s" .B..hhh.B." spr-row
  s" .BBBBBBBB." spr-row
  s" ..BBBBB..." spr-row
  s" ...BBB...." spr-row
  s" ...BBB...." spr-row
  s" ...B.B...." spr-row
  s" ...B.B...." spr-row
  s" ..BB.BB..." spr-row
;sprite

10 10 sprite: lr-fall
  s" B..hhh..B." spr-row
  s" B..hhh..B." spr-row
  s" .BBBBBBBB." spr-row
  s" ..BBBBB..." spr-row
  s" ..BBBBB..." spr-row
  s" ...BBB...." spr-row
  s" ..BB.BB..." spr-row
  s" ..B...B..." spr-row
  s" .........." spr-row
  s" .........." spr-row
;sprite

10 10 sprite: lr-run-l1
  s" ....hhh..." spr-row
  s" ...hhhh..." spr-row
  s" ....hhh..." spr-row
  s" ...BBBBB.." spr-row
  s" ..BBBBBBB." spr-row
  s" ..B.BBB.B." spr-row
  s" ....BBB..." spr-row
  s" ...BB.BB.." spr-row
  s" ..BB...BB." spr-row
  s" ..B.....B." spr-row
;sprite

10 10 sprite: lr-run-l2
  s" ....hhh..." spr-row
  s" ...hhhh..." spr-row
  s" ....hhh..." spr-row
  s" ...BBBBB.." spr-row
  s" ..BBBBBBB." spr-row
  s" ..B.BBB.B." spr-row
  s" ....BBB..." spr-row
  s" ....BBB..." spr-row
  s" ....B.B..." spr-row
  s" ...BB.BB.." spr-row
;sprite

10 10 sprite: lr-hang-l
  s" .........." spr-row
  s" .B.hhh..B." spr-row
  s" .B.hhh..B." spr-row
  s" .BBBBBBBB." spr-row
  s" ...BBBBB.." spr-row
  s" ....BBB..." spr-row
  s" ....B.B..." spr-row
  s" ...BB.BB.." spr-row
  s" ...B...B.." spr-row
  s" ...B...B.." spr-row
;sprite

10 10 sprite: lr-hang-l2
  s" .........." spr-row
  s" .B.hhh..B." spr-row
  s" .B.hhh..B." spr-row
  s" .BBBBBBBB." spr-row
  s" ...BBBBB.." spr-row
  s" ....BBB..." spr-row
  s" ....BBB..." spr-row
  s" ....B.B..." spr-row
  s" ....B.B..." spr-row
  s" ...BB.BB.." spr-row
;sprite

