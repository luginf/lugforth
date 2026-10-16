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

// A real keyboard and mouse, beside the serial line: key down and up, the
// mouse position and buttons, for the sdl2 words; the keys typed also go
// to the console (key, the prompt). Drivers: virtio-input on QEMU virt
// (below), USB HID on the Pi (usb.h). Both report Linux key codes
// (input-event-codes.h) to input_key(); this file turns them into the
// codes of pressed? and into characters. Polled, no interrupts.

// (The input_ functions are unused on the Pi 4, which has no driver yet.)

// ---- the state read by the sdl2 words ----
static uint8_t in_down[256], in_hit[256], in_snap[256];
static int in_last_key;            // last key pressed, sdl2 code, for last-key
static int64_t in_mouse_x, in_mouse_y, in_wheel, in_wheel_acc;
static int in_mouse_seen;          // a pointer device sent something
static int in_shift, in_ctrl;

// ---- characters for the console ----
static uint8_t in_chars[64];
static unsigned in_chars_head, in_chars_tail;

static void in_put_char(int c) {
  if (in_chars_head - in_chars_tail < sizeof in_chars) in_chars[in_chars_head++ % sizeof in_chars] = (uint8_t) c;
}

static int in_get_char(void) {
  if (in_chars_head == in_chars_tail) return -1;
  return in_chars[in_chars_tail++ % sizeof in_chars];
}

// Linux key codes 0..63, US layout: unshifted and shifted characters.
static const char key_plain[64] = {
  0, 27, '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '-', '=', 8, 9,
  'q', 'w', 'e', 'r', 't', 'y', 'u', 'i', 'o', 'p', '[', ']', 13, 0, 'a', 's',
  'd', 'f', 'g', 'h', 'j', 'k', 'l', ';', '\'', '`', 0, '\\', 'z', 'x', 'c', 'v',
  'b', 'n', 'm', ',', '.', '/', 0, '*', 0, ' ', 0, 0, 0, 0, 0, 0,
};
static const char key_shift[64] = {
  0, 27, '!', '@', '#', '$', '%', '^', '&', '*', '(', ')', '_', '+', 8, 9,
  'Q', 'W', 'E', 'R', 'T', 'Y', 'U', 'I', 'O', 'P', '{', '}', 13, 0, 'A', 'S',
  'D', 'F', 'G', 'H', 'J', 'K', 'L', ':', '"', '~', 0, '|', 'Z', 'X', 'C', 'V',
  'B', 'N', 'M', '<', '>', '?', 0, '*', 0, ' ', 0, 0, 0, 0, 0, 0,
};

#define KEY_LEFTCTRL 29
#define KEY_LEFTSHIFT 42
#define KEY_RIGHTSHIFT 54
#define KEY_RIGHTCTRL 97
#define KEY_UP 103
#define KEY_LEFT 105
#define KEY_RIGHT 106
#define KEY_DOWN 108
#define BTN_LEFT 0x110
#define BTN_RIGHT 0x111
#define BTN_MIDDLE 0x112

// The code pressed? uses (ports/rpi/sdl2.fs): characters in lower case,
// 13 27 9 8 for Enter Esc Tab Backspace, 207..210 for the arrows, 253..255
// for the mouse buttons. 0: not tracked.
static int key_sdl_code(int code) {
  switch (code) {
    case KEY_LEFT: return 208;
    case KEY_RIGHT: return 207;
    case KEY_UP: return 210;
    case KEY_DOWN: return 209;
    case BTN_LEFT: return 255;
    case BTN_MIDDLE: return 254;
    case BTN_RIGHT: return 253;
  }
  if (code > 0 && code < 64) return (unsigned char) key_plain[code];
  return 0;
}

// A key or button went down (1), up (0) or repeated (2).
__attribute__((unused)) static void input_key(int code, int value) {
  if (code == KEY_LEFTSHIFT || code == KEY_RIGHTSHIFT) { in_shift = value != 0; return; }
  if (code == KEY_LEFTCTRL || code == KEY_RIGHTCTRL) { in_ctrl = value != 0; return; }
  int k = key_sdl_code(code);
  if (k) {
    if (value == 1) { in_down[k] = 1; in_hit[k] = 1; in_last_key = k; }
    if (value == 0) in_down[k] = 0;
  }
  if (!value) return;
  // Characters for the console, arrows as the terminal sends them.
  const char *arrow = code == KEY_UP ? "\033[A" : code == KEY_DOWN ? "\033[B" :
                      code == KEY_RIGHT ? "\033[C" : code == KEY_LEFT ? "\033[D" : 0;
  if (arrow) { while (*arrow) in_put_char(*arrow++); return; }
  if (code <= 0 || code >= 64) return;
  int c = (unsigned char) (in_shift ? key_shift[code] : key_plain[code]);
  if (!c) return;
  if (in_ctrl && ((c | 32) >= 'a' && (c | 32) <= 'z')) c = (c | 32) - 'a' + 1;
  if (c == 8) c = 127;                        // what a terminal sends for Backspace
  in_put_char(c);
}

// Absolute position, 0..32767 on each axis of the whole screen.
__attribute__((unused)) static void input_abs(int x_or_y, int64_t v) {
  in_mouse_seen = 1;
  if (x_or_y == 0) in_mouse_x = fb_w ? v * fb_w / 32768 : 0;
  else in_mouse_y = fb_h ? v * fb_h / 32768 : 0;
}

// Relative motion (a mouse), in screen pixels, kept on the screen.
__attribute__((unused)) static void input_rel(int x_or_y, int64_t d) {
  in_mouse_seen = 1;
  int64_t *p = x_or_y == 0 ? &in_mouse_x : &in_mouse_y;
  int64_t lim = x_or_y == 0 ? fb_w : fb_h;
  *p += fb_zoom ? d / fb_zoom : d;
  if (*p < 0) *p = 0;
  if (lim && *p >= lim) *p = lim - 1;
}

__attribute__((unused)) static void input_wheel(int64_t d) { in_wheel_acc += d; }

// ---- virtio-input on QEMU virt (-device virtio-keyboard-device,
// virtio-tablet-device or virtio-mouse-device): events of 8 bytes, in the
// format of Linux evdev, on queue 0. ----
#ifdef RPI_BOARD_VIRT
# define VIN_MAX 4
# define VIN_BUFS 64
struct vin_event { uint16_t type, code; int32_t value; };
static struct { struct virtq q; volatile struct vin_event *ev; } vin[VIN_MAX];
static int vin_count;

static void vin_init(void) {
  for (int n = 0; n < VIN_MAX; n++) {
    uint64_t b = virtio_find(18, n);
    if (!b) break;
    if (virtio_begin(b)) continue;
    struct virtq *q = &vin[vin_count].q;
    if (virtq_init(q, b, 0, VIN_BUFS)) continue;
    volatile struct vin_event *ev = (volatile struct vin_event *) vio_alloc(q->num * sizeof *ev);
    if (!ev) continue;
    vin[vin_count].ev = ev;
    for (uint32_t i = 0; i < q->num; i++) {
      q->desc[i].addr = (uint64_t) &ev[i];
      q->desc[i].len = sizeof *ev;
      q->desc[i].flags = VQ_WRITE;
      q->desc[i].next = 0;
      virtq_offer(q, (uint16_t) i);
    }
    virtio_ready(b);
    virtq_notify(q);
    vin_count++;
  }
}

static void vin_poll(void) {
  for (int n = 0; n < vin_count; n++) {
    struct virtq *q = &vin[n].q;
    int id, any = 0;
    while ((id = virtq_take(q, 0)) >= 0) {
      volatile struct vin_event *e = &vin[n].ev[id];
      switch (e->type) {
        case 1: input_key(e->code, e->value); break;                // EV_KEY
        case 2:                                                     // EV_REL
          if (e->code <= 1) input_rel(e->code, e->value);
          else if (e->code == 8) input_wheel(e->value);             // REL_WHEEL
          break;
        case 3: if (e->code <= 1) input_abs(e->code, e->value); break;  // EV_ABS
      }
      virtq_offer(q, (uint16_t) id);
      any = 1;
    }
    if (any) virtq_notify(q);
  }
}
#endif

#ifndef RPI_BOARD_VIRT
static void usb_poll(void);
#endif

#ifndef RPI_BOARD_VIRT
static void usb_init(void);
#endif

static void input_init(void) {
#ifdef RPI_BOARD_VIRT
  vin_init();
#else
  usb_init();
#endif
}

static void input_poll(void) {
#ifdef RPI_BOARD_VIRT
  vin_poll();
#else
  usb_poll();
#endif
}

// events: the keys held or pressed since the last call, the wheel since.
// The characters typed meanwhile were game keys, not console input: dropped.
static void input_snapshot(void) {
  input_poll();
  in_chars_tail = in_chars_head;
  for (int k = 0; k < 256; k++) { in_snap[k] = in_down[k] | in_hit[k]; in_hit[k] = 0; }
  in_wheel = in_wheel_acc; in_wheel_acc = 0;
}

static cell_t input_last_key(void) { int k = in_last_key; in_last_key = 0; return k; }
static cell_t input_down(cell_t k) { return in_snap[k & 255] ? -1 : 0; }
