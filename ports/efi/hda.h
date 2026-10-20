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

// Sound on the PC: a minimal Intel High Definition Audio driver (the
// controller of nearly every PC since 2005, and of QEMU's -device intel-hda).
// The first controller found on the PCI bus (class 4, subclass 3) is reset;
// the first codec is asked, through the immediate command registers, for its
// audio function group; every connected output pin (line out, speaker,
// headphones) is followed down to a DAC, and the nodes on the way are powered
// and unmuted. One output stream, 44100 Hz 16-bit stereo, loops on a ring of
// samples that snd_pump() keeps filled from the mixer (ports/baremetal/mixer.h).
// The memory is identity mapped under the UEFI boot services, so buffers are
// given to the controller by their address.
// Tested under QEMU (intel-hda + hda-duplex), not on a real PC: codecs differ
// (pin choices, amplifier ranges), and some controllers need more set up than
// this (snoop bits, quirks). Without a controller, the words stay silent.

#include "ports/baremetal/mixer.h"

static inline void outl(uint16_t port, uint32_t v) { __asm__ volatile("outl %0, %1" :: "a"(v), "Nd"(port)); }
static inline uint32_t inl(uint16_t port) { uint32_t v; __asm__ volatile("inl %1, %0" : "=a"(v) : "Nd"(port)); return v; }

static uint32_t pci_read(int bus, int dev, int fn, int reg) {
  outl(0xCF8, 0x80000000u | (uint32_t) bus << 16 | (uint32_t) dev << 11 | (uint32_t) fn << 8 | (uint32_t) (reg & 0xFC));
  return inl(0xCFC);
}

static void pci_write(int bus, int dev, int fn, int reg, uint32_t v) {
  outl(0xCF8, 0x80000000u | (uint32_t) bus << 16 | (uint32_t) dev << 11 | (uint32_t) fn << 8 | (uint32_t) (reg & 0xFC));
  outl(0xCFC, v);
}

static volatile uint8_t *hda;           // the controller's registers
#define HR8(o) (*(volatile uint8_t *) (hda + (o)))
#define HR16(o) (*(volatile uint16_t *) (hda + (o)))
#define HR32(o) (*(volatile uint32_t *) (hda + (o)))

#define HDA_RING 16384                  // frames in the ring (372 ms)
#define HDA_AHEAD 6144                  // frames kept filled in front of the play position (139 ms)
static int16_t (*hda_ring)[2];
static unsigned hda_sd;                 // offset of the output stream descriptor
static int hda_codec;
static uint64_t hda_played, hda_wr;     // frames played and written since the start
static unsigned hda_last;
static uint64_t hda_clock;              // us_ticks() at the last snd_pump

static void hda_delay(unsigned us) { BS->Stall(us); }

// The start-up may not hold the machine: after this time everything fails at once.
static uint64_t hda_deadline;
static int hda_late(void) { return us_ticks() > hda_deadline; }

// One verb sent to the codec by the immediate command registers. Returns the
// response, or 0 when the codec does not answer.
static uint32_t hda_cmd(int nid, uint32_t verb, uint32_t payload) {
  uint32_t cmd = (uint32_t) hda_codec << 28 | (uint32_t) nid << 20;
  cmd |= verb > 0xFF ? (verb << 8 | (payload & 0xFF)) : (verb << 16 | (payload & 0xFFFF));
  if (hda_late()) return 0;
  for (int t = 0; t < 200 && (HR16(0x68) & 1); t++) hda_delay(10);   // busy
  HR16(0x68) = 2;                       // clear "result valid"
  HR32(0x60) = cmd;
  HR16(0x68) = 1;                       // go
  for (int t = 0; t < 200; t++) {                       // a codec answers in a few microseconds
    if ((HR16(0x68) & 3) == 2) return HR32(0x64);
    hda_delay(10);
  }
  hda_deadline = 0;                                     // no answer: give up
  return 0;
}

static uint32_t hda_param(int nid, uint32_t p) { return hda_cmd(nid, 0xF00, p); }

#define HDA_NODES 256
static uint8_t hda_type[HDA_NODES];
static uint32_t hda_caps[HDA_NODES];
static uint32_t hda_afg_outamp, hda_afg_inamp;
static int hda_dacs[8], hda_ndacs;

// The connection list of a node: the nodes it can take its input from.
static int hda_conns(int nid, int *list, int max) {
  uint32_t n = hda_param(nid, 0x0E);
  int count = (int) (n & 0x7F), long_form = (n & 0x80) != 0, got = 0;
  int per = long_form ? 2 : 4;
  for (int i = 0; i < count && got < max; i += per) {
    uint32_t r = hda_cmd(nid, 0xF02, (uint32_t) i);
    for (int j = 0; j < per && i + j < count && got < max; j++)
      list[got++] = (int) (long_form ? (r >> (16 * j)) & 0x7FFF : (r >> (8 * j)) & 0xFF);
  }
  return got;
}

// Turn a node on and its amplifiers to 0 dB (not muted).
static void hda_open_node(int nid, int input_index) {
  hda_cmd(nid, 0x705, 0);                                   // power D0
  uint32_t caps = hda_caps[nid];
  if (caps & 4) {                                           // output amplifier
    uint32_t a = (caps & 8) ? hda_param(nid, 0x12) : hda_afg_outamp;
    hda_cmd(nid, 0x3, 0xB000 | (a & 0x7F));
  }
  if ((caps & 2) && input_index >= 0) {                     // input amplifier
    uint32_t a = (caps & 8) ? hda_param(nid, 0x0D) : hda_afg_inamp;
    hda_cmd(nid, 0x3, 0x7000 | (uint32_t) input_index << 8 | (a & 0x7F));
  }
}

// Follow the connections of a node down to a DAC (up to `depth` nodes), opening
// the nodes on the way. Returns the DAC, or 0.
static int hda_route(int nid, int depth) {
  if (nid <= 0 || nid >= HDA_NODES) return 0;
  if ((hda_caps[nid] >> 20 & 15) == 0) { hda_open_node(nid, -1); return nid; }
  if (depth <= 0) return 0;
  int list[32], n = hda_conns(nid, list, 32);
  for (int i = 0; i < n; i++) {
    int dac = hda_route(list[i], depth - 1);
    if (!dac) continue;
    if (n > 1) hda_cmd(nid, 0x701, (uint32_t) i);          // select it
    hda_open_node(nid, i);
    return dac;
  }
  return 0;
}

// The audio function group: every output pin that is connected, to a DAC.
static void hda_setup_afg(int afg) {
  hda_cmd(afg, 0x705, 0);
  hda_delay(10000);
  hda_afg_outamp = hda_param(afg, 0x12);
  hda_afg_inamp = hda_param(afg, 0x0D);
  uint32_t sub = hda_param(afg, 0x04);
  int first = (int) (sub >> 16 & 0xFF), count = (int) (sub & 0xFF);
  for (int nid = first; nid < first + count && nid < HDA_NODES; nid++) {
    hda_caps[nid] = hda_param(nid, 0x09);
    hda_type[nid] = (uint8_t) (hda_caps[nid] >> 20 & 15);
  }
  for (int nid = first; nid < first + count && nid < HDA_NODES; nid++) {
    if (hda_type[nid] != 4) continue;                       // pins only
    uint32_t pincaps = hda_param(nid, 0x0C);
    uint32_t cfg = hda_cmd(nid, 0xF1C, 0);
    int dev = (int) (cfg >> 20 & 15), conn = (int) (cfg >> 30);
    if (!(pincaps & 0x10) || conn == 1 || dev > 2) continue;   // output, connected, line out/speaker/headphones
    int dac = hda_route(nid, 3);
    if (!dac) continue;
    hda_open_node(nid, -1);
    hda_cmd(nid, 0x707, dev == 2 ? 0xC0 : 0x40);           // output enable (and headphone amp)
    if (pincaps & 0x10000) hda_cmd(nid, 0x70C, 2);          // external amplifier on
    int seen = 0;
    for (int i = 0; i < hda_ndacs; i++) if (hda_dacs[i] == dac) seen = 1;
    if (!seen && hda_ndacs < 8) hda_dacs[hda_ndacs++] = dac;
  }
}

static int hda_find(int *bus, int *dev, int *fn) {
  for (int b = 0; b < 256; b++)
    for (int d = 0; d < 32; d++) {
      if ((pci_read(b, d, 0, 0) & 0xFFFF) == 0xFFFF) continue;
      int fns = (pci_read(b, d, 0, 0x0C) >> 16 & 0x80) ? 8 : 1;
      for (int f = 0; f < fns; f++) {
        if ((pci_read(b, d, f, 0) & 0xFFFF) == 0xFFFF) continue;
        if ((pci_read(b, d, f, 8) >> 16) == 0x0403) { *bus = b; *dev = d; *fn = f; return 1; }
      }
    }
  return 0;
}

static void hda_init(void) {
  hda_deadline = us_ticks() + 400000;
  int bus, dev, fn;
  if (!hda_find(&bus, &dev, &fn)) return;
  uint32_t bar = pci_read(bus, dev, fn, 0x10);
  if (bar & 1) return;                                      // an I/O port: not a controller
  uint64_t base = bar & ~0xFULL;
  if ((bar & 6) == 4) base |= (uint64_t) pci_read(bus, dev, fn, 0x14) << 32;
  if (!base) return;
  pci_write(bus, dev, fn, 4, (pci_read(bus, dev, fn, 4) & 0xFFFF) | 6);   // memory space, bus master
  hda = (volatile uint8_t *) base;
  // Reset the link, then wait for the codecs to announce themselves.
  HR32(0x08) &= ~1u;
  for (int t = 0; t < 1000 && (HR32(0x08) & 1); t++) hda_delay(100);
  hda_delay(200);
  HR32(0x08) |= 1u;
  int up = 0;
  for (int t = 0; t < 1000 && !up; t++) { hda_delay(100); up = HR32(0x08) & 1; }
  if (!up) return;
  hda_delay(10000);
  if (hda_late()) return;
  uint16_t codecs = HR16(0x0E);
  if (!codecs) return;
  for (hda_codec = 0; hda_codec < 15 && !(codecs >> hda_codec & 1); hda_codec++) {}
  // The audio function group of the codec.
  uint32_t sub = hda_param(0, 0x04);
  int first = (int) (sub >> 16 & 0xFF), count = (int) (sub & 0xFF), afg = 0;
  for (int nid = first; nid < first + count && !afg; nid++)
    if ((hda_param(nid, 0x05) & 0xFF) == 1) afg = nid;
  if (!afg) return;
  hda_setup_afg(afg);
  if (!hda_ndacs || hda_late()) return;
  // The output stream: after the input ones.
  uint32_t gcap = HR16(0x00);
  unsigned iss = gcap >> 8 & 15, oss = gcap >> 12 & 15;
  if (!oss) return;
  hda_sd = 0x80 + 0x20 * iss;
  EFI_PHYSICAL_ADDRESS mem = 0xFFFFFFFFULL;
  if (BS->AllocatePages(AllocateMaxAddress, EfiBootServicesData, 1 + HDA_RING * 4 / 4096, &mem)) return;
  memset((void *) mem, 0, 4096 + HDA_RING * 4);
  uint32_t *bdl = (uint32_t *) mem;                         // two entries: the two halves of the ring
  hda_ring = (int16_t (*)[2]) (mem + 4096);
  for (int i = 0; i < 2; i++) {
    uint64_t at = (uint64_t) hda_ring + (uint64_t) i * HDA_RING * 2;
    bdl[i * 4] = (uint32_t) at; bdl[i * 4 + 1] = (uint32_t) (at >> 32);
    bdl[i * 4 + 2] = HDA_RING * 2; bdl[i * 4 + 3] = 0;
  }
  HR32(hda_sd) |= 1; for (int t = 0; t < 1000 && !(HR32(hda_sd) & 1); t++) hda_delay(10);   // stream reset
  HR32(hda_sd) &= ~1u; for (int t = 0; t < 1000 && (HR32(hda_sd) & 1); t++) hda_delay(10);
  HR32(hda_sd + 0x00) = 1u << 20;                           // stream tag 1 (bits 20-23)
  HR32(hda_sd + 0x08) = HDA_RING * 4;                       // cyclic buffer length
  HR16(hda_sd + 0x0C) = 1;                                  // last valid index
  HR16(hda_sd + 0x12) = 0x4011;                             // 44100 Hz, 16 bits, 2 channels
  HR32(hda_sd + 0x18) = (uint32_t) mem; HR32(hda_sd + 0x1C) = (uint32_t) (mem >> 32);
  for (int i = 0; i < hda_ndacs; i++) {                     // the DACs: stream 1, channel 0, same format
    hda_cmd(hda_dacs[i], 0x2, 0x4011);
    hda_cmd(hda_dacs[i], 0x706, 0x10);
  }
  hda_wr = HDA_AHEAD;                                       // silence first, then the mixer
  hda_clock = us_ticks();
  HR32(hda_sd) |= 2;                                        // run
  snd_ready = 1;
}

// Frames are counted without end: written (hda_wr) and played (hda_played, from
// the controller's position in the ring). If the program was away so long that
// the playing caught up with the writing, the writing starts again from there.

static void snd_pump(void) {
  if (!snd_ready) return;
  unsigned pos = HR32(hda_sd + 0x04) / 4 % HDA_RING;        // LPIB: the frame being played
  uint64_t now = us_ticks();
  uint64_t est = (now - hda_clock) * 44100 / 1000000;       // frames the clock says went by
  hda_clock = now;
  uint64_t d = (pos + HDA_RING - hda_last) % HDA_RING;
  // LPIB only tells the position inside the ring. If the program was away for
  // longer than a turn of the ring (a slow firmware, a long wait), whole turns
  // are lost: the clock tells how many, else old sound would loop for a long time.
  if (est > d + HDA_RING / 2) d += (est - d + HDA_RING / 2) / HDA_RING * HDA_RING;
  hda_played += d;
  hda_last = pos;
  if (hda_wr < hda_played) {                                // ran dry: what is left in the ring is old, not to be heard again
    memset(hda_ring, 0, HDA_RING * 4);
    hda_wr = hda_played;
  }
  while (hda_wr - hda_played < HDA_AHEAD) {
    int16_t l, r;
    snd_next_stereo(&l, &r);
    hda_ring[hda_wr % HDA_RING][0] = l; hda_ring[hda_wr % HDA_RING][1] = r;
    hda_wr++;
  }
}

// The controller is started at the first use of a sound word, not at boot: a PC
// whose audio hardware answers badly then costs nothing to the programs that make no sound.
static int hda_tried, hda_off;
static void snd_wake(void) {
  if (hda_tried || hda_off) return;
  hda_tried = 1;
  hda_init();
}

static cell_t snd_ok(void) { snd_wake(); return snd_ready ? -1 : 0; }

// sound-off / sound-on: off stops the stream and resets the controller (a PC whose
// codec answers badly makes noise), on lets the next sound word start it again.
static void snd_enable(cell_t on) {
  hda_off = !on;
  if (on) return;
  if (snd_ready) { HR32(hda_sd) &= ~2u; HR32(0x08) &= ~1u; }
  snd_ready = 0;
  hda_tried = 0;
}
