\ Sprites of gfx_digger.fs, drawn by gfx_sprited.fs: edit them there.
\ Colors: R is the digger body (the game changes it: flash, death).

$A0FFC0 69 spr-pal   \ E
$E02020 82 spr-pal   \ R
$F0D040 89 spr-pal   \ Y
$B89A58 98 spr-pal   \ b
$A0E0FF 99 spr-pal   \ c
$705830 100 spr-pal   \ d
$20D060 101 spr-pal   \ e
$808080 103 spr-pal   \ g
$E08080 104 spr-pal   \ h
$000000 107 spr-pal   \ k
$30C030 110 spr-pal   \ n
$FFD0D0 112 spr-pal   \ p
$E02020 114 spr-pal   \ r
$30A030 115 spr-pal   \ s
$FFFFFF 119 spr-pal   \ w
$F0C020 121 spr-pal   \ y

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
  s" ....bbbbbYbYbbbb...." spr-row
  s" ...bbbbbYYYYYbbb...." spr-row
  s" ...bbbbbbYbYbYbb...." spr-row
  s" ...bbbbbbYbYbYbb...." spr-row
  s" ...bbbbbbYbYbYbb...." spr-row
  s" ...bbbbbbYbYbYbb...." spr-row
  s" ...bbbbbYYYYYbbb...." spr-row
  s" ...bbbbbbYbYbbbb...." spr-row
  s" ....bbbbbbbbbbbb...." spr-row
  s" .....bbbbbbbbbb....." spr-row
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
  s" .........RRR........" spr-row
  s" .........RRR........" spr-row
  s" ........RR.RR......." spr-row
  s" ........R...R......." spr-row
  s" ........R...R......." spr-row
  s" ........R...R......." spr-row
  s" .....RRrRrrrRR......" spr-row
  s" ....RRrrrrrrrR.eeeee" spr-row
  s" ....RRrrrrrrrReeeeee" spr-row
  s" ...RRRrrrrrrRRee...." spr-row
  s" ...RRRRRRRRRRRee...." spr-row
  s" ...RR..RRRRRRRReeeee" spr-row
  s" ...R.RR.RRRR..Reeeee" spr-row
  s" ....R..R.RR.RR.RR..." spr-row
  s" ....R..R...R..R....." spr-row
  s" .....RR....R..R....." spr-row
  s" ............RR......" spr-row
  s" ...................." spr-row
;sprite

20 18 sprite: digger-l
  s" ........RRR........." spr-row
  s" ........RRR........." spr-row
  s" .......RR.RR........" spr-row
  s" .......R...R........" spr-row
  s" .......R...R........" spr-row
  s" .......R...R........" spr-row
  s" ......RRrrrRrRR....." spr-row
  s" eeeee.RrrrrrrrRR...." spr-row
  s" eeeeeeRrrrrrrrRR...." spr-row
  s" ....eeRRrrrrrrRRR..." spr-row
  s" ....eeRRRRRRRRRRR..." spr-row
  s" eeeeeRRRRRRRR..RR..." spr-row
  s" eeeeeR..RRRR.RR.R..." spr-row
  s" ...RR.RR.RR.R..R...." spr-row
  s" .....R..R...R..R...." spr-row
  s" .....R..R....RR....." spr-row
  s" ......RR............" spr-row
  s" ...................." spr-row
;sprite

20 18 sprite: digger-l-old
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
  s" ......ee..ee........" spr-row
  s" ......ee..ee........" spr-row
  s" .....Ree..ee........" spr-row
  s" .....Reeeeee........" spr-row
  s" ...RR.RReee........." spr-row
  s" ..R..R.RRRRRR......." spr-row
  s" ..R..R.RRRrrRRRRR..." spr-row
  s" ...RR.RRRrrrr...RRR." spr-row
  s" .....RRRRrrrr....RR." spr-row
  s" .....RRRRrrrr...RRR." spr-row
  s" ......RRRrrrRRRRR..." spr-row
  s" ....RR.RRrrrr......." spr-row
  s" ...R..R.RrrrR......." spr-row
  s" ...R..R.RRRRR......." spr-row
  s" ....RR.RRRRR........" spr-row
  s" ......RRRR.........." spr-row
  s" ...................." spr-row
  s" ...................." spr-row
;sprite

20 18 sprite: digger-d
  s" ...................." spr-row
  s" ...................." spr-row
  s" ..........RRRR......" spr-row
  s" ........RRRRR.RR...." spr-row
  s" .......RRRRR.R..R..." spr-row
  s" .......RrrrR.R..R..." spr-row
  s" .......rrrrRR.RR...." spr-row
  s" ...RRRRRrrrRRR......" spr-row
  s" .RRR...rrrrRRRR....." spr-row
  s" .RR....rrrrRRRR....." spr-row
  s" .RRR...rrrrRRR.RR..." spr-row
  s" ...RRRRRrrRRR.R..R.." spr-row
  s" .......RRRRRR.R..R.." spr-row
  s" .........eeeRR.RR..." spr-row
  s" ........eeeeeeR....." spr-row
  s" ........ee..eeR....." spr-row
  s" ........ee..ee......" spr-row
  s" ........ee..ee......" spr-row
;sprite

20 18 sprite: cross
  s" ...................." spr-row
  s" ...................." spr-row
  s" ........RRRR........" spr-row
  s" ........RRRR........" spr-row
  s" ........RRRR........" spr-row
  s" ........RRRR........" spr-row
  s" ....RRRR.RR.RRRR...." spr-row
  s" ....RRRRRRRRRRRR...." spr-row
  s" ....RRRRRRRRRRRR...." spr-row
  s" .........RR........." spr-row
  s" ........RRRR........" spr-row
  s" .........RR........." spr-row
  s" ........RRRR........" spr-row
  s" .......RRRRRR......." spr-row
  s" .......RRRRRR......." spr-row
  s" ......RRRRRRRR......" spr-row
  s" ......RRRRRRRR......" spr-row
  s" ...................." spr-row
;sprite

20 18 sprite: copy12
  s" ........RRR........." spr-row
  s" ........RRR........." spr-row
  s" .......RR.RR........" spr-row
  s" .......R...R........" spr-row
  s" .......R...R........" spr-row
  s" .......R...R........" spr-row
  s" ......RRrrrRrRR....." spr-row
  s" eeeee.RrrrrrrrRR...." spr-row
  s" eeeeeeRrrrrrrrRR...." spr-row
  s" ....eeRRrrrrrrRRR..." spr-row
  s" ....eeRRRRRRRRRRR..." spr-row
  s" eeeeeRRRRRRRR..RR..." spr-row
  s" eeeeeR..RRRR.RR.R..." spr-row
  s" ...RR.RR.RR.R..R...." spr-row
  s" .....R..R...R..R...." spr-row
  s" .....R..R....RR....." spr-row
  s" ......RR............" spr-row
  s" ...................." spr-row
;sprite

