\ Sprites of gfx_digger.fs, drawn by gfx_sprited.fs: edit them there.
\ Colors: R is the digger body (the game changes it: flash, death).

$20d060 101 spr-pal   \ e
$a0ffc0 69 spr-pal   \ E
$f0c020 121 spr-pal   \ y
$30a030 115 spr-pal   \ s
$e02020 114 spr-pal   \ r
$b89a58 98 spr-pal   \ b
$705830 100 spr-pal   \ d
$30c030 110 spr-pal   \ n
$ffffff 119 spr-pal   \ w
$000000 107 spr-pal   \ k
$e08080 104 spr-pal   \ h
$ffd0d0 112 spr-pal   \ p
$808080 103 spr-pal   \ g
$a0e0ff 99 spr-pal   \ c
$e02020 82 spr-pal   \ R
$f0d040 89 spr-pal   \ Y

20 18 sprite: emerald
  s" ...................." spr-row
  s" ...................." spr-row
  s" ...................." spr-row
  s" ...................." spr-row
  s" ........eeee........" spr-row
  s" ........eeee........" spr-row
  s" ......eeeeeeee......" spr-row
  s" ......eeEEeeee......" spr-row
  s" ......eeEEeeee......" spr-row
  s" ......eeeeeeee......" spr-row
  s" ......eeeeeeee......" spr-row
  s" ......eeeeeeee......" spr-row
  s" ........eeee........" spr-row
  s" ........eeee........" spr-row
  s" ...................." spr-row
  s" ...................." spr-row
  s" ...................." spr-row
  s" ...................." spr-row
;sprite

20 18 sprite: gold
  s" ...................." spr-row
  s" ...................." spr-row
  s" ...................." spr-row
  s" ...................." spr-row
  s" ...................." spr-row
  s" ...................." spr-row
  s" .......yyyyyy......." spr-row
  s" .......yyyyyy......." spr-row
  s" .......yyyyyy......." spr-row
  s" .......yyyyyy......." spr-row
  s" ....yyyyyyyyyyyy...." spr-row
  s" ....yyyyyyyyyyyy...." spr-row
  s" ....yyyyyyyyyyyy...." spr-row
  s" ....yyyyyyyyyyyy...." spr-row
  s" ....yyyyyyyyyyyy...." spr-row
  s" ....yyyyyyyyyyyy...." spr-row
  s" ...................." spr-row
  s" ...................." spr-row
;sprite

20 18 sprite: cherry
  s" ...................." spr-row
  s" ...................." spr-row
  s" ...................." spr-row
  s" .........ss........." spr-row
  s" .........ss........." spr-row
  s" .........ss........." spr-row
  s" .........ss........." spr-row
  s" .........ss........." spr-row
  s" .........ss........." spr-row
  s" .........ss........." spr-row
  s" .....rrrrr.........." spr-row
  s" .....rrrrr.rrrrr...." spr-row
  s" .....rrrrr.rrrrr...." spr-row
  s" .....rrrrr.rrrrr...." spr-row
  s" .....rrrrr.rrrrr...." spr-row
  s" ...........rrrrr...." spr-row
  s" ...................." spr-row
  s" ...................." spr-row
;sprite

20 18 sprite: bag
  s" ...................." spr-row
  s" ...................." spr-row
  s" .......dddddd......." spr-row
  s" ......bddddddb......" spr-row
  s" ......bbbbbbbb......" spr-row
  s" ....bbbbbbbbbbbb...." spr-row
  s" ....bbbbbbbbbbbb...." spr-row
  s" ....bbbbbbbbbbbb...." spr-row
  s" ....bbbbbYYbbbbb...." spr-row
  s" ....bbbYYYYYYbbb...." spr-row
  s" ....bbbYYYYYYbbb...." spr-row
  s" ....bbbbbYYbbbbb...." spr-row
  s" ....bbbbbYYbbbbb...." spr-row
  s" ....bbbbbYYbbbbb...." spr-row
  s" ....bbbbbbbbbbbb...." spr-row
  s" ....bbbbbbbbbbbb...." spr-row
  s" ....bbbbbbbbbbbb...." spr-row
  s" ...................." spr-row
;sprite

20 18 sprite: nobbin
  s" ...................." spr-row
  s" ...................." spr-row
  s" .....nnnnnnnnnn....." spr-row
  s" .....nnnnnnnnnn....." spr-row
  s" ...nnnnnnnnnnnnnn..." spr-row
  s" ...nnnnnnnnnnnnnn..." spr-row
  s" ...nnnwwwnnwwwnnn..." spr-row
  s" ...nnnwkwnnwkwnnn..." spr-row
  s" ...nnnwwwnnwwwnnn..." spr-row
  s" ...nnnnnnnnnnnnnn..." spr-row
  s" ...nnnnnnnnnnnnnn..." spr-row
  s" ...nnnnnnnnnnnnnn..." spr-row
  s" ...nnnnnnnnnnnnnn..." spr-row
  s" ...nnnnnnnnnnnnnn..." spr-row
  s" ...nnnnnnnnnnnnnn..." spr-row
  s" ...................." spr-row
  s" ...................." spr-row
  s" ...................." spr-row
;sprite

20 18 sprite: hobbin
  s" ...................." spr-row
  s" ...................." spr-row
  s" ...................." spr-row
  s" ....hhhhhhhhhhhh...." spr-row
  s" ....hhhhhhhhhhhh...." spr-row
  s" ....hhhhhhhhhhhh...." spr-row
  s" ....hhppphhppphh...." spr-row
  s" .hhhhhprphhprphhhhh." spr-row
  s" .hhhhhprphhprphhhhh." spr-row
  s" .hhhhhhhhhhhhhhhhhh." spr-row
  s" .hhhhhhhhhhhhhhhhhh." spr-row
  s" .hhhhhhhhhhhhhhhhhh." spr-row
  s" .hhhhhhkkkkkkhhhhhh." spr-row
  s" ....hhhhhhhhhhhh...." spr-row
  s" ....hhhhhhhhhhhh...." spr-row
  s" ...................." spr-row
  s" ...................." spr-row
  s" ...................." spr-row
;sprite

20 18 sprite: digger-r
  s" ...................." spr-row
  s" ...................." spr-row
  s" ...................." spr-row
  s" ...................." spr-row
  s" ...RRRRRRRRRRRRRR..." spr-row
  s" ...RRRRRRRRRRRRRR..." spr-row
  s" ...RRRRcccccRRRRR..." spr-row
  s" ...RRRRcccccRRRRRggg" spr-row
  s" ...RRRRcccccRRRRRggg" spr-row
  s" ...RRRRcccccRRRRRggg" spr-row
  s" ...RRRRRRRRRRRRRRggg" spr-row
  s" ...RRRRRRRRRRRRRR..." spr-row
  s" ...RRRRRRRRRRRRRR..." spr-row
  s" ...RRRRRRRRRRRRRR..." spr-row
  s" ...gggggggggggggg..." spr-row
  s" ...gggggggggggggg..." spr-row
  s" ...gggggggggggggg..." spr-row
  s" ...................." spr-row
;sprite

20 18 sprite: digger-l
  s" ...................." spr-row
  s" ...................." spr-row
  s" ...................." spr-row
  s" ...................." spr-row
  s" ...RRRRRRRRRRRRRR..." spr-row
  s" ...RRRRRRRRRRRRRR..." spr-row
  s" ...RRRRcccccRRRRR..." spr-row
  s" gggRRRRcccccRRRRR..." spr-row
  s" gggRRRRcccccRRRRR..." spr-row
  s" gggRRRRcccccRRRRR..." spr-row
  s" gggRRRRRRRRRRRRRR..." spr-row
  s" ...RRRRRRRRRRRRRR..." spr-row
  s" ...RRRRRRRRRRRRRR..." spr-row
  s" ...RRRRRRRRRRRRRR..." spr-row
  s" ...gggggggggggggg..." spr-row
  s" ...gggggggggggggg..." spr-row
  s" ...gggggggggggggg..." spr-row
  s" ...................." spr-row
;sprite

20 18 sprite: digger-u
  s" ........gggg........" spr-row
  s" ........gggg........" spr-row
  s" ....ggRRRRRRRRgg...." spr-row
  s" ....ggRRRRRRRRgg...." spr-row
  s" ....ggRRRRRRRRgg...." spr-row
  s" ....ggRRRRRRRRgg...." spr-row
  s" ....ggRRRRRRRRgg...." spr-row
  s" ....ggRRccccRRgg...." spr-row
  s" ....ggRRccccRRgg...." spr-row
  s" ....ggRRccccRRgg...." spr-row
  s" ....ggRRccccRRgg...." spr-row
  s" ....ggRRccccRRgg...." spr-row
  s" ....ggRRRRRRRRgg...." spr-row
  s" ....ggRRRRRRRRgg...." spr-row
  s" ....ggRRRRRRRRgg...." spr-row
  s" ....ggRRRRRRRRgg...." spr-row
  s" ...................." spr-row
  s" ...................." spr-row
;sprite

20 18 sprite: digger-d
  s" ...................." spr-row
  s" ...................." spr-row
  s" ....ggRRRRRRRRgg...." spr-row
  s" ....ggRRRRRRRRgg...." spr-row
  s" ....ggRRRRRRRRgg...." spr-row
  s" ....ggRRRRRRRRgg...." spr-row
  s" ....ggRRRRRRRRgg...." spr-row
  s" ....ggRRccccRRgg...." spr-row
  s" ....ggRRccccRRgg...." spr-row
  s" ....ggRRccccRRgg...." spr-row
  s" ....ggRRccccRRgg...." spr-row
  s" ....ggRRccccRRgg...." spr-row
  s" ....ggRRRRRRRRgg...." spr-row
  s" ....ggRRRRRRRRgg...." spr-row
  s" ....ggRRRRRRRRgg...." spr-row
  s" ....ggRRRRRRRRgg...." spr-row
  s" ........gggg........" spr-row
  s" ........gggg........" spr-row
;sprite

20 18 sprite: cross
  s" ...................." spr-row
  s" ........RRRR........" spr-row
  s" ........RRRR........" spr-row
  s" .....RRRRRRRRRR....." spr-row
  s" .....RRRRRRRRRR....." spr-row
  s" .....RRRRRRRRRR....." spr-row
  s" ...RRRRRRRRRRRRRR..." spr-row
  s" ...RRRRRRRRRRRRRR..." spr-row
  s" ...RRRRRRRRRRRRRR..." spr-row
  s" .....RRRRRRRRRR....." spr-row
  s" .....RRRRRRRRRR....." spr-row
  s" .....RRRRRRRRRR....." spr-row
  s" .....RRRRRRRRRR....." spr-row
  s" .....RRRRRRRRRR....." spr-row
  s" .....RRRRRRRRRR....." spr-row
  s" ........RRRR........" spr-row
  s" ........RRRR........" spr-row
  s" ...................." spr-row
;sprite
