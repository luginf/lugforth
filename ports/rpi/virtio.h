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

// virtio over MMIO, the modern interface (version 2), on QEMU virt: its 32
// transports sit at 0x0a000000 + 0x200 * n. QEMU makes them legacy by
// default, so run it with -global virtio-mmio.force-legacy=false (the
// Makefile and tools/rpi_qemu_*.py do). No interrupts: the drivers poll
// the used rings. Memory is identity-mapped, so an address is the same for
// the device. Used by input.h and sound.h.

#ifdef RPI_BOARD_VIRT

#define VIRTIO_BASE 0x0a000000UL
#define VIRTIO_SLOTS 32
#define VREG(base, off) (*(volatile uint32_t *) ((base) + (off)))

struct vq_desc { uint64_t addr; uint32_t len; uint16_t flags, next; };
struct vq_used_elem { uint32_t id, len; };

struct virtq {
  uint64_t base;            // the transport
  uint32_t index, num;
  volatile struct vq_desc *desc;
  volatile uint16_t *avail; // flags, idx, ring[num]
  volatile uint16_t *used;  // flags, idx, then elements
  uint16_t last_used;
};

#define VQ_NEXT 1
#define VQ_WRITE 2

static void vio_barrier(void) { __asm__ volatile("dsb sy" ::: "memory"); }

// The n-th transport holding a device of that type (1 network, 18 input,
// 25 sound...), or 0.
static uint64_t virtio_find(uint32_t device_id, int nth) {
  for (int i = 0; i < VIRTIO_SLOTS; i++) {
    uint64_t b = VIRTIO_BASE + 0x200UL * i;
    if (VREG(b, 0x000) != 0x74726976 || VREG(b, 0x004) != 2) continue;  // "virt", modern
    if (VREG(b, 0x008) != device_id) continue;
    if (nth-- == 0) return b;
  }
  return 0;
}

// Reset, acknowledge, accept VIRTIO_F_VERSION_1 and nothing else. 0: ok.
static int virtio_begin(uint64_t b) {
  VREG(b, 0x070) = 0;                         // Status: reset
  vio_barrier();
  VREG(b, 0x070) = 1 | 2;                     // ACKNOWLEDGE, DRIVER
  VREG(b, 0x014) = 1;                         // DeviceFeaturesSel: bits 32..63
  if (!(VREG(b, 0x010) & 1)) return -1;       // VERSION_1
  VREG(b, 0x024) = 0; VREG(b, 0x020) = 0;     // DriverFeatures 0..31: none
  VREG(b, 0x024) = 1; VREG(b, 0x020) = 1;     // 32..63: VERSION_1
  VREG(b, 0x070) = 1 | 2 | 8;                 // FEATURES_OK
  if (!(VREG(b, 0x070) & 8)) return -1;
  return 0;
}

static void virtio_ready(uint64_t b) { VREG(b, 0x070) = 1 | 2 | 8 | 4; }  // DRIVER_OK

static void *vio_alloc(uint64_t size) {
  uint8_t *p = (uint8_t *) malloc(size + 4096);
  if (!p) return 0;
  p = (uint8_t *) (((uint64_t) p + 4095) & ~4095UL);
  memset(p, 0, size);
  return p;
}

// Queue `index` with up to `num` descriptors. 0: ok.
static int virtq_init(struct virtq *q, uint64_t b, uint32_t index, uint32_t num) {
  VREG(b, 0x030) = index;                     // QueueSel
  if (VREG(b, 0x044)) return -1;              // QueueReady: already in use
  uint32_t max = VREG(b, 0x034);              // QueueNumMax
  if (!max) return -1;
  if (num > max) num = max;
  uint8_t *mem = (uint8_t *) vio_alloc(16 * num + 4096 + 8 * num + 4096);
  if (!mem) return -1;
  q->base = b; q->index = index; q->num = num; q->last_used = 0;
  q->desc = (volatile struct vq_desc *) mem;
  q->avail = (volatile uint16_t *) (mem + 16 * num);
  q->used = (volatile uint16_t *) (mem + ((16 * num + 6 + 2 * num + 4095) & ~4095UL));
  VREG(b, 0x038) = num;                       // QueueNum
  uint64_t d = (uint64_t) q->desc, a = (uint64_t) q->avail, u = (uint64_t) q->used;
  VREG(b, 0x080) = (uint32_t) d; VREG(b, 0x084) = (uint32_t) (d >> 32);
  VREG(b, 0x090) = (uint32_t) a; VREG(b, 0x094) = (uint32_t) (a >> 32);
  VREG(b, 0x0a0) = (uint32_t) u; VREG(b, 0x0a4) = (uint32_t) (u >> 32);
  VREG(b, 0x044) = 1;                         // QueueReady
  return 0;
}

// Offer descriptor chain `head` to the device (no notify).
static void virtq_offer(struct virtq *q, uint16_t head) {
  uint16_t idx = q->avail[1];
  q->avail[2 + idx % q->num] = head;
  vio_barrier();
  q->avail[1] = (uint16_t) (idx + 1);
  vio_barrier();
}

static void virtq_notify(struct virtq *q) { VREG(q->base, 0x050) = q->index; }

// The next descriptor chain the device gave back, or -1; *len its length.
static int virtq_take(struct virtq *q, uint32_t *len) {
  vio_barrier();
  if (q->used[1] == q->last_used) return -1;
  volatile struct vq_used_elem *e =
      (volatile struct vq_used_elem *) (q->used + 2) + q->last_used % q->num;
  q->last_used++;
  if (len) *len = e->len;
  return (int) e->id;
}

#endif  // RPI_BOARD_VIRT
