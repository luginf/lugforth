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

// USB keyboard and mouse on the Pi 3: the DWC2 host controller at
// PERIPHERAL_BASE + 0x980000, in buffer DMA mode (the only one QEMU's
// raspi3b emulates), no interrupts. At boot it powers the controller,
// resets the port, enumerates the device there and, through a hub, the
// devices behind it; it keeps the HID interfaces: boot keyboards, boot
// mice, and QEMU's usb-tablet (absolute). Then usb_poll() asks each one for
// its report, without waiting: a channel per device. Keys go to input.h
// as Linux key codes.
//
// Tested under QEMU raspi3b (-device usb-kbd, usb-mouse, usb-tablet, with
// its full-speed hub). Not done: split transactions, needed for a full or
// low speed device behind a high speed hub, which is how the USB ports of
// a real Pi 3 B (LAN9514 hub) are wired; a Pi 3 A+ has no hub. The Pi 4's
// USB-A ports are on an xHCI controller (VL805), not handled.

#ifndef RPI_BOARD_VIRT

#ifdef RPI_BOARD_PI3
# define USB_BASE (PERIPHERAL_BASE + 0x980000UL)
#endif

#ifndef USB_BASE
static void usb_init(void) {}
static void usb_poll(void) {}
#else

#define UREG(off) (*(volatile uint32_t *) (USB_BASE + (off)))
#define HPRT_W1C ((1u << 1) | (1u << 2) | (1u << 3) | (1u << 5))

static void delay_ms(uint64_t ms) {
  uint64_t end = us_ticks() + ms * 1000;
  while (us_ticks() < end) {}
}

static uint32_t usb_bus(const void *p) { return (uint32_t) (uint64_t) p | 0xC0000000u; }

// ---- one transfer on a channel, waited for ----
enum { PID_DATA0 = 0, PID_DATA1 = 2, PID_SETUP = 3 };
enum { EP_CONTROL = 0, EP_INTERRUPT = 3 };

static uint32_t usb_hcchar(int addr, int ep, int in, int type, int mps, int low_speed) {
  return (uint32_t) (mps & 0x7FF) | ((uint32_t) (ep & 15) << 11) | ((uint32_t) (in ? 1 : 0) << 15) |
         ((uint32_t) (low_speed ? 1 : 0) << 17) | ((uint32_t) type << 18) | (1u << 20) |
         ((uint32_t) (addr & 127) << 22);
}

static void usb_start(int ch, uint32_t hcchar, int pid, void *buf, int len, int mps) {
  int packets = len ? (len + mps - 1) / mps : 1;
  dcache_clean(buf, len ? (uint64_t) len : 1);
  UREG(0x508 + 0x20 * ch) = 0x7FF;                        // HCINT: clear
  UREG(0x514 + 0x20 * ch) = usb_bus(buf);                 // HCDMA
  UREG(0x510 + 0x20 * ch) = (uint32_t) len | ((uint32_t) packets << 19) | ((uint32_t) pid << 29);
  uint32_t odd = (UREG(0x408) & 1) ? 0 : (1u << 29);      // next frame's parity
  UREG(0x500 + 0x20 * ch) = hcchar | odd | (1u << 31);    // enable
}

// The channel's result once halted: 0 done, 1 NAK, -1 error; 2 still busy.
static int usb_status(int ch) {
  uint32_t i = UREG(0x508 + 0x20 * ch);
  if (!(i & 2)) return 2;                                 // not yet halted
  if (i & 1) return 0;                                    // XferCompl
  if (i & 0x10) return 1;                                 // NAK
  return -1;                                              // STALL, errors
}

static int usb_wait(int ch) {
  uint64_t end = us_ticks() + 200000;
  for (;;) {
    int r = usb_status(ch);
    if (r != 2) return r;
    if (us_ticks() > end) {
      UREG(0x500 + 0x20 * ch) |= (1u << 30) | (1u << 31);   // disable
      return -1;
    }
  }
}

// Bytes received in the last IN transfer of `len` asked.
static int usb_received(int ch, int len) { return len - (int) (UREG(0x510 + 0x20 * ch) & 0x7FFFF); }

struct usb_dev { int addr, mps0, low_speed; };

// A control transfer: setup, data (IN when the request type says so),
// status. Retries a NAKed stage. Bytes moved, or -1.
static int usb_control(struct usb_dev *d, int rtype, int req, int value, int index,
                       void *data, int len) {
  static uint8_t setup[8] __attribute__((aligned(64)));
  static uint8_t buf[512] __attribute__((aligned(64)));
  int in = rtype & 0x80;
  setup[0] = (uint8_t) rtype; setup[1] = (uint8_t) req;
  setup[2] = (uint8_t) value; setup[3] = (uint8_t) (value >> 8);
  setup[4] = (uint8_t) index; setup[5] = (uint8_t) (index >> 8);
  setup[6] = (uint8_t) len; setup[7] = (uint8_t) (len >> 8);
  if (len > (int) sizeof buf) return -1;
  int r, got = 0;
  for (int t = 0; t < 10; t++) {
    usb_start(0, usb_hcchar(d->addr, 0, 0, EP_CONTROL, d->mps0, d->low_speed), PID_SETUP, setup, 8, d->mps0);
    if ((r = usb_wait(0)) != 1) break;
  }
  if (r) return -1;
  if (len) {
    if (!in) memcpy(buf, data, len);
    for (int t = 0; t < 100; t++) {
      usb_start(0, usb_hcchar(d->addr, 0, in, EP_CONTROL, d->mps0, d->low_speed), PID_DATA1, buf, len, d->mps0);
      if ((r = usb_wait(0)) != 1) break;
      delay_ms(1);
    }
    if (r) return -1;
    got = in ? usb_received(0, len) : len;
    if (in) { dcache_clean(buf, len); memcpy(data, buf, got); }
  }
  for (int t = 0; t < 100; t++) {
    usb_start(0, usb_hcchar(d->addr, 0, !in || !len, EP_CONTROL, d->mps0, d->low_speed), PID_DATA1, buf, 0, d->mps0);
    if ((r = usb_wait(0)) != 1) break;
    delay_ms(1);
  }
  return r ? -1 : got;
}

// ---- the HID devices kept, each with its own channel ----
enum { HID_KEYBOARD, HID_MOUSE, HID_TABLET };
#define USB_MAX_HID 6
static struct usb_hid {
  struct usb_dev d;
  int kind, ep, mps, interval, toggle, busy;
  uint64_t next_us;
  uint8_t report[64] __attribute__((aligned(64)));
  uint8_t last[8];
} usb_hids[USB_MAX_HID];
static int usb_hid_count, usb_next_addr = 1;

// HID usage (keyboard page) to Linux key code; 0: none.
static int hid_to_linux(int u) {
  static const uint8_t letters[26] = {30, 48, 46, 32, 18, 33, 34, 35, 23, 36, 37, 38, 50,
                                      49, 24, 25, 16, 19, 31, 20, 22, 47, 17, 45, 21, 44};
  static const uint8_t from_0x28[17] = {28, 1, 14, 15, 57, 12, 13, 26, 27, 43, 0, 39, 40, 41, 51, 52, 53};
  if (u >= 0x04 && u <= 0x1D) return letters[u - 0x04];
  if (u >= 0x1E && u <= 0x27) return 2 + (u - 0x1E);       // 1..9 0
  if (u >= 0x28 && u <= 0x38) return from_0x28[u - 0x28];
  switch (u) {
    case 0x3D: return 62;          // F4
    case 0x45: return 88;          // F12
    case 0x4A: return KEY_HOME;
    case 0x64: return KEY_102ND;   // the key beside the left Shift of an ISO keyboard
    case 0x4F: return KEY_RIGHT;
    case 0x50: return KEY_LEFT;
    case 0x51: return KEY_DOWN;
    case 0x52: return KEY_UP;
  }
  return 0;
}

static void hid_keyboard_report(struct usb_hid *h, const uint8_t *r) {
  static const int mods[8] = {KEY_LEFTCTRL, KEY_LEFTSHIFT, 56, 125, KEY_RIGHTCTRL, KEY_RIGHTSHIFT, 100, 126};
  uint8_t changed = r[0] ^ h->last[0];
  for (int b = 0; b < 8; b++) {
    if (changed & (1 << b)) input_key(mods[b], (r[0] >> b) & 1);
  }
  for (int i = 2; i < 8; i++) {          // released: in the old report, not the new
    int u = h->last[i], still = 0;
    for (int j = 2; j < 8; j++) still |= r[j] == u;
    if (u > 3 && !still && hid_to_linux(u)) input_key(hid_to_linux(u), 0);
  }
  for (int i = 2; i < 8; i++) {          // pressed: in the new, not the old
    int u = r[i], before = 0;
    for (int j = 2; j < 8; j++) before |= h->last[j] == u;
    if (u > 3 && !before && hid_to_linux(u)) input_key(hid_to_linux(u), 1);
  }
  memcpy(h->last, r, 8);
}

static void hid_buttons(struct usb_hid *h, int b) {
  static const int btn[3] = {BTN_LEFT, BTN_RIGHT, BTN_MIDDLE};
  for (int i = 0; i < 3; i++) {
    if ((b ^ h->last[0]) & (1 << i)) input_key(btn[i], (b >> i) & 1);
  }
  h->last[0] = (uint8_t) b;
}

static void hid_report(struct usb_hid *h, const uint8_t *r, int n) {
  if (h->kind == HID_KEYBOARD) {
    if (n >= 8) hid_keyboard_report(h, r);
  } else if (h->kind == HID_MOUSE) {             // boot mouse: buttons, dx, dy, wheel
    if (n < 3) return;
    hid_buttons(h, r[0]);
    input_rel(0, (int8_t) r[1]);
    input_rel(1, (int8_t) r[2]);
    if (n >= 4 && r[3]) input_wheel((int8_t) r[3]);
  } else {                                       // QEMU tablet: buttons, x, y (0..32767), wheel
    if (n < 5) return;
    hid_buttons(h, r[0]);
    input_abs(0, r[1] | (r[2] << 8));
    input_abs(1, r[3] | (r[4] << 8));
    if (n >= 6 && r[5]) input_wheel((int8_t) r[5]);
  }
}

// ---- enumeration ----
static int usb_enumerate(int low_speed, int depth);

// Hub class: power each port, reset the ones with a device, enumerate it.
static void usb_hub(struct usb_dev *hub, int depth) {
  uint8_t desc[16];
  if (usb_control(hub, 0xA0, 6, 0x2900, 0, desc, 8) < 3) return;   // hub descriptor
  int ports = desc[2];
  for (int p = 1; p <= ports; p++) usb_control(hub, 0x23, 3, 8, p, 0, 0);   // SET_FEATURE PORT_POWER
  delay_ms(desc[5] * 2 + 50);
  for (int p = 1; p <= ports; p++) {
    uint8_t st[4];
    if (usb_control(hub, 0xA3, 0, 0, p, st, 4) != 4 || !(st[0] & 1)) continue;   // connected?
    usb_control(hub, 0x23, 3, 4, p, 0, 0);                                    // PORT_RESET
    for (int t = 0; t < 50; t++) {
      delay_ms(10);
      if (usb_control(hub, 0xA3, 0, 0, p, st, 4) == 4 && !(st[0] & 0x10)) break;   // reset over
    }
    usb_control(hub, 0x23, 1, 20, p, 0, 0);                                   // clear C_PORT_RESET
    delay_ms(20);
    if (!(st[0] & 2)) continue;                                               // enabled?
    usb_enumerate((st[1] >> 1) & 1, depth + 1);                               // low speed bit 9
  }
}

// The device now at address 0: give it an address, read its configuration,
// keep its HID interfaces or walk it as a hub. 0: ok.
static int usb_enumerate(int low_speed, int depth) {
  struct usb_dev d = {0, 8, low_speed};
  uint8_t dev[18], cfg[256];
  if (depth > 3 || usb_next_addr > 127) return -1;
  if (usb_control(&d, 0x80, 6, 0x0100, 0, dev, 8) < 8) return -1;    // device descriptor, start
  d.mps0 = dev[7] ? dev[7] : 8;
  int addr = usb_next_addr++;
  if (usb_control(&d, 0x00, 5, addr, 0, 0, 0) < 0) return -1;        // SET_ADDRESS
  delay_ms(10);
  d.addr = addr;
  if (usb_control(&d, 0x80, 6, 0x0100, 0, dev, 18) < 18) return -1;
  if (usb_control(&d, 0x80, 6, 0x0200, 0, cfg, 9) < 9) return -1;    // configuration
  int total = cfg[2] | (cfg[3] << 8);
  if (total > (int) sizeof cfg) total = sizeof cfg;
  if (usb_control(&d, 0x80, 6, 0x0200, 0, cfg, total) < total) return -1;
  if (usb_control(&d, 0x00, 9, cfg[5], 0, 0, 0) < 0) return -1;      // SET_CONFIGURATION
  if (dev[4] == 9) { usb_hub(&d, depth); return 0; }
  int iface = -1, iclass = 0, isub = 0, iproto = 0;
  for (int i = 0; i + 2 <= total && cfg[i] >= 2; i += cfg[i]) {
    if (cfg[i + 1] == 4) {                                           // interface
      iface = cfg[i + 2]; iclass = cfg[i + 5]; isub = cfg[i + 6]; iproto = cfg[i + 7];
    } else if (cfg[i + 1] == 5 && iclass == 3 && (cfg[i + 2] & 0x80) &&
               (cfg[i + 3] & 3) == 3 && usb_hid_count < USB_MAX_HID) {   // interrupt IN endpoint
      struct usb_hid *h = &usb_hids[usb_hid_count];
      h->kind = isub == 1 && iproto == 1 ? HID_KEYBOARD : isub == 1 && iproto == 2 ? HID_MOUSE : HID_TABLET;
      if (isub == 1) usb_control(&d, 0x21, 0x0B, 0, iface, 0, 0);    // SET_PROTOCOL boot
      usb_control(&d, 0x21, 0x0A, 0, iface, 0, 0);                    // SET_IDLE: only changes
      h->d = d;
      h->ep = cfg[i + 2] & 15;
      h->mps = (cfg[i + 4] | (cfg[i + 5] << 8)) & 0x7FF;
      if (h->mps > (int) sizeof h->report) h->mps = sizeof h->report;
      h->interval = cfg[i + 6] ? cfg[i + 6] : 10;
      usb_hid_count++;
      iclass = 0;                                                     // one endpoint per interface
    }
  }
  return 0;
}

static void usb_init(void) {
  static volatile uint32_t m[8] __attribute__((aligned(16)));
  m[0] = 8 * 4; m[1] = 0;
  m[2] = 0x28001; m[3] = 8; m[4] = 0; m[5] = 3; m[6] = 3;   // power on device 3 (USB), wait
  m[7] = 0;
  mbox_call(m);
  if ((UREG(0x040) & 0xFFFFF000u) != 0x4F542000u) return;   // GSNPSID: "OT2"
  UREG(0x008) = 0;                                          // GAHBCFG: no interrupts, no DMA yet
  while (!(UREG(0x010) & (1u << 31))) {}                    // GRSTCTL: AHB idle
  UREG(0x010) = 1;                                          // core soft reset
  for (int t = 0; t < 1000 && (UREG(0x010) & 1); t++) delay_ms(1);
  delay_ms(100);
  UREG(0x00C) = (UREG(0x00C) & ~(1u << 30)) | (1u << 29);   // GUSBCFG: force host mode
  delay_ms(50);
  UREG(0x400) = 0;                                          // HCFG: 30/60 MHz UTMI+ PHY clock
  UREG(0x024) = 1024;                                       // receive FIFO, words
  UREG(0x028) = (1024u << 16) | 1024;                       // non-periodic TX FIFO
  UREG(0x100) = (1024u << 16) | 2048;                       // periodic TX FIFO
  UREG(0x010) = (0x10u << 6) | (1u << 5);                   // flush all TX FIFOs
  for (int t = 0; t < 100 && (UREG(0x010) & (1u << 5)); t++) delay_ms(1);
  UREG(0x010) = 1u << 4;                                    // flush RX FIFO
  for (int t = 0; t < 100 && (UREG(0x010) & (1u << 4)); t++) delay_ms(1);
  UREG(0x008) = 1u << 5;                                    // GAHBCFG: DMA
  uint32_t p = UREG(0x440) & ~HPRT_W1C;
  UREG(0x440) = p | (1u << 12);                             // HPRT: port power
  delay_ms(100);
  if (!(UREG(0x440) & 1)) return;                           // nothing connected
  p = UREG(0x440) & ~HPRT_W1C;
  UREG(0x440) = p | (1u << 8);                              // port reset
  delay_ms(60);
  UREG(0x440) = p & ~(1u << 8);
  delay_ms(20);
  uint32_t hprt = UREG(0x440);
  UREG(0x440) = (hprt & ~HPRT_W1C) | (hprt & ((1u << 1) | (1u << 3) | (1u << 5)));  // ack changes
  usb_enumerate(((hprt >> 17) & 3) == 2, 0);
}

// Ask each HID device for a report, at its own pace, without waiting: a
// transfer is started on its channel and looked at on a later call.
static void usb_poll(void) {
  uint64_t now = us_ticks();
  for (int n = 0; n < usb_hid_count; n++) {
    struct usb_hid *h = &usb_hids[n];
    int ch = 1 + n;
    if (h->busy) {
      int r = usb_status(ch);
      if (r == 2) continue;
      h->busy = 0;
      if (r == 0) {
        int got = usb_received(ch, h->mps);
        dcache_clean(h->report, h->mps);
        h->toggle ^= 1;
        if (got > 0) hid_report(h, h->report, got);
      }
      h->next_us = now + (uint64_t) h->interval * 1000;
    }
    if (!h->busy && now >= h->next_us) {
      usb_start(ch, usb_hcchar(h->d.addr, h->ep, 1, EP_INTERRUPT, h->mps, h->d.low_speed),
                h->toggle ? PID_DATA1 : PID_DATA0, h->report, h->mps, h->mps);
      h->busy = 1;
    }
  }
}

#endif  // USB_BASE
#endif  // !RPI_BOARD_VIRT
