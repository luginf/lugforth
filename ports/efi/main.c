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

// Lugforth as a UEFI application for x86-64 PCs: BOOTX64.EFI on a FAT32 USB
// stick, started from the firmware's boot menu (QEMU: OVMF). It stays in
// UEFI boot services, which give what a PC needs without a driver of ours:
// the screen (GOP framebuffer), the keyboard (the laptop's or a USB one),
// the files of the stick, a timer. The console is the screen, drawn by
// ports/baremetal/console.h, and the COM1 serial port when there is one (QEMU).
// The screen, console, keyboard decoding and sdl2 words are those of
// ports/baremetal; this file gives them their UEFI drivers.

#include <efi.h>
#include <stdint.h>
#include <setjmp.h>

static EFI_SYSTEM_TABLE *ST;
static EFI_BOOT_SERVICES *BS;
static EFI_RUNTIME_SERVICES *RT;
static EFI_HANDLE image_handle;

#include "ports/efi/libc.h"
#include "ports/baremetal/term.h"

#include "common/tier0_opcodes.h"
#include "common/tier1_opcodes.h"
#include "common/tier2_opcodes.h"
#include "common/floats.h"
#include "common/calls.h"

#define HEAP_SIZE (64 * 1024 * 1024)
#define STACK_CELLS (8 * 1024)
#define BOARD_NAME "pc-efi"

static void efi_halt(void) { for (;;) __asm__ volatile("hlt"); }

static inline void outb(uint16_t port, uint8_t v) { __asm__ volatile("outb %0, %1" :: "a"(v), "Nd"(port)); }
static inline uint8_t inb(uint16_t port) { uint8_t v; __asm__ volatile("inb %1, %0" : "=a"(v) : "Nd"(port)); return v; }

// ---- COM1, output only (the firmware's terminal driver reads it into the
// console input). A laptop has none: its status port reads 0xFF. ----
static int com1_ok;

static void com1_init(void) { com1_ok = inb(0x3FD) != 0xFF; }

static void com1_putc(int c) {
  if (!com1_ok) return;
  for (int t = 0; t < 100000 && !(inb(0x3FD) & 0x20); t++) {}   // transmitter empty
  outb(0x3F8, (uint8_t) c);
}

static void uart_write(const unsigned char *s, cell_t n) {
  for (cell_t i = 0; i < n; i++) {
    if (s[i] == '\n') com1_putc('\r');
    com1_putc(s[i]);
  }
}

static void uart_puts(const char *s) { uart_write((const unsigned char *) s, strlen(s)); }

static void uart_puthex(uint64_t n) {
  char b[19] = "0x";
  for (int i = 0; i < 16; i++) b[2 + i] = "0123456789abcdef"[(n >> (60 - 4 * i)) & 15];
  b[18] = 0;
  uart_puts(b);
}

static int uart_getc_nowait(void) { return -1; }   // the serial line comes through the console input

// ---- time: the TSC, measured against the firmware's Stall at start ----
static uint64_t tsc_hz = 1000000000;

static uint64_t rdtsc(void) { uint32_t lo, hi; __asm__ volatile("rdtsc" : "=a"(lo), "=d"(hi)); return ((uint64_t) hi << 32) | lo; }

static void tsc_init(void) {
  uint64_t t0 = rdtsc();
  BS->Stall(50000);
  uint64_t d = rdtsc() - t0;
  if (d > 1000) tsc_hz = d * 20;
}

static uint64_t ms_ticks(void) { uint64_t c = rdtsc(); return (c / tsc_hz) * 1000 + (c % tsc_hz) * 1000 / tsc_hz; }
static uint64_t us_ticks(void) { uint64_t c = rdtsc(); return (c / tsc_hz) * 1000000 + (c % tsc_hz) * 1000000 / tsc_hz; }
static uint64_t timer_freq(void) { return tsc_hz; }

// The date: the PC's battery clock (UEFI GetTime), then counted by the TSC.
static int64_t clock_base;

static int64_t days_from_civil(int64_t y, int m, int d) {
  y -= m <= 2;
  int64_t era = y / 400, yoe = y - era * 400;
  int64_t doy = (153 * (m + (m > 2 ? -3 : 9)) + 2) / 5 + d - 1;
  return era * 146097 + yoe * 365 + yoe / 4 - yoe / 100 + doy - 719468;
}

static int64_t unix_time(void) { return clock_base + (int64_t) (ms_ticks() / 1000); }
static void unix_time_set(cell_t secs) { clock_base = secs - (int64_t) (ms_ticks() / 1000); }

static void clock_init(void) {
  EFI_TIME t;
  int64_t s = 0;
  if (RT->GetTime(&t, NULL) == EFI_SUCCESS) {
    s = days_from_civil(t.Year, t.Month, t.Day) * 86400 + t.Hour * 3600 + t.Minute * 60 + t.Second;
    if (t.TimeZone != EFI_UNSPECIFIED_TIMEZONE) s += t.TimeZone * 60;   // to UTC
  }
  unix_time_set(s);
}

// ---- waiting: the firmware sleeps until a key or the timer ----
static EFI_EVENT wait_timer;
static EFI_EVENT key_event;

static void sleep_us(uint64_t us) {
  if (!wait_timer || us < 100) return;
  BS->SetTimer(wait_timer, TimerRelative, us * 10);        // 100 ns units
  EFI_EVENT ev[2] = { wait_timer, key_event };
  UINTN index;
  BS->WaitForEvent(key_event ? 2 : 1, ev, &index);
  BS->SetTimer(wait_timer, TimerCancel, 0);
}

static int console_extra_char(void);   // the keyboard's, through input.h
static void con_sync(void);            // console.h

static int getchar_timeout_us(cell_t us) {
  if (!TERM_FOCUSED()) return -1;                  // the keyboard is the focused terminal's
  uint64_t end = us_ticks() + (uint64_t) us;
  for (;;) {
    con_sync();      // first: a slow poll or a short timeout must not keep the output hidden until the next key
    int c = console_extra_char();
    if (c >= 0) return c;
    uint64_t now = us_ticks();
    if (now >= end) return -1;
    sleep_us(end - now < 10000 ? end - now : 10000);
  }
}

static void efi_terminate(cell_t code) {
  (void) code;
  RT->ResetSystem(EfiResetShutdown, EFI_SUCCESS, 0, NULL);
  efi_halt();
}

// ---- faults: the processor's exceptions become a THROW. The firmware's
// IDT is copied (its timer interrupts must keep working) and the entries
// of the exceptions replaced by those of start.S. ----
static jmp_buf g_forth_fault;
static int g_forth_signal;
static int g_faults_ready, g_in_fault;

#define FAULT_ENTRY \
  if (setjmp(g_forth_fault)) { THROWIT(g_forth_signal); }

static void forth_faults_setup(void) { g_faults_ready = 1; }

__attribute__((force_align_arg_pointer))
static void efi_fault_resume(void) {
  g_in_fault = 0;
  longjmp(g_forth_fault, 1);
}

uint64_t efi_exception(uint64_t vec, uint64_t err, uint64_t rip, uint64_t cr2) {
  uart_puts("\nlugforth: fault "); uart_puthex(vec);
  uart_puts(" error "); uart_puthex(err);
  uart_puts(" at "); uart_puthex(rip);
  if (vec == 14) { uart_puts(" address "); uart_puthex(cr2); }
  uart_puts("\n");
  static uint64_t burst_start;
  static int burst;
  uint64_t now = ms_ticks();
  if (now - burst_start > 1000) { burst_start = now; burst = 0; }
  if (!g_faults_ready || g_in_fault || ++burst > 50) {
    uart_puts("lugforth: halted\n");
    efi_halt();
  }
  g_in_fault = 1;
  switch (vec) {
    case 0: g_forth_signal = -10; break;             // division by zero
    case 17: g_forth_signal = -23; break;            // alignment
    case 6: g_forth_signal = -256 - 4; break;        // invalid opcode, as SIGILL
    default: g_forth_signal = -9; break;             // invalid address
  }
  return (uint64_t) efi_fault_resume;
}

extern char exc_0[], exc_6[], exc_12[], exc_13[], exc_14[], exc_16[], exc_17[], exc_19[];

static void faults_install(void) {
  struct __attribute__((packed)) { uint16_t limit; uint64_t base; } idtr;
  __asm__ volatile("sidt %0" : "=m"(idtr));
  uint8_t *idt = malloc(idtr.limit + 1 + 16);
  if (!idt) return;
  idt = (uint8_t *) (((uint64_t) idt + 15) & ~15UL);
  memcpy(idt, (void *) idtr.base, idtr.limit + 1);
  uint16_t cs;
  __asm__ volatile("mov %%cs, %0" : "=r"(cs));
  struct { int vec; char *entry; } e[] = {
    {0, exc_0}, {6, exc_6}, {12, exc_12}, {13, exc_13}, {14, exc_14}, {16, exc_16}, {17, exc_17}, {19, exc_19},
  };
  for (unsigned k = 0; k < sizeof e / sizeof e[0]; k++) {
    uint64_t a = (uint64_t) e[k].entry;
    uint8_t *g = idt + 16 * e[k].vec;
    if ((unsigned) (16 * e[k].vec + 15) > idtr.limit) continue;
    g[0] = (uint8_t) a; g[1] = (uint8_t) (a >> 8);
    g[2] = (uint8_t) cs; g[3] = (uint8_t) (cs >> 8);
    g[4] = 0;                                         // IST
    g[5] = 0x8E;                                      // present, interrupt gate
    g[6] = (uint8_t) (a >> 16); g[7] = (uint8_t) (a >> 24);
    g[8] = (uint8_t) (a >> 32); g[9] = (uint8_t) (a >> 40);
    g[10] = (uint8_t) (a >> 48); g[11] = (uint8_t) (a >> 56);
    g[12] = g[13] = g[14] = g[15] = 0;
  }
  idtr.base = (uint64_t) idt;
  __asm__ volatile("lidt %0" :: "m"(idtr));
}

// ---- the screen: the GOP framebuffer. A request smaller than the screen
// is centered in it (the rest black); screen.h and console.h draw through
// fb_mem and fb_pitch as on the Pi. ----
#define FB_HW_EXTERNAL
static EFI_GRAPHICS_OUTPUT_PROTOCOL *gop;
static uint32_t gop_w, gop_h;

#include "ports/baremetal/screen.h"

static int fb_hw_open(uint32_t pw, uint32_t ph) {
  if (!gop || pw > gop_w || ph > gop_h) return -1;
  if (!TERM_FOCUSED()) return 0;                          // the screen is the focused terminal's
  EFI_GRAPHICS_OUTPUT_MODE_INFORMATION *info = gop->Mode->Info;
  uint32_t pitch = info->PixelsPerScanLine * 4;
  volatile uint8_t *base = (volatile uint8_t *) gop->Mode->FrameBufferBase;
  for (uint32_t y = 0; y < gop_h; y++) {                 // all black
    volatile uint32_t *row = (volatile uint32_t *) (base + (uint64_t) y * pitch);
    for (uint32_t x = 0; x < gop_w; x++) row[x] = 0;
  }
  fb_pitch = pitch;
  fb_mem = (volatile uint32_t *) (base + (uint64_t) ((gop_h - ph) / 2) * pitch + (gop_w - pw) / 2 * 4);
  fb_rgb = info->PixelFormat == PixelRedGreenBlueReserved8BitPerColor;
  return 0;
}

static void gop_init(void) {
  EFI_GUID guid = EFI_GRAPHICS_OUTPUT_PROTOCOL_GUID;
  if (BS->LocateProtocol(&guid, NULL, (void **) &gop) != EFI_SUCCESS) { gop = 0; return; }
  EFI_GRAPHICS_OUTPUT_MODE_INFORMATION *info;
  UINTN size;
  // Below 1280x960 (QEMU's 1280x800, say), take the smallest mode that holds
  // it, for a console of 80x30 at zoom 2. A laptop's own mode is kept.
  if (gop->Mode->Info->HorizontalResolution < 1280 || gop->Mode->Info->VerticalResolution < 960) {
    int best = -1;
    uint64_t best_area = ~0ULL;
    for (UINT32 m = 0; m < gop->Mode->MaxMode; m++) {
      if (gop->QueryMode(gop, m, &size, &info) != EFI_SUCCESS) continue;
      if (info->PixelFormat != PixelBlueGreenRedReserved8BitPerColor &&
          info->PixelFormat != PixelRedGreenBlueReserved8BitPerColor) continue;
      uint64_t a = (uint64_t) info->HorizontalResolution * info->VerticalResolution;
      if (info->HorizontalResolution >= 1280 && info->VerticalResolution >= 960 && a < best_area) {
        best = (int) m; best_area = a;
      }
    }
    if (best >= 0) gop->SetMode(gop, (UINT32) best);
  }
  info = gop->Mode->Info;
  if (info->PixelFormat != PixelBlueGreenRedReserved8BitPerColor &&
      info->PixelFormat != PixelRedGreenBlueReserved8BitPerColor) { gop = 0; return; }
  gop_w = info->HorizontalResolution;
  gop_h = info->VerticalResolution;
}

#define CON_ZOOM 2
#include "ports/baremetal/console.h"

// The console's zoom from the screen: the largest of 1 to 3 that fits.
static void con_zoom_fit(void) {
  int z = 3;
  while (z > 1 && (640u * z > gop_w || 480u * z > gop_h)) z--;
  con_zoom = z;
}

// ---- the keyboard: the firmware's console input (the laptop's keyboard,
// a USB one, and in QEMU the serial line too) turned into Linux key codes
// for input.h, which makes console characters (AZERTY too) and the key
// state of the sdl2 words. The firmware gives key presses, repeated while
// held, but no releases: a key counts as held for KEY_HOLD_MS after its
// last press, as a key typed on a serial terminal. ----
#include "ports/baremetal/input.h"

#define KEY_HOLD_MS 120
static EFI_SIMPLE_TEXT_INPUT_EX_PROTOCOL *kbd_ex;
static uint64_t key_until[128];       // when each held key is let go
static uint8_t char_code[128], char_shift[128];

static void ptr_init(void);

static void usb_init(void) {
  EFI_GUID guid = EFI_SIMPLE_TEXT_INPUT_EX_PROTOCOL_GUID;
  if (BS->HandleProtocol(ST->ConsoleInHandle, &guid, (void **) &kbd_ex) != EFI_SUCCESS) kbd_ex = 0;
  key_event = kbd_ex ? kbd_ex->WaitForKeyEx : ST->ConIn->WaitForKey;
  ptr_init();
  for (int code = 63; code > 0; code--) {             // a character -> the key typing it
    unsigned char p = (unsigned char) key_plain[code], s = (unsigned char) key_shift[code];
    if (p && p < 128) { char_code[p] = (uint8_t) code; char_shift[p] = 0; }
    if (s && s < 128 && s != p) { char_code[s] = (uint8_t) code; char_shift[s] = 1; }
  }
  char_code[13] = 28; char_code[8] = 14; char_code[127] = 14; char_code[9] = 15; char_code[27] = 1;
}

static int scan_to_code(int scan) {
  static const uint8_t codes[] = {
    0, KEY_UP, KEY_DOWN, KEY_RIGHT, KEY_LEFT, KEY_HOME, KEY_END, KEY_INS, KEY_DEL, KEY_PGUP, KEY_PGDN,
    59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 87, 88, 1,   // F1..F12, Esc (0x17)
  };
  return scan > 0 && scan < (int) sizeof codes ? codes[scan] : 0;
}

static void key_press(int code, int shift, int ctrl, int alt, int altgr) {
  if (shift) input_key(KEY_LEFTSHIFT, 1);
  if (ctrl) input_key(KEY_LEFTCTRL, 1);
  if (alt) input_key(KEY_LEFTALT, 1);
  if (altgr) input_key(KEY_RIGHTALT, 1);
  input_key(code, in_down[key_sdl_code(code)] ? 2 : 1);
  if (key_sdl_code(code)) in_hit[key_sdl_code(code)] = 1;
  if (code < 128) key_until[code] = ms_ticks() + KEY_HOLD_MS;
  if (altgr) input_key(KEY_RIGHTALT, 0);
  if (alt) input_key(KEY_LEFTALT, 0);
  if (ctrl) input_key(KEY_LEFTCTRL, 0);
  if (shift) input_key(KEY_LEFTSHIFT, 0);
}

// ---- the mouse: every pointer the firmware has (a USB mouse; a touchpad or
// touchscreen when the firmware exposes it, which many laptops' I2C ones are
// not). Relative motion is accumulated in screen pixels, and handed to
// input.h in whole logical pixels. ----
#define PTR_MAX 8
static EFI_SIMPLE_POINTER_PROTOCOL *rel_ptr[PTR_MAX];
static EFI_ABSOLUTE_POINTER_PROTOCOL *abs_ptr[PTR_MAX];
static int rel_count, abs_count;
static int ptr_gain = 10;             // device counts -> screen pixels, in percent (MOUSE-SPEED)
static int64_t ptr_acc[2];
static int ptr_left, ptr_right;
static int ptr_rel_seen;              // a relative pointer has reported: absolute ones are then ignored
static int64_t ptr_dbg[12];            // PTR-STAT: last raw values, to see what the firmware reports

static void ptr_init(void) {
  EFI_GUID rg = EFI_SIMPLE_POINTER_PROTOCOL_GUID, ag = EFI_ABSOLUTE_POINTER_PROTOCOL_GUID;
  EFI_HANDLE *h;
  UINTN n;
  if (BS->LocateHandleBuffer(ByProtocol, &rg, NULL, &n, &h) == EFI_SUCCESS) {
    for (UINTN i = 0; i < n && rel_count < PTR_MAX; i++) {
      EFI_SIMPLE_POINTER_PROTOCOL *p;
      if (BS->HandleProtocol(h[i], &rg, (void **) &p) != EFI_SUCCESS) continue;
      p->Reset(p, 0);
      rel_ptr[rel_count++] = p;
    }
    BS->FreePool(h);
  }
  if (BS->LocateHandleBuffer(ByProtocol, &ag, NULL, &n, &h) == EFI_SUCCESS) {
    for (UINTN i = 0; i < n && abs_count < PTR_MAX; i++) {
      EFI_ABSOLUTE_POINTER_PROTOCOL *p;
      if (BS->HandleProtocol(h[i], &ag, (void **) &p) != EFI_SUCCESS) continue;
      p->Reset(p, 0);
      abs_ptr[abs_count++] = p;
    }
    BS->FreePool(h);
  }
}

static void ptr_buttons(int left, int right) {
  if (left != ptr_left) { ptr_left = left; input_key(BTN_LEFT, left); }
  if (right != ptr_right) { ptr_right = right; input_key(BTN_RIGHT, right); }
}

static void ptr_poll(void) {
  for (int i = 0; i < rel_count; i++) {
    EFI_SIMPLE_POINTER_STATE st;
    int moved = 0;
    while (rel_ptr[i]->GetState(rel_ptr[i], &st) == EFI_SUCCESS) {
      int64_t dx = st.RelativeMovementX, dy = st.RelativeMovementY;
      ptr_dbg[0] = dx; ptr_dbg[1] = dy; ptr_dbg[2]++;
      // Counts per millimetre of the device: EDK2's USB mouse says 8 and reports
      // small counts; a firmware that reports 16384 says so here. Scale to the 8.
      UINT64 rx = rel_ptr[i]->Mode->ResolutionX, ry = rel_ptr[i]->Mode->ResolutionY;
      ptr_dbg[8] = rx; ptr_dbg[9] = ry;
      if (rx > 8) dx = dx * 8 / (int64_t) rx;
      if (ry > 8) dy = dy * 8 / (int64_t) ry;
      if (dx || dy) ptr_rel_seen = 1;
      if (dx > 1000) dx = 1000; else if (dx < -1000) dx = -1000;     // garbage from a firmware
      if (dy > 1000) dy = 1000; else if (dy < -1000) dy = -1000;
      ptr_acc[0] += dx * ptr_gain;      // in 1/100 pixel
      ptr_acc[1] += dy * ptr_gain;
      ptr_buttons(st.LeftButton != 0, st.RightButton != 0);
      moved = 1;
    }
    if (moved) {
      int z = fb_zoom ? fb_zoom : 1;
      for (int a = 0; a < 2; a++) {
        int64_t d = ptr_acc[a] / (100 * z) * z;       // whole logical pixels
        if (d) { input_rel(a, d); ptr_acc[a] -= d * 100; }
      }
      in_mouse_seen = 1;
    }
  }
  for (int i = 0; i < abs_count; i++) {
    EFI_ABSOLUTE_POINTER_STATE st;
    if (ptr_rel_seen) {                  // a mouse is moving: the same device may also show up as an absolute one
      while (abs_ptr[i]->GetState(abs_ptr[i], &st) == EFI_SUCCESS) { ptr_dbg[5]++; }
      continue;
    }
    EFI_ABSOLUTE_POINTER_MODE *m = abs_ptr[i]->Mode;
    while (abs_ptr[i]->GetState(abs_ptr[i], &st) == EFI_SUCCESS) {
      ptr_dbg[3] = st.CurrentX; ptr_dbg[4] = st.CurrentY; ptr_dbg[5]++;
      ptr_dbg[6] = m->AbsoluteMaxX; ptr_dbg[7] = m->AbsoluteMaxY;
      if (m->AbsoluteMaxX > m->AbsoluteMinX && m->AbsoluteMaxY > m->AbsoluteMinY) {
        int64_t ax = ((int64_t) st.CurrentX - m->AbsoluteMinX) * 32767 / (int64_t) (m->AbsoluteMaxX - m->AbsoluteMinX);
        int64_t ay = ((int64_t) st.CurrentY - m->AbsoluteMinY) * 32767 / (int64_t) (m->AbsoluteMaxY - m->AbsoluteMinY);
        input_abs(0, ax < 0 ? 0 : ax > 32767 ? 32767 : ax);
        input_abs(1, ay < 0 ? 0 : ay > 32767 ? 32767 : ay);
      }
      ptr_buttons((st.ActiveButtons & 1) != 0, (st.ActiveButtons & 2) != 0);
    }
  }
}

static void usb_poll(void) {
  ptr_poll();
  uint64_t now = ms_ticks();
  for (int code = 1; code < 128; code++) {             // held keys let go
    if (key_until[code] && now >= key_until[code]) { key_until[code] = 0; input_key(code, 0); }
  }
  // Only while the console's queue has room (a key may give 7 bytes): the
  // keys not read yet wait in the firmware's own buffer.
  while (in_chars_head - in_chars_tail + 16 <= sizeof in_chars) {
    EFI_KEY_DATA kd;
    memset(&kd, 0, sizeof kd);
    if (kbd_ex) {
      if (kbd_ex->ReadKeyStrokeEx(kbd_ex, &kd) != EFI_SUCCESS) break;
    } else if (ST->ConIn->ReadKeyStroke(ST->ConIn, &kd.Key) != EFI_SUCCESS) {
      break;
    }
    UINT32 st = kd.KeyState.KeyShiftState;
    int valid = (st & EFI_SHIFT_STATE_VALID) != 0;
    int ctrl = valid && (st & (EFI_LEFT_CONTROL_PRESSED | EFI_RIGHT_CONTROL_PRESSED));
    int alt = valid && (st & EFI_LEFT_ALT_PRESSED);
    int altgr = valid && (st & EFI_RIGHT_ALT_PRESSED);
    int c = kd.Key.UnicodeChar, code = 0, shift = 0;
    if (kd.Key.ScanCode) code = scan_to_code(kd.Key.ScanCode);
    else if (c >= 1 && c <= 26 && c != 8 && c != 9 && c != 13) {   // Ctrl+letter
      code = char_code['a' + c - 1]; ctrl = 1;
    } else if (c > 0 && c < 128) {
      code = char_code[c]; shift = char_shift[c];
    } else if (c >= 128) {                             // beyond ASCII: as typed, to the console
      unsigned char u[3];
      int k = 0;
      if (c < 0x800) { u[k++] = (unsigned char) (0xC0 | (c >> 6)); }
      else { u[k++] = (unsigned char) (0xE0 | (c >> 12)); u[k++] = (unsigned char) (0x80 | ((c >> 6) & 63)); }
      u[k++] = (unsigned char) (0x80 | (c & 63));
      for (int i = 0; i < k; i++) { in_put_text(u[i]); in_put_char(u[i]); }
      continue;
    }
    if (code) key_press(code, shift, ctrl, alt, altgr);
  }
}

// A screen opened: the keys hit before (the prompt's typing, the Esc that
// ended the last game, which the firmware's serial terminal may give late)
// are not the new program's. Nor are the keys still counted as held: UEFI
// gives no releases, so the last ones typed (the Enter, the spaces of the
// line that started the program) would stay held KEY_HOLD_MS more.
static void input_flush(void) {
  usb_poll();
  for (int code = 1; code < 128; code++) {
    if (key_until[code]) { key_until[code] = 0; input_key(code, 0); }
  }
  memset(in_hit, 0, sizeof in_hit);
  memset(in_snap, 0, sizeof in_snap);
  in_last_key = 0;
}

// ---- files: the UEFI file system of the device we were started from ----
#include "ports/efi/files.h"

// ---- sound: Intel HD Audio (hda.h), over the shared mixer ----
#include "ports/efi/hda.h"

static int console_extra_char(void) { snd_pump(); if (!TERM_FOCUSED()) return -1; input_poll(); return in_get_char(); }

#include "ports/efi/builtins.h"

#include "common/bits.h"
#include "common/core.h"
#include "common/calling.h"
#include "common/interp.h"
#include "gen/efi_boot.h"
#include "ports/baremetal/term_run.h"

// The firmware binds some drivers (USB mice and touchpads) only when
// something asks, as the shell's `connect -r` does.
static void connect_all(void) {
  EFI_HANDLE *h;
  UINTN n;
  if (BS->LocateHandleBuffer(AllHandles, NULL, NULL, &n, &h) != EFI_SUCCESS) return;
  for (UINTN i = 0; i < n; i++) BS->ConnectController(h[i], NULL, NULL, TRUE);
  BS->FreePool(h);
}

// Called by gnu-efi's crt0 (after its relocation pass) with the System V convention.
EFI_STATUS efi_main(EFI_HANDLE image, EFI_SYSTEM_TABLE *systab) {
  image_handle = image;
  ST = systab; BS = systab->BootServices; RT = systab->RuntimeServices;
  libc_bs = BS;
  BS->SetWatchdogTimer(0, 0, 0, NULL);                 // else the firmware resets in 5 minutes
  BS->CreateEvent(EVT_TIMER, 0, NULL, NULL, &wait_timer);
  com1_init();
  tsc_init();
  clock_init();
  connect_all();                                       // before the screen: a driver may change it
  gop_init();
  input_init();                                        // the keyboard: usb_init below
  fs_init();
  faults_install();
  void *heap = malloc(HEAP_SIZE);
  if (!heap) { uart_puts("lugforth: no memory for the heap\n"); efi_halt(); }
  if (gop) { con_zoom_fit(); con_init(); }
  static char arg0[] = "lugforth";
  static char *argv[] = { arg0, 0 };
  forth_init(1, argv, heap, HEAP_SIZE, boot, sizeof(boot));
  term_boot();
  for (;;) { g_sys->rp = forth_run(g_sys->rp); term_next(); }
  return EFI_SUCCESS;
}
