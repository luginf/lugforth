---
name: sdl-xvfb-check
description: Run an SDL2 example in a real X window under Xvfb, press keys with xdotool and take a screenshot; use to verify visually, not only headless
---

    Xvfb :99 &                                  # or use the real display :0
    DISPLAY=:99 out/posix/lugforth examples/games/<game>.fs &
    DISPLAY=:99 xdotool keydown Right; sleep 0.25; DISPLAY=:99 xdotool keyup Right
    DISPLAY=:99 import -window root /tmp/claude-1004/.../scratchpad/shot.png     # then Read the png

- A quick `xdotool key` is missed by the game loop: hold with keydown, sleep 0.25, keyup.
- Headless only (CI): `SDL_VIDEODRIVER=dummy`, pixels through `SDL_RenderReadPixels`; `make -C examples smoke`.
- The user reports what they see literally: if a visual feature "doesn't show", believe it and look at a real window.
- Kill Xvfb and the game by PID (`ps`), never `pkill -f` on a pattern of your own command line.
- Web pages: headless Chromium over CDP, see `docs/en/dev/PITFALLS.md`.
