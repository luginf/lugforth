# lugforth on Android

Run `ninja` and `ninja web` once first (they generate `out/gen/posix_boot.h` and `out/web`).

**APK with both versions** (SDK command line tools and NDK, no gradle)

    SDL_SRC=/path/to/SDL2-2.32.x ports/android/build_apk.sh [arm64-v8a|x86_64|armeabi-v7a]
    adb install -r out/android/lugforth.apk

One package. A start screen offers:

* **Web version**: the web build in a WebView (terminal, Breakout, Drums, Roguelike).
* **SDL2 version**: the native binary inside `SDLActivity`, with an on-screen console.
  The examples are copied on first start to `Android/data/org.lugforth.app/files/examples`
  (never overwritten, so your files stay) and the console runs there.

Both have a hamburger bar at the top: arrows, Tab, Ctrl (tap it, then a letter: Ctrl+S, Ctrl+Q...),
a ⋮ button for the examples, text size (A- A+) and help, and back to the
start screen. Pinch out or in changes the text size. The console has history (up and down),
cursor keys and Tab completion of words and, in the SDL2 version, file names.

The console uses a smooth TrueType font (SDL2_ttf is fetched and built by the script,
DejaVu Sans Mono is bundled). SDL_image and SDL_mixer are not built yet, so the SDL2
version has no sprites or sound files (the roguelike and piano roll run only in the web version or on the desktop).

**Command line binary** (NDK)

    ports/android/build.sh aarch64        # also x86_64 for the emulator
    adb push out/android/lugforth-aarch64 /data/local/tmp/lugforth
    adb shell -t /data/local/tmp/lugforth

Or copy the binary into Termux. No SDL2 words in this mode.
