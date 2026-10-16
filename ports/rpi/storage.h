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

// Blocks of 512 bytes for fat.h: blk_read() and blk_write(), 0 on success.
// - Pi 3: the SD card on the EMMC controller (an SDHCI, Arasan) at
//   PERIPHERAL_BASE + 0x300000, polled, data moved by the CPU (no DMA).
//   QEMU raspi3b: -drive if=sd,format=raw,file=card.img (a size that is a
//   power of 2).
// - Pi 4: the same driver on EMMC2 (0xFE340000). WRITTEN BLIND.
// - QEMU virt: virtio-blk (-drive if=none,id=d0,format=raw,file=disk.img
//   -device virtio-blk-device,drive=d0), over virtio.h.

static int blk_ready;

#if defined(RPI_BOARD_VIRT)
// ---- virtio-blk: a request is a header, the data, a status byte ----
static struct virtq blk_q;
static struct { uint32_t type, reserved; uint64_t sector; } *blk_hdr;
static volatile uint8_t *blk_status;
static uint8_t *blk_buf;

static void blk_init(void) {
  uint64_t b = virtio_find(2, 0);
  if (!b || virtio_begin(b)) return;
  if (virtq_init(&blk_q, b, 0, 4)) return;
  blk_hdr = vio_alloc(16);
  blk_status = vio_alloc(1);
  blk_buf = vio_alloc(512);
  if (!blk_hdr || !blk_status || !blk_buf) return;
  virtio_ready(b);
  blk_ready = 1;
}

static int blk_io(uint32_t lba, void *buf, int write) {
  if (!blk_ready) return -1;
  blk_hdr->type = write ? 1 : 0; blk_hdr->reserved = 0; blk_hdr->sector = lba;
  if (write) memcpy(blk_buf, buf, 512);
  *blk_status = 0xFF;
  blk_q.desc[0].addr = (uint64_t) blk_hdr; blk_q.desc[0].len = 16;
  blk_q.desc[0].flags = VQ_NEXT; blk_q.desc[0].next = 1;
  blk_q.desc[1].addr = (uint64_t) blk_buf; blk_q.desc[1].len = 512;
  blk_q.desc[1].flags = VQ_NEXT | (write ? 0 : VQ_WRITE); blk_q.desc[1].next = 2;
  blk_q.desc[2].addr = (uint64_t) blk_status; blk_q.desc[2].len = 1;
  blk_q.desc[2].flags = VQ_WRITE; blk_q.desc[2].next = 0;
  virtq_offer(&blk_q, 0);
  virtq_notify(&blk_q);
  uint64_t end = us_ticks() + 2000000;
  while (virtq_take(&blk_q, 0) < 0) { if (us_ticks() > end) return -1; }
  vio_barrier();
  if (*blk_status != 0) return -1;
  if (!write) memcpy(buf, blk_buf, 512);
  return 0;
}

#elif defined(PERIPHERAL_BASE)
// ---- SDHCI ----
# ifdef RPI_BOARD_PI3
#  define EMMC_BASE (PERIPHERAL_BASE + 0x300000UL)
# else
#  define EMMC_BASE 0xFE340000UL
# endif
# define EREG(off) (*(volatile uint32_t *) (EMMC_BASE + (off)))
# define E_ARG1 0x08
# define E_BLKSIZECNT 0x04
# define E_CMDTM 0x0C
# define E_RESP0 0x10
# define E_DATA 0x20
# define E_STATUS 0x24
# define E_CONTROL0 0x28
# define E_CONTROL1 0x2C
# define E_INTERRUPT 0x30
# define E_IRPT_MASK 0x34
# define E_IRPT_EN 0x38

// CMDTM: response type (bits 17:16), CRC and index checks, data.
# define R_NONE 0
# define R_136 (1u << 16)
# define R_48 ((2u << 16) | (1u << 19) | (1u << 20))
# define R_48_NOCRC (2u << 16)                       // R3 (ACMD41): no CRC, no index
# define R_48B ((3u << 16) | (1u << 19) | (1u << 20))
# define CMD_DATA (1u << 21)
# define CMD_READ (1u << 4)

static int sd_high_capacity;                         // block addressing (SDHC/SDXC)
static uint32_t sd_rca;
static uint32_t sd_base_hz = 50000000;

static void sd_delay_us(uint64_t us) { uint64_t e = us_ticks() + us; while (us_ticks() < e) {} }

// Wait for any of `bits` in INTERRUPT; 0 if seen (and cleared), -1 on an
// error or a timeout (and the error bits cleared).
static int sd_wait(uint32_t bits, uint64_t timeout_us) {
  uint64_t end = us_ticks() + timeout_us;
  for (;;) {
    uint32_t i = EREG(E_INTERRUPT);
    if (i & 0xFFFF0000u) { EREG(E_INTERRUPT) = i; return -1; }
    if (i & bits) { EREG(E_INTERRUPT) = i & bits; return 0; }
    if (us_ticks() > end) return -1;
  }
}

static int sd_cmd(uint32_t index, uint32_t flags, uint32_t arg) {
  uint64_t end = us_ticks() + 100000;
  while (EREG(E_STATUS) & 1) { if (us_ticks() > end) return -1; }   // CMD_INHIBIT
  EREG(E_INTERRUPT) = 0xFFFFFFFFu;
  EREG(E_ARG1) = arg;
  EREG(E_CMDTM) = (index << 24) | flags;
  return sd_wait(1, 500000);                                         // CMD_DONE
}

static int sd_app_cmd(uint32_t index, uint32_t flags, uint32_t arg) {
  if (sd_cmd(55, R_48, sd_rca << 16)) return -1;
  return sd_cmd(index, flags, arg);
}

// The SD clock from the base clock: SDHCI 3.0, a 10-bit divider, base/(2d).
static int sd_clock(uint32_t hz) {
  uint32_t d = (sd_base_hz + 2 * hz - 1) / (2 * hz);
  if (d > 0x3FF) d = 0x3FF;
  uint32_t c1 = EREG(E_CONTROL1);
  EREG(E_CONTROL1) = c1 & ~(1u << 2);                               // clock off
  sd_delay_us(10);
  c1 = (c1 & ~0xFFE0u) | ((d & 0xFF) << 8) | (((d >> 8) & 3) << 6) | (0xEu << 16) | 1;
  EREG(E_CONTROL1) = c1;                                            // internal clock on
  uint64_t end = us_ticks() + 100000;
  while (!(EREG(E_CONTROL1) & 2)) { if (us_ticks() > end) return -1; }   // stable
  EREG(E_CONTROL1) = c1 | (1u << 2);                                // SD clock on
  sd_delay_us(10);
  return 0;
}

static void blk_init(void) {
# ifdef RPI_BOARD_PI3
  // GPIO 48..53 to ALT3: the card slot on this controller (not SDHOST).
  for (int pin = 48; pin <= 53; pin++) {
    uint64_t r = GPIO_BASE + 4 * (pin / 10);
    uint32_t s = 3 * (pin % 10);
    REG(r) = (REG(r) & ~(7u << s)) | (7u << s);                     // ALT3 = 7
  }
# endif
  static volatile uint32_t m[8] __attribute__((aligned(16)));
  m[0] = 8 * 4; m[1] = 0; m[2] = 0x30002; m[3] = 8; m[4] = 0; m[5] = 1; m[6] = 0; m[7] = 0;
  if (mbox_call(m) && m[6]) sd_base_hz = m[6];                      // EMMC clock rate
  EREG(E_CONTROL0) = 0;
  EREG(E_CONTROL1) = 1u << 24;                                      // reset the host
  uint64_t end = us_ticks() + 100000;
  while (EREG(E_CONTROL1) & (7u << 24)) { if (us_ticks() > end) return; }
  if (sd_clock(400000)) return;
  EREG(E_IRPT_EN) = 0;                                              // polled
  EREG(E_IRPT_MASK) = 0xFFFFFFFFu;
  EREG(E_INTERRUPT) = 0xFFFFFFFFu;
  sd_delay_us(2000);
  if (sd_cmd(0, R_NONE, 0)) return;                                 // GO_IDLE
  int v2 = sd_cmd(8, R_48, 0x1AA) == 0 && (EREG(E_RESP0) & 0xFFF) == 0x1AA;
  uint32_t ocr = 0;
  for (int t = 0; t < 100; t++) {                                   // up to 1 s
    if (sd_app_cmd(41, R_48_NOCRC, 0x00FF8000u | (v2 ? (1u << 30) : 0))) return;
    ocr = EREG(E_RESP0);
    if (ocr & (1u << 31)) break;                                    // powered up
    sd_delay_us(10000);
  }
  if (!(ocr & (1u << 31))) return;
  sd_high_capacity = (ocr >> 30) & 1;
  if (sd_cmd(2, R_136, 0)) return;                                  // ALL_SEND_CID
  if (sd_cmd(3, R_48, 0)) return;                                   // SEND_RELATIVE_ADDR
  sd_rca = EREG(E_RESP0) >> 16;
  if (sd_cmd(7, R_48B, sd_rca << 16)) return;                       // SELECT
  if (sd_clock(25000000)) return;
  if (!sd_high_capacity && sd_cmd(16, R_48, 512)) return;           // SET_BLOCKLEN
  blk_ready = 1;
}

static int blk_io(uint32_t lba, void *buf, int write) {
  if (!blk_ready) return -1;
  uint32_t *w = (uint32_t *) buf;
  uint64_t end = us_ticks() + 100000;
  while (EREG(E_STATUS) & 2) { if (us_ticks() > end) return -1; }   // DAT_INHIBIT
  EREG(E_BLKSIZECNT) = (1u << 16) | 512;
  uint32_t arg = sd_high_capacity ? lba : lba * 512;
  if (sd_cmd(write ? 24 : 17, R_48 | CMD_DATA | (write ? 0 : CMD_READ), arg)) return -1;
  if (sd_wait(write ? (1u << 4) : (1u << 5), 500000)) return -1;   // WRITE_RDY / READ_RDY
  for (int i = 0; i < 128; i++) {
    if (write) EREG(E_DATA) = w[i];
    else w[i] = EREG(E_DATA);
  }
  return sd_wait(2, 1000000);                                       // DATA_DONE
}

#else
static void blk_init(void) {}
static int blk_io(uint32_t lba, void *buf, int write) { (void) lba; (void) buf; (void) write; return -1; }
#endif

static int blk_read(uint32_t lba, void *buf) { return blk_io(lba, buf, 0); }
static int blk_write(uint32_t lba, const void *buf) { return blk_io(lba, (void *) buf, 1); }
