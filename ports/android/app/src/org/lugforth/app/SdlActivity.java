package org.lugforth.app;

import android.os.Bundle;
import android.os.Process;
import android.system.ErrnoException;
import android.system.Os;
import android.view.KeyEvent;
import android.view.MotionEvent;
import android.view.ScaleGestureDetector;
import android.view.ViewGroup;
import java.io.File;
import org.libsdl.app.SDLActivity;

// SDLActivity that runs lugforth (libmain.so) on assets/forth/main.fs, in the
// examples folder of the user. Under the menu bar; a pinch is sent to the Forth
// console as the keys F12 (bigger) and F11 (smaller), as the buttons are.
public class SdlActivity extends SDLActivity implements MenuBar.Target {
  private File home;
  private ScaleGestureDetector pinch;
  private float acc = 1f;

  @Override protected void onCreate(Bundle state) {
    home = Files.home(this);
    Files.prepare(this, home);
    try {
      Os.setenv("LUGFORTH_HOME", home.getPath(), true);
      Os.setenv("LUGFORTH_CWD", new File(home, "examples").getPath(), true);
      Os.setenv("LUGFORTH_FONT", new File(home, "font.ttf").getPath(), true);
    } catch (ErrnoException e) {
      android.util.Log.e("lugforth", "setenv failed", e);
    }
    super.onCreate(state);
    ViewGroup content = mLayout;
    ((ViewGroup) content.getParent()).removeView(content);
    setContentView(MenuBar.wrap(this, content, "Lugforth SDL2", this));
    pinch = new ScaleGestureDetector(this,
        new ScaleGestureDetector.SimpleOnScaleGestureListener() {
          @Override public boolean onScale(ScaleGestureDetector d) {
            acc *= d.getScaleFactor();
            if (acc > 1.1f) { font(1); acc = 1f; }
            else if (acc < 0.9f) { font(-1); acc = 1f; }
            return true;
          }
          @Override public boolean onScaleBegin(ScaleGestureDetector d) {
            acc = 1f;
            return true;
          }
        });
  }

  @Override protected String[] getLibraries() {
    return new String[] { "SDL2", "main" };
  }

  @Override protected String[] getArguments() {
    return new String[] { new File(home, "main.fs").getPath() };
  }

  @Override public boolean dispatchTouchEvent(MotionEvent e) {
    if (pinch != null) pinch.onTouchEvent(e);
    return super.dispatchTouchEvent(e);
  }

  // The back button quits the running program (SDL_QUIT), as closing a window.
  // SDL would otherwise turn it into a key event, so it is caught before.
  @Override public boolean dispatchKeyEvent(KeyEvent e) {
    if (e.getKeyCode() == KeyEvent.KEYCODE_BACK) {
      if (e.getAction() == KeyEvent.ACTION_UP) nativeSendQuit();
      return true;
    }
    return super.dispatchKeyEvent(e);
  }

  @Override public void key(int keyCode, boolean down) {
    if (down) onNativeKeyDown(keyCode); else onNativeKeyUp(keyCode);
  }

  @Override public void font(int delta) {
    int code = delta > 0 ? KeyEvent.KEYCODE_F12 : KeyEvent.KEYCODE_F11;
    onNativeKeyDown(code);
    onNativeKeyUp(code);
  }

  // The .fs files of the examples folder and of its folders (games/, music/fm/...), as
  // "folder/name.fs", sorted; the tests are left out
  @Override public String[] examples() {
    java.util.ArrayList<String> out = new java.util.ArrayList<String>();
    scan(new File(home, "examples"), "", 0, out);
    String[] l = out.toArray(new String[0]);
    java.util.Arrays.sort(l);
    return l;
  }

  private static void scan(File dir, String prefix, int depth, java.util.ArrayList<String> out) {
    File[] fs = dir.listFiles();
    if (fs == null) return;
    for (File f : fs) {
      String n = f.getName();
      if (f.isDirectory()) {
        if (depth < 3) scan(f, prefix + n + "/", depth + 1, out);
      } else if (n.endsWith(".fs") && !n.endsWith("_tests.fs")) {
        out.add(prefix + n);
      }
    }
  }

  // Types "include name" and Enter. The text goes to SDL as a committed text (any character,
  // as '/' of games/x.fs, whatever the keyboard layout); the key events of a virtual keyboard are
  // the fallback.
  @Override public void run(String name) {
    String line = "include " + name;
    boolean typed = false;
    try {
      Class<?> c = Class.forName("org.libsdl.app.SDLInputConnection");
      java.lang.reflect.Method m = c.getDeclaredMethod("nativeCommitText", String.class, int.class);
      m.setAccessible(true);
      m.invoke(null, line, 1);
      typed = true;
    } catch (Throwable e) {
      android.util.Log.e("lugforth", "commit text failed, typing keys", e);
    }
    if (!typed) {
      KeyEvent[] ev = android.view.KeyCharacterMap.load(
          android.view.KeyCharacterMap.VIRTUAL_KEYBOARD).getEvents(line.toCharArray());
      if (ev != null) for (KeyEvent e : ev) dispatchKeyEvent(e);
      else android.util.Log.e("lugforth", "no key events for: " + line);
    }
    long t = android.os.SystemClock.uptimeMillis();
    dispatchKeyEvent(new KeyEvent(t, t, KeyEvent.ACTION_DOWN, KeyEvent.KEYCODE_ENTER, 0));
    dispatchKeyEvent(new KeyEvent(t, t, KeyEvent.ACTION_UP, KeyEvent.KEYCODE_ENTER, 0));
  }

  @Override public void start() { finish(); }

  @Override public String help() {
    return "Pinch out or in: bigger or smaller text.\n\n"
        + "Menu keys: Tab completes a word or a file name, arrows move in the line and through the history, "
        + "Ctrl then a letter types a control code (Ctrl, S saves in ed). The \u22ee button lists the examples (a tap includes one), the text size and this help. A keyboard has F11 and F12 "
        + "for the text size.\n\n"
        + "Try:\n  1 2 + .\n  words\n  ls\n  include games/gfx_breakout.fs\n\n"
        + "The examples are in\n" + new File(home, "examples").getPath() + "\n"
        + "Files you add there are kept. Edit them with any editor and "
        + "include them.\n\n"
        + "Back quits the running program and returns to the start screen.";
  }

  // Leaving ends the process: SDL does not start twice in one process
  @Override protected void onDestroy() {
    if (isFinishing()) Process.killProcess(Process.myPid());
    super.onDestroy();
  }
}
