#!/bin/sh
# Builds SDL2 and the X11 libraries as static libraries against musl, for `ninja posix-static-sdl`.
# Everything goes to out/musl-deps/ (sources, build, stage/usr); the sources are checked against a sha256.
# Needs: x86_64-linux-musl-gcc (package musl-dev), curl, cmake, ninja, make, python3, pkg-config.
# Usage: tools/build_musl_sdl.sh      (idempotent: does nothing if SDL2, SDL2_image, SDL2_ttf and SDL2_mixer are already built in out/musl-deps/stage)
set -e
ROOT=$(cd "$(dirname "$0")/.." && pwd)
D=$ROOT/out/musl-deps
STAGE=$D/stage
[ -f $STAGE/usr/lib/x86_64-linux-gnu/libSDL2.a ] && [ -f $STAGE/usr/lib/libSDL2_image.a ] && [ -f $STAGE/usr/lib/libSDL2_ttf.a ] && [ -f $STAGE/usr/lib/libSDL2_mixer.a ] && [ -f $STAGE/usr/lib/libasound.a ] && exit 0
mkdir -p $D/src $D/stage $D/build $D/khdr
export CC=x86_64-linux-musl-gcc
export PKG_CONFIG_LIBDIR=$STAGE/usr/lib/pkgconfig:$STAGE/usr/share/pkgconfig
export PKG_CONFIG_SYSROOT_DIR=$STAGE
export CPPFLAGS="-I$STAGE/usr/include -I$D/khdr"
export CFLAGS="-O2 -I$D/khdr"
export LDFLAGS="-L$STAGE/usr/lib"
# musl has no <linux/...>: the kernel headers of the host are enough for the X11 and SDL sources
ln -sfn /usr/include/linux $D/khdr/linux
ln -sfn /usr/include/x86_64-linux-gnu/asm $D/khdr/asm
ln -sfn /usr/include/asm-generic $D/khdr/asm-generic

get() { # name url sha256
  mkdir -p $D/src/$1; f=$D/src/$1/$(basename "$2")
  [ -f "$f" ] || curl -fsSL -o "$f" "$2"
  echo "$3  $f" | sha256sum -c --quiet - || { echo "bad sha256: $f"; rm -f "$f"; exit 1; }
}
get xorgproto https://www.x.org/releases/individual/proto/xorgproto-2023.2.tar.xz b61fbc7db82b14ce2dc705ab590efc32b9ad800037113d1973811781d5118c2c
get xtrans https://www.x.org/releases/individual/lib/xtrans-1.5.0.tar.xz 1ba4b703696bfddbf40bacf25bce4e3efb2a0088878f017a50e9884b0c8fb1bd
get libXau https://www.x.org/releases/individual/lib/libXau-1.0.11.tar.xz f3fa3282f5570c3f6bd620244438dbfbdd580fc80f02f549587a0f8ab329bbeb
get xcb-proto https://xorg.freedesktop.org/archive/individual/proto/xcb-proto-1.16.0.tar.xz a75a1848ad2a89a82d841a51be56ce988ff3c63a8d6bf4383ae3219d8d915119
get libxcb https://xorg.freedesktop.org/archive/individual/lib/libxcb-1.16.tar.xz 4348566aa0fbf196db5e0a576321c65966189210cb51328ea2bb2be39c711d71
get libX11 https://www.x.org/releases/individual/lib/libX11-1.8.7.tar.xz 05f267468e3c851ae2b5c830bcf74251a90f63f04dd7c709ca94dc155b7e99ee
get libXext https://www.x.org/releases/individual/lib/libXext-1.3.6.tar.xz edb59fa23994e405fdc5b400afdf5820ae6160b94f35e3dc3da4457a16e89753
get SDL2 https://github.com/libsdl-org/SDL/releases/download/release-2.30.0/SDL2-2.30.0.tar.gz 36e2e41557e0fa4a1519315c0f5958a87ccb27e25c51776beb6f1239526447b0
get alsa-lib https://www.alsa-project.org/files/pub/lib/alsa-lib-1.2.11.tar.bz2 9f3f2f69b995f9ad37359072fbc69a3a88bfba081fc83e9be30e14662795bb4d
get freetype https://download.savannah.gnu.org/releases/freetype/freetype-2.13.2.tar.xz 12991c4e55c506dd7f9b765933e62fd2be2e06d421505d7950a132e4f1bb484d
get SDL2_ttf https://github.com/libsdl-org/SDL_ttf/releases/download/release-2.22.0/SDL2_ttf-2.22.0.tar.gz d48cbd1ce475b9e178206bf3b72d56b66d84d44f64ac05803328396234d67723
get SDL2_mixer https://github.com/libsdl-org/SDL_mixer/releases/download/release-2.8.0/SDL2_mixer-2.8.0.tar.gz 1cfb34c87b26dbdbc7afd68c4f545c0116ab5f90bbfecc5aebe2a9cb4bb31549
get SDL2_image https://github.com/libsdl-org/SDL_image/releases/download/release-2.8.2/SDL2_image-2.8.2.tar.gz 8f486bbfbcf8464dd58c9e5d93394ab0255ce68b51c5a966a918244820a76ddc

build() { # name tarball [configure args]
  name=$1; tb=$2; shift 2
  rm -rf $D/build/$name; mkdir -p $D/build/$name; cd $D/build/$name
  tar xf $D/src/$name/$tb --strip-components=1
  echo "=== $name"
  ./configure --prefix=/usr --enable-static --disable-shared "$@" > configure.log 2>&1 || { tail -30 configure.log; exit 1; }
  make -j4 > make.log 2>&1 || { tail -30 make.log; exit 1; }
  make install DESTDIR=$STAGE > install.log 2>&1 || { tail -30 install.log; exit 1; }
  find $STAGE -name '*.la' -delete   # libtool files hold absolute paths
  export PYTHONPATH=$(echo $STAGE/usr/lib/python3*/site-packages)
}
build xorgproto xorgproto-2023.2.tar.xz
build xtrans xtrans-1.5.0.tar.xz
build libXau libXau-1.0.11.tar.xz
build xcb-proto xcb-proto-1.16.0.tar.xz
build libxcb libxcb-1.16.tar.xz --disable-devel-docs
build libX11 libX11-1.8.7.tar.xz --disable-specs --disable-docs
build libXext libXext-1.3.6.tar.xz --disable-specs
# freetype alone: no harfbuzz, png, bzip2, brotli, zlib (the console font and SDL2_ttf need nothing more)
build freetype freetype-2.13.2.tar.xz --without-harfbuzz --without-png --without-bzip2 --without-brotli --without-zlib

# alsa-lib (LGPL 2.1): PCM only. Static means no plugin loading: the host's alsa.conf is read, but a plugin that lives in a shared
# library (pulse, pipewire) cannot be opened, only the built-in ones and the hardware devices.
build alsa-lib alsa-lib-1.2.11.tar.bz2 --with-libdl=no --with-configdir=/usr/share/alsa --disable-python --disable-mixer \
  --disable-rawmidi --disable-seq --disable-ucm --disable-topology --disable-aload --disable-hwdep --disable-old-symbols

# SDL2: X11 and ALSA linked in (no dlopen), software renderer, no joystick. The two HAVE_* are given
# because cmake's own try-compile cannot link a static libX11 (it lacks libxcb) and would turn the features off.
echo "=== SDL2"
rm -rf $D/build/SDL2 $D/build/SDL2-b; mkdir -p $D/build/SDL2 $D/build/SDL2-b
tar xf $D/src/SDL2/SDL2-2.30.0.tar.gz -C $D/build/SDL2 --strip-components=1
cd $D/build/SDL2-b
cmake -S $D/build/SDL2 -B . -G Ninja \
  -DCMAKE_C_COMPILER=$CC -DCMAKE_BUILD_TYPE=Release -DCMAKE_INSTALL_PREFIX=/usr \
  -DCMAKE_C_FLAGS="-O2 -I$D/khdr -I$STAGE/usr/include" \
  -DCMAKE_PREFIX_PATH=$STAGE/usr -DCMAKE_FIND_ROOT_PATH=$STAGE \
  -DCMAKE_FIND_ROOT_PATH_MODE_INCLUDE=ONLY -DCMAKE_FIND_ROOT_PATH_MODE_LIBRARY=ONLY \
  -DSDL_STATIC=ON -DSDL_SHARED=OFF -DSDL_TEST=OFF -DSDL_TESTS=OFF -DSDL_INSTALL_TESTS=OFF \
  -DSDL_X11=ON -DSDL_X11_SHARED=OFF -DSDL_X11_XCURSOR=OFF -DSDL_X11_XDBE=OFF -DSDL_X11_XFIXES=OFF \
  -DSDL_X11_XINPUT=OFF -DSDL_X11_XRANDR=OFF -DSDL_X11_XSCRNSAVER=OFF -DSDL_X11_XSHAPE=OFF \
  -DSDL_WAYLAND=OFF -DSDL_KMSDRM=OFF -DSDL_OPENGL=OFF -DSDL_OPENGLES=OFF -DSDL_VULKAN=OFF \
  -DSDL_RPI=OFF -DSDL_VIVANTE=OFF -DSDL_DIRECTFB=OFF -DSDL_OFFSCREEN=ON -DSDL_DUMMYVIDEO=ON \
  -DSDL_ALSA=ON -DSDL_ALSA_SHARED=OFF -DALSA_LIBRARY=$STAGE/usr/lib/libasound.a -DALSA_INCLUDE_DIR=$STAGE/usr/include -DSDL_PULSEAUDIO=OFF -DSDL_PIPEWIRE=OFF -DSDL_JACK=OFF -DSDL_SNDIO=OFF -DSDL_OSS=OFF \
  -DSDL_ARTS=OFF -DSDL_ESD=OFF -DSDL_NAS=OFF -DSDL_FUSIONSOUND=OFF -DSDL_LIBSAMPLERATE=OFF \
  -DSDL_DBUS=OFF -DSDL_IBUS=OFF -DSDL_LIBUDEV=OFF -DSDL_HIDAPI=OFF -DSDL_JOYSTICK=OFF -DSDL_HAPTIC=OFF \
  -DSDL_SENSOR=OFF -DSDL_VIRTUAL_JOYSTICK=OFF -DHAVE_XGENERICEVENT=1 -DSDL_VIDEO_DRIVER_X11_HAS_XKBKEYCODETOKEYSYM=1 \
  -DSDL_RPATH=OFF -DSDL_LIBICONV=OFF \
  -DX11_X11_INCLUDE_PATH=$STAGE/usr/include -DX11_X11_LIB=$STAGE/usr/lib/libX11.a \
  -DX11_Xext_INCLUDE_PATH=$STAGE/usr/include -DX11_Xext_LIB=$STAGE/usr/lib/libXext.a \
  > cmake.log 2>&1 || { tail -40 cmake.log; exit 1; }
ninja > ninja.log 2>&1 || { tail -40 ninja.log; exit 1; }
DESTDIR=$STAGE ninja install > install.log 2>&1 || { tail -20 install.log; exit 1; }
echo "SDL2 static: $STAGE/usr/lib/x86_64-linux-gnu/libSDL2.a"

# SDL2_image: the decoders of stb_image inside (PNG, JPEG, BMP, GIF...), no libpng, no libjpeg, no SVG, no TIFF/WEBP/AVIF.
echo "=== SDL2_image"
rm -rf $D/build/SDL2_image $D/build/SDL2_image-b; mkdir -p $D/build/SDL2_image $D/build/SDL2_image-b
tar xf $D/src/SDL2_image/SDL2_image-2.8.2.tar.gz -C $D/build/SDL2_image --strip-components=1
cd $D/build/SDL2_image-b
cmake -S $D/build/SDL2_image -B . -G Ninja \
  -DCMAKE_C_COMPILER=$CC -DCMAKE_BUILD_TYPE=Release -DCMAKE_INSTALL_PREFIX=/usr \
  -DCMAKE_C_FLAGS="-O2 -I$D/khdr -I$STAGE/usr/include/SDL2" \
  -DSDL2_DIR=$STAGE/usr/lib/x86_64-linux-gnu/cmake/SDL2 -DCMAKE_PREFIX_PATH=$STAGE/usr \
  -DBUILD_SHARED_LIBS=OFF -DSDL2IMAGE_DEPS_SHARED=OFF -DSDL2IMAGE_VENDORED=OFF -DSDL2IMAGE_INSTALL=ON \
  -DSDL2IMAGE_SAMPLES=OFF -DSDL2IMAGE_TESTS=OFF -DSDL2IMAGE_BACKEND_STB=ON \
  -DSDL2IMAGE_SVG=OFF -DSDL2IMAGE_AVIF=OFF -DSDL2IMAGE_JXL=OFF -DSDL2IMAGE_TIF=OFF -DSDL2IMAGE_WEBP=OFF \
  > cmake.log 2>&1 || { tail -40 cmake.log; exit 1; }
ninja > ninja.log 2>&1 || { tail -40 ninja.log; exit 1; }
DESTDIR=$STAGE ninja install > install.log 2>&1 || { tail -20 install.log; exit 1; }
echo "SDL2_image static: $STAGE/usr/lib/libSDL2_image.a"

# SDL2_ttf: freetype from the stage, no harfbuzz.
echo "=== SDL2_ttf"
rm -rf $D/build/SDL2_ttf $D/build/SDL2_ttf-b; mkdir -p $D/build/SDL2_ttf $D/build/SDL2_ttf-b
tar xf $D/src/SDL2_ttf/SDL2_ttf-2.22.0.tar.gz -C $D/build/SDL2_ttf --strip-components=1
cd $D/build/SDL2_ttf-b
cmake -S $D/build/SDL2_ttf -B . -G Ninja \
  -DCMAKE_C_COMPILER=$CC -DCMAKE_BUILD_TYPE=Release -DCMAKE_INSTALL_PREFIX=/usr \
  -DCMAKE_C_FLAGS="-O2 -I$D/khdr -I$STAGE/usr/include/SDL2 -I$STAGE/usr/include/freetype2" \
  -DSDL2_DIR=$STAGE/usr/lib/x86_64-linux-gnu/cmake/SDL2 -DCMAKE_PREFIX_PATH=$STAGE/usr \
  -DBUILD_SHARED_LIBS=OFF -DSDL2TTF_VENDORED=OFF -DSDL2TTF_HARFBUZZ=OFF -DSDL2TTF_SAMPLES=OFF -DSDL2TTF_INSTALL=ON \
  -DFREETYPE_LIBRARY=$STAGE/usr/lib/libfreetype.a -DFREETYPE_INCLUDE_DIR_freetype2=$STAGE/usr/include/freetype2 \
  -DFREETYPE_INCLUDE_DIR_ft2build=$STAGE/usr/include/freetype2 \
  > cmake.log 2>&1 || { tail -40 cmake.log; exit 1; }
ninja > ninja.log 2>&1 || { tail -40 ninja.log; exit 1; }
DESTDIR=$STAGE ninja install > install.log 2>&1 || { tail -20 install.log; exit 1; }
echo "SDL2_ttf static: $STAGE/usr/lib/libSDL2_ttf.a"

# SDL2_mixer: only the decoders that are inside it (WAV, MP3 minimp3, FLAC drflac, OGG stb_vorbis, MIDI timidity).
echo "=== SDL2_mixer"
rm -rf $D/build/SDL2_mixer $D/build/SDL2_mixer-b; mkdir -p $D/build/SDL2_mixer $D/build/SDL2_mixer-b
tar xf $D/src/SDL2_mixer/SDL2_mixer-2.8.0.tar.gz -C $D/build/SDL2_mixer --strip-components=1
cd $D/build/SDL2_mixer-b
cmake -S $D/build/SDL2_mixer -B . -G Ninja \
  -DCMAKE_C_COMPILER=$CC -DCMAKE_BUILD_TYPE=Release -DCMAKE_INSTALL_PREFIX=/usr \
  -DCMAKE_C_FLAGS="-O2 -I$D/khdr -I$STAGE/usr/include/SDL2" \
  -DSDL2_DIR=$STAGE/usr/lib/x86_64-linux-gnu/cmake/SDL2 -DCMAKE_PREFIX_PATH=$STAGE/usr \
  -DBUILD_SHARED_LIBS=OFF -DSDL2MIXER_DEPS_SHARED=OFF -DSDL2MIXER_VENDORED=OFF -DSDL2MIXER_INSTALL=ON \
  -DSDL2MIXER_SAMPLES=OFF -DSDL2MIXER_CMD=OFF -DSDL2MIXER_MOD=OFF -DSDL2MIXER_OPUS=OFF -DSDL2MIXER_WAVPACK=OFF \
  -DSDL2MIXER_GME=OFF -DSDL2MIXER_MIDI_FLUIDSYNTH=OFF -DSDL2MIXER_MIDI_NATIVE=OFF -DSDL2MIXER_MIDI_TIMIDITY=ON \
  -DSDL2MIXER_MP3_MINIMP3=ON -DSDL2MIXER_FLAC_DRFLAC=ON -DSDL2MIXER_VORBIS=STB \
  > cmake.log 2>&1 || { tail -40 cmake.log; exit 1; }
ninja > ninja.log 2>&1 || { tail -40 ninja.log; exit 1; }
DESTDIR=$STAGE ninja install > install.log 2>&1 || { tail -20 install.log; exit 1; }
echo "SDL2_mixer static: $STAGE/usr/lib/libSDL2_mixer.a"
