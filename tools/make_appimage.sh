#!/bin/bash
# Adapted from the Di-111 synth project (same author).
# Packs a built Standalone binary into an AppImage.
#
#   tools/make_appimage.sh --name lugforth --binary path/to/lugforth --icon docs/images/lugforth_256.png \
#       --comment "..." [--terminal] [--categories "Development;"] [--version 0.9.5] [--out dir] [--update-info "gh-releases-zsync|user|repo|latest|Name-*x86_64.AppImage.zsync"] [--bundle libjack.so.0 --bundle libdb-5.3.so]
#
# Run it on an OLD system (the CI job uses a debian:10 container, see
# .github/workflows/release.yml): an AppImage uses the host's C library, so what it needs is
# fixed by the system it was BUILT on. One built on a recent system fails on older ones with
# "version `GLIBC_2.38' not found" (that is what happens with a hand-made one).
#
# --examples DIR ships DIR in the AppImage. On each run AppRun copies the files that are missing into
# ${NAME_HOME:-${XDG_DATA_HOME:-~/.local/share}/NAME}/examples (never overwriting what the user changed), and exports
# NAME_EXAMPLES with that path. With --terminal, an AppImage started with no terminal (a click on the desktop: no tty, stdin on
# /dev/null) and no argument opens a terminal emulator and runs itself in it, in the NAME_HOME directory, so that
# `include examples/foo.fs` works; started from a console it runs as usual in the current directory.
#
# --gui makes the click run `NAME --gui` (a window with a menu and a console, made with SDL2) instead of opening a
# terminal emulator. The terminal stays the fallback when that window cannot open. It runs in the examples folder.
# --font FILE (and --font-license FILE) ships a TrueType font for that window; the user can still pick another one.
#
# --bundle LIB copies LIB (found through the binary's own `ldd`) into the AppImage. Everything else (ALSA, X11,
# freetype, fontconfig...) is expected from the host, as for any AppImage.
set -euo pipefail

NAME="" BINARY="" ICON="" COMMENT="" VERSION="" OUT="." UPDATE_INFO="" BUNDLE=() EXAMPLES="" GUI=false FONT="" FONT_LICENSE="" TERMINAL=false CATEGORIES="Utility;"
while [ $# -gt 0 ]; do
	case "$1" in
	--name) NAME="$2"; shift 2 ;;
	--binary) BINARY="$2"; shift 2 ;;
	--icon) ICON="$2"; shift 2 ;;
	--comment) COMMENT="$2"; shift 2 ;;
	--version) VERSION="$2"; shift 2 ;;
	--out) OUT="$2"; shift 2 ;;
	--update-info) UPDATE_INFO="$2"; shift 2 ;;
	--examples) EXAMPLES="$2"; shift 2 ;;
	--bundle) BUNDLE+=("$2"); shift 2 ;;
	--terminal) TERMINAL=true; shift ;;
	--gui) GUI=true; shift ;;
	--font) FONT="$2"; shift 2 ;;
	--font-license) FONT_LICENSE="$2"; shift 2 ;;
	--categories) CATEGORIES="$2"; shift 2 ;;
	*) echo "unknown argument: $1" >&2; exit 2 ;;
	esac
done
[ -n "$NAME" ] && [ -f "$BINARY" ] && [ -f "$ICON" ] || { echo "need --name, --binary and --icon (existing files)" >&2; exit 2; }
[ -n "$COMMENT" ] || COMMENT="$NAME"
ID=$(echo "$NAME" | tr 'A-Z' 'a-z')
mkdir -p "$OUT"
OUT=$(cd "$OUT" && pwd)

WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT
APPDIR="$WORK/$NAME.AppDir"
mkdir -p "$APPDIR/usr/bin" "$APPDIR/usr/lib" "$APPDIR/usr/share/applications" "$APPDIR/usr/share/icons/hicolor/256x256/apps"

install -m755 "$BINARY" "$APPDIR/usr/bin/$NAME"
strip "$APPDIR/usr/bin/$NAME" 2>/dev/null || true

for lib in "${BUNDLE[@]}"; do
	path=$(ldd "$BINARY" | awk -v l="$lib" '$1 == l { print $3; exit }')
	# a library the binary only needs through another one (libdb through libjack) is not in its own ldd
	[ -n "$path" ] || path=$(ldconfig -p | awk -v l="$lib" '$1 == l { print $NF; exit }')
	if [ -f "$path" ]; then cp -L "$path" "$APPDIR/usr/lib/$lib"; else echo "warning: cannot find $lib to bundle, skipped" >&2; fi
done

cp "$ICON" "$APPDIR/$ID.png"
cp "$ICON" "$APPDIR/usr/share/icons/hicolor/256x256/apps/$ID.png"
cp "$ICON" "$APPDIR/.DirIcon"

cat > "$APPDIR/$ID.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=$NAME
Comment=$COMMENT
Exec=$NAME
Icon=$ID
Categories=$CATEGORIES
Terminal=false
X-AppImage-Name=$NAME
${VERSION:+X-AppImage-Version=$VERSION}
EOF
cp "$APPDIR/$ID.desktop" "$APPDIR/usr/share/applications/$ID.desktop"

if [ -n "$EXAMPLES" ]; then
	[ -d "$EXAMPLES" ] || { echo "--examples: $EXAMPLES is not a directory" >&2; exit 2; }
	mkdir -p "$APPDIR/usr/share/$ID/examples"
	cp -R "$EXAMPLES/." "$APPDIR/usr/share/$ID/examples/"
fi

if [ -n "$FONT" ]; then
	[ -f "$FONT" ] || { echo "--font: $FONT is not a file" >&2; exit 2; }
	mkdir -p "$APPDIR/usr/share/$ID/desktop"
	cp "$FONT" "$APPDIR/usr/share/$ID/desktop/font.ttf"
	[ -z "$FONT_LICENSE" ] || cp "$FONT_LICENSE" "$APPDIR/usr/share/$ID/desktop/font-license.txt"
fi

# Terminal=false in the .desktop file on purpose: not every desktop knows how to open a terminal for Terminal=true,
# and AppRun below does it itself when there is none (see the header).
VAR=$(echo "$ID" | tr 'a-z' 'A-Z' | tr -c 'A-Z0-9\n' _)
{
	cat <<'EOS'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
export LD_LIBRARY_PATH="$HERE/usr/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
DATA="${@VAR@_HOME:-${XDG_DATA_HOME:-$HOME/.local/share}/@ID@}"
if [ -d "$HERE/usr/share/@ID@/examples" ]; then
	# the AppImage is read-only: copy without its modes, and never overwrite the user's own versions
	mkdir -p "$DATA/examples" 2>/dev/null &&
		cp -R -n --no-preserve=mode "$HERE/usr/share/@ID@/examples/." "$DATA/examples/" 2>/dev/null
	export @VAR@_EXAMPLES="$DATA/examples"
fi
EOS
	if $TERMINAL || $GUI; then
		cat <<'EOS'
# No terminal and no argument (a click on the desktop: no tty, stdin on /dev/null or closed). A pipe
# (`echo ... | lugforth`) is not a click, and runs as is.
if [ $# -eq 0 ] && [ -z "$@VAR@_NO_TERMINAL" ] && [ ! -t 0 ] && [ ! -t 1 ] && [ ! -t 2 ] &&
	{ [ ! -e /proc/self/fd/0 ] || [ /proc/self/fd/0 -ef /dev/null ]; } &&
	{ [ -n "$DISPLAY" ] || [ -n "$WAYLAND_DISPLAY" ]; }; then
	SELF="${APPIMAGE:-$HERE/AppRun}"
	mkdir -p "$DATA" 2>/dev/null && cd "$DATA"
EOS
		if $GUI; then
			cat <<'EOS'
	# A window of our own, in the examples folder; if it cannot open (exit code 1), a terminal below
	[ -d "$DATA/examples" ] && cd "$DATA/examples"
	[ -f "$HERE/usr/share/@ID@/desktop/font.ttf" ] && export @VAR@_FONT="$HERE/usr/share/@ID@/desktop/font.ttf"
	"$HERE/usr/bin/@NAME@" --gui && exit 0
	cd "$DATA"
EOS
		fi
		if $TERMINAL; then
			cat <<'EOS'
	have() { command -v "$1" >/dev/null 2>&1; }
	if have x-terminal-emulator; then exec x-terminal-emulator -e "$SELF"
	elif have gnome-terminal; then exec gnome-terminal -- "$SELF"
	elif have konsole; then exec konsole -e "$SELF"
	elif have xfce4-terminal; then exec xfce4-terminal -x "$SELF"
	elif have mate-terminal; then exec mate-terminal -x "$SELF"
	elif have lxterminal; then exec lxterminal -e "$SELF"
	elif have kitty; then exec kitty "$SELF"
	elif have alacritty; then exec alacritty -e "$SELF"
	elif have foot; then exec foot "$SELF"
	elif have xterm; then exec xterm -e "$SELF"
	fi
	MSG="@NAME@ needs a terminal and none was found: run it from a console."
	if have notify-send; then notify-send "@NAME@" "$MSG"; elif have zenity; then zenity --error --text="$MSG"; else echo "$MSG" >&2; fi
	exit 1
EOS
		else
			echo '	exit 1'
		fi
		echo 'fi'
	fi
	echo 'exec "$HERE/usr/bin/@NAME@" "$@"'
} | sed -e "s/@VAR@/$VAR/g" -e "s/@ID@/$ID/g" -e "s/@NAME@/$NAME/g" > "$APPDIR/AppRun"
chmod +x "$APPDIR/AppRun"

# appimagetool from the AppImage project: its runtime is the current static "type 2" one, which needs neither
# libfuse2 nor a particular C library on the machine that runs the result. Override with APPIMAGETOOL=path.
TOOL="${APPIMAGETOOL:-}"
TOOL_ARGS=()
if [ -z "$TOOL" ]; then
	TOOL="$WORK/appimagetool-x86_64.AppImage"
	curl -fsSL -o "$TOOL" https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage
	chmod +x "$TOOL"
	# no FUSE in a container: unpack and run instead of mounting
	TOOL_ARGS=(--appimage-extract-and-run)
fi

TARGET="$OUT/$NAME-x86_64.AppImage"
# Update information (gh-releases-zsync: AppImageUpdate looks at the latest non-prerelease GitHub release for a
# *.AppImage.zsync asset). appimagetool embeds it and, when zsyncmake is installed, writes $TARGET.zsync into the
# current directory: that file must be published next to the AppImage in the release.
UPDATE_ARGS=()
if [ -n "$UPDATE_INFO" ]; then
	command -v zsyncmake >/dev/null || { echo "zsyncmake is needed to build the .zsync file" >&2; exit 1; }
	UPDATE_ARGS=(-u "$UPDATE_INFO")
fi
(cd "$OUT" && ARCH=x86_64 "$TOOL" "${TOOL_ARGS[@]}" --no-appstream "${UPDATE_ARGS[@]}" "$APPDIR" "$TARGET")
echo "built $TARGET"
echo "glibc symbol versions it needs (should not exceed the build system's LTS):"
objdump -T "$APPDIR/usr/bin/$NAME" | grep -o 'GLIBC_[0-9.]*' | sort -uV | tail -1
