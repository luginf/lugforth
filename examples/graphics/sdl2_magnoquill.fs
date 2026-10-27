#! /usr/bin/env lugforth

\ Magnoquill Driftflare Quiverbloom, with the sdl2 words of Lugforth.
\ Ported from the gforth + SDL2 version, itself a port of the TIC-80 cart and of the
\ shader https://github.com/Rage997/Magnoquill-Driftflare-Quiverbloom
\ (a point cloud driven by a few sines, in rainbow colors, additive blending).
\
\ Run:  out/posix/lugforth examples/graphics/sdl2_magnoquill.fs
\
\ Controls:
\   Up/Down      animation speed 1..10 (held)
\   Left/Right   point density (held)
\   Space        pause / resume
\   C            rainbow colors / monochrome
\   B            additive blending / normal
\   R            time back to 0
\   Q or Esc     quit
\ Additive blending needs the SDL2 renderer of the POSIX build, the other
\ boots draw with the normal blending.
\
\ Case is ignored: no word here shares its name with a sdl2 word (frame,
\ dot...), the names of the shader's variables got an f in front.

sdl2

800 constant win-w   800 constant win-h
500 constant npts-min   20000 constant npts-max   500 constant npts-step
0.004e0 fconstant base-tstep      \ time step per frame at speed 1
16 constant frame-ms              \ about 60 frames a second

variable frames   variable npts   variable speed-level
variable paused   variable colormode   variable blendmode   \ colormode 1: rainbow, blendmode 1: additive
fvariable qt                      \ the shader's t
fvariable fx   fvariable fy   fvariable fk   fvariable fe   fvariable fd
fvariable fq   fvariable fc   fvariable fpx  fvariable fpy  fvariable fbright
fvariable fhue fvariable fsat fvariable fval fvariable fcc fvariable fxc fvariable fm
fvariable frr  fvariable fgg  fvariable fbb
fvariable f-colortime  fvariable f-phase1  fvariable f-phase2  fvariable f-phase3
fvariable f-hprime     fvariable f-hmod

[defined] fmod 0= [if]
: fmod ( f: a b -- r )   fover fover f/ f>s s>f f* f- ;   \ a - b*trunc(a/b)
[then]

: xstep ( f: -- 10000/npts )   10000e0 npts @ s>f f/ ;

\ ---- the formulas of the shader ----
: calc-xy ( i -- )   s>f xstep f*  10000e0 fswap f-  fdup fx f!  235e0 f/ fy f! ;
\ k = (4 + sin(x/11 + t*8)) * cos(x/14)
: calc-k   fx f@ 11e0 f/  qt f@ 8e0 f* f+ fsin  4e0 f+   fx f@ 14e0 f/ fcos  f*  fk f! ;
\ e = y/8 - 19
: calc-e   fy f@ 8e0 f/ 19e0 f-  fe f! ;
\ d = sqrt(k*k + e*e) + sin(y/9 + t*2)
: calc-d   fk f@ fdup f*  fe f@ fdup f*  f+ fsqrt   fy f@ 9e0 f/  qt f@ 2e0 f* f+ fsin  f+  fd f! ;
\ q = 2*sin(2k) + sin(y/17)*k*(9 + 2*sin(y - 3d))
: calc-q
   fy f@  fd f@ 3e0 f* f-  fsin  2e0 f*  9e0 f+   fk f@ f*   fy f@ 17e0 f/ fsin f*
   fk f@ 2e0 f* fsin 2e0 f* f+  fq f! ;
\ c = d*d/49 - t
: calc-c   fd f@ fdup f* 49e0 f/  qt f@ f-  fc f! ;
\ px = q + 50*cos(c) + 200,  py = q*sin(c) + d*39 - 440
: calc-p
   fq f@  fc f@ fcos 50e0 f* f+  200e0 f+  fpx f!
   fq f@ fc f@ fsin f*  fd f@ 39e0 f* f+  440e0 f-  fpy f! ;
\ brightness = abs(sin(t*2 + x*0.001)) + 0.5
: calc-bright   qt f@ 2e0 f*  fx f@ 0.001e0 f* f+ fsin fabs 0.5e0 f+  fbright f! ;

\ ---- color: the shader's HSV cycle, in real RGB ----
: hue-rgb ( f: hprime -- )
   fdup 1e0 f< if fdrop fcc f@ fxc f@ 0e0 else
   fdup 2e0 f< if fdrop fxc f@ fcc f@ 0e0 else
   fdup 3e0 f< if fdrop 0e0 fcc f@ fxc f@ else
   fdup 4e0 f< if fdrop 0e0 fxc f@ fcc f@ else
   fdup 5e0 f< if fdrop fxc f@ 0e0 fcc f@ else
   fdrop fcc f@ 0e0 fxc f@
   then then then then then
   fbb f! fgg f! frr f! ;

: calc-color
   qt f@ 0.5e0 f*  f-colortime f!
   fx f@ 0.0001e0 f*  f-colortime f@ f+ fsin 0.5e0 f* 0.5e0 f+  f-phase1 f!
   fy f@ 0.01e0 f*  f-colortime f@ 1.3e0 f* f+ fsin 0.5e0 f* 0.5e0 f+  f-phase2 f!
   fd f@ 0.1e0 f*  f-colortime f@ 0.7e0 f* f+ fsin 0.5e0 f* 0.5e0 f+  f-phase3 f!
   f-phase1 f@ 360e0 f*  f-colortime f@ 20e0 f* f+  360e0 fmod  fhue f!
   f-phase2 f@ 0.4e0 f* 0.6e0 f+  fsat f!
   f-phase3 f@ 0.2e0 f* 0.8e0 f+  fval f!
   fval f@ fsat f@ f*  fcc f!
   fhue f@ 60e0 f/  f-hprime f!
   f-hprime f@ 2e0 fmod 1e0 f- fabs  f-hmod f!
   fcc f@  1e0 f-hmod f@ f-  f*  fxc f!
   f-hprime f@ hue-rgb
   fval f@ fcc f@ f-  fm f!
   frr f@ fm f@ f+ frr f!   fgg f@ fm f@ f+ fgg f!   fbb f@ fm f@ f+ fbb f! ;

: byte ( f: v -- n )   fbright f@ f* 255e0 f* f>s  0 max 255 min ;

: draw-point ( i -- )
   calc-xy calc-k calc-e calc-d calc-q calc-c calc-p calc-bright
   colormode @ if calc-color else 1e0 fdup fdup frr f! fgg f! fbb f! then
   frr f@ byte  fgg f@ byte  fbb f@ byte  rgb
   fpx f@ 2e0 f* f>s   400e0 fpy f@ f- 2e0 f* f>s   dot ;

: draw-creature   npts @ 0 do i draw-point loop ;

\ ---- controls ----
\ additive blending: SDL_BLENDMODE_ADD, only where the raw binding exists
[defined] SDL_SetRenderDrawBlendMode [if]
: set-blend ( f -- )
   renderer swap if SDL_BLENDMODE_ADD else SDL_BLENDMODE_BLEND then SDL_SetRenderDrawBlendMode drop ;
[else]
: set-blend ( f -- )   drop ;
[then]

create prev 128 allot
: tap? ( c -- f )   dup pressed? swap prev + dup c@ >r  over swap c!  0= r> 0= 0= and ;
: tap-toggle ( c a -- )   swap tap? if dup @ 0= swap ! else drop then ;
: adjust-speed ( d -- )   speed-level @ + 1 max 10 min speed-level ! ;
: adjust-density ( d -- )   npts @ + npts-min max npts-max min npts ! ;

: handle-input
   [char] c colormode tap-toggle
   [char] b blendmode tap-toggle   blendmode @ set-blend
   [char] r tap? if 0e0 qt f! then
   [char] q pressed? if quit! then
   key-esc pressed? if quit! then
   key-space pressed? if  paused @ 0= paused !  200 delay then
   frames @ 4 mod 0= if
      key-up pressed?    if  1 adjust-speed  then
      key-down pressed?  if -1 adjust-speed  then
      key-right pressed? if  npts-step adjust-density  then
      key-left pressed?  if  npts-step negate adjust-density  then
   then ;

: step
   ticks >r
   events handle-input
   black color cls
   paused @ 0= if speed-level @ s>f base-tstep f*  qt f@ f+ qt f! then
   draw-creature flip
   1 frames +!
   frame-ms ticks r> - - dup 0> if delay else drop then ;

: magnoquill ( -- )
   win-w win-h screen
   s" Magnoquill Driftflare Quiverbloom" title
   0 frames !  4000 npts !  5 speed-level !  0e0 qt f!
   0 paused !  1 colormode !  1 blendmode !
   prev 128 erase
   blendmode @ set-blend
   begin step quit? until
   close-screen ;

magnoquill
