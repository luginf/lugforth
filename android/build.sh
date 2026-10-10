#!/bin/bash
# Cross-compile the POSIX ueforth for Android with the NDK (command line binary,
# to run from Termux or adb shell). Needs out/gen/posix_boot.h (run ninja first).
# Usage: android/build.sh [aarch64|x86_64|armv7a|i686]   (default: aarch64)
set -e
cd "$(dirname "$0")/.."
ABI=${1:-aarch64}
API=${API:-24}
NDK=${ANDROID_NDK_HOME:-$(ls -d ~/Android/Sdk/ndk/* | sort -V | tail -1)}
case $ABI in
  armv7a) TRIPLE=armv7a-linux-androideabi$API ;;
  i686)   TRIPLE=i686-linux-android$API ;;
  *)      TRIPLE=$ABI-linux-android$API ;;
esac
CXX=$NDK/toolchains/llvm/prebuilt/linux-x86_64/bin/$TRIPLE-clang++
[ -f out/gen/posix_boot.h ] || { echo "run ninja first (out/gen/posix_boot.h missing)"; exit 1; }
mkdir -p out/android
$CXX -x c++ -std=c++11 -O2 -w -DUEFORTH_MINIMAL -D__thread= -fno-exceptions -fno-stack-protector \
  -ffunction-sections -fdata-sections -Wl,--gc-sections -s \
  -I. -Iout posix/main.c -o out/android/ueforth-$ABI -ldl
ls -l out/android/ueforth-$ABI
