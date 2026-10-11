\ Started by SdlActivity in the examples folder. A console appears on the screen.
include sdl2_console.fs
smooth-font
console
s" Lugforth on Android. Tap the screen for the keyboard." type cr
s" Games: include sdl2_breakout.fs   include sdl2_drums.fs" type cr
s" Menu (top left): arrows, Tab, text size, help. Pinch to zoom." type cr
( the plain search order, and a game that ends does not end the session )
only forth also internals 1 'argc ! previous definitions
