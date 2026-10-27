# Ports

*Traduction française de [docs/pages/ports.md](../pages/ports.md).*

| Plateforme | Dossier | Notes |
|----------|-----------|-------|
| Linux, macOS | `ports/posix` | version principale; binaire universel macOS, mots SDL2 testés |
| Windows | `ports/windows` | exe compilé avec MSVC (CI) ou MinGW (`ninja windows-mingw`); console SDL2 (`--gui`), `ls`, éditeur de ligne, `ed`, `writhdeck` |
| Web | `ports/web` | navigateur, mots SDL2 sur un canevas |
| Android | `ports/android` | un APK: version web ou version native SDL2; ligne de commande |
| ESP32 | `ports/esp32` | sketch Arduino |
| Raspberry Pi Pico | `ports/pico` | série USB, `tools/pico_send.py` charge un programme |
| pico-ice | `ports/pico-ice` | RP2040 avec un FPGA |
| Game Boy Advance | `ports/gba` | prototype, avec un roguelike |
| Raspberry Pi 3/4, QEMU | `ports/rpi` | AArch64 en matériel nu, console UART, mots `sdl2` (écran, texte, images, clavier/souris, son); testé sous QEMU `virt` et `raspi3b` |
| PC x86-64 (UEFI) | `ports/efi` | matériel nu sous UEFI (clé USB), partage avec le Pi le code d'écran, de console, d'entrées et de `sdl2` de `ports/baremetal`; pas de son, pas de souris sous QEMU; testé sous QEMU + OVMF |
