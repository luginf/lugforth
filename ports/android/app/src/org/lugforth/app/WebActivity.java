package org.lugforth.app;

import android.app.Activity;
import android.os.Bundle;
import android.view.KeyEvent;
import android.webkit.MimeTypeMap;
import android.webkit.WebResourceRequest;
import android.webkit.WebResourceResponse;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import java.io.IOException;
import java.io.InputStream;

// Shows the web build of lugforth (assets/web) in a WebView, under the menu bar.
// The pages are served from a fake https origin so that XMLHttpRequest on the
// .fs files works. The page does the pinch zoom itself (platform.fs).
public class WebActivity extends Activity implements MenuBar.Target {
  static final String ORIGIN = "https://appassets.androidplatform.net/";
  private WebView web;

  @Override protected void onCreate(Bundle state) {
    super.onCreate(state);
    web = new WebView(this);
    WebSettings s = web.getSettings();
    s.setJavaScriptEnabled(true);
    s.setDomStorageEnabled(true);  // the text size is remembered in localStorage
    s.setMediaPlaybackRequiresUserGesture(false);
    s.setAllowFileAccess(false);
    web.setWebViewClient(new WebViewClient() {
      @Override public WebResourceResponse shouldInterceptRequest(
          WebView v, WebResourceRequest r) {
        String url = r.getUrl().toString();
        if (!url.startsWith(ORIGIN)) return null;
        String path = url.substring(ORIGIN.length());
        int q = path.indexOf('?'); if (q >= 0) path = path.substring(0, q);
        int h = path.indexOf('#'); if (h >= 0) path = path.substring(0, h);
        if (path.isEmpty()) path = "index.html";
        try {
          InputStream in = getAssets().open("web/" + path);
          String ext = MimeTypeMap.getFileExtensionFromUrl(path);
          String mime = "fs".equals(ext) ? "text/plain" :
              "js".equals(ext) ? "application/javascript" :
              MimeTypeMap.getSingleton().getMimeTypeFromExtension(ext);
          return new WebResourceResponse(
              mime == null ? "application/octet-stream" : mime, "utf-8", in);
        } catch (IOException e) {
          return new WebResourceResponse("text/plain", "utf-8", 404, "Not Found",
              null, null);
        }
      }
    });
    web.loadUrl(ORIGIN + "index.html");
    setContentView(MenuBar.wrap(this, web, "Lugforth web", this));
  }

  private void js(String code) {
    web.evaluateJavascript("(function(){" + code + "})()", null);
  }

  @Override public void key(int keyCode, boolean down) {
    if (!down) return;
    String name;
    switch (keyCode) {
      case KeyEvent.KEYCODE_DPAD_UP: name = "ArrowUp"; break;
      case KeyEvent.KEYCODE_DPAD_DOWN: name = "ArrowDown"; break;
      case KeyEvent.KEYCODE_DPAD_LEFT: name = "ArrowLeft"; break;
      case KeyEvent.KEYCODE_DPAD_RIGHT: name = "ArrowRight"; break;
      default: name = "Tab";
    }
    js("if (window.lugkey) lugkey('" + name + "');");
  }

  @Override public void font(int delta) {
    js("if (window.lugfont) lugfont(" + delta + ");");
  }

  @Override public void start() { finish(); }

  @Override public String[] examples() { return null; }

  @Override public void run(String name) {}

  @Override public String help() {
    return "Pinch out or in: bigger or smaller text.\n\n"
        + "Menu keys: arrows move in the line and through the history, "
        + "Tab completes a word.\n\n"
        + "In the terminal try:\n  1 2 + .\n  words\n  see dup\n\n"
        + "Back goes to the previous page, then to the start screen.";
  }

  @Override public void onBackPressed() {
    if (web.canGoBack()) web.goBack(); else super.onBackPressed();
  }

  @Override protected void onPause() { super.onPause(); web.onPause(); }
  @Override protected void onResume() { super.onResume(); web.onResume(); }
}
