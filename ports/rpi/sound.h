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

// Sound for the sdl2 words: a mixer of 44100 Hz mono samples, as in
// ports/posix/sdl2.fs. Beeps play one after the other; hits (drum-hit) play
// at once over them, 8 at a time, fading out, their pitch falling by
// pitch-drop percent; a tune (sdl2-abc) is a list of notes played by
// simple oscillators, as on the web. Outputs, fed by snd_pump(), which runs
// at every pause (RAW-YIELD) and while waiting for a key:
// - QEMU virt: virtio-sound (-device virtio-sound-device,audiodev=...),
//   16-bit stereo, 8 periods of 10 ms in flight.
// - Pi 3: the PWM of the headphone jack (GPIO 40 and 41), fed by DMA from a
//   ring of samples. WRITTEN WITHOUT A BOARD: QEMU does not emulate the PWM
//   (snd_init sees it and stays silent there).
// Without either, the words are silent.

#define SND_RATE 44100

// ---- the mixer ----
static uint32_t snd_seed = 1;

static int snd_wave(int wave, uint32_t phase) {   // phase 0..65535 -> -32768..32767
  switch (wave) {
    case 1: return phase < 32768 ? (int) phase * 2 - 32768 : (65535 - (int) phase) * 2 - 32768;
    case 2: return (int) phase - 32768;
    case 3: snd_seed = snd_seed * 1103515245u + 12345u; return (int) ((snd_seed >> 16) & 0xFFFF) - 32768;
  }
  return phase < 32768 ? 32767 : -32768;
}

#define SND_BEEPS 128
static struct { uint32_t freq, left; int wave, vol; } snd_beeps[SND_BEEPS];
static unsigned snd_beep_head, snd_beep_tail;
static uint32_t snd_beep_phase;

#define SND_HITS 8
static struct { uint32_t phase, freq, left, total; int wave, drop, vol; } snd_hits[SND_HITS];

#define SND_TUNE_MAX 1024
#define SND_TUNE_VOICES 16
static struct { uint32_t start, len, freq; } snd_tune[SND_TUNE_MAX];
static int snd_tune_n, snd_tune_next, snd_tune_wave = 1, snd_tune_vol = 2500, snd_tune_loop;
static uint32_t snd_tune_total, snd_tune_pos;
static int snd_tune_on;
static struct { uint32_t phase, freq, left, total; } snd_voices[SND_TUNE_VOICES];

static uint32_t snd_ms(cell_t ms) { return ms <= 0 ? 0 : (uint32_t) ((uint64_t) ms * SND_RATE / 1000); }

static void snd_beep(cell_t freq, cell_t ms, cell_t wave, cell_t vol) {
  if (snd_beep_head - snd_beep_tail >= SND_BEEPS || ms <= 0) return;
  unsigned i = snd_beep_head % SND_BEEPS;
  snd_beeps[i].freq = freq > 0 ? (uint32_t) freq : 0;
  snd_beeps[i].left = snd_ms(ms);
  snd_beeps[i].wave = (int) wave; snd_beeps[i].vol = (int) vol;
  snd_beep_head++;
}

static void snd_hit(cell_t freq, cell_t ms, cell_t wave, cell_t vol, cell_t drop) {
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
  snd_beep_tail = snd_beep_head;
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
  if (snd_beep_head != snd_beep_tail) return -1;
  for (int i = 0; i < SND_HITS; i++) if (snd_hits[i].left) return -1;
  return 0;
}

__attribute__((unused)) static int16_t snd_next(void) {   // unused on the Pi 4 (no output yet)
  int32_t s = 0;
  while (snd_beep_head != snd_beep_tail && !snd_beeps[snd_beep_tail % SND_BEEPS].left) {
    snd_beep_tail++; snd_beep_phase = 0;
  }
  if (snd_beep_head != snd_beep_tail) {
    unsigned i = snd_beep_tail % SND_BEEPS;
    if (snd_beeps[i].freq) s += snd_wave(snd_beeps[i].wave, snd_beep_phase >> 16) * snd_beeps[i].vol >> 15;
    snd_beep_phase += (uint32_t) ((uint64_t) snd_beeps[i].freq * 65536 * 65536 / SND_RATE);
    snd_beeps[i].left--;
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

// ---- QEMU virt: virtio-sound ----
#ifdef RPI_BOARD_VIRT
# define SND_PERIOD 441                 // frames, 10 ms
# define SND_SLOTS 8
static struct virtq snd_ctlq, snd_evtq, snd_txq;
static int snd_channels;
static struct snd_slot {
  uint32_t stream;                      // virtio_snd_pcm_xfer
  int16_t data[SND_PERIOD * 2];
  uint32_t status[2];                   // virtio_snd_pcm_status
} *snd_slots;

// One control request (the request, then the response). 0: VIRTIO_SND_S_OK.
static int snd_control(void *req, uint32_t req_len, void *resp, uint32_t resp_len) {
  snd_ctlq.desc[0].addr = (uint64_t) req; snd_ctlq.desc[0].len = req_len;
  snd_ctlq.desc[0].flags = VQ_NEXT; snd_ctlq.desc[0].next = 1;
  snd_ctlq.desc[1].addr = (uint64_t) resp; snd_ctlq.desc[1].len = resp_len;
  snd_ctlq.desc[1].flags = VQ_WRITE; snd_ctlq.desc[1].next = 0;
  virtq_offer(&snd_ctlq, 0);
  virtq_notify(&snd_ctlq);
  uint64_t end = us_ticks() + 500000;
  while (virtq_take(&snd_ctlq, 0) < 0) { if (us_ticks() > end) return -1; }
  vio_barrier();
  return *(volatile uint32_t *) resp == 0x8000 ? 0 : -1;
}

static void snd_fill_slot(int k) {
  struct snd_slot *s = &snd_slots[k];
  for (int f = 0; f < SND_PERIOD; f++) {
    int16_t l, r;
    snd_next_stereo(&l, &r);
    if (snd_channels == 2) { s->data[f * 2] = l; s->data[f * 2 + 1] = r; }
    else s->data[f] = (int16_t) (((int32_t) l + r) / 2);
  }
  uint16_t d = (uint16_t) (3 * k);
  snd_txq.desc[d].addr = (uint64_t) &s->stream; snd_txq.desc[d].len = 4;
  snd_txq.desc[d].flags = VQ_NEXT; snd_txq.desc[d].next = (uint16_t) (d + 1);
  snd_txq.desc[d + 1].addr = (uint64_t) s->data; snd_txq.desc[d + 1].len = SND_PERIOD * 2 * snd_channels;
  snd_txq.desc[d + 1].flags = VQ_NEXT; snd_txq.desc[d + 1].next = (uint16_t) (d + 2);
  snd_txq.desc[d + 2].addr = (uint64_t) s->status; snd_txq.desc[d + 2].len = 8;
  snd_txq.desc[d + 2].flags = VQ_WRITE; snd_txq.desc[d + 2].next = 0;
  virtq_offer(&snd_txq, d);
}

static void snd_init(void) {
  uint64_t b = virtio_find(25, 0);
  if (!b || virtio_begin(b)) return;
  if (virtq_init(&snd_ctlq, b, 0, 4) || virtq_init(&snd_evtq, b, 1, 4) ||
      virtq_init(&snd_txq, b, 2, 3 * SND_SLOTS)) return;
  virtio_ready(b);
  static uint32_t req[6], resp[1 + 8 * 4] __attribute__((aligned(16)));
  // PCM_INFO of stream 0: its channel range.
  req[0] = 0x0100; req[1] = 0; req[2] = 1; req[3] = 32;
  if (snd_control(req, 16, resp, 4 + 32)) return;
  uint8_t *info = (uint8_t *) &resp[1];
  if (info[24] != 0) return;                        // direction: output
  snd_channels = info[25] <= 2 && info[26] >= 2 ? 2 : info[25];
  if (snd_channels < 1 || snd_channels > 2) return;
  // SET_PARAMS: S16 (5), 44100 Hz (6), 8 periods.
  uint32_t period = SND_PERIOD * 2 * snd_channels;
  req[0] = 0x0101; req[1] = 0; req[2] = period * SND_SLOTS; req[3] = period; req[4] = 0;
  req[5] = (uint32_t) snd_channels | (5u << 8) | (6u << 16);
  if (snd_control(req, 24, resp, 4)) return;
  req[0] = 0x0102; req[1] = 0;                      // PREPARE
  if (snd_control(req, 8, resp, 4)) return;
  snd_slots = (struct snd_slot *) vio_alloc(sizeof(struct snd_slot) * SND_SLOTS);
  if (!snd_slots) return;
  for (int k = 0; k < SND_SLOTS; k++) { snd_slots[k].stream = 0; snd_fill_slot(k); }
  virtq_notify(&snd_txq);
  req[0] = 0x0104; req[1] = 0;                      // START
  if (snd_control(req, 8, resp, 4)) return;
  snd_ready = 1;
}

static void snd_pump(void) {
  if (!snd_ready) return;
  int id, any = 0;
  while ((id = virtq_take(&snd_txq, 0)) >= 0) {
    snd_fill_slot(id / 3);
    any = 1;
  }
  if (any) virtq_notify(&snd_txq);
}

// ---- Pi 3: PWM on the headphone jack, DMA channel 5 ----
// Two control blocks, each half of a ring of samples. The chain always ends:
// while the DMA plays one half, snd_pump fills the other and links it after
// it. (A chain looping on itself would be simpler, but QEMU runs a whole DMA
// chain at once, without the PWM's pacing, and would never come back.)
#elif defined(RPI_BOARD_PI3)
# define PWM_BASE (PERIPHERAL_BASE + 0x20C000UL)
# define CM_BASE (PERIPHERAL_BASE + 0x101000UL)
# define DMA5_BASE (PERIPHERAL_BASE + 0x7000UL + 5 * 0x100UL)
# define SND_HALF 2048                  // frames in each half
static uint32_t snd_range;
static uint32_t *snd_ring;              // left, right words, 0..range
static volatile struct { uint32_t ti, src, dst, len, stride, next, pad[2]; } *snd_cb;
static int snd_filled[2];

static uint32_t snd_bus(const volatile void *p) { return (uint32_t) (uint64_t) p | 0xC0000000u; }

static void snd_fill_half(int h) {
  uint32_t *w = snd_ring + h * SND_HALF * 2;
  for (int f = 0; f < SND_HALF; f++) {
    int16_t l, r;
    snd_next_stereo(&l, &r);
    w[2 * f] = (uint32_t) ((int32_t) l + 32768) * snd_range / 65536;
    w[2 * f + 1] = (uint32_t) ((int32_t) r + 32768) * snd_range / 65536;
  }
  dcache_clean(w, SND_HALF * 8);
  snd_cb[h].next = 0;
  dcache_clean((const void *) &snd_cb[h], 32);
  snd_filled[h] = 1;
}

static void snd_start(void) {
  snd_fill_half(0);
  snd_filled[1] = 0;
  REG(DMA5_BASE + 0x00) = 1u << 31;                          // reset
  REG(DMA5_BASE + 0x04) = snd_bus(&snd_cb[0]);               // CONBLK_AD
  REG(DMA5_BASE + 0x00) = 1u | (8u << 16) | (8u << 20);      // active, priorities
}

static void snd_init(void) {
  uint8_t *mem = (uint8_t *) malloc(SND_HALF * 2 * 8 + 2 * 32 + 64);
  if (!mem) return;
  snd_ring = (uint32_t *) (((uint64_t) mem + 31) & ~31UL);
  snd_cb = (void *) (snd_ring + SND_HALF * 4);
  // GPIO 40 and 41: ALT0, PWM0 and PWM1.
  uint32_t sel = REG(GPIO_BASE + 0x10);
  sel &= ~(7u | (7u << 3)); sel |= 4u | (4u << 3);
  REG(GPIO_BASE + 0x10) = sel;
  // PWM clock: PLLD (500 MHz) / 2.
  REG(CM_BASE + 0xA0) = 0x5A000000 | (1u << 5);              // CM_PWMCTL: kill
  for (int t = 0; t < 100000 && (REG(CM_BASE + 0xA0) & (1u << 7)); t++) {}   // busy
  REG(CM_BASE + 0xA4) = 0x5A000000 | (2u << 12);             // CM_PWMDIV: 2
  REG(CM_BASE + 0xA0) = 0x5A000000 | 6u | (1u << 4);         // PLLD, enable
  snd_range = 250000000u / SND_RATE;
  REG(PWM_BASE + 0x00) = 0;
  REG(PWM_BASE + 0x10) = snd_range;                          // RNG1
  REG(PWM_BASE + 0x20) = snd_range;                          // RNG2
  REG(PWM_BASE + 0x00) = (1u << 6);                          // clear the FIFO
  REG(PWM_BASE + 0x08) = (1u << 31) | (7u << 8) | 7u;        // DMAC: enable, thresholds
  REG(PWM_BASE + 0x00) = 1u | (1u << 5) | (1u << 8) | (1u << 13);   // both channels, FIFO
  for (int h = 0; h < 2; h++) {
    snd_cb[h].ti = (5u << 16) | (1u << 8) | (1u << 6) | (1u << 3);   // PWM DREQ, src inc, dest DREQ, wait
    snd_cb[h].src = snd_bus(snd_ring + h * SND_HALF * 2);
    snd_cb[h].dst = 0x7E20C018;                               // PWM FIF1, bus address
    snd_cb[h].len = SND_HALF * 8;
    snd_cb[h].stride = 0;
    snd_cb[h].next = 0;
  }
  REG(PERIPHERAL_BASE + 0x7FF0) |= 1u << 5;                  // DMA ENABLE: channel 5
  snd_start();
  // Done already? Nothing paces it (QEMU): stay silent rather than spin.
  if (!(REG(DMA5_BASE + 0x00) & 1)) return;
  snd_ready = 1;
}

static void snd_pump(void) {
  if (!snd_ready) return;
  if (!(REG(DMA5_BASE + 0x00) & 1)) { snd_start(); return; }  // ran dry: start again
  uint32_t at = REG(DMA5_BASE + 0x04);                        // the block being played
  int h = at == snd_bus(&snd_cb[1]) ? 1 : 0;
  if (!snd_filled[1 - h]) {                                   // fill the other, link it
    snd_fill_half(1 - h);
    snd_cb[h].next = snd_bus(&snd_cb[1 - h]);
    dcache_clean((const void *) &snd_cb[h], 32);
    snd_filled[h] = 0;                                        // h is being played: refill it next
  }
}

#else
static void snd_init(void) {}
static void snd_pump(void) {}
#endif

static cell_t snd_ok(void) { return snd_ready ? -1 : 0; }
