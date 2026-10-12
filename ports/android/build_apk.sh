#!/bin/bash
# Build one debug-signed APK with both versions of lugforth, with the command line
# tools of the SDK and the NDK only (no gradle):
#   - the web build in a WebView (needs out/web: run `ninja web`),
#   - lugforth as libmain.so inside SDLActivity, SDL2 built from source with ndk-build
#     (needs out/gen/posix_boot.h: run `ninja`, and an SDL2 source tree).
# A start screen lets the user pick one.
# Usage: SDL_SRC=/path/to/SDL2-2.x ports/android/build_apk.sh [abi]   (default arm64-v8a)
# Result: out/android/lugforth.apk
set -e
cd "$(dirname "$0")/../.."
ABI=${1:-arm64-v8a}
case $ABI in
  arm64-v8a) TRIPLE=aarch64-linux-android ;;
  x86_64)    TRIPLE=x86_64-linux-android ;;
  armeabi-v7a) TRIPLE=armv7a-linux-androideabi ;;
  *) echo "unknown abi $ABI"; exit 1 ;;
esac
API=24
SDL_SRC=${SDL_SRC:-$HOME/emsdk/upstream/emscripten/cache/ports/sdl2/SDL-release-2.32.10}
SDK=${ANDROID_HOME:-$HOME/Android/Sdk}
NDK=${ANDROID_NDK_HOME:-$SDK/ndk/$(ls $SDK/ndk | sort -V | tail -1)}
BT=$(ls -d $SDK/build-tools/* | sort -V | tail -1)
JAR=$(ls -d $SDK/platforms/android-* | sort -V | tail -1)/android.jar
O=out/android; W=$O/apk-$ABI
[ -f out/gen/posix_boot.h ] || { echo "run ninja first"; exit 1; }
[ -f out/web/terminal.html ] || { echo "run ninja web first"; exit 1; }
[ -d "$SDL_SRC/android-project" ] || { echo "SDL_SRC has no android-project"; exit 1; }

# 1. SDL2 and SDL2_ttf (FreeType inside, no HarfBuzz) with ndk-build. SDL2_ttf is fetched once
# into out/android/deps, it gives the smooth font of the console.
TTF_VER=2.24.0
TTF_SRC=$PWD/$O/deps/SDL2_ttf-$TTF_VER
if [ ! -d "$TTF_SRC/external/freetype" ]; then
  mkdir -p $O/deps
  curl -sL -o $O/deps/ttf.tar.gz https://github.com/libsdl-org/SDL_ttf/releases/download/release-$TTF_VER/SDL2_ttf-$TTF_VER.tar.gz
  tar xzf $O/deps/ttf.tar.gz -C $O/deps
fi
mkdir -p $O/ndk
cat > $O/ndk/Android.mk <<EOM
include $SDL_SRC/Android.mk
include $TTF_SRC/Android.mk
EOM
$NDK/ndk-build -j"$(nproc)" NDK_PROJECT_PATH=$PWD/$O/ndk APP_BUILD_SCRIPT=$PWD/$O/ndk/Android.mk \
  SUPPORT_HARFBUZZ=false \
  APP_PLATFORM=android-$API APP_ABI=$ABI NDK_OUT=$PWD/$O/ndk/obj NDK_LIBS_OUT=$PWD/$O/ndk/libs >$O/ndk.log 2>&1 \
  || { tail -20 $O/ndk.log; exit 1; }

# 2. lugforth as libmain.so
mkdir -p $O/lib/$ABI
$NDK/toolchains/llvm/prebuilt/linux-x86_64/bin/$TRIPLE$API-clang++ -shared -fPIC -x c++ -std=c++11 -O2 -w \
  -DLUGFORTH_MINIMAL -D__thread= -fno-exceptions -fno-stack-protector -I. -Iout \
  ports/android/app/native/main_android.cpp -o $O/lib/$ABI/libmain.so -static-libstdc++ -ldl -llog

# 3. Java and dex
rm -rf $W; mkdir -p $W/classes $W/assets/web $W/assets/examples $W/assets/forth
SDLJ=$SDL_SRC/android-project/app/src/main/java
javac -nowarn -source 8 -target 8 -cp $JAR -d $W/classes \
  $(find $SDLJ ports/android/app/src -name '*.java') 2>&1 | grep -v "^warning\|^Note" || true
$BT/d8 --min-api $API --lib $JAR --output $W $(find $W/classes -name '*.class')

# 4. Assets: the web pages, the examples (as for the AppImage, minus what needs
# SDL_image or SDL_mixer, which are not built here), the Forth files of the app
cp ports/android/app/assets/index.html $W/assets/web/
for f in terminal.html lugforth.js sdl2_breakout.html sdl2_breakout.fs \
         sdl2_drums.html sdl2_drums.fs sdl2_roguelike.html sdl2_roguelike.fs \
         sdl2_tiles.png; do
  cp out/web/$f $W/assets/web/
done
cp -R examples/. $W/assets/examples/
(cd $W/assets/examples && rm -rf bench_* *.c *.tal *.rom* Makefile *.py *xtensa* espnow.fs \
   *.sf2 *.mid sdl2_pianoroll* sdl2_roguelike.fs sdl2_tiles*.png)
cp ports/android/app/assets/forth/main.fs $W/assets/forth/
# the font of the console (DejaVu Sans Mono, free license); without it the bitmap font stays
DEJAVU=/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf
if [ -f $DEJAVU ]; then
  cp $DEJAVU $W/assets/forth/font.ttf
  cp /usr/share/doc/fonts-dejavu-core/copyright $W/assets/forth/font-license.txt 2>/dev/null || true
fi

# 5. Package and sign
$BT/aapt2 compile --dir ports/android/app/res -o $W/res.zip
$BT/aapt2 link -o $W/base.apk -I $JAR --manifest ports/android/app/AndroidManifest.xml \
  --min-sdk-version $API --target-sdk-version 35 -A $W/assets $W/res.zip
mkdir -p $W/pack/lib/$ABI
cp $O/ndk/libs/$ABI/libSDL2.so $O/ndk/libs/$ABI/libSDL2_ttf.so $O/lib/$ABI/libmain.so $W/pack/lib/$ABI/
(cd $W && zip -q base.apk classes.dex && cd pack && zip -q -r ../base.apk lib)
$BT/zipalign -f -p 4 $W/base.apk $W/aligned.apk
# KEYSTORE, KEYSTORE_PASS and KEY_ALIAS pick a release key (the CI takes it from a secret);
# without them a debug key is made in out/android and reused, keep that file to update an install
KS=${KEYSTORE:-$O/debug.keystore}
KSPASS=${KEYSTORE_PASS:-android}
KALIAS=${KEY_ALIAS:-debug}
[ -f $KS ] || keytool -genkeypair -keystore $KS -storepass android -keypass android \
  -alias debug -keyalg RSA -keysize 2048 -validity 10000 -dname "CN=lugforth debug" >/dev/null 2>&1
$BT/apksigner sign --ks $KS --ks-pass pass:$KSPASS --ks-key-alias $KALIAS --out $O/lugforth.apk $W/aligned.apk
ls -l $O/lugforth.apk
