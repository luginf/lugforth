---
name: efi-qemu-test
description: Boot the bare-metal EFI image (ports/efi) under QEMU + OVMF and check it, or type into it; use after touching ports/efi, ports/baremetal, or the EFI side of an example
---

Facts and limits: section "UEFI PC" of `docs/dev/PORTS.md`. The procedure:

- Full test: `make -C ports/efi test` (124 checks, `tools/efi_qemu_test.py`). Needs `gnu-efi ovmf qemu-system-x86
  dosfstools mtools`; without root, unpack them from `apt download` and pass
  `EFIINC= EFILIB= OVMF=` (see PORTS.md). Three checks are known flaky or failing (second session
  screen, two session timeouts; the dead key test): compare with a baseline run (`git stash`) before blaming a change.
- By hand: stage the stick with `make stage` (the wildcards pick up new `.fs` / `.z5` files), keep the
  directory path SHORT (e.g. `/tmp/zme`): the QEMU monitor socket path has a length limit. Type with the monitor
  (`sendkey`) or QMP, read the screen with `screendump`.
- EFI has no `argc` / `argv` (they come from `common/desktop.fs`): guard command-line code with
  `[defined] argv`, or an `include` runs on into `bye`.
- OVMF never polls USB mice: pointer changes can only be built and regression-tested here, never felt.
- Never `pkill -f` / `pgrep -f` a pattern that is in your own command line: kill QEMU by PID.
