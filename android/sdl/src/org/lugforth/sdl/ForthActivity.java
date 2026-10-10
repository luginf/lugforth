package org.lugforth.sdl;

import android.os.Bundle;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import org.libsdl.app.SDLActivity;

// SDLActivity that runs lugforth (libmain.so) on a Forth file. The files under
// assets/forth are copied to the app files directory, and the one named in
// assets/forth/autostart.txt (default main.fs) is given to lugforth as argv[1].
public class ForthActivity extends SDLActivity {
  private String startFile = "main.fs";

  @Override protected void onCreate(Bundle state) {
    try {
      String[] names = getAssets().list("forth");
      for (String n : names) copyAsset("forth/" + n, new File(getFilesDir(), n));
      File marker = new File(getFilesDir(), "autostart.txt");
      if (marker.exists()) {
        byte[] b = new byte[(int) marker.length()];
        try (java.io.FileInputStream in = new java.io.FileInputStream(marker)) {
          in.read(b);
        }
        String s = new String(b).trim();
        if (!s.isEmpty()) startFile = s;
      }
    } catch (IOException e) {
      android.util.Log.e("lugforth", "asset copy failed", e);
    }
    super.onCreate(state);
  }

  private void copyAsset(String asset, File dst) throws IOException {
    try (InputStream in = getAssets().open(asset);
         FileOutputStream out = new FileOutputStream(dst)) {
      byte[] buf = new byte[16384];
      int n;
      while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
    }
  }

  @Override protected String[] getLibraries() {
    return new String[] { "SDL2", "main" };
  }

  @Override protected String[] getArguments() {
    return new String[] { new File(getFilesDir(), startFile).getPath() };
  }

  // The back button quits the running program (SDL_QUIT), as closing a window.
  // SDL would otherwise turn it into a key event, so it is caught before.
  @Override public boolean dispatchKeyEvent(android.view.KeyEvent e) {
    if (e.getKeyCode() == android.view.KeyEvent.KEYCODE_BACK) {
      if (e.getAction() == android.view.KeyEvent.ACTION_UP) nativeSendQuit();
      return true;
    }
    return super.dispatchKeyEvent(e);
  }
}
