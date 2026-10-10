#!/bin/bash
# Build a debug-signed APK that wraps the web build of lugforth in a WebView,
# with the command line tools of the SDK only (no gradle).
# Needs out/web (run ninja first). Result: out/android/lugforth.apk
set -e
cd "$(dirname "$0")/.."
SDK=${ANDROID_HOME:-$HOME/Android/Sdk}
BT=$(ls -d $SDK/build-tools/* | sort -V | tail -1)
JAR=$(ls -d $SDK/platforms/android-* | sort -V | tail -1)/android.jar
W=out/android/apk; rm -rf $W; mkdir -p $W/assets $W/classes
cp android/app/assets/index.html $W/assets/
for f in terminal.html lugforth.js sdl2_breakout.html sdl2_breakout.fs \
         sdl2_drums.html sdl2_drums.fs sdl2_roguelike.html sdl2_roguelike.fs \
         sdl2_tiles.png; do
  cp out/web/$f $W/assets/
done
javac -nowarn -source 8 -target 8 -cp $JAR -d $W/classes $(find android/app/src -name '*.java') 2>&1 | grep -v "^warning\|^Note" || true
$BT/d8 --min-api 24 --lib $JAR --output $W $(find $W/classes -name '*.class')
$BT/aapt2 link -o $W/base.apk -I $JAR --manifest android/app/AndroidManifest.xml \
  --min-sdk-version 24 --target-sdk-version 35 -A $W/assets
(cd $W && zip -q base.apk classes.dex)
$BT/zipalign -f 4 $W/base.apk $W/aligned.apk
KS=out/android/debug.keystore
[ -f $KS ] || keytool -genkeypair -keystore $KS -storepass android -keypass android \
  -alias debug -keyalg RSA -keysize 2048 -validity 10000 -dname "CN=lugforth debug" 2>/dev/null
$BT/apksigner sign --ks $KS --ks-pass pass:android --out out/android/lugforth.apk $W/aligned.apk
ls -l out/android/lugforth.apk
