\ draw a chaotic landscape 
\ code from https://www.onirom.fr/wiki/codegolf/x-87/



640 constant width
400 constant height
  3 constant bpp
width height * bpp * constant bsize
width bpp * constant rowbytes

create x 256 floats allot
create y 256 floats allot
create b bsize allot
create pal 3 c, 3 c, 3 c,
           3 c, 1 c, 2 c,
           2 c, 3 c, 2 c,
           1 c, 1 c, 1 c,

0 value xa      \ adresse de x[ib]
0 value ya      \ adresse de y[ib]
0 value pa      \ adresse du pixel dans b
640e fconstant fwidth
fvariable fj    \ numero de frame, en flottant

: setup
    x 256 floats erase
    y 256 floats erase
    b bsize erase
    255 0 do
        i s>f i floats x + f!
        0e i floats y + f!
    loop ;

: draw
    366 1 do \ frames
        i s>f fj f!
        65535 0 do \ iterations
            i 255 and floats  dup x + to xa  y + to ya

            -0.995683e ya f@ f*  \ chaotic lines
            0.009429633e             \ chaotic lines
            xa f@  fj f@ 128e f/  ya f@  xa f@ 32e f/ 1e f+ f/  f+     \ chaotic lines
            fcos f- fcos f/
            5.1594524e f- f+                \ p    \ chaotic lines

            fdup 32e f* ya f@ f*                  \ chaotic lines  
            ftan fj f@ f/ 1e f+ xa f@ f+    \ p ynew
            fdup ya f!
            fswap xa f!                     \ ynew

            xa f@ 14e f+ 32e f/             \ sx
            fswap 3.141592653e f+ 0.8561283e fswap f/ 0.36063576e f+ fdup \ sy
            0.36063576e f> if     \ "sky" separation
                0.98328006e f*              \ sy
                fswap 4e f*                 \ sx
            else
                fswap fover 2e f/ f-        \ sx
            then

            fwidth f* f>s                   \ px
            fwidth f* f>s                   \ py
            rowbytes * swap bpp * + dup to pa

            0 bsize within if
                pa b + to pa
                pa c@ pa 1+ c@ pa 2 + c@ + + 79 + 192 / 3 and \ p
                3 * pal +
                dup 1+ c@ pa c@ + 255 min pa c!
                dup 2 + c@ pa 1+ c@ + 255 min pa 1+ c!
                c@ pa 2 + c@ + 255 min pa 2 + c!
            then
        loop
    loop ;

: write \ ecrit le tampon RGB8 640x400 (brut) dans un fichier
    s" landscape.data" w/o create-file throw >r
    b bsize r@ write-file throw
    r> close-file throw ;

\ setup draw write  \ direct write to raw
                                   \ convert it to png with: convert -size 640x400 -depth 8 rgb:landscape.data landscape.png

also posix
z" system" 1 sysfunc libc-system   \ ( z -- n ) lance une commande shell
previous

: convert-png ( -- )
    z" convert -size 640x400 -depth 8 rgb:landscape.data landscape.png"
    libc-system drop ;

setup draw write convert-png
bye


