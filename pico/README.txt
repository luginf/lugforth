+-------------------------------------+
|  Lugforth for the Raspberry Pi Pico  |
+-------------------------------------+

Note: this port was adapted by Luginfo10 from the pico-ice port of uEforth by
Bradley D. Nelson. It is not part of upstream uEforth and is not endorsed
by its author.

This is Lugforth v{{VERSION}} (Revision {{REVISION}}) for the Raspberry Pi Pico
(RP2040).

Lugforth is an indirect threaded Forth.
It uses a small C "kernel" that boots via inline Forth code.

To install:
  * Hold down the BOOTSEL button on the Pico.
  * Connect the Pico to USB (then release BOOTSEL).
  * Copy lugforth-pico.uf2 to the mounted RPI-RP2 drive.
  * Open the USB serial port with a terminal (115200 8N1, any speed works).

Example (blink the on-board LED):
  pico
  PICO_DEFAULT_LED_PIN constant led
  led gpio_init  led GPIO_OUT gpio_set_dir
  : blink  10 0 do led 1 gpio_put 250 ms led 0 gpio_put 250 ms loop ;
  blink

Documentation and the latest version of uEforth are available at:
https://eforth.appspot.com/

Full unexpanded source code and an issue tracker are available at:
https://github.com/flagxor/ueforth
