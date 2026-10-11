package org.lugforth.app;

import android.content.Context;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;

// The user's folder, as ~/.local/share/lugforth is for the AppImage: the bundled
// examples are copied to <home>/examples the first time and never overwritten,
// so what the user edits or adds stays.
final class Files {
  private Files() {}

  // Android/data/org.lugforth.app/files, which a file manager or USB can reach
  static File home(Context c) {
    File d = c.getExternalFilesDir(null);
    return d != null ? d : c.getFilesDir();
  }

  static void prepare(Context c, File home) {
    try {
      copyTree(c, "examples", new File(home, "examples"), false);
      copyTree(c, "forth", home, true);  // main.fs: always the current one
    } catch (IOException e) {
      android.util.Log.e("lugforth", "copying files failed", e);
    }
  }

  private static void copyTree(Context c, String asset, File dst, boolean overwrite)
      throws IOException {
    String[] names = c.getAssets().list(asset);
    if (names == null || names.length == 0) {  // a file
      if (overwrite || !dst.exists()) copyFile(c, asset, dst);
      return;
    }
    dst.mkdirs();
    for (String n : names) copyTree(c, asset + "/" + n, new File(dst, n), overwrite);
  }

  private static void copyFile(Context c, String asset, File dst) throws IOException {
    try (InputStream in = c.getAssets().open(asset);
         FileOutputStream out = new FileOutputStream(dst)) {
      byte[] buf = new byte[16384];
      int n;
      while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
    }
  }
}
