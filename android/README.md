# lugforth on Android (command line binary)

`build.sh` cross-compiles `posix/main.c` with the NDK. Run `ninja` once first
(it generates `out/gen/posix_boot.h`), then:

    android/build.sh aarch64      # real phones; also x86_64 for the emulator

Test on a device with USB debugging:

    adb push out/android/lugforth-aarch64 /data/local/tmp/lugforth
    adb shell chmod +x /data/local/tmp/lugforth
    adb shell -t /data/local/tmp/lugforth

or copy the binary into Termux (`~/lugforth`) and run it there.
SDL2 words need libSDL2, which is not available in this mode.

# lugforth on Android (APK with a WebView, route B)

`build_apk.sh` wraps the web build (`out/web`: terminal and the SDL2 games)
in a WebView, with the SDK command line tools only (no gradle). Run `ninja`
first, then:

    android/build_apk.sh          # -> out/android/lugforth.apk (debug-signed)
    adb install -r out/android/lugforth.apk

The pages are served from assets through a fake https origin, so the `.fs`
files load with XMLHttpRequest. Not yet tested on a device: touch controls,
and sound (WebAudio) depend on the pages.

# lugforth native in SDLActivity (route C)

`build_apk_sdl.sh` builds SDL2 from source with `ndk-build`, compiles lugforth
as `libmain.so` (`android/sdl/main_android.cpp`: entry point `SDL_main`, output
to logcat, `libFoo-2.0.so.0` falls back to `libFoo.so`) and packs an APK around
SDL's `SDLActivity`. `ForthActivity` copies `assets/forth/*` to the app files
directory and gives `main.fs` (or the name in `autostart.txt`) to lugforth as
its first argument.

    SDL_SRC=/path/to/SDL2-2.32.x android/build_apk_sdl.sh [arm64-v8a|x86_64|armeabi-v7a]
    adb install -r out/android-c/lugforth-sdl.apk
    adb logcat -s lugforth          # what Forth prints

SDL_image, SDL_ttf and SDL_mixer are not built yet (the roguelike needs the first
one). There is no on-screen REPL yet: the app runs one Forth file.
Not tested on a device.
