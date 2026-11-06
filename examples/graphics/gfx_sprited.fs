\ gfx_sprited.fs: a sprite editor in Lugforth (POSIX, SDL2).
\
\ It opens a file of sprites written with gfx_spr.fs (a plain Forth
\ file: colors, then `w h sprite: name ... ;sprite`), lets you draw
\ them, and writes the file back. The game that includes the file gets
\ the new sprites at its next start.
\
\   lugforth examples/graphics/gfx_sprited.fs   \ edits examples/games/gfx_digger_sprites.fs
\
\ Another file: define sprited-no-run, load this file, then
\   : sprited-no-run ;  include examples/graphics/gfx_sprited.fs
\   s" my_sprites.fs" edit-sprites
\ (the file must exist and hold at least one sprite: begin it by hand
\ with a line `16 16 sprite: first` and a line `;sprite`).
\
\ Mouse: left button paints, right button erases, middle button picks
\ the color under it; click a color or a sprite name to choose it.
\ Keys: s save, u undo, c clear, h v flip, r e turn a quarter (clockwise,
\ the other way), arrows shift (wrapping), y copies the sprite and p pastes
\ it over the sprite you chose next,
\ d duplicate the sprite, n new color (a copy of the current one),
\ 1 2 / 3 4 / 5 6 less and more red / green / blue, Tab next sprite,
\ o (or the OPEN button) opens another file of sprites: a list of the
\ folders and of the files made of sprites only (any other file would run
\ when included), arrows and Enter or the mouse, Esc cancels,
\ Esc quit (without saving).

gfx
include gfx_spr.fs

480 constant sed-w   300 constant sed-h

\ ---- A tiny 3x5 font ----
create sed-digits
  $7b6f , $2c97 , $73e7 , $73cf , $5bc9 , $79cf , $79ef , $7249 , $7bef , $7bcf ,
create sed-font
  $2bed , $6bae , $3923 , $6b6e , $79a7 , $79a4 , $396b , $5bed , $7497 , $126a , $5bad , $4927 , $5fed ,
  $6b6d , $2b6a , $6ba4 , $2b7b , $6bad , $388e , $7492 , $5b6f , $5b6a , $5bfd , $5aad , $5a92 , $72a7 ,
: sed-bits { bits x y s }
  15 0 do
    bits 14 i - rshift 1 and if  x i 3 mod s * +  y i 3 / s * +  s s box  then
  loop ;
: sed-glyph ( c -- bits )
  dup 48 58 within if 48 - cells sed-digits + @ exit then
  32 or dup 97 < over 122 > or if drop 0 exit then
  97 - cells sed-font + @ ;
: sed-text { a n x y s }   n 0 do  a i + c@ sed-glyph  x i 4 s * * +  y s sed-bits  loop ;

\ ---- State ----
0 value sed-cur       \ index of the sprite being edited
46 value sed-ch       \ the character painted: '.' is transparent
0 value sed-dirty     \ changes not saved
0 value sed-msg-t   0 value sed-msg-a   0 value sed-msg-n
create sed-file 200 allot   0 value sed-file-n
create sed-head 1024 allot  0 value sed-head-n   \ the comment lines at the top of the file
create sed-undo 4096 allot  0 value sed-undo-sp   \ the grid before the last change, and its sprite
create sed-tmp 32768 allot
0 value sed-fid
create sed-cl 100 allot   0 value sed-ncl         \ the characters of the palette, '.' first

: sed-nth ( i -- spr )   cells spr-all + @ ;
: sed-spr ( -- spr )   sed-cur sed-nth ;
: sed-say ( a n -- )   to sed-msg-n to sed-msg-a  150 to sed-msg-t ;

create sed-was 256 allot   sed-was 256 erase
: sed-just ( k -- f )   dup pressed? swap 255 and sed-was + 2dup c@ 0= and >r c! r> ;

\ ---- Geometry ----
8 constant sed-gx   24 constant sed-gy
: sed-cs ( -- n )   256  sed-spr spr-w sed-spr spr-h max  /  1 max ;   \ size of a drawn pixel
: sed-cell ( mx my -- x y f )   ( the pixel under the mouse )
  sed-gy -  swap sed-gx -  swap
  2dup 0< swap 0< or if 2drop 0 0 0 exit then
  sed-cs /  swap sed-cs /  swap
  2dup sed-spr spr-h <  swap sed-spr spr-w <  and ;

\ ---- Editing ----
: sed-snap ( -- )
  sed-spr spr-grid sed-undo  sed-spr spr-w sed-spr spr-h *  cmove  sed-cur 1+ to sed-undo-sp ;
: sed-undo! ( -- )
  sed-undo-sp sed-cur 1+ = if
    sed-undo sed-spr spr-grid  sed-spr spr-w sed-spr spr-h *  cmove
    sed-spr spr-build  s" UNDONE" sed-say
  then ;
: sed-set ( ch x y -- )   sed-spr spr-pix!  sed-spr spr-build  -1 to sed-dirty ;

: sed-colors ( -- )   ( the list of the characters of the palette )
  46 sed-cl c!  1 to sed-ncl
  127 33 do
    i 46 <> if i spr-defined? if i sed-cl sed-ncl + c!  1 +to sed-ncl then then
  loop ;

: sed-pick-color ( mx my -- )
  sed-colors
  20 - 18 /  swap 284 - 18 /  swap      \ column row
  2dup 0< swap 0< or if 2drop exit then
  over 8 < 0= if 2drop exit then
  8 * +  dup sed-ncl < if sed-cl + c@ to sed-ch else drop then ;

: sed-pick-sprite ( mx my -- )
  196 - 9 /  swap 280 - 96 /  swap     \ column row
  2dup 0< swap 0< or if 2drop exit then
  over 2 < 0= if 2drop exit then
  dup 10 < 0= if 2drop exit then
  swap 10 * +  dup spr-count < if to sed-cur  0 to sed-undo-sp else drop then ;

0 value fw   0 value fh   0 value fm
: sed-src ( x y -- sx sy )   ( where the pixel x y of the result comes from )
  fm case
    0 of swap fw 1- swap - swap endof
    1 of fh 1- swap - endof
    2 of swap 1+ fw mod swap endof
    3 of swap fw + 1- fw mod swap endof
    4 of 1+ fh mod endof
    5 of fh + 1- fh mod endof
  endcase ;
: sed-xform ( mode -- )   ( flips and shifts )
  to fm  sed-snap  sed-spr spr-w to fw  sed-spr spr-h to fh
  fh 0 do fw 0 do
    i j sed-src  fw * + sed-undo + c@
    i j sed-spr spr-pix!
  loop loop
  sed-spr spr-build  -1 to sed-dirty ;

: sed-clear ( -- )
  sed-snap  sed-spr spr-grid sed-spr spr-w sed-spr spr-h *  [char] . fill
  sed-spr spr-build  -1 to sed-dirty ;

: sed-new-color ( -- )   ( a copy of the current color, under a free character )
  s" 0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"  0 do
    dup i + c@ spr-defined? 0= if
      sed-ch 46 = if $ffffff else sed-ch spr-color@ then  over i + c@ spr-pal
      dup i + c@ to sed-ch  -1 to sed-dirty  leave
    then
  loop drop ;

0 value ch-s   0 value ch-d   0 value ch-c
: sed-old@ ( x y -- ch )   ( in the grid saved by sed-snap )   fw * + sed-undo + c@ ;
0 value ro-x   0 value ro-y   0 value ro-ox   0 value ro-oy   0 value ro-dir
: sed-rot ( dir -- )   ( a quarter turn: 1 clockwise, -1 the other way; a sprite that is not square is turned in the middle of its own size, what does not fit is cut )
  to ro-dir  sed-snap  sed-spr spr-w to fw  sed-spr spr-h to fh
  fw fh - 2/ to ro-ox   fh fw - 2/ to ro-oy
  fh 0 do fw 0 do
    i ro-ox - to ro-x   j ro-oy - to ro-y
    ro-x -1 >  ro-x fh <  and  ro-y -1 >  and  ro-y fw <  and if
      ro-dir 0> if ro-y fh 1- ro-x - else fw 1- ro-y - ro-x then sed-old@
    else 46 then
    i j sed-spr spr-pix!
  loop loop
  sed-spr spr-build  -1 to sed-dirty
  fw fh = if s" TURNED" else s" TURNED, CUT TO ITS SIZE" then sed-say ;

-1 value sed-clip      \ the sprite copied with y
: sed-yank ( -- )   sed-cur to sed-clip  s" COPIED: CHOOSE ANOTHER SPRITE, THEN P" sed-say ;
: sed-paste ( -- )   ( the copied sprite over this one, from the top left corner )
  sed-clip 0< if s" NOTHING COPIED YET: Y" sed-say exit then
  sed-clip sed-cur = if s" CHOOSE ANOTHER SPRITE" sed-say exit then
  sed-snap
  sed-spr spr-h 0 do sed-spr spr-w 0 do
    i sed-clip sed-nth spr-w <  j sed-clip sed-nth spr-h <  and
    if i j sed-clip sed-nth spr-pix@ else 46 then
    i j sed-spr spr-pix!
  loop loop
  sed-spr spr-build  -1 to sed-dirty  s" PASTED" sed-say ;

: sed-chan ( shift delta -- )   ( change a channel of the current color )
  sed-ch 46 = if 2drop exit then
  to ch-d to ch-s   sed-ch spr-color@ to ch-c
  ch-c ch-s rshift 255 and  ch-d + 0 max 255 min  ch-s lshift
  ch-c  255 ch-s lshift invert and  or   sed-ch spr-pal
  -1 to sed-dirty ;

: sed-dup ( -- )   ( a copy of the sprite, named copyN )
  spr-count spr-max < 0= if s" TOO MANY SPRITES" sed-say exit then
  spr-reset  sed-spr spr-w spr-num  32 spr-c+  sed-spr spr-h spr-num
  s"  sprite: copy" spr+  spr-count spr-num  s"  ;sprite" spr+
  spr-buf spr-len evaluate
  sed-spr spr-grid  spr-count 1- sed-nth spr-grid  sed-spr spr-w sed-spr spr-h *  cmove
  spr-count 1- sed-nth spr-build
  spr-count 1- to sed-cur  -1 to sed-dirty  s" DUPLICATED" sed-say ;

\ ---- Files ----
0 value rn   0 value rp   create sed-nl 10 c,
: sed-read-head ( -- )   ( the comment lines at the top of the file )
  0 to sed-head-n
  sed-file sed-file-n r/o open-file if drop exit then  to sed-fid
  sed-tmp 32767 sed-fid read-file drop  to rn
  sed-fid close-file drop  0 to rp
  begin rp rn < if sed-tmp rp + c@ 92 = else 0 then while
    begin rp rn < if sed-tmp rp + c@ 10 <> else 0 then while 1 +to rp repeat
    1 +to rp
  repeat
  rp 1023 min to sed-head-n   sed-tmp sed-head sed-head-n cmove ;
: sed-save ( -- )
  sed-file sed-file-n w/o create-file if drop s" SAVE FAILED" sed-say exit then
  to sed-fid
  sed-head sed-head-n sed-fid write-file drop   sed-nl 1 sed-fid write-file drop
  spr-pal-source sed-fid write-file drop
  sed-nl 1 sed-fid write-file drop
  spr-count 0 do
    i sed-nth  spr-source  sed-fid write-file drop
    sed-nl 1 sed-fid write-file drop
  loop
  sed-fid close-file drop
  0 to sed-dirty  s" SAVED" sed-say ;

: sed-load ( -- f )   ( include the file, found from the working directory; true on failure )
  sed-file sed-file-n r/o open-file if drop -1 exit then
  dup ['] include-file catch  swap close-file drop  0<> ;

: sed-try ( -- f )   ( load the file named in sed-file: true if there are sprites )
  0 to spr-count  spr-pt spr-pal-size erase
  sed-load if 0 exit then
  spr-count 0<> ;
: sed-start-file ( -- )
  sed-read-head
  sed-head-n 0= if s" \ Sprites, drawn with examples/graphics/gfx_sprited.fs" 2dup sed-head swap cmove
    to sed-head-n drop  10 sed-head sed-head-n + c!  1 +to sed-head-n then
  0 to sed-cur  46 to sed-ch  0 to sed-dirty  0 to sed-undo-sp  -1 to sed-clip ;
: sed-title ( -- )   spr-reset  s" Sprite editor - " spr+  sed-file sed-file-n spr+  spr-buf spr-len title ;

create sed-old 200 allot   0 value sed-old-n
: sed-switch ( a n -- )   ( open another file; if it fails, the old one comes back )
  sed-file sed-old sed-file-n cmove   sed-file-n to sed-old-n
  199 min dup to sed-file-n  sed-file swap cmove
  sed-try if sed-start-file sed-title s" OPENED" sed-say exit then
  sed-old sed-file sed-old-n cmove   sed-old-n to sed-file-n
  sed-try drop   s" CANNOT OPEN: NO SPRITE IN THAT FILE" sed-say ;

\ ---- The open menu: the folders and the .fs files of a folder ----
200 constant mn-max   15 constant mn-rows
create mn-ent mn-max 40 * allot   0 value mn-n      \ count, name, and at 39 a flag: folder
create sed-dir 200 allot   0 value sed-dir-n         \ the folder shown
create mn-full 260 allot   0 value mn-full-n
create mn-tmp 40 allot
0 value mn-on   0 value mn-sel   0 value mn-top   0 value mn-arm
0 value mn-la   0 value mn-ln   0 value mn-a   0 value mn-b   0 value mn-e   0 value mn-dh

: mn-name ( i -- a n )   40 * mn-ent + count ;
: mn-dir? ( i -- f )   40 * mn-ent + 39 + c@ ;
: mn-add ( a n d -- )
  mn-n mn-max >= if drop 2drop exit then
  mn-n 40 * mn-ent + >r  r@ 39 + c!  38 min dup r@ c!  r> 1+ swap cmove  1 +to mn-n ;
: mn-ends? ( a n b m -- f )   ( the text a n ends with b m )
  { a n b m }  n m < if 0 exit then  a n + m -  m  b m  compare 0= ;
: mn-less? ( i j -- f )   ( folders first, then by name )
  2dup mn-dir? swap mn-dir? <> if drop mn-dir? exit then
  mn-name rot mn-name 2swap compare 0< ;
: mn-swap ( i j -- )
  40 * mn-ent + to mn-b  40 * mn-ent + to mn-a
  mn-a mn-tmp 40 cmove  mn-b mn-a 40 cmove  mn-tmp mn-b 40 cmove ;
: mn-sort ( -- )
  mn-n 0 ?do  mn-n 1- 0 ?do  i 1+ i mn-less? if i 1+ i mn-swap then  loop  loop ;

: mn-join ( a n -- a2 n2 )   ( the folder, a slash and the name )
  to mn-ln to mn-la
  sed-dir-n 1 =  sed-dir c@ [char] . =  and if mn-la mn-ln exit then
  sed-dir mn-full sed-dir-n cmove   sed-dir-n to mn-full-n
  sed-dir-n 1 =  sed-dir c@ [char] / =  and 0= if [char] / mn-full mn-full-n + c!  1 +to mn-full-n then
  mn-la mn-full mn-full-n + mn-ln cmove   mn-ln +to mn-full-n
  mn-full mn-full-n ;

0 value mn-fid   0 value mn-ok   0 value mn-seen
: mn-line-ok? ( a n -- f )   ( a line that a file of sprites can hold )
  begin dup 0> if over c@ 32 = else 0 then while 1 /string repeat
  dup 0= if 2drop -1 exit then
  over c@ dup [char] \ = swap [char] ; = or if 2drop -1 exit then
  over c@ [char] : = if 2drop 0 exit then
  2dup s" sprite: " search nip nip if 1 to mn-seen 2drop -1 exit then
  2dup s"  spr-pal" search nip nip if 2drop -1 exit then
  2dup s"  spr-row" search nip nip if 2drop -1 exit then
  2drop 0 ;
: mn-sprite-file? ( a n -- f )   ( only files made of sprites are listed: including any other file would run it )
  mn-join r/o open-file if drop 0 exit then  to mn-fid
  sed-tmp 32767 mn-fid read-file drop to rn   mn-fid close-file drop
  rn 32767 = if 0 exit then
  0 to rp  -1 to mn-ok  0 to mn-seen
  begin rp rn < mn-ok and while
    sed-tmp rp +
    rp begin dup rn < if sed-tmp over + c@ 10 <> else 0 then while 1+ repeat
    dup rp - swap 1+ to rp
    mn-line-ok? 0= if 0 to mn-ok then
  repeat
  mn-ok mn-seen and ;

also posix
: mn-scan ( -- )
  0 to mn-n  0 to mn-sel  0 to mn-top
  s" .." -1 mn-add
  sed-dir sed-dir-n open-dir if drop mn-sort exit then  to mn-dh
  begin mn-dh readdir dup while
    to mn-e
    mn-e .d_name z>s to mn-ln to mn-la
    mn-la c@ [char] . <> if
      mn-e .d_type 4 = if mn-la mn-ln -1 mn-add
      else mn-la mn-ln s" .fs" mn-ends? if mn-la mn-ln mn-sprite-file? if mn-la mn-ln 0 mn-add then then then
    then
  repeat
  drop mn-dh closedir drop  mn-sort ;
previous

: mn-set-dir ( a n -- )   199 min dup to sed-dir-n  sed-dir swap cmove ;
: mn-cwd ( -- )   ( start in the folder of the file being edited )
  sed-file-n begin dup 0> if sed-file over 1- + c@ [char] / <> else 0 then while 1- repeat
  dup 0= if drop s" ." mn-set-dir exit then
  dup 1 = if drop s" /" mn-set-dir exit then
  1- sed-file swap mn-set-dir ;
: mn-up ( -- )
  sed-dir-n 1 =  sed-dir c@ [char] . =  and if s" .." mn-set-dir exit then
  sed-dir sed-dir-n s" .." mn-ends? if
    sed-dir-n 190 < if s" /.." sed-dir sed-dir-n + swap cmove  3 +to sed-dir-n then exit
  then
  sed-dir-n begin dup 0> if sed-dir over 1- + c@ [char] / <> else 0 then while 1- repeat
  dup 0= if drop s" ." mn-set-dir exit then
  dup 1 = if drop s" /" mn-set-dir exit then
  1- sed-dir swap mn-set-dir ;
: mn-start ( -- )   mn-cwd mn-scan  -1 to mn-on ;
: mn-request ( -- )   ( o: the menu; with changes not saved, ask twice )
  sed-dirty mn-arm 0= and if
    150 to mn-arm  s" MODIFIED: S SAVES, O AGAIN OPENS WITHOUT SAVING" sed-say exit
  then
  0 to mn-arm  mn-start ;
: mn-go ( -- )
  mn-n 0= if exit then
  mn-sel mn-name
  mn-sel mn-dir? if
    2dup s" .." compare 0= if 2drop mn-up else mn-join mn-set-dir then
    mn-scan exit
  then
  mn-join sed-switch  0 to mn-on ;
: mn-clamp ( -- )
  mn-sel 0 max mn-n 1- min to mn-sel
  mn-sel mn-top < if mn-sel to mn-top then
  mn-sel mn-top mn-rows + >= if mn-sel mn-rows - 1+ to mn-top then ;
: mn-input ( -- )
  key-up sed-just if -1 +to mn-sel then
  key-down sed-just if 1 +to mn-sel then
  key-enter sed-just if mn-go then
  LEFT-BUTTON sed-just if
    mouse-x 30 450 within  mouse-y 52 - dup 0< 0= and if
      14 / mn-top +  dup mn-n < if to mn-sel mn-go else drop then
    else drop then
  then
  mn-on if mn-clamp then ;

\ ---- Drawing ----
0 value px   0 value py
: sed-draw-grid ( -- )
  sed-spr spr-h 0 do sed-spr spr-w 0 do
    i j sed-spr spr-pix@  dup 46 = if
      drop  i j + 1 and if $505050 else $404040 then
    else spr-color@ then  color
    sed-gx i sed-cs * +  sed-gy j sed-cs * +  sed-cs 1- dup box
  loop loop ;

: sed-draw-palette ( -- )
  sed-colors
  sed-ncl 0 do
    i 8 mod 18 * 284 + to px   i 8 / 18 * 20 + to py
    i sed-cl + c@ sed-ch = if white color  px py 16 16 box then
    i sed-cl + c@ dup 46 = if drop $404040 else spr-color@ then color
    px 1+ py 1+ 14 14 box
  loop ;

: sed-draw-list ( -- )
  spr-count 0 do
    i 10 / 96 * 280 + to px   i 10 mod 9 * 196 + to py
    i sed-cur = if white else gray then color
    i sed-nth spr-name  px py 1 sed-text
  loop ;

: sed-draw-preview ( -- )
  $9a4a1c color  280 108 190 84 box
  sed-spr 286 114 spr-draw
  sed-spr 324 114  sed-spr spr-h 24 > if 2 else 3 then  spr-drawn ;

: sed-draw-info ( -- )
  spr-reset  sed-spr spr-name spr+  32 spr-c+
  sed-spr spr-w spr-num  [char] X spr-c+  sed-spr spr-h spr-num  32 spr-c+
  sed-spr spr-n spr-num  s"  BOXES " spr+
  white color  spr-buf spr-len 8 2 2 sed-text
  sed-dirty if orange color  s" MODIFIED" 380 2 2 sed-text then
  gray color  sed-file sed-file-n 8 15 1 sed-text      \ the file being edited
  spr-reset  sed-ch 46 = if s" TRANSPARENT" spr+ else sed-ch spr-color@ spr-hex then
  gray color  spr-buf spr-len 284 4 1 sed-text ;

: sed-draw-help ( -- )
  sed-msg-t 0> if
    -1 +to sed-msg-t  yellow color  sed-msg-a sed-msg-n 8 288 2 sed-text
  else
    gray color  s" S SAVE  O OPEN  U UNDO  C CLEAR  H V FLIP  R E TURN  ARROWS SHIFT  Y P COPY TO  D DUP  N COLOR  1-6 RGB  TAB NEXT" 8 290 1 sed-text
  then ;

: sed-draw-button ( -- )
  $303060 color  436 20 40 14 box   white color  s" OPEN" 443 25 1 sed-text ;

: mn-draw-row ( i y -- )
  to py
  dup mn-sel = if $4040a0 color  34 py 2 - 412 14 box then
  dup mn-dir? if $e0c040 color  40 py 2 + 8 6 box  cyan else white then color
  mn-name 54 py 2 sed-text ;
: mn-draw ( -- )   ( the list over the editor )
  white color  28 18 424 264 box   $202048 color  30 20 420 260 box
  white color  s" OPEN" 38 26 2 sed-text
  gray color
  sed-dir-n 50 > if sed-dir sed-dir-n + 50 -  50 else sed-dir sed-dir-n then  90 28 1 sed-text
  mn-rows 0 do
    mn-top i + dup mn-n < if i 14 * 52 + mn-draw-row else drop then
  loop
  gray color  s" UP DOWN SELECT  ENTER OR CLICK OPEN  ESC CANCEL" 38 270 1 sed-text ;

: sed-draw ( -- )
  black color cls
  sed-draw-grid sed-draw-palette sed-draw-list sed-draw-preview sed-draw-info sed-draw-help sed-draw-button
  mn-on if mn-draw then
  flip ;

\ ---- Input ----
0 value jl   0 value jr   0 value jm
: sed-input ( -- )
  mn-on if mn-input exit then
  LEFT-BUTTON sed-just to jl   RIGHT-BUTTON sed-just to jr   MIDDLE-BUTTON sed-just to jm
  jl jr or if mouse-x mouse-y sed-cell nip nip if sed-snap then then
  jl if mouse-x mouse-y sed-pick-color  mouse-x mouse-y sed-pick-sprite then
  mouse-x mouse-y sed-cell if
    jm if 2dup sed-spr spr-pix@ to sed-ch then
    LEFT-BUTTON pressed? if sed-ch -rot sed-set
    else RIGHT-BUTTON pressed? if 46 -rot sed-set else 2drop then then
  else 2drop then
  jl if mouse-x 436 476 within  mouse-y 20 34 within and if mn-request then then
  [char] o sed-just if mn-request then
  [char] s sed-just if sed-save then
  [char] u sed-just if sed-undo! then
  [char] c sed-just if sed-clear then
  [char] h sed-just if 0 sed-xform then
  [char] v sed-just if 1 sed-xform then
  key-left sed-just if 2 sed-xform then
  key-right sed-just if 3 sed-xform then
  key-up sed-just if 4 sed-xform then
  key-down sed-just if 5 sed-xform then
  [char] r sed-just if 1 sed-rot then
  [char] e sed-just if -1 sed-rot then
  [char] y sed-just if sed-yank then
  [char] p sed-just if sed-paste then
  [char] d sed-just if sed-dup then
  [char] n sed-just if sed-new-color then
  [char] 1 sed-just if 16 -16 sed-chan then
  [char] 2 sed-just if 16 16 sed-chan then
  [char] 3 sed-just if 8 -16 sed-chan then
  [char] 4 sed-just if 8 16 sed-chan then
  [char] 5 sed-just if 0 -16 sed-chan then
  [char] 6 sed-just if 0 16 sed-chan then
  key-tab sed-just if sed-cur 1+ spr-count mod to sed-cur  0 to sed-undo-sp then ;

: sed-frame ( -- )
  events
  key-esc sed-just if mn-on if 0 to mn-on else quit! then then
  mn-arm 0> if -1 +to mn-arm then
  sed-input sed-draw
  dt 16 < if 16 dt - delay then ;

: edit-sprites ( a n -- )   ( edit the sprites of a file )
  199 min dup to sed-file-n  sed-file swap cmove
  sed-try 0= if ." cannot load " sed-file sed-file-n type cr ." or no sprite in it" cr exit then
  sed-start-file
  2 to zoom  sed-w sed-h screen  sed-title
  begin sed-frame quit? until close-screen ;

: sed-exists? ( a n -- f )   r/o open-file if drop 0 else close-file drop -1 then ;

[defined] sprited-no-run [if] [else]
s" examples/games/gfx_digger_sprites.fs" 2dup sed-exists? 0= [if] 2drop s" games/gfx_digger_sprites.fs" [then]
edit-sprites
script-bye
[then]
