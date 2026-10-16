# Ports

| Platform | Directory | Notes |
|----------|-----------|-------|
| Linux, macOS | `ports/posix` | main build; macOS does not start yet |
| Windows | `ports/windows` | MSVC |
| Web | `ports/web` | browser, SDL2 words on a canvas |
| Android | `ports/android` | one APK: web version or native SDL2 version; command line |
| ESP32 | `ports/esp32` | Arduino sketch |
| Raspberry Pi Pico | `ports/pico` | USB serial, `tools/pico_send.py` loads a program |
| pico-ice | `ports/pico-ice` | RP2040 with an FPGA |
| Game Boy Advance | `ports/gba` | prototype, with a roguelike |
| Raspberry Pi 3/4, QEMU | `ports/rpi` | bare-metal AArch64, UART console, `sdl2` words (screen, text, images, keyboard/mouse, sound); tested under QEMU `virt` and `raspi3b` |
