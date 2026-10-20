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

// Lugforth on bare-metal AArch64, no operating system: QEMU's `virt`
// machine, QEMU's `raspi3b` (and a real Raspberry Pi 3), a Raspberry Pi 4
// (written without one: see README.md). Same kernel (common/*.h) and boot
// chain shape as ports/pico; the C library is picolibc, the console is the
// PL011 UART. start.S drops to EL1 and calls rpi_start() below.

#include <stdio.h>
#include <setjmp.h>
#include <stdint.h>

#include "ports/baremetal/term.h"
#include "common/tier0_opcodes.h"
#include "common/tier1_opcodes.h"
#include "common/tier2_opcodes.h"
#include "common/floats.h"
#include "common/calls.h"

#define HEAP_SIZE (16 * 1024 * 1024)
#define STACK_CELLS (8 * 1024)

// ---- the boards. RAM_LIMIT is only for the page tables: what is mapped
// as cacheable memory; the C heap ends at __ram_end (Makefile). ----
#if defined(RPI_BOARD_VIRT)
# define BOARD_NAME "qemu-virt"
# define UART_BASE 0x09000000UL
# define GPIO_BASE 0UL           // virt has a PL061, not the Pi's GPIO
# define PM_BASE 0UL
#elif defined(RPI_BOARD_PI3)
# define BOARD_NAME "pi3"
# define PERIPHERAL_BASE 0x3F000000UL
#elif defined(RPI_BOARD_PI4)
# define BOARD_NAME "pi4"
# define PERIPHERAL_BASE 0xFE000000UL
#else
# error "define RPI_BOARD_VIRT, RPI_BOARD_PI3 or RPI_BOARD_PI4"
#endif
#ifdef PERIPHERAL_BASE
# define UART_BASE (PERIPHERAL_BASE + 0x201000UL)
# define GPIO_BASE (PERIPHERAL_BASE + 0x200000UL)
# define PM_BASE (PERIPHERAL_BASE + 0x100000UL)
#endif

#define REG(a) (*(volatile uint32_t *) (a))

// ---- memory map: identity mapping of the first 4G in 2M blocks. The MMU
// is not optional: with it off, every access is a Device access, and an
// unaligned one faults (picolibc's memcpy does them, Forth's @ can). ----
enum { MAP_NONE, MAP_NORMAL, MAP_DEVICE };

static int map_kind(uint64_t a) {
#if defined(RPI_BOARD_VIRT)
  if (a < 0x40000000UL) return MAP_DEVICE;    // flash, GIC, UART, virtio...
  if (a < 0x80000000UL) return MAP_NORMAL;    // RAM (-m up to 1G)
  return MAP_NONE;
#elif defined(RPI_BOARD_PI3)
  if (a < 0x3F000000UL) return MAP_NORMAL;
  if (a < 0x40200000UL) return MAP_DEVICE;    // peripherals, local timer/IRQs
  return MAP_NONE;
#else
  if (a < 0x40000000UL) return MAP_NORMAL;    // the first 1G, every model has it
  if (a >= 0xFC000000UL) return MAP_DEVICE;   // peripherals, low-peripheral mode
  return MAP_NONE;
#endif
}

static uint64_t page_l1[512] __attribute__((aligned(4096)));
static uint64_t page_l2[4][512] __attribute__((aligned(4096)));

// Runs with the MMU off: aligned stores only, no library call (hence no
// memset generated from a loop).
__attribute__((optimize("no-tree-loop-distribute-patterns")))
static void mmu_init(void) {
  for (int g = 0; g < 4; g++) {
    for (int i = 0; i < 512; i++) {
      uint64_t a = ((uint64_t) g << 30) | ((uint64_t) i << 21);
      uint64_t d;
      switch (map_kind(a)) {
        case MAP_NORMAL:   // AttrIndx 1, inner shareable, AF
          d = a | (1 << 2) | (3 << 8) | (1 << 10) | 1; break;
        case MAP_DEVICE:   // AttrIndx 0, AF, never executed
          d = a | (0 << 2) | (1 << 10) | (3ULL << 53) | 1; break;
        default:
          d = 0; break;
      }
      page_l2[g][i] = d;
    }
    page_l1[g] = (uint64_t) page_l2[g] | 3;
  }
  uint64_t mair = 0x00 | (0xFFULL << 8);  // 0: Device-nGnRnE, 1: Normal WB
  uint64_t tcr = 25                        // T0SZ: 39-bit VA, starts at level 1
               | (1 << 8) | (1 << 10)      // inner/outer write-back
               | (3 << 12)                 // inner shareable
               | (0 << 14)                 // 4K granule
               | (1 << 23)                 // no TTBR1 walks
               | (1ULL << 32);             // IPS: 36-bit physical
  __asm__ volatile(
    "msr mair_el1, %0\n"
    "msr tcr_el1, %1\n"
    "msr ttbr0_el1, %2\n"
    "isb\n"
    "tlbi vmalle1\n"
    "dsb ish\n"
    "isb\n" :: "r"(mair), "r"(tcr), "r"(page_l1) : "memory");
  uint64_t sctlr;
  __asm__ volatile("mrs %0, sctlr_el1" : "=r"(sctlr));
  sctlr |= (1 << 0) | (1 << 2) | (1 << 12);  // MMU, data cache, instruction cache
  sctlr &= ~(uint64_t) (1 << 1);             // no alignment check
  __asm__ volatile("msr sctlr_el1, %0\nisb" :: "r"(sctlr) : "memory");
}

// ---- PL011 UART: 115200 8N1 on the Pi (48 MHz UART clock, the firmware's
// default), whatever QEMU gives on virt. ----
static void uart_init(void) {
#ifndef PERIPHERAL_BASE
  REG(UART_BASE + 0x2C) = (3 << 5) | (1 << 4);      // LCRH: 8 bits, FIFOs (QEMU: 16 bytes at a time)
#else
  REG(UART_BASE + 0x30) = 0;                        // CR: off
  uint32_t sel = REG(GPIO_BASE + 0x04);             // GPFSEL1: GPIO 14 and 15
  sel &= ~((7u << 12) | (7u << 15));
  sel |= (4u << 12) | (4u << 15);                   // ALT0 = TXD0/RXD0
  REG(GPIO_BASE + 0x04) = sel;
# ifdef RPI_BOARD_PI3
  REG(GPIO_BASE + 0x94) = 0;                        // GPPUD: no pull
  for (volatile int i = 0; i < 300; i++) {}
  REG(GPIO_BASE + 0x98) = (1u << 14) | (1u << 15);  // GPPUDCLK0
  for (volatile int i = 0; i < 300; i++) {}
  REG(GPIO_BASE + 0x98) = 0;
# else
  REG(GPIO_BASE + 0xE4) &= ~(0xFu << 28);           // GPIO_PUP_PDN_CNTRL_REG0
# endif
  REG(UART_BASE + 0x44) = 0x7FF;                    // ICR: clear interrupts
  REG(UART_BASE + 0x24) = 26;                       // IBRD \ 48e6 / (16 * 115200)
  REG(UART_BASE + 0x28) = 3;                        // FBRD /   = 26.042
  REG(UART_BASE + 0x2C) = (3 << 5) | (1 << 4);      // LCRH: 8 bits, FIFOs
  REG(UART_BASE + 0x30) = (1 << 9) | (1 << 8) | 1;  // CR: RX, TX, on
#endif
}

static void uart_putc(int c) {
  while (REG(UART_BASE + 0x18) & (1 << 5)) {}       // FR.TXFF
  REG(UART_BASE) = (uint32_t) c;
}

static void uart_write(const unsigned char *s, cell_t n) {
  for (cell_t i = 0; i < n; i++) {
    if (s[i] == '\n') uart_putc('\r');
    uart_putc(s[i]);
  }
}

static void uart_puts(const char *s) {
  uart_write((const unsigned char *) s, strlen(s));
}

static void uart_puthex(uint64_t n) {
  char b[19] = "0x";
  for (int i = 0; i < 16; i++) b[2 + i] = "0123456789abcdef"[(n >> (60 - 4 * i)) & 15];
  b[18] = 0;
  uart_puts(b);
}

static int uart_getc_nowait(void) {
  if (REG(UART_BASE + 0x18) & (1 << 4)) return -1;  // FR.RXFE
  return REG(UART_BASE) & 0xFF;
}

// ---- generic timer ----
static uint64_t timer_count(void) {
  uint64_t c;
  __asm__ volatile("isb\nmrs %0, cntvct_el0" : "=r"(c));
  return c;
}

static uint64_t timer_freq(void) {
  uint64_t f;
  __asm__ volatile("mrs %0, cntfrq_el0" : "=r"(f));
  return f ? f : 1;
}

static uint64_t ms_ticks(void) {
  uint64_t c = timer_count(), f = timer_freq();
  return (c / f) * 1000 + (c % f) * 1000 / f;
}

static uint64_t us_ticks(void) {
  uint64_t c = timer_count(), f = timer_freq();
  return (c / f) * 1000000 + (c % f) * 1000000 / f;
}

// ---- the date and time: a counter on the timer. The Pi has no battery clock:
// it starts from the day the kernel was built, and UNIX-TIME! sets it. QEMU
// virt has a PL031 RTC, read once at the start (the host's UTC time). ----
static int64_t clock_base;          // Unix seconds when the timer read 0

static int64_t days_from_civil(int64_t y, int m, int d) {
  y -= m <= 2;
  int64_t era = y / 400, yoe = y - era * 400;
  int64_t doy = (153 * (m + (m > 2 ? -3 : 9)) + 2) / 5 + d - 1;
  return era * 146097 + yoe * 365 + yoe / 4 - yoe / 100 + doy - 719468;
}

static int64_t build_epoch(void) {              // __DATE__ "Oct 17 2026", __TIME__ "12:34:56"
  static const char *mon = "JanFebMarAprMayJunJulAugSepOctNovDec";
  const char *d = __DATE__, *t = __TIME__;
  int m = 1;
  for (int i = 0; i < 12; i++) if (!memcmp(d, mon + 3 * i, 3)) m = i + 1;
  int day = (d[4] == ' ' ? 0 : d[4] - '0') * 10 + (d[5] - '0');
  int y = (d[7] - '0') * 1000 + (d[8] - '0') * 100 + (d[9] - '0') * 10 + (d[10] - '0');
  return days_from_civil(y, m, day) * 86400 + ((t[0] - '0') * 10 + (t[1] - '0')) * 3600 +
         ((t[3] - '0') * 10 + (t[4] - '0')) * 60 + (t[6] - '0') * 10 + (t[7] - '0');
}

static int64_t unix_time(void) { return clock_base + (int64_t) (ms_ticks() / 1000); }
static void unix_time_set(cell_t secs) { clock_base = secs - (int64_t) (ms_ticks() / 1000); }

static void clock_init(void) {
  int64_t t = 0;
#if defined(RPI_BOARD_VIRT)
  t = REG(0x09010000UL);                        // PL031 DR
#endif
  unix_time_set(t > 1600000000 ? t : build_epoch());
}

// ---- sleeping while waiting for a key: the virtual timer's interrupt
// line wakes WFI. Interrupts stay masked (DAIF), nothing is ever taken:
// WFI wakes on a pending interrupt whatever the mask, and stopping the
// timer lowers the line again. Without this, QEMU spins at 100% CPU at
// the prompt. The line only has to reach the core: through the GIC on
// virt and the Pi 4 (PPI 27), the local interrupt controller on the Pi 3. ----
#if defined(RPI_BOARD_VIRT)
# define GICD_BASE 0x08000000UL
# define GICC_BASE 0x08010000UL
#elif defined(RPI_BOARD_PI4)
# define GICD_BASE 0xFF841000UL
# define GICC_BASE 0xFF842000UL
#endif

static void wake_init(void) {
#ifdef GICD_BASE
  REG(GICD_BASE + 0x000) = 1;                // GICD_CTLR: forward interrupts
  REG(GICD_BASE + 0x100) = 1u << 27;         // GICD_ISENABLER0: PPI 27
  REG(GICC_BASE + 0x004) = 0xFF;             // GICC_PMR: every priority
  REG(GICC_BASE + 0x000) = 1;                // GICC_CTLR: signal the core
#else
  REG(0x40000040) = 1u << 3;                 // core 0 timer control: CNTV IRQ
#endif
}

static void sleep_us(uint64_t us) {
  uint64_t ticks = us * timer_freq() / 1000000;
  if (!ticks) return;
  __asm__ volatile(
    "msr cntv_tval_el0, %0\n"
    "mov x1, #1\n"
    "msr cntv_ctl_el0, x1\n"                // enabled, not masked
    "isb\n"
    "wfi\n"
    "msr cntv_ctl_el0, xzr\n"
    "isb\n" :: "r"(ticks) : "x1", "memory");
}

// A character, or -1 after `us` microseconds without one. Sleeps in
// slices of at most 1 ms: a key waits that long at worst.
static int console_extra_char(void);   // a keyboard's, input.h

static void con_sync(void);            // the screen console, console.h

static int getchar_timeout_us(cell_t us) {
  if (!TERM_FOCUSED()) return -1;                  // the keyboard is the focused terminal's
  uint64_t end = us_ticks() + (uint64_t) us;
  for (;;) {
    con_sync();      // first: a slow poll or a short timeout must not keep the output hidden until the next key
    int c = uart_getc_nowait();
    if (c < 0) c = console_extra_char();
    if (c >= 0) return c;
    uint64_t now = us_ticks();
    if (now >= end) return -1;
    sleep_us(end - now < 1000 ? end - now : 1000);
  }
}

// ---- leaving: PSCI SYSTEM_OFF on virt (QEMU exits), a watchdog reset on
// the Pi (QEMU with -no-reboot exits, a real board restarts). ----
static void rpi_halt(void) {
  for (;;) { __asm__ volatile("wfe"); }
}

static void rpi_terminate(cell_t code) {
  (void) code;
#if defined(RPI_BOARD_VIRT)
  register uint64_t x0 __asm__("x0") = 0x84000008;  // PSCI SYSTEM_OFF
  __asm__ volatile("hvc #0" : "+r"(x0) :: "memory");
#else
  REG(PM_BASE + 0x24) = 0x5A000000 | 10;            // PM_WDOG: 10 ticks
  REG(PM_BASE + 0x1C) = 0x5A000000 | 0x20;          // PM_RSTC: full reset
#endif
  rpi_halt();
}

// ---- faults: a synchronous exception (data abort...) becomes a THROW, as
// a signal does in ports/posix/faults.h. start.S's vectors call
// rpi_exception(), which returns where to resume: rpi_fault_resume, which
// longjmps into forth_run. Anything else is printed and halts. ----
static jmp_buf g_forth_fault;
static int g_forth_signal;
static int g_faults_ready;
static int g_in_fault;

#define FAULT_ENTRY \
  if (setjmp(g_forth_fault)) { THROWIT(g_forth_signal); }

static void forth_faults_setup(void) {
  g_faults_ready = 1;
}

static void rpi_fault_resume(void) {
  g_in_fault = 0;
  longjmp(g_forth_fault, 1);
}

uint64_t rpi_exception(uint64_t kind, uint64_t esr, uint64_t elr, uint64_t far) {
  uint64_t ec = esr >> 26;
  uart_puts("\nlugforth: fault ESR="); uart_puthex(esr);
  uart_puts(" ELR="); uart_puthex(elr);
  uart_puts(" FAR="); uart_puthex(far);
  uart_puts("\n");
  // kind 4: synchronous, from EL1 itself, with SP_EL1 (what Forth runs as).
  if (kind != 4 || !g_faults_ready || g_in_fault) {
    uart_puts("lugforth: halted, vector "); uart_puthex(kind); uart_puts("\n");
    rpi_halt();
  }
  // A fault that comes back at once, again and again (a broken boot, where
  // each throw faults anew): stop instead of filling the console.
  static uint64_t burst_start;
  static int burst;
  uint64_t now = ms_ticks();
  if (now - burst_start > 1000) { burst_start = now; burst = 0; }
  if (++burst > 50) {
    uart_puts("lugforth: too many faults, halted\n");
    rpi_halt();
  }
  g_in_fault = 1;
  switch (ec) {
    case 0x20: case 0x21: case 0x24: case 0x25:      // instruction/data abort
      g_forth_signal = ((esr & 0x3F) == 0x21) ? -23 : -9;  // alignment fault: -23
      break;
    case 0x22: case 0x26: g_forth_signal = -23; break;   // PC / SP alignment
    default: g_forth_signal = -256 - (int) ec; break;
  }
  return (uint64_t) rpi_fault_resume;
}

// ---- picolibc glue: stdio on the UART, TLS. ----
static int stdio_putc(char c, FILE *f) {
  (void) f;
  if (c == '\n') uart_putc('\r');
  uart_putc((unsigned char) c);
  return (unsigned char) c;
}

static int stdio_getc(FILE *f) {
  (void) f;
  int c;
  while ((c = uart_getc_nowait()) < 0) {}
  return c;
}

static FILE g_stdio = FDEV_SETUP_STREAM(stdio_putc, stdio_getc, NULL, _FDEV_SETUP_RW);
FILE *const stdin = &g_stdio;
FILE *const stdout = &g_stdio;
FILE *const stderr = &g_stdio;

void _exit(int code) { rpi_terminate(code); for (;;) {} }

extern char __tls_base[];
extern void _set_tls(void *tls);

#include "ports/baremetal/screen.h"
#include "ports/baremetal/console.h"
#include "ports/rpi/virtio.h"
#include "ports/baremetal/input.h"
#include "ports/rpi/usb.h"
#include "ports/rpi/sound.h"
#include "ports/rpi/storage.h"
#include "ports/rpi/fat.h"

static void input_flush(void) {}   // the PC drains its firmware queue here (efi/main.c)
static int console_extra_char(void) { snd_pump(); if (!TERM_FOCUSED()) return -1; input_poll(); return in_get_char(); }
#include "ports/rpi/builtins.h"

#include "common/bits.h"
#include "common/core.h"
#include "common/calling.h"
#include "common/interp.h"
#include "gen/rpi_boot.h"
#include "ports/baremetal/term_run.h"

int main(int argc, char *argv[]) {
  void *heap = malloc(HEAP_SIZE);
  if (!heap) { uart_puts("lugforth: no memory for the heap\n"); rpi_halt(); }
  clock_init();
  input_init();
  con_init();
  snd_init();
  fs_init();
  forth_init(argc, argv, heap, HEAP_SIZE, boot, sizeof(boot));
  term_boot();
  for (;;) { g_sys->rp = forth_run(g_sys->rp); term_next(); }
  return 1;
}

void rpi_start(void) {
  mmu_init();
  _set_tls(__tls_base);
  uart_init();
  wake_init();
  static char arg0[] = "lugforth";
  static char *argv[] = { arg0, 0 };
  main(1, argv);
}
