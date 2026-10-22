\ Started by SdlActivity in the examples folder. A console appears on the screen.
gui-load
smooth-font
console
s" Lugforth on Android. Tap the screen for the keyboard." type cr
s" Games: include games/sdl2_breakout.fs   include music/sdl2_drums.fs" type cr
s" Menu (top left): Tab, arrows, Ex (examples), text size, help. Pinch to zoom." type cr
( the plain search order, and a game that ends does not end the session )
only forth also internals 1 'argc ! previous definitions
