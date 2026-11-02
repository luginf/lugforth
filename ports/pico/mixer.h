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

// The mixer of the ABC player (ports/pico/abc.fs), in C: 4 square or pulse
// waves added and written as the level of an 8 bit PWM, at 31250 samples a
// second (32 microseconds, kept by the 1 MHz timer). The Forth version of the
// same loop is slower and its pitch depends on its speed; this one is exact
// and fast enough for notes of 2 kHz. The Forth word loop only sets the slots
// (mix_slot) now and then and asks for blocks of samples (mix_block).
//
// A slot is a phase of 24 bits that goes up by inc each sample: the wave is
// high (amp) while the phase is under thr, low (-amp) after it. In the
// simulator nothing waits and the levels go into mix_log, to be compared with
// the Forth loop.

#ifndef LUGFORTH_PICO_MIXER_H
#define LUGFORTH_PICO_MIXER_H

#include <stdint.h>

#define MIX_SLOTS 4
#define MIX_PERIOD_US 32          // 31250 samples a second

static uint32_t mix_ph[MIX_SLOTS], mix_inc[MIX_SLOTS], mix_thr[MIX_SLOTS];
static int32_t mix_amp[MIX_SLOTS];
static int mix_pin = 0;

#ifdef LUGFORTH_SIM
static uint8_t mix_log[1024];
static unsigned mix_logn = 0;
static inline void mix_out(int level) { mix_log[mix_logn++ & 1023] = (uint8_t) level; }
static inline void mix_wait(void) {}
static inline void mix_sync(void) {}
static inline void mix_stop(void) {}
static inline void mix_feed(void) {}
static inline void mix_start(int pin) {
  mix_pin = pin;
  for (int i = 0; i < MIX_SLOTS; ++i) { mix_ph[i] = 0; mix_inc[i] = 0; mix_thr[i] = 0; mix_amp[i] = 0; }
}
#else
// On the chip the samples are made by an interrupt of a hardware alarm of the
// timer, every 32 microseconds, whatever the Forth does: a loop that makes the
// samples in blocks stops the sound each time the Forth prepares the next
// block, 60 times a second, and that is heard as a tremolo. The Forth only sets
// the slots (mix_slot) from time to time. If it stops doing that (an error in
// the middle of a tune), the interrupt stops by itself after 4000 samples.
static volatile int32_t mix_countdown = 0;
static uint32_t mix_next = 0;
static int mix_slice = 0, mix_chan = 0, mix_alarm = -1, mix_on = 0;

static void __not_in_flash_func(mix_isr)(void) {
  timer_hw->intr = 1u << mix_alarm;
  mix_next += MIX_PERIOD_US;
  uint32_t now = timer_hw->timerawl;
  if ((int32_t) (mix_next - now) < 6) mix_next = now + 6;   // late: do not try to catch up
  timer_hw->alarm[mix_alarm] = mix_next;
  int32_t s = 128;
  for (int i = 0; i < MIX_SLOTS; ++i) {
    mix_ph[i] = (mix_ph[i] + mix_inc[i]) & 0xffffff;
    s += mix_ph[i] < mix_thr[i] ? mix_amp[i] : -mix_amp[i];
  }
  if (s < 0) s = 0;
  if (s > 255) s = 255;
  pwm_set_chan_level(mix_slice, mix_chan, (uint16_t) s);
  if (--mix_countdown <= 0) {
    timer_hw->inte &= ~(1u << mix_alarm);
    pwm_set_chan_level(mix_slice, mix_chan, 0);
    mix_on = 0;
  }
}

static inline void mix_stop(void) {
  if (mix_alarm < 0) return;
  timer_hw->inte &= ~(1u << mix_alarm);
  mix_on = 0;
}

static inline void mix_start(int pin) {
  mix_stop();
  mix_pin = pin;
  mix_slice = pwm_gpio_to_slice_num(pin);
  mix_chan = pwm_gpio_to_channel(pin);
  for (int i = 0; i < MIX_SLOTS; ++i) { mix_ph[i] = 0; mix_inc[i] = 0; mix_thr[i] = 0; mix_amp[i] = 0; }
  if (mix_alarm < 0) {
    mix_alarm = hardware_alarm_claim_unused(true);
    irq_set_exclusive_handler(TIMER_IRQ_0 + mix_alarm, mix_isr);
    irq_set_priority(TIMER_IRQ_0 + mix_alarm, 0);       // above the USB
  }
  mix_countdown = 4000;
  mix_next = timer_hw->timerawl + 100;
  timer_hw->alarm[mix_alarm] = mix_next;
  timer_hw->intr = 1u << mix_alarm;
  timer_hw->inte |= 1u << mix_alarm;
  irq_set_enabled(TIMER_IRQ_0 + mix_alarm, true);
  mix_on = 1;
}
static inline void mix_feed(void) { mix_countdown = 4000; }
// A block of samples made by the program itself, without the interrupt (tests).
static inline void mix_out(int level) { pwm_set_gpio_level(mix_pin, level); }
static inline void mix_sync(void) {
  uint32_t now = time_us_32();
  if ((int32_t) (now - mix_next) > 2 * MIX_PERIOD_US) mix_next = now;
}
static inline void mix_wait(void) {
  while ((int32_t) (time_us_32() - mix_next) < 0) {}
  mix_next += MIX_PERIOD_US;
}
#endif

static inline void mix_slot(int i, uint32_t inc, int32_t amp, uint32_t thr) {
  i &= MIX_SLOTS - 1;
  mix_inc[i] = inc; mix_amp[i] = amp; mix_thr[i] = thr;
  mix_feed();
}

static inline void mix_block(int n) {
  mix_sync();
  for (int k = 0; k < n; ++k) {
    int32_t s = 128;
    for (int i = 0; i < MIX_SLOTS; ++i) {
      mix_ph[i] = (mix_ph[i] + mix_inc[i]) & 0xffffff;
      s += mix_ph[i] < mix_thr[i] ? mix_amp[i] : -mix_amp[i];
    }
    if (s < 0) s = 0;
    if (s > 255) s = 255;
    mix_wait();
    mix_out((int) s);
  }
}

#endif
