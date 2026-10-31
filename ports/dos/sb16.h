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

// The sound of DOS: a Sound Blaster 16 (DOSBox emulates one), found from the
// BLASTER variable (A220 I7 D1 H5 T6), playing the mixer of
// ports/baremetal/mixer.h: 44100 Hz, 16-bit stereo, from a ring in
// conventional memory that the 16-bit DMA channel reads again and again
// (auto-init). As ports/efi/hda.h, no interrupt: snd_pump, called at each
// pause and while waiting for a key, reads where the DMA is and fills the
// ring behind it. Started at the first sound word. Without a Sound Blaster 16
// (BLASTER missing, an older card), the sound words do nothing.

#include "ports/baremetal/mixer.h"

#define SB_RING 8192                        // frames in the ring (186 ms)
#define SB_AHEAD 4096                       // frames written ahead of the one playing
static int sb_base, sb_dma16;
static unsigned long sb_ring_lin;           // linear address of the ring (conventional memory)
static uint64_t sb_wr, sb_played, sb_clock;
static unsigned sb_last;

static int sb_dsp_write(int v) {
  for (int i = 0; i < 100000; i++) if (!(inportb(sb_base + 0xC) & 0x80)) { outportb(sb_base + 0xC, v); return 0; }
  return -1;
}

static int sb_dsp_read(void) {
  for (int i = 0; i < 100000; i++) if (inportb(sb_base + 0xE) & 0x80) return inportb(sb_base + 0xA);
  return -1;
}

static int sb_reset(void) {
  outportb(sb_base + 6, 1);
  uint64_t t = us_ticks();
  while (us_ticks() - t < 10) {}
  outportb(sb_base + 6, 0);
  return sb_dsp_read() == 0xAA ? 0 : -1;
}

// The DMA ports of the channels 5 to 7 (second controller, counts in words).
static const int sb_dma_addr[4] = { 0, 0xC4, 0xC8, 0xCC }, sb_dma_count[4] = { 0, 0xC6, 0xCA, 0xCE };
static const int sb_dma_page[4] = { 0, 0x8B, 0x89, 0x8A };

static unsigned sb_dma_pos(void) {          // the frame the DMA reads now
  int c = sb_dma16 & 3;
  outportb(0xD8, 0);                        // the flip-flop: low byte first
  unsigned lo = inportb(sb_dma_count[c]), hi = inportb(sb_dma_count[c]);
  unsigned left = ((hi << 8) | lo) + 1;     // words still to read in this turn
  unsigned words = SB_RING * 2;
  return (words - (left > words ? words : left)) / 2 % SB_RING;
}

static void sb_init(void) {
  const char *b = getenv("BLASTER");
  if (!b) return;
  for (const char *p = b; *p; p++) {
    if (*p == 'A' || *p == 'a') sb_base = (int) strtol(p + 1, 0, 16);
    if (*p == 'H' || *p == 'h') sb_dma16 = (int) strtol(p + 1, 0, 10);
  }
  if (!sb_base || sb_dma16 < 5 || sb_dma16 > 7 || sb_reset()) return;
  if (sb_dsp_write(0xE1) || sb_dsp_read() < 4) return;      // DSP 4.x: a Sound Blaster 16
  sb_dsp_read();
  // The ring must not cross a 128 KB boundary (16-bit DMA): twice its size is
  // taken, and the half that does not cross is used.
  int sel, bytes = SB_RING * 4;
  int seg = __dpmi_allocate_dos_memory((bytes * 2 + 15) / 16, &sel);
  if (seg < 0) return;
  unsigned long lin = (unsigned long) seg * 16;
  if ((lin >> 17) != ((lin + bytes - 1) >> 17)) lin = (lin + bytes) & ~0x1FFFFul;   // from the boundary on
  sb_ring_lin = lin;
  for (int i = 0; i < bytes; i += 4) _farpokel(_dos_ds, lin + i, 0);
  int c = sb_dma16 & 3;
  outportb(0xD4, 4 | c);                                    // mask the channel
  outportb(0xD8, 0);
  outportb(0xD6, 0x58 | c);                                 // single, auto-init, memory to device
  outportb(sb_dma_addr[c], (lin >> 1) & 0xFF);
  outportb(sb_dma_addr[c], (lin >> 9) & 0xFF);
  outportb(sb_dma_page[c], (lin >> 16) & 0xFE);
  outportb(0xD8, 0);
  unsigned words = SB_RING * 2 - 1;
  outportb(sb_dma_count[c], words & 0xFF);
  outportb(sb_dma_count[c], (words >> 8) & 0xFF);
  outportb(0xD4, c);                                        // unmask
  sb_dsp_write(0x41); sb_dsp_write(SND_RATE >> 8); sb_dsp_write(SND_RATE & 0xFF);
  sb_dsp_write(0xB6);                                       // 16-bit, auto-init, FIFO
  sb_dsp_write(0x30);                                       // signed, stereo
  unsigned samples = SB_RING * 2 - 1;                       // the length of a block: the whole ring
  sb_dsp_write(samples & 0xFF); sb_dsp_write(samples >> 8);
  sb_wr = SB_AHEAD; sb_played = 0; sb_last = 0;
  sb_clock = us_ticks();
  snd_ready = 1;
}

// As hda.h: frames counted without end, written (sb_wr) and played (sb_played).
static void snd_pump(void) {
  if (!snd_ready) return;
  inportb(sb_base + 0xF);                                   // acknowledge the block's interrupt (16-bit)
  unsigned pos = sb_dma_pos();
  uint64_t now = us_ticks();
  uint64_t est = (now - sb_clock) * SND_RATE / 1000000;
  sb_clock = now;
  uint64_t d = (pos + SB_RING - sb_last) % SB_RING;
  if (est > d + SB_RING / 2) d += (est - d + SB_RING / 2) / SB_RING * SB_RING;
  sb_played += d;
  sb_last = pos;
  if (sb_wr < sb_played) sb_wr = sb_played;
  while (sb_wr - sb_played < SB_AHEAD) {
    int16_t l, r;
    snd_next_stereo(&l, &r);
    _farpokel(_dos_ds, sb_ring_lin + (sb_wr % SB_RING) * 4, (uint16_t) l | ((uint32_t) (uint16_t) r << 16));
    sb_wr++;
  }
}

static int sb_tried, sb_off;
static void snd_wake(void) {
  if (sb_tried || sb_off) return;
  sb_tried = 1;
  sb_init();
}

static cell_t snd_ok(void) { snd_wake(); return snd_ready ? -1 : 0; }

static void sb_stop(void) {                                 // at the end of the program
  if (!snd_ready) return;
  sb_dsp_write(0xD9);                                       // end the auto-init transfer
  outportb(0xD4, 4 | (sb_dma16 & 3));
  snd_ready = 0;
}
