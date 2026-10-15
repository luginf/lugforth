# FM synthesizer

A two operator FM synthesizer with MIDI input, as an example: it is **not** part of the interpreter, nothing here is compiled into `lugforth` or `lugforth.js`.

* `sdl2_fm.fs`: the instrument (sliders, six sounds, keyboard on screen). `lugforth examples/fm/sdl2_fm.fs` on Linux; `sdl2_fm.html` in a browser (`python3 -m http.server` in `out/web`, built by `ninja web`).
* `engine_posix.fs`, `engine_web.fs`: the synthesizer words for each platform (Forth samples and ALSA MIDI; WebAudio and Web MIDI). `include` one, then type `sdl2-fm`.
* `midi_parser.fs`: the MIDI byte parser both share.
* `sdl2_fm_tests.fs`: 24 tests. `SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy lugforth examples/fm/sdl2_fm_tests.fs`.

The interpreter only offers three small generic hooks used here: `render-hook` and `active-hook` (extra sources for the mix of `pump`), `latency` as a value, and `scan?` (keys by place).

## Words

`sdl2-fm` (after `include engine_posix.fs`) is a two operator FM synthesizer (a modulator bends the phase of a carrier,
as in a DX7, each with its own envelope; 8 voices) with MIDI input. The samples are
computed in Forth (about 10 percent of one core with 8 voices sounding) and mixed with the hits of
`drum-hit` into the same audio queue, so a loop that calls `flip` or `delay` is enough.
`sdl2_fm.fs` is a complete instrument with sliders, six sounds and a keyboard
on screen, and runs unchanged in the browser (`sdl2_fm.html`).

```forth
sdl2-fm
200 to fm-ratio   30 to fm-index         \ modulator = 2 x carrier, index 3.0
60 100 fm-on   500 delay   60 fm-off     \ middle C, velocity 100, for half a second
midi-open drop   midi-connect-all drop   \ listen to the MIDI keyboards
320 200 screen
: play  begin events midi-poll flip quit? until  close-screen ;
play
```

The patch is a set of values read when a note starts (`fm-release` when it ends):
`fm-ratio` (modulator / carrier frequency times 100), `fm-index` (modulation index
times 10), `fm-attack fm-decay fm-release fm-mdecay` (milliseconds), `fm-sustain
fm-msus fm-volume` (percent). The modulation index follows its own envelope: it
starts at `fm-index` and falls to `fm-msus` percent in about `fm-mdecay` ms, which
gives the bright start of a struck note. Notes: `fm-on ( note vel -- )`,
`fm-off ( note -- )`, `fm-all-off`, `fm-bend ( -8192..8191 -- )` (two semitones either
way), `fm-reset`, `fm-active ( -- n )` (voices sounding).

MIDI comes from the ALSA sequencer (`libasound.so.2`): `midi-open ( -- f )` opens a
client named lugforth with one input port, `midi-client ( -- n )` is its number
(`aconnect 20:0 128:0` or `aplaymidi -p 128:0 file.mid` connect to it), and
`midi-connect-all ( -- n )` connects every readable port of the other clients, so a
USB keyboard works (call it again after plugging one). `midi-poll` reads what arrived
and plays it: call it once per frame. `midi-raw ( a n -- f )` reads raw MIDI bytes
from a device or a fifo instead (`/dev/snd/midiC1D0`). Your user must be allowed to
open `/dev/snd/seq` (the `audio` group on Debian and Ubuntu); `midi-open` answers
false otherwise, and the ALSA library prints its own message. Understood: note on
and off (velocity 0 too), pitch bend, CC 1 (sets the index), CC 7 (volume), CC 120 and
123 (all notes off), running status; SysEx and real time bytes are skipped, MIDI
channel 10 (drums) is ignored. To react to the messages yourself, `' word to midi-hook`
with `word ( status d1 d2 -- )`; `midi-feed ( byte -- )` takes the bytes of any MIDI
stream (a file, a test) one at a time.

`latency` (value, samples kept queued by `flip`, 2205 by default) can be lowered to
about 1500 for a more reactive instrument, as the example does. The two sides of a note
differ a little between Linux and the browser: the browser applies pitch bend to the
frequencies of the modulator and carrier together, and keeps the modulation depth fixed
in Hz, so a strong bend changes the index slightly. There is no operator feedback.


## Reference

Include `engine_posix.fs` and type `sdl2-fm` once. Two operator FM (the phase of a carrier is bent by a modulator, both with envelopes), 8 voices computed in Forth and mixed into the queue of `drum-hit`. Patch values are read when a note starts (`fm-release` when it ends). MIDI input uses the ALSA sequencer. The same words exist in the web build, played by WebAudio and Web MIDI.

| Word | Stack | What it does |
|------|-------|--------------|
| `fm-on` | `( note vel -- )` | Start a note (MIDI key 0 to 127, velocity 1 to 127). The oldest voice is taken when all 8 are busy; the same note again restarts its voice. |
| `fm-off` | `( note -- )` | Release a note (it fades for `fm-release` ms). |
| `fm-all-off` | `( -- )` | Release every note. |
| `fm-bend` | `( n -- )` | Pitch bend, -8192 to 8191, two semitones either way. |
| `fm-reset` | `( -- )` | Silence every voice at once and reset the bend. |
| `fm-active` | `( -- n )` | Number of voices sounding (released ones count until they fade). |
| `fm-ratio` | `( -- n )` | Value, 200. Modulator frequency over carrier frequency, times 100. Whole ratios give a pitched sound, others a bell. |
| `fm-index` | `( -- n )` | Value, 30. Modulation index in radians, times 10. Scaled by the velocity (half to full). |
| `fm-attack` `fm-decay` `fm-release` | `( -- n )` | Values, 5 400 250. Milliseconds. Decay is the time to settle at the sustain level. |
| `fm-sustain` | `( -- n )` | Value, 30. Percent of the peak level kept while the note is held. |
| `fm-mdecay` `fm-msus` | `( -- n )` | Values, 300 and 20. Milliseconds for the modulation index to fall to `fm-msus` percent of its start. |
| `fm-volume` | `( -- n )` | Value, 70. Percent. |
| `midi-open` | `( -- f )` | Open the ALSA sequencer and an input port named lugforth. False when it cannot be opened (no permission on `/dev/snd/seq`: join the `audio` group). |
| `midi-close` | `( -- )` | Close it, and the raw device. |
| `midi-client` | `( -- n )` | Client number of the port, for `aconnect` and `aplaymidi -p n:0`. |
| `midi-connect-all` | `( -- n )` | Connect every readable port of the other clients (keyboards, sequencers) to the input port, and give how many were connected. |
| `midi-raw` | `( a n -- f )` | Read raw MIDI bytes from a device or a fifo, such as `/dev/snd/midiC1D0`, instead. |
| `midi-in?` | `( -- f )` | True when a MIDI input is open. |
| `midi-poll` | `( -- )` | Play the MIDI messages that arrived. Call it once per frame. |
| `midi-feed` | `( byte -- )` | The next byte of a MIDI stream: running status, note on with velocity 0, SysEx, real time bytes and system messages are handled. |
| `midi-reset` | `( -- )` | Forget a half received message. |
| `midi-hook` | `( -- xt )` | Value. Run as `( status d1 d2 -- )` for each complete message; `fm-midi` by default. |
| `fm-midi` | `( status d1 d2 -- )` | Play one message: note on and off, pitch bend, CC 1 (index), CC 7 (volume), CC 120 and 123 (all notes off). Channel 10 notes are ignored. |

### Internal words (synthesizer)

| Word | Stack | What it does |
|------|-------|--------------|
| `fm-render` | `( n -- )` | The `render-hook`: add n samples of the voices to `mix`. |
| `fm-active?` | `( -- f )` | The `active-hook`: true while a voice sounds. |
| `fvoice` | `( i -- a )` | Address of voice i, 17 cells (`fv-stage fv-note fv-phc fv-phm fv-inc fv-ratio fv-ea fv-em fv-amp fv-idx fv-astep fv-kd fv-sus fv-kr fv-kmd fv-msus fv-age`). |
| `fv-sine` | `( -- a )` | Table of 4096 cells, one sine period, 16 bit. |
| `fv-incs` | `( -- a )` | Phase increment of each MIDI key (24 bit phase, one cycle is 16777216). |
| `fv-block` | `( -- n )` | 32. Samples between two envelope updates. |
| `fv-k` | `( ms -- k )` | Share of the way to the target made per block (16 bits) for a time of ms. |
| `snd_seq_open` `snd_seq_close` `snd_seq_set_client_name` `snd_seq_client_id` `snd_seq_create_simple_port` `snd_seq_event_input` `snd_seq_connect_from` | | Raw ALSA calls (`asound` is the library), with `snd_seq_client_info_*`, `snd_seq_port_info_*` and `snd_seq_query_next_*` for `midi-connect-all`. |

## In the browser

The web engine has the same words as on
  Linux: `fm-on fm-off fm-all-off fm-bend fm-reset fm-active`, the patch values `fm-ratio
  fm-index fm-attack fm-decay fm-sustain fm-release fm-mdecay fm-msus fm-volume`, and
  `midi-open midi-close midi-poll midi-in? midi-connect-all midi-feed midi-hook`. A
  modulator oscillator feeds the frequency of a carrier through a gain, so WebAudio
  does the work and the spectrum is the same as the phase modulation of the Linux build.
  MIDI uses the Web MIDI API (Chrome, Edge, Opera, Firefox with its add-on; not Safari),
  on https or localhost: the browser asks first, and `midi-open` only starts the request
  (`midi-in?` turns true once an input is there). MIDI messages do not count as a user
  gesture, so the sound still waits for a click or a key press on the page. For tests,
  `window.lugforth.sdl.midiIn([0x90, 60, 100])` pushes MIDI bytes as if a keyboard sent them
  (the page has to expose `lugforth` as `sdl2_fm.html` does). `midi-client` answers -1,
  `midi-raw` false, and `latency` exists but does nothing.
