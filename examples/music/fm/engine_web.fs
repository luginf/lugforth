\ Copyright 2026 Luginfo10
\
\ Licensed under the Apache License, Version 2.0 (the "License");
\ you may not use this file except in compliance with the License.
\ You may obtain a copy of the License at
\
\     http://www.apache.org/licenses/LICENSE-2.0
\
\ Unless required by applicable law or agreed to in writing, software
\ distributed under the License is distributed on an "AS IS" BASIS,
\ WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
\ See the License for the specific language governing permissions and
\ limitations under the License.

( A 2 operator FM synthesizer with MIDI input, played with WebAudio )
\
\ Not part of the interpreter: the page loads midi_parser.fs, then this file, then the
\ program, which types sdl2-fm.
\
\ The same words as engine_posix.fs. A modulator oscillator feeds, through a
\ gain, the frequency of a carrier oscillator: that is FM with the same spectrum as the
\ phase modulation computed in Forth on Linux. Envelopes are WebAudio automation.
\ MIDI comes from the Web MIDI API (Chrome, Edge, Opera, and Firefox with its add-on;
\ not Safari), on https or localhost. The browser may ask for permission, and keeps the
\ AudioContext suspended until a click or a key press: MIDI messages do not count.
\ NOTE: the block below is a string, so it must not contain a vertical bar, in Forth or
\ in the JavaScript (write a ternary instead of a logical or). A tilde ends each
\ JavaScript part.

also internals
\ sdl2 is looked up when this runs, by evaluate: written directly it would be the
\ loader that existed when this was compiled, which would load everything again.
: sdl2-fm
  s" sdl2" evaluate
  r|
sdl2 definitions also web

200 value fm-ratio     ( modulator frequency / carrier frequency, times 100 )
30 value fm-index      ( modulation index, times 10: 30 is 3.0 radians )
5 value fm-attack      ( milliseconds )
400 value fm-decay     ( milliseconds to settle at the sustain level )
30 value fm-sustain    ( percent of the peak level )
250 value fm-release   ( milliseconds )
300 value fm-mdecay    ( milliseconds for the modulation to fall to fm-msus )
20 value fm-msus       ( percent of the index kept while the note is held )
70 value fm-volume     ( percent )

JSWORD: js-fm-setup { }
  var S = context.sdl;
  S.fmVoices = []; S.fmBend = 0; S.midiQ = []; S.midiAccess = null;
  S.midiInputs = 0; S.midiAsked = 0;
  S.fmFreq = function(v) {
    return v.fc * Math.pow(2, S.fmBend * 2 / 12);
  };
  S.fmRel = function(v, r, t) {
    if (v.released) { return; }
    v.released = true;
    var tau = Math.max(r, 1) / 4000;
    var gs = [v.gC.gain, v.gM.gain];
    for (var i = 0; i < 2; ++i) {
      if (gs[i].cancelAndHoldAtTime) { gs[i].cancelAndHoldAtTime(t); }
      else { gs[i].cancelScheduledValues(t); }
      gs[i].setTargetAtTime(0, t, tau);
    }
    var end = t + tau * 8 + 0.05;
    try { v.oscC.stop(end); v.oscM.stop(end); } catch (e) {}
  };
  S.fmDone = function(v) {
    var k = S.fmVoices.indexOf(v);
    if (k >= 0) { S.fmVoices.splice(k, 1); }
    try { v.oscC.disconnect(); v.oscM.disconnect(); v.gC.disconnect(); v.gM.disconnect(); } catch (e) {}
  };
  S.midiAttach = function() {
    var n = 0;
    S.midiAccess.inputs.forEach(function(inp) {
      inp.onmidimessage = function(e) {
        for (var i = 0; i < e.data.length; ++i) {
          if (S.midiQ.length < 4096) { S.midiQ.push(e.data[i]); }
        }
      };
      n = n + 1;
    });
    S.midiInputs = n;
  };
  S.midiIn = function(bytes) {
    for (var i = 0; i < bytes.length; ++i) { S.midiQ.push(bytes[i]); }
  };
~
js-fm-setup

JSWORD: js-fm-on { note vel ratio idx a d sus md msus vol }
  var S = context.sdl;
  S.ensureAudio();
  var ac = S.ac;
  if (!ac) { return; }
  if (ac.state !== 'running') { return; }
  var t = ac.currentTime;
  for (var i = 0; i < S.fmVoices.length; ++i) {
    var o = S.fmVoices[i];
    if (o.note === note) { S.fmRel(o, 15, t); }
  }
  var live = S.fmVoices.filter(function(x) { return !x.released; });
  if (live.length >= 8) { S.fmRel(live[0], 15, t); }
  if (!S.fmOut) { S.fmOut = ac.createGain(); S.fmOut.connect(ac.destination); }
  var v = { note: note, released: false };
  v.fc = 440 * Math.pow(2, (note - 69) / 12);
  var fc = S.fmFreq(v);
  var beta = idx / 10 * (vel + 127) / 254;
  var peakM = beta * fc * ratio / 100;
  var amp = vol / 100 * vel / 127 / 3;
  v.oscC = ac.createOscillator(); v.oscM = ac.createOscillator();
  v.gC = ac.createGain(); v.gM = ac.createGain();
  v.oscC.frequency.value = fc; v.oscM.frequency.value = fc * ratio / 100;
  v.ratio = ratio;
  v.oscM.connect(v.gM); v.gM.connect(v.oscC.frequency);
  v.oscC.connect(v.gC); v.gC.connect(S.fmOut);
  var ta = t + a / 1000;
  var gm = v.gM.gain; var gc = v.gC.gain;
  gm.setValueAtTime(peakM, t);
  if (md > 0) { gm.setTargetAtTime(peakM * msus / 100, t, md / 4000); }
  else { gm.setValueAtTime(peakM * msus / 100, t); }
  if (a > 0) { gc.setValueAtTime(0, t); gc.linearRampToValueAtTime(amp, ta); }
  else { gc.setValueAtTime(amp, t); }
  if (d > 0) { gc.setTargetAtTime(amp * sus / 100, ta, d / 4000); }
  else { gc.setValueAtTime(amp * sus / 100, ta); }
  v.oscC.onended = function() { S.fmDone(v); };
  v.oscC.start(t); v.oscM.start(t);
  S.fmVoices.push(v);
~
: fm-on ( note vel -- ) fm-ratio fm-index fm-attack fm-decay fm-sustain fm-mdecay fm-msus fm-volume js-fm-on ;

JSWORD: js-fm-off { note r }
  var S = context.sdl;
  if (!S.ac) { return; }
  var t = S.ac.currentTime;
  for (var i = 0; i < S.fmVoices.length; ++i) {
    var o = S.fmVoices[i];
    if (o.note === note) { S.fmRel(o, r, t); }
  }
~
: fm-off ( note -- ) fm-release js-fm-off ;

JSWORD: fm-all-off { }
  var S = context.sdl;
  if (!S.ac) { return; }
  var t = S.ac.currentTime;
  for (var i = 0; i < S.fmVoices.length; ++i) { S.fmRel(S.fmVoices[i], 100, t); }
~
JSWORD: fm-reset { }
  var S = context.sdl;
  if (!S.fmVoices) { return; }
  for (var i = 0; i < S.fmVoices.length; ++i) {
    var v = S.fmVoices[i];
    try { v.oscC.stop(); v.oscM.stop(); } catch (e) {}
  }
  S.fmVoices = []; S.fmBend = 0;
~
JSWORD: fm-bend { n }
  var S = context.sdl;
  S.fmBend = n / 8192;
  if (!S.ac) { return; }
  var t = S.ac.currentTime;
  for (var i = 0; i < S.fmVoices.length; ++i) {
    var v = S.fmVoices[i];
    var f = S.fmFreq(v);
    v.oscC.frequency.setTargetAtTime(f, t, 0.005);
    v.oscM.frequency.setTargetAtTime(f * v.ratio / 100, t, 0.005);
  }
~
JSWORD: fm-active { -- n }
  var S = context.sdl;
  return S.fmVoices ? S.fmVoices.length : 0;
~

( ---- MIDI input from the Web MIDI API ---- )
JSWORD: midi-open { -- f }
  var S = context.sdl;
  if (!navigator.requestMIDIAccess) { return 0; }
  if (S.midiAsked) { return -1; }
  S.midiAsked = 1;
  navigator.requestMIDIAccess().then(function(a) {
    S.midiAccess = a;
    S.midiAttach();
    a.onstatechange = function() { S.midiAttach(); };
  }, function() { S.midiAsked = 0; });
  return -1;
~
JSWORD: midi-close { }
  var S = context.sdl;
  if (S.midiAccess) {
    S.midiAccess.inputs.forEach(function(inp) { inp.onmidimessage = null; });
    S.midiAccess.onstatechange = null;
  }
  S.midiAccess = null; S.midiAsked = 0; S.midiInputs = 0;
~
JSWORD: midi-inputs { -- n }
  return context.sdl.midiInputs;
~
JSWORD: js-midi-byte { -- n }
  var q = context.sdl.midiQ;
  return q.length > 0 ? q.shift() : -1;
~
: midi-in? ( -- f ) midi-inputs 0<> ;
: midi-connect-all ( -- n ) midi-inputs ;   ( every input is listened to at once )
: midi-client ( -- n ) -1 ;
: midi-raw ( a n -- f ) 2drop 0 ;   ( no raw device in a browser )
|  evaluate
  fm-midi-parser evaluate
  r|
sdl2 definitions
: midi-poll ( -- )   ( plays the MIDI messages that arrived; call it once per frame )
  begin js-midi-byte dup 0< 0= while midi-feed repeat drop ;
previous
| evaluate ;
previous
