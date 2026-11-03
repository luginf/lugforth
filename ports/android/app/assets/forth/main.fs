\ Started by SdlActivity in the examples folder. A console appears on the screen.
gui-load
smooth-font
console
s" Lugforth on Android" type cr
( the plain search order, and a game that ends does not end the session )
only forth also internals 1 'argc ! previous
s" also sdl2 definitions" evaluate   ( what a program defines goes in sdl2, as in a terminal )
