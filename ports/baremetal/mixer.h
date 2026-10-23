// Copyright 2026 Luginfo10
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

// The sound mixer of the bare-metal ports, shared by ports/rpi (sound.h) and
// ports/efi (hda.h): 44100 Hz mono samples as in ports/posix/sdl2.fs. Beeps play
// one after the other; hits (drum-hit) play at once over them, 8 at a time,
// fading out, their pitch falling by pitch-drop percent; a tune (sdl2-abc) is a
// list of notes played by simple oscillators. The port's output calls
// snd_next_stereo() for each frame and snd_pump() fills its buffers.

#define SND_RATE 44100

// ---- the mixer ----
static uint32_t snd_seed = 1;

static int snd_sine[256];
static int snd_wave(int wave, uint32_t phase) {   // phase 0..65535 -> -32768..32767
  switch (wave) {
    case 4:
      if (!snd_sine[64]) for (int i = 0; i < 256; i++) snd_sine[i] = (int) (sin(i * 6.283185307179586 / 256) * 32767);
      return snd_sine[(phase >> 8) & 255];
    case 1: return phase < 32768 ? (int) phase * 2 - 32768 : (65535 - (int) phase) * 2 - 32768;
    case 2: return (int) phase - 32768;
    case 3: snd_seed = snd_seed * 1103515245u + 12345u; return (int) ((snd_seed >> 16) & 0xFFFF) - 32768;
  }
  return phase < 32768 ? 32767 : -32768;
}

// Beeps: SND_CH channels (the voices of play, channel 0 for beep), each a queue played in order.
#define SND_BEEPS 512
#define SND_CH 4
static struct { uint32_t freq, left, total; int wave, vol, env, a, d, s, r; } snd_beeps[SND_CH][SND_BEEPS];
static unsigned snd_beep_head[SND_CH], snd_beep_tail[SND_CH];
static uint32_t snd_beep_phase[SND_CH];
static struct { int on; cell_t a, d, s, r; } snd_env;   // ADSR in ms and percent; off by default

#define SND_HITS 8
static struct { uint32_t phase, freq, left, total; int wave, drop, vol; } snd_hits[SND_HITS];

#define SND_TUNE_MAX 1024
#define SND_TUNE_VOICES 16
static struct { uint32_t start, len, freq; } snd_tune[SND_TUNE_MAX];
static int snd_tune_n, snd_tune_next, snd_tune_wave = 1, snd_tune_vol = 2500, snd_tune_loop;
static uint32_t snd_tune_total, snd_tune_pos;
static int snd_tune_on;
static struct { uint32_t phase, freq, left, total; } snd_voices[SND_TUNE_VOICES];

static void snd_wake(void);          // the port starts its output at the first use (ports/efi/hda.h), or not at all

static uint32_t snd_ms(cell_t ms) { return ms <= 0 ? 0 : (uint32_t) ((uint64_t) ms * SND_RATE / 1000); }

static void snd_adsr(cell_t a, cell_t d, cell_t s, cell_t r, cell_t on) {
  snd_env.on = on != 0; snd_env.a = a; snd_env.d = d;
  snd_env.s = s < 0 ? 0 : s > 100 ? 100 : s; snd_env.r = r;
}

static void snd_vbeep(cell_t freq, cell_t ms, cell_t wave, cell_t vol, cell_t ch) {
  snd_wake();
  if (ch < 0 || ch >= SND_CH) return;
  if (snd_beep_head[ch] - snd_beep_tail[ch] >= SND_BEEPS || ms <= 0) return;
  unsigned i = snd_beep_head[ch] % SND_BEEPS;
  snd_beeps[ch][i].freq = freq > 0 ? (uint32_t) freq : 0;
  snd_beeps[ch][i].left = snd_beeps[ch][i].total = snd_ms(ms);
  snd_beeps[ch][i].wave = (int) wave; snd_beeps[ch][i].vol = (int) vol;
  snd_beeps[ch][i].env = snd_env.on && freq > 0;
  snd_beeps[ch][i].a = (int) snd_ms(snd_env.a); snd_beeps[ch][i].d = (int) snd_ms(snd_env.d);
  snd_beeps[ch][i].s = (int) snd_env.s * 10;     snd_beeps[ch][i].r = (int) snd_ms(snd_env.r);
  snd_beep_head[ch]++;
}

static void snd_beep(cell_t freq, cell_t ms, cell_t wave, cell_t vol) { snd_vbeep(freq, ms, wave, vol, 0); }

// Pads the shorter channels with a rest so that the next notes of every channel start together.
static void snd_sync(void) {
  uint64_t pend[SND_CH], most = 0;
  for (int c = 0; c < SND_CH; c++) {
    pend[c] = 0;
    for (unsigned k = snd_beep_tail[c]; k != snd_beep_head[c]; k++) pend[c] += snd_beeps[c][k % SND_BEEPS].left;
    if (pend[c] > most) most = pend[c];
  }
  for (int c = 0; c < SND_CH; c++) {
    if (pend[c] >= most || snd_beep_head[c] - snd_beep_tail[c] >= SND_BEEPS) continue;
    unsigned i = snd_beep_head[c] % SND_BEEPS;
    snd_beeps[c][i].freq = 0; snd_beeps[c][i].env = 0; snd_beeps[c][i].wave = 0; snd_beeps[c][i].vol = 0;
    snd_beeps[c][i].left = snd_beeps[c][i].total = (uint32_t) (most - pend[c]);
    snd_beep_head[c]++;
  }
}

static void snd_hit(cell_t freq, cell_t ms, cell_t wave, cell_t vol, cell_t drop) {
  snd_wake();
  int k = 0;
  for (int i = 0; i < SND_HITS; i++) {      // a free voice, or the oldest
    if (!snd_hits[i].left) { k = i; break; }
    if (snd_hits[i].left < snd_hits[k].left) k = i;
  }
  snd_hits[k].phase = 0; snd_hits[k].freq = freq > 0 ? (uint32_t) freq : 0;
  snd_hits[k].left = snd_hits[k].total = snd_ms(ms);
  snd_hits[k].wave = (int) wave; snd_hits[k].vol = (int) vol;
  snd_hits[k].drop = drop < 0 ? 0 : drop > 100 ? 100 : (int) drop;
}

static void snd_silence(void) {
  for (int c = 0; c < SND_CH; c++) snd_beep_tail[c] = snd_beep_head[c];
  for (int i = 0; i < SND_HITS; i++) snd_hits[i].left = 0;
}

static void snd_tune_clear(void) { snd_tune_n = 0; }

static void snd_tune_add(cell_t key, cell_t at, cell_t ms) {
  if (snd_tune_n >= SND_TUNE_MAX) return;
  snd_tune[snd_tune_n].start = snd_ms(at);
  snd_tune[snd_tune_n].len = snd_ms(ms);
  snd_tune[snd_tune_n].freq = (uint32_t) (440.0 * pow(2.0, (double) (key - 69) / 12.0) + 0.5);
  snd_tune_n++;
}

static void snd_tune_stop(void) {
  snd_tune_on = 0;
  for (int i = 0; i < SND_TUNE_VOICES; i++) snd_voices[i].left = 0;
}

static void snd_tune_run(cell_t loop, cell_t total, cell_t wave, cell_t vol) {
  snd_wake();
  snd_tune_stop();
  snd_tune_loop = loop != 0; snd_tune_total = snd_ms(total);
  snd_tune_wave = (int) wave; snd_tune_vol = (int) vol;
  snd_tune_pos = 0; snd_tune_next = 0; snd_tune_on = snd_tune_n > 0;
}

static int snd_voices_busy(void) {
  for (int i = 0; i < SND_TUNE_VOICES; i++) if (snd_voices[i].left) return 1;
  return 0;
}

static cell_t snd_tune_playing(void) { return snd_tune_on || snd_voices_busy() ? -1 : 0; }

static cell_t snd_busy(void) {
  for (int c = 0; c < SND_CH; c++) if (snd_beep_head[c] != snd_beep_tail[c]) return -1;
  for (int i = 0; i < SND_HITS; i++) if (snd_hits[i].left) return -1;
  return 0;
}

// Level of an ADSR note in thousandths: the release is the end of the note.
static int snd_env_ad(uint32_t i, int a, int d, int s) {
  if (i < (uint32_t) a) return (int) ((uint64_t) i * 1000 / a);
  if (i < (uint32_t) a + d) return 1000 - (int) ((uint64_t) (1000 - s) * (i - a) / d);
  return s;
}
#define SND_ENV_T __typeof__(snd_beeps[0][0])
static int snd_env_gain(const SND_ENV_T *e) {
  uint32_t i = e->total - e->left;
  uint32_t rs = e->total > (uint32_t) e->r ? e->total - e->r : 0;
  if (i < rs) return snd_env_ad(i, e->a, e->d, e->s);
  return (int) ((uint64_t) snd_env_ad(rs, e->a, e->d, e->s) * (e->total - i) / (e->total - rs));
}

__attribute__((unused)) static int16_t snd_next(void) {   // unused on the Pi 4 (no output yet)
  int32_t s = 0;
  for (int c = 0; c < SND_CH; c++) {
    while (snd_beep_head[c] != snd_beep_tail[c] && !snd_beeps[c][snd_beep_tail[c] % SND_BEEPS].left) {
      snd_beep_tail[c]++; snd_beep_phase[c] = 0;
    }
    if (snd_beep_head[c] == snd_beep_tail[c]) continue;
    unsigned i = snd_beep_tail[c] % SND_BEEPS;
    if (snd_beeps[c][i].freq) {
      int32_t v = snd_wave(snd_beeps[c][i].wave, snd_beep_phase[c] >> 16) * snd_beeps[c][i].vol >> 15;
      if (snd_beeps[c][i].env) v = (int32_t) ((int64_t) v * snd_env_gain(&snd_beeps[c][i]) / 1000);
      s += v;
    }
    snd_beep_phase[c] += (uint32_t) ((uint64_t) snd_beeps[c][i].freq * 65536 * 65536 / SND_RATE);
    snd_beeps[c][i].left--;
  }
  for (int i = 0; i < SND_HITS; i++) {
    if (!snd_hits[i].left) continue;
    uint32_t left = snd_hits[i].left, total = snd_hits[i].total;
    int32_t v = (int32_t) ((int64_t) snd_wave(snd_hits[i].wave, snd_hits[i].phase >> 16) *
                           snd_hits[i].vol >> 15) * (int64_t) left / total;
    s += v;
    uint64_t f = snd_hits[i].freq * (uint64_t) (100 * total - snd_hits[i].drop * (total - left)) / (100 * total);
    snd_hits[i].phase += (uint32_t) (f * 65536 * 65536 / SND_RATE);
    snd_hits[i].left--;
  }
  if (snd_tune_on) {
    while (snd_tune_next < snd_tune_n && snd_tune[snd_tune_next].start <= snd_tune_pos) {
      for (int v = 0; v < SND_TUNE_VOICES; v++) {
        if (snd_voices[v].left) continue;
        snd_voices[v].phase = 0; snd_voices[v].freq = snd_tune[snd_tune_next].freq;
        snd_voices[v].left = snd_voices[v].total = snd_tune[snd_tune_next].len;
        break;
      }
      snd_tune_next++;
    }
    if (++snd_tune_pos >= snd_tune_total) {
      snd_tune_pos = 0; snd_tune_next = 0;
      if (!snd_tune_loop) snd_tune_on = 0;
    }
  }
  for (int v = 0; v < SND_TUNE_VOICES; v++) {
    if (!snd_voices[v].left) continue;
    uint32_t done = snd_voices[v].total - snd_voices[v].left, attack = SND_RATE / 100;
    int64_t env = done < attack ? (int64_t) done * 1024 / attack
                                : (int64_t) snd_voices[v].left * 1024 / (snd_voices[v].total - attack + 1);
    s += (int32_t) ((int64_t) snd_wave(snd_tune_wave, snd_voices[v].phase >> 16) * snd_tune_vol * env >> 25);
    snd_voices[v].phase += (uint32_t) ((uint64_t) snd_voices[v].freq * 65536 * 65536 / SND_RATE);
    snd_voices[v].left--;
  }
  if (s > 32767) s = 32767;
  if (s < -32768) s = -32768;
  return (int16_t) s;
}

// A queue of stereo frames played by the output, for programs that make their
// own samples (the varvara port): frames are int16 left, right, 44100 Hz. The
// mixer's mono sound is added to it.
#define SND_PCM_FRAMES 16384
static int16_t snd_pcm[SND_PCM_FRAMES][2];
static unsigned snd_pcm_head, snd_pcm_tail;

static cell_t snd_pcm_queued(void) { return (cell_t) (snd_pcm_head - snd_pcm_tail); }

static void snd_pcm_put(const int16_t *lr, cell_t frames) {
  snd_wake();
  for (cell_t i = 0; i < frames && snd_pcm_head - snd_pcm_tail < SND_PCM_FRAMES; i++, lr += 2) {
    snd_pcm[snd_pcm_head % SND_PCM_FRAMES][0] = lr[0];
    snd_pcm[snd_pcm_head % SND_PCM_FRAMES][1] = lr[1];
    snd_pcm_head++;
  }
}

static void snd_clamp_add(int16_t *dst, int32_t v) {
  v += *dst;
  *dst = (int16_t) (v > 32767 ? 32767 : v < -32768 ? -32768 : v);
}

// The next stereo frame: the mixer's sound on both sides, then the queue's.
__attribute__((unused)) static void snd_next_stereo(int16_t *l, int16_t *r) {
  *l = *r = snd_next();
  if (snd_pcm_head != snd_pcm_tail) {
    snd_clamp_add(l, snd_pcm[snd_pcm_tail % SND_PCM_FRAMES][0]);
    snd_clamp_add(r, snd_pcm[snd_pcm_tail % SND_PCM_FRAMES][1]);
    snd_pcm_tail++;
  }
}

static int snd_ready;   // an output works
