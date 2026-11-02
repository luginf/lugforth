// Copyright 2024 Bradley D. Nelson
// Modifications Copyright 2026 Luginfo10
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

#include <stdio.h>
#include <errno.h>
#include <unistd.h>
#include <fcntl.h>
#include <sys/types.h>
#include <sys/stat.h>

#ifndef LUGFORTH_SIM
# include "pico/stdlib.h"
# include "pico/time.h"
# include "hardware/adc.h"
# include "hardware/gpio.h"
# include "hardware/pwm.h"
# include "hardware/timer.h"
# include "hardware/irq.h"
#endif

// TODO: Implement FLUSH-FILE.

#define PLATFORM_OPCODE_LIST \
  REQUIRED_MEMORY_SUPPORT \
  REQUIRED_SYSTEM_SUPPORT \
  REQUIRED_FILES_SUPPORT \
  OPTIONAL_TIME_SUPPORT \
  OPTIONAL_GPIO_SUPPORT \
  OPTIONAL_PWM_SUPPORT \
  MIXER_SUPPORT \
  OPTIONAL_ADC_SUPPORT \
  CALLING_OPCODE_LIST \
  FLOATING_POINT_LIST

#define REQUIRED_MEMORY_SUPPORT \
  YV(internals, MALLOC, SET malloc(n0)) \
  YV(internals, SYSFREE, free(a0); DROP) \
  YV(internals, REALLOC, SET realloc(a1, n0); NIP)

#ifndef LUGFORTH_SIM
# define REQUIRED_SYSTEM_SUPPORT \
  X("MS-TICKS", MS_TICKS, PUSH us_to_ms(get_absolute_time())) \
  XV(internals, "RAW-YIELD", RAW_YIELD, tight_loop_contents()) \
  XV(internals, "RAW-TERMINATE", RAW_TERMINATE, exit(n0); DROP) \
  YV(internals, getchar_timeout_us, n0 = getchar_timeout_us(n0))
#else
# define REQUIRED_SYSTEM_SUPPORT \
  X("MS-TICKS", MS_TICKS, PUSH (time(0) * 1000)) \
  XV(internals, "RAW-YIELD", RAW_YIELD, sleep(0)) \
  XV(internals, "RAW-TERMINATE", RAW_TERMINATE, exit(n0); DROP) \
  YV(internals, getchar_timeout_us, DROP; PUSH fgetc(stdin))
#endif

// File 0, 1 and 2 are the console (stdio, USB on the chip); the others are in the flash (ports/pico/flashfs.h): a directory of at most
// 119 names of 23 characters, no folders; a file open for writing at a time.
#include "ports/pico/flashfs.h"
#include "ports/pico/mixer.h"

#define REQUIRED_FILES_SUPPORT \
  X("R/O", R_O, PUSH 0) \
  X("W/O", W_O, PUSH 1) \
  X("R/W", R_W, PUSH 2) \
  Y(BIN, ) \
  X("CLOSE-FILE", CLOSE_FILE, tos = ffs_close(tos)) \
  X("OPEN-FILE", OPEN_FILE, cell_t mode = n0; DROP; cell_t len = n0; DROP; \
    n0 = ffs_open((const char *) a0, len, mode); PUSH n0 < 0 ? -n0 : 0) \
  X("CREATE-FILE", CREATE_FILE, DROP; cell_t len = n0; DROP; \
    n0 = ffs_create((const char *) a0, len); PUSH n0 < 0 ? -n0 : 0) \
  X("DELETE-FILE", DELETE_FILE, cell_t len = n0; DROP; \
    n0 = ffs_delete((const char *) a0, len)) \
  X("RENAME-FILE", RENAME_FILE, \
    cell_t len = n0; DROP; const char *to = (const char *) a0; DROP; \
    cell_t len2 = n0; DROP; n0 = ffs_rename((const char *) a0, len2, to, len)) \
  X("WRITE-FILE", WRITE_FILE, cell_t fd = n0; DROP; cell_t len = n0; DROP; \
    cell_t rv = fd < 3 ? (cell_t) write(fd, a0, len) : ffs_write(fd, a0, len); n0 = rv == len ? 0 : (rv < 0 ? -rv : EIO)) \
  X("READ-FILE", READ_FILE, cell_t fd = n0; DROP; cell_t len = n0; DROP; \
    cell_t rv = fd < 3 ? (cell_t) read(fd, a0, len) : ffs_read(fd, a0, len); n0 = rv < 0 ? 0 : rv; PUSH rv < 0 ? -rv : 0) \
  X("FILE-POSITION", FILE_POSITION, \
    cell_t rv = ffs_tell(n0); n0 = rv < 0 ? 0 : rv; PUSH rv < 0 ? -rv : 0) \
  X("REPOSITION-FILE", REPOSITION_FILE, cell_t fd = n0; DROP; \
    cell_t rv = ffs_seek(fd, (uint32_t) n0); n0 = rv < 0 ? -rv : 0) \
  X("FILE-SIZE", FILE_SIZE, \
    cell_t rv = ffs_size(n0); n0 = rv < 0 ? 0 : rv; PUSH rv < 0 ? -rv : 0) \
  X("FLUSH-FILE", FLUSH_FILE, n0 = 0) \
  X("RESIZE-FILE", RESIZE_FILE, cell_t fd = n0; DROP; cell_t cur = ffs_size(fd); \
    n0 = (cur >= 0 && n0 <= cur) ? 0 : 27) \
  X("NON-BLOCK", NON_BLOCK, n0 = 0) \
  X("OPEN-DIR", OPEN_DIR, n1 = ffs_opendir((const char *) a1, n0); n0 = n1 ? 0 : ENOENT) \
  X("CLOSE-DIR", CLOSE_DIR, n0 = 0) \
  YV(internals, READDIR, SET (cell_t) ffs_readdir()) \
  YV(pico, ffs_free_sectors, PUSH ffs_free_sectors())

#ifdef LUGFORTH_SIM
# define OPTIONAL_TIME_SUPPORT
#else
# define OPTIONAL_TIME_SUPPORT \
   YV(pico, sleep_ms, sleep_ms(n0); DROP) \
   YV(pico, sleep_us, sleep_us(n0); DROP) \
   YV(pico, time_us_32, PUSH time_us_32())
#endif

#if defined(LUGFORTH_SIM) || !defined(PICO_DEFAULT_LED_PIN)
# define OPTIONAL_LED_PIN_SUPPORT
#else
# define OPTIONAL_LED_PIN_SUPPORT \
   XV(pico, "PICO_DEFAULT_LED_PIN", PICO_LED_PIN, PUSH PICO_DEFAULT_LED_PIN)
#endif

#ifdef LUGFORTH_SIM
# define OPTIONAL_GPIO_SUPPORT
#else
# define OPTIONAL_GPIO_SUPPORT \
   OPTIONAL_LED_PIN_SUPPORT \
   XV(pico, "GPIO_IN", PICO_GPIO_IN, PUSH GPIO_IN) \
   XV(pico, "GPIO_OUT", PICO_GPIO_OUT, PUSH GPIO_OUT) \
   XV(pico, "GPIO_FUNC_SPI", PICO_GPIO_FUNC_SPI, PUSH GPIO_FUNC_SPI) \
   XV(pico, "GPIO_FUNC_UART", PICO_GPIO_FUNC_UART, PUSH GPIO_FUNC_UART) \
   XV(pico, "GPIO_FUNC_I2C", PICO_GPIO_FUNC_I2C, PUSH GPIO_FUNC_I2C) \
   XV(pico, "GPIO_FUNC_PWM", PICO_GPIO_FUNC_PWM, PUSH GPIO_FUNC_PWM) \
   XV(pico, "GPIO_FUNC_SIO", PICO_GPIO_FUNC_SIO, PUSH GPIO_FUNC_SIO) \
   XV(pico, "GPIO_FUNC_PIO0", PICO_GPIO_FUNC_PIO0, PUSH GPIO_FUNC_PIO0) \
   XV(pico, "GPIO_FUNC_PIO1", PICO_GPIO_FUNC_PIO1, PUSH GPIO_FUNC_PIO1) \
   XV(pico, "GPIO_FUNC_NULL", PICO_GPIO_FUNC_NULL, PUSH GPIO_FUNC_NULL) \
   YV(pico, gpio_init, gpio_init(n0); DROP) \
   YV(pico, gpio_deinit, gpio_deinit(n0); DROP) \
   YV(pico, gpio_init_mask, gpio_init_mask(n0); DROP) \
   YV(pico, gpio_set_function, gpio_set_function(n1, n0); DROPn(2)) \
   YV(pico, gpio_get_function, n0 = gpio_get_function(n0)) \
   YV(pico, gpio_set_dir, gpio_set_dir(n1, n0); DROPn(2)) \
   YV(pico, gpio_get_dir, n0 = gpio_get_dir(n0)) \
   YV(pico, gpio_set_dir_out_masked, gpio_set_dir_out_masked(n0); DROP) \
   YV(pico, gpio_set_dir_in_masked, gpio_set_dir_in_masked(n0); DROP) \
   YV(pico, gpio_put, gpio_put(n1, n0); DROPn(2)) \
   YV(pico, gpio_get, n0 = gpio_get(n0) ? -1 : 0) \
   YV(pico, gpio_get_all, PUSH gpio_get_all()) \
   YV(pico, gpio_set_mask, gpio_set_mask(n0); DROP) \
   YV(pico, gpio_clr_mask, gpio_clr_mask(n0); DROP) \
   YV(pico, gpio_xor_mask, gpio_xor_mask(n0); DROP) \
   YV(pico, gpio_put_masked, gpio_put_masked(n1, n0); DROPn(2)) \
   YV(pico, gpio_put_all, gpio_put_all(n0); DROP) \
   YV(pico, gpio_pull_up, gpio_pull_up(n0); DROP) \
   YV(pico, gpio_pull_down, gpio_pull_down(n0); DROP) \
   YV(pico, gpio_disable_pulls, gpio_disable_pulls(n0); DROP) \
   YV(pico, gpio_set_pulls, gpio_set_pulls(n2, n1, n0); DROPn(3))
#endif

#ifdef LUGFORTH_SIM
# define OPTIONAL_PWM_SUPPORT
#else
# define OPTIONAL_PWM_SUPPORT \
   YV(pico, pwm_gpio_to_slice_num, n0 = pwm_gpio_to_slice_num(n0)) \
   YV(pico, pwm_set_wrap, pwm_set_wrap(n1, n0); DROPn(2)) \
   YV(pico, pwm_set_clkdiv_int_frac, pwm_set_clkdiv_int_frac(n2, n1, n0); DROPn(3)) \
   YV(pico, pwm_set_gpio_level, pwm_set_gpio_level(n1, n0); DROPn(2)) \
   YV(pico, pwm_set_enabled, pwm_set_enabled(n1, n0 != 0); DROPn(2))
#endif

// The mixer of the ABC player (ports/pico/mixer.h), in the vocabulary pico.
#ifdef LUGFORTH_SIM
# define MIXER_SIM_SUPPORT \
   YV(pico, mix_log, PUSH (cell_t) mix_log) \
   YV(pico, mix_logn, PUSH mix_logn)
#else
# define MIXER_SIM_SUPPORT
#endif
#define MIXER_SUPPORT \
   YV(pico, mix_start, mix_start(n0); DROP) \
   YV(pico, mix_slot, mix_slot(n3, (uint32_t) n2, (int32_t) n1, (uint32_t) n0); DROPn(4)) \
   YV(pico, mix_block, mix_block(n0); DROP) \
   YV(pico, mix_stop, mix_stop()) \
   MIXER_SIM_SUPPORT

#ifdef LUGFORTH_SIM
# define OPTIONAL_ADC_SUPPORT
#else
# define OPTIONAL_ADC_SUPPORT \
   YV(pico, adc_init, adc_init()) \
   YV(pico, adc_gpio_init, adc_gpio_init(n0); DROP) \
   YV(pico, adc_select_input, adc_select_input(n0); DROP) \
   YV(pico, adc_get_selected_input, PUSH adc_get_selected_input()) \
   YV(pico, adc_set_round_robin, adc_set_round_robin(n0); DROP) \
   YV(pico, adc_set_temp_sensor_enabled, adc_set_temp_sensor_enabled(n0); DROP) \
   YV(pico, adc_read, PUSH adc_read()) \
   YV(pico, adc_run, adc_run(n0); DROP) \
   YV(pico, adc_set_clkdiv, adc_set_clkdiv(*fp--)) \
   YV(pico, adc_fifo_setup, adc_fifo_setup(n4, n3, n2, n1, n0); DROPn(5)) \
   YV(pico, adc_fifo_is_empty, PUSH adc_fifo_is_empty()) \
   YV(pico, adc_fifo_get_level, PUSH adc_fifo_get_level()) \
   YV(pico, adc_fifo_get, PUSH adc_fifo_get()) \
   YV(pico, adc_fifo_get_blocking, PUSH adc_fifo_get_blocking()) \
   YV(pico, adc_fifo_drain, adc_fifo_drain()) \
   YV(pico, adc_irq_set_enabled, adc_irq_set_enabled(n0); DROP)
#endif

#define VOCABULARY_LIST V(forth) V(internals) V(pico)

#define PATH_MAX 256
