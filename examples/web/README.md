# Web examples

`button.html`: runs words of a Forth program from HTML buttons. The Forth code is in a
`<script type="text/forth">` placed before `lugforth.js` (build it with `ninja web`, the file
is `out/web/lugforth.js`). The function `forth(text)` pushes the characters of a line, then
Enter, into `lugforth.inbuffer`, the queue of keys the interpreter reads, so the line runs as
if it was typed at the prompt. It only runs when Forth is waiting at the prompt, not when a
word is waiting in `key`.

The other way, from Forth to JavaScript, is `s" alert('hi')" jseval`.

`vector_logo.html`: writes LUGFORTH with strokes. Each letter is a short string of points on a
4 x 6 grid (`"00 06 46"` is an L, `/` lifts the pen), drawn with `beginPath`, `moveTo`, `lineTo`
and `stroke`, in three sizes.

`string_art.html`: 100 points on a circle, point `i` joined to point `i*k` (buttons for k), in
floating point (`fsin`, `fcos`). Both use `viewport@` so that one canvas pixel is one screen
pixel, otherwise the browser stretches the drawing. The `Back to the terminal` button runs
`text`.

`vector_logo_outline.html`: the same lettering as filled polygons with an outline. Each letter is
one or more polygons on a 5 x 7 grid (`/` closes one and starts the next, which is how O, R and
G get their holes: the path is filled with the `evenodd` rule, then stroked).
