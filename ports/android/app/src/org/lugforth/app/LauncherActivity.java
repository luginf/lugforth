package org.lugforth.app;

import android.app.Activity;
import android.content.Intent;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowInsets;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;

// The first screen: the web version or the native SDL2 version.
public class LauncherActivity extends Activity {
  private float dp;

  private int px(float v) { return (int) (v * dp + 0.5f); }

  private View choice(String title, String text, final Class<?> target) {
    LinearLayout card = new LinearLayout(this);
    card.setOrientation(LinearLayout.VERTICAL);
    card.setPadding(px(20), px(18), px(20), px(18));
    GradientDrawable bg = new GradientDrawable();
    bg.setColor(0xFF3C4043);
    bg.setCornerRadius(px(10));
    card.setBackground(bg);
    TextView t = new TextView(this);
    t.setText(title);
    t.setTextColor(0xFFFFFFFF);
    t.setTextSize(22);
    TextView d = new TextView(this);
    d.setText(text);
    d.setTextColor(0xFFBDC1C6);
    d.setTextSize(15);
    d.setPadding(0, px(6), 0, 0);
    card.addView(t);
    card.addView(d);
    card.setOnClickListener(new View.OnClickListener() {
      @Override public void onClick(View v) {
        startActivity(new Intent(LauncherActivity.this, target));
      }
    });
    LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
        ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
    lp.topMargin = px(16);
    card.setLayoutParams(lp);
    return card;
  }

  @Override protected void onCreate(Bundle state) {
    super.onCreate(state);
    dp = getResources().getDisplayMetrics().density;
    LinearLayout root = new LinearLayout(this);
    root.setOrientation(LinearLayout.VERTICAL);
    root.setBackgroundColor(0xFF111111);
    root.setGravity(Gravity.CENTER_VERTICAL);
    root.setPadding(px(24), px(24), px(24), px(24));
    LinearLayout head = new LinearLayout(this);
    head.setOrientation(LinearLayout.HORIZONTAL);
    head.setGravity(Gravity.CENTER_VERTICAL);
    int logo = getResources().getIdentifier("ic_launcher", "drawable", getPackageName());
    if (logo != 0) {
      ImageView img = new ImageView(this);
      img.setImageResource(logo);
      head.addView(img, new LinearLayout.LayoutParams(px(72), px(72)));
    }
    TextView title = new TextView(this);
    title.setText("Lugforth");
    title.setTextColor(0xFFFFFFFF);
    title.setTextSize(32);
    title.setPadding(px(16), 0, 0, 0);
    head.addView(title);
    root.addView(head);
    root.addView(choice("Web version",
        "Terminal in a web view, and the games Breakout, Drums and Roguelike.",
        WebActivity.class));
    root.addView(choice("SDL2 version",
        "Native Forth with SDL2. The examples are copied to a folder you can edit.",
        SdlActivity.class));
    root.setOnApplyWindowInsetsListener(new View.OnApplyWindowInsetsListener() {
      @Override public WindowInsets onApplyWindowInsets(View v, WindowInsets in) {
        v.setPadding(px(24) + in.getSystemWindowInsetLeft(),
                     px(24) + in.getSystemWindowInsetTop(),
                     px(24) + in.getSystemWindowInsetRight(),
                     px(24) + in.getSystemWindowInsetBottom());
        return in;
      }
    });
    setContentView(root);
  }
}
