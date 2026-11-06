\ code from https://www.onirom.fr/wiki/codegolf/x-87/



640 constant width
400 constant height
  3 constant bpp

create x 256 floats allot
create y 256 floats allot
create b width height * bpp * allot
create pal 3 c, 3 c, 3 c,
           3 c, 1 c, 2 c,
           2 c, 3 c, 2 c,
           1 c, 1 c, 1 c,

0 value ib      \ indice du point courant (0..255)
0 value idx     \ position du pixel dans le tampon

: xib ( -- a ) ib floats x + ;
: yib ( -- a ) ib floats y + ;

: setup
    x 256 floats erase
    y 256 floats erase
    b width height * bpp * erase
    255 0 do
        i s>f i floats x + f!
        0e i floats y + f!
    loop ;

: draw
    366 1 do \ frames
        65535 0 do \ iterations
            i 255 and to ib

            \ nouveau x : p = -0.995683*y + c/cos(x - cos(j/128 + y/(x/32+1))) - 5.1594524
            -0.995683e yib f@ f*
            0.009429633e
            xib f@  j s>f 128e f/  yib f@  xib f@ 32e f/ 1e f+ f/  f+
            fcos f- fcos f/
            5.1594524e f- f+            \ p

            \ nouveau y : tan(32*p*y)/j + 1 + x   (avec l'ancien x et l'ancien y)
            fdup 32e f* yib f@ f*
            ftan j s>f f/ 1e f+ xib f@ f+   \ p ynew
            fdup yib f!
            fswap xib f!                    \ ynew

            xib f@ 14e f+ 32e f/            \ sx
            fswap 3.141592653e f+ 0.8561283e fswap f/ 0.36063576e f+ fdup \ sy
            0.36063576e f> if
                0.98328006e f*              \ sy
                fswap 4e f*                 \ sx
            else
                fswap fover 2e f/ f-        \ sx
            then

            width s>f f* f>s                \ px
            width s>f f* f>s                \ py
            width * swap + bpp * to idx

            idx 0 width height * bpp * within if
                idx b + c@ idx 1 + b + c@ idx 2 + b + c@ + + 79 + 192 / 3 and \ p
                3 * pal + dup dup           \ c
                1 + c@ idx b + c@ + 255 min idx b + c!
                2 + c@ idx 1 + b + c@ + 255 min idx 1 + b + c!
                c@ idx 2 + b + c@ + 255 min idx 2 + b + c!
            then
        loop
    loop ;

: write \ ecrit le tampon RGB8 640x400 (brut) dans un fichier
    s" landscape.data" w/o create-file throw >r
    b width height * bpp * r@ write-file throw
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


