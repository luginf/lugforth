#!/bin/bash
# Build an APK that runs lugforth natively inside SDLActivity (route C): SDL2 built
# from source with ndk-build, lugforth compiled as libmain.so, no gradle.
# Needs out/gen/posix_boot.h (run ninja first) and an SDL2 source tree.
# Usage: SDL_SRC=/path/to/SDL2-2.x ports/android/build_apk_sdl.sh [abi]   (default arm64-v8a)
# Result: out/android-c/lugforth-sdl.apk
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
O=out/android-c; W=$O/apk-$ABI
[ -f out/gen/posix_boot.h ] || { echo "run ninja first"; exit 1; }
[ -d "$SDL_SRC/android-project" ] || { echo "SDL_SRC has no android-project"; exit 1; }

# 1. SDL2
mkdir -p $O/ndk
$NDK/ndk-build -j"$(nproc)" NDK_PROJECT_PATH=$PWD/$O/ndk APP_BUILD_SCRIPT=$SDL_SRC/Android.mk \
  APP_PLATFORM=android-$API APP_ABI=$ABI NDK_OUT=$PWD/$O/ndk/obj NDK_LIBS_OUT=$PWD/$O/ndk/libs >$O/ndk.log 2>&1 \
  || { tail -20 $O/ndk.log; exit 1; }

# 2. lugforth as libmain.so
mkdir -p $O/lib/$ABI
$NDK/toolchains/llvm/prebuilt/linux-x86_64/bin/$TRIPLE$API-clang++ -shared -fPIC -x c++ -std=c++11 -O2 -w \
  -DLUGFORTH_MINIMAL -D__thread= -fno-exceptions -fno-stack-protector -I. -Iout \
  ports/android/sdl/main_android.cpp -o $O/lib/$ABI/libmain.so -static-libstdc++ -ldl -llog

# 3. Java, dex, resources
rm -rf $W; mkdir -p $W/classes $W/assets/forth
SDLJ=$SDL_SRC/android-project/app/src/main/java
javac -nowarn -source 8 -target 8 -cp $JAR -d $W/classes \
  $(find $SDLJ ports/android/sdl/src -name '*.java') 2>&1 | grep -v "^warning\|^Note" || true
$BT/d8 --min-api $API --lib $JAR --output $W $(find $W/classes -name '*.class')
cp examples/sdl2_breakout.fs examples/sdl2_drums.fs ports/posix/sdl2_console.fs $W/assets/forth/
cp ports/android/sdl/assets/* $W/assets/forth/ 2>/dev/null || true
[ -f $W/assets/forth/main.fs ] || echo 'include sdl2_breakout.fs' > $W/assets/forth/main.fs
$BT/aapt2 link -o $W/base.apk -I $JAR --manifest ports/android/sdl/AndroidManifest.xml \
  --min-sdk-version $API --target-sdk-version 35 -A $W/assets
mkdir -p $W/pack/lib/$ABI
cp $O/ndk/libs/$ABI/libSDL2.so $O/lib/$ABI/libmain.so $W/pack/lib/$ABI/
(cd $W && zip -q base.apk classes.dex && cd pack && zip -q -r ../base.apk lib)
$BT/zipalign -f -p 4 $W/base.apk $W/aligned.apk
KS=out/android/debug.keystore; mkdir -p out/android
[ -f $KS ] || keytool -genkeypair -keystore $KS -storepass android -keypass android \
  -alias debug -keyalg RSA -keysize 2048 -validity 10000 -dname "CN=lugforth debug" >/dev/null 2>&1
$BT/apksigner sign --ks $KS --ks-pass pass:android --out $O/lugforth-sdl.apk $W/aligned.apk
ls -l $O/lugforth-sdl.apk
