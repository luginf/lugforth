package org.lugforth.web;

import android.app.Activity;
import android.os.Bundle;
import android.webkit.MimeTypeMap;
import android.webkit.WebResourceRequest;
import android.webkit.WebResourceResponse;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import java.io.IOException;
import java.io.InputStream;

// Shows the web build of lugforth (assets/) in a WebView. The pages are served
// from a fake https origin so that XMLHttpRequest on the .fs files works.
public class MainActivity extends Activity {
  static final String ORIGIN = "https://appassets.androidplatform.net/";
  private WebView web;

  @Override protected void onCreate(Bundle state) {
    super.onCreate(state);
    web = new WebView(this);
    setContentView(web);
    WebSettings s = web.getSettings();
    s.setJavaScriptEnabled(true);
    s.setDomStorageEnabled(true);
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
          InputStream in = getAssets().open(path);
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
  }

  @Override public void onBackPressed() {
    if (web.canGoBack()) web.goBack(); else super.onBackPressed();
  }

  @Override protected void onPause() { super.onPause(); web.onPause(); }
  @Override protected void onResume() { super.onResume(); web.onResume(); }
}
