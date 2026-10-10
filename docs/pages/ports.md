# Ports

| Platform | Directory | Notes |
|----------|-----------|-------|
| Linux, macOS | `ports/posix` | the main build; macOS does not start properly yet |
| Windows | `ports/windows` | built with MSVC |
| Web | `ports/web` | JavaScript, runs in a browser, with the SDL2 words on a canvas |
| Android | `ports/android` | command line binary, WebView app and native SDL2 app with its own console |
| ESP32 | `ports/esp32` | Arduino sketch |
| Raspberry Pi Pico (RP2040) | `ports/pico` | USB serial console, `tools/pico_send.py` loads a program |
| pico-ice | `ports/pico-ice` | RP2040 with an FPGA |
| Game Boy Advance | `ports/gba` | prototype, with a roguelike |

Details of the Android and Game Boy Advance ports follow.
