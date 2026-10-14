package org.lugforth.app;

import android.app.Activity;
import android.app.Dialog;
import android.graphics.Typeface;
import android.view.Window;
import android.widget.ScrollView;
import android.graphics.drawable.GradientDrawable;
import android.view.Gravity;
import android.view.KeyEvent;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowInsets;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.TextView;

// The bar at the top of both consoles: a hamburger that unfolds the keys a phone
// keyboard lacks (arrows, Tab), the text size and the help.
final class MenuBar {
  interface Target {
    void key(int keyCode, boolean down);  // Android key code
    void font(int delta);                 // +1 bigger, -1 smaller
    void start();                         // back to the start screen
    String help();
    String[] examples();                  // file names for the Examples button, or null
    void run(String name);                // include one of them
  }

  private static final int BAR = 0xFF202124, KEY = 0xFF3C4043, KEY_DOWN = 0xFF5F6368;

  private final Activity act;
  private final float dp;

  private MenuBar(Activity a) {
    act = a;
    dp = a.getResources().getDisplayMetrics().density;
  }

  static View wrap(Activity a, View content, String title, Target t) {
    return new MenuBar(a).build(content, title, t);
  }

  private int px(float v) { return (int) (v * dp + 0.5f); }

  private TextView label(String text, int sp) {
    TextView v = new TextView(act);
    v.setText(text);
    v.setTextColor(0xFFFFFFFF);
    v.setTextSize(sp);
    v.setGravity(Gravity.CENTER);
    return v;
  }

  private TextView button(String text, final Runnable press, final Runnable release) {
    final TextView v = label(text, 16);
    final GradientDrawable bg = new GradientDrawable();
    bg.setColor(KEY);
    bg.setCornerRadius(px(6));
    v.setBackground(bg);
    v.setOnTouchListener(new View.OnTouchListener() {
      @Override public boolean onTouch(View view, MotionEvent e) {
        switch (e.getActionMasked()) {
          case MotionEvent.ACTION_DOWN:
            bg.setColor(KEY_DOWN);
            if (press != null) press.run();
            return true;
          case MotionEvent.ACTION_UP:
          case MotionEvent.ACTION_CANCEL:
            bg.setColor(KEY);
            if (release != null) release.run();
            return true;
        }
        return true;
      }
    });
    LinearLayout.LayoutParams lp =
        new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MATCH_PARENT, 1);
    lp.setMargins(px(3), px(4), px(3), px(4));
    v.setLayoutParams(lp);
    return v;
  }

  // A key that stays pressed as long as the finger is on it, as a real key does
  private TextView key(String text, final int code, final Target t) {
    return button(text,
        new Runnable() { public void run() { t.key(code, true); } },
        new Runnable() { public void run() { t.key(code, false); } });
  }

  private TextView action(String text, Runnable r) {
    return button(text, null, r);
  }

  // A card with rounded corners over the dimmed console, not the system alert box
  private void showHelp(String text) {
    final Dialog dlg = new Dialog(act);
    dlg.requestWindowFeature(Window.FEATURE_NO_TITLE);
    LinearLayout card = new LinearLayout(act);
    card.setOrientation(LinearLayout.VERTICAL);
    card.setPadding(px(22), px(20), px(22), px(14));
    GradientDrawable bg = new GradientDrawable();
    bg.setColor(0xFF2B2D30);
    bg.setCornerRadius(px(20));
    card.setBackground(bg);
    TextView title = label("Lugforth", 22);
    title.setGravity(Gravity.START);
    card.addView(title);
    TextView body = label(text, 15);
    body.setGravity(Gravity.START);
    body.setTextColor(0xFFD0D3D8);
    body.setLineSpacing(0, 1.2f);
    body.setPadding(0, px(12), 0, px(12));
    ScrollView sv = new ScrollView(act);
    sv.addView(body);
    card.addView(sv, new LinearLayout.LayoutParams(
        ViewGroup.LayoutParams.MATCH_PARENT, 0, 1));
    TextView ok = label("OK", 16);
    ok.setTypeface(Typeface.DEFAULT_BOLD);
    ok.setTextColor(0xFF8AB4F8);
    ok.setPadding(px(24), px(10), px(24), px(10));
    ok.setOnClickListener(new View.OnClickListener() {
      @Override public void onClick(View v) { dlg.dismiss(); }
    });
    LinearLayout.LayoutParams ol = new LinearLayout.LayoutParams(
        ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
    ol.gravity = Gravity.END;
    card.addView(ok, ol);
    dlg.setContentView(card);
    Window w = dlg.getWindow();
    w.setBackgroundDrawable(new android.graphics.drawable.ColorDrawable(0));
    int width = act.getResources().getDisplayMetrics().widthPixels - px(32);
    int height = (int) (act.getResources().getDisplayMetrics().heightPixels * 0.8f);
    dlg.show();
    w.setLayout(width, ViewGroup.LayoutParams.WRAP_CONTENT);
    card.setMinimumHeight(0);
    sv.setLayoutParams(new LinearLayout.LayoutParams(
        ViewGroup.LayoutParams.MATCH_PARENT, Math.min(height, px(460))));
  }

  // The list of the example files; a tap includes one in the console
  private void showExamples(final Target t) {
    final String[] names = t.examples();
    android.app.AlertDialog.Builder b = new android.app.AlertDialog.Builder(act);
    b.setTitle("Examples");
    if (names.length == 0) b.setMessage("No .fs file in the examples folder.");
    else b.setItems(names, new android.content.DialogInterface.OnClickListener() {
      @Override public void onClick(android.content.DialogInterface d, int i) { t.run(names[i]); }
    });
    b.show();
  }

  // Ex, A-, A+ and ? are in a card, to keep room for the keys in the bar
  private void showMore(final Target t) {
    final java.util.ArrayList<String> items = new java.util.ArrayList<String>();
    if (t.examples() != null) items.add("Examples");
    items.add("Text smaller (A-)");
    items.add("Text bigger (A+)");
    items.add("Help (?)");
    final boolean ex = t.examples() != null;
    new android.app.AlertDialog.Builder(act)
        .setItems(items.toArray(new String[0]), new android.content.DialogInterface.OnClickListener() {
          @Override public void onClick(android.content.DialogInterface d, int i) {
            if (ex) i--;
            if (i < 0) showExamples(t);
            else if (i == 0) t.font(-1);
            else if (i == 1) t.font(1);
            else showHelp(t.help());
          }
        }).show();
  }

  private View build(View content, String title, final Target t) {
    final FrameLayout root = new FrameLayout(act);
    root.setBackgroundColor(0xFF000000);
    final int barH = px(44);

    LinearLayout col = new LinearLayout(act);
    col.setOrientation(LinearLayout.VERTICAL);

    LinearLayout bar = new LinearLayout(act);
    bar.setBackgroundColor(BAR);
    bar.setOrientation(LinearLayout.HORIZONTAL);
    bar.setGravity(Gravity.CENTER_VERTICAL);
    TextView burger = label("☰", 22);
    burger.setPadding(px(16), 0, px(16), 0);
    TextView name = label(title, 16);
    name.setGravity(Gravity.CENTER_VERTICAL);
    bar.addView(burger, new LinearLayout.LayoutParams(
        ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.MATCH_PARENT));
    bar.addView(name, new LinearLayout.LayoutParams(
        ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.MATCH_PARENT));
    col.addView(bar, new LinearLayout.LayoutParams(
        ViewGroup.LayoutParams.MATCH_PARENT, barH));
    col.addView(content, new LinearLayout.LayoutParams(
        ViewGroup.LayoutParams.MATCH_PARENT, 0, 1));
    root.addView(col, new FrameLayout.LayoutParams(
        ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));

    // The unfolded menu sits between the bar and the console, which gets smaller
    final LinearLayout panel = new LinearLayout(act);
    panel.setOrientation(LinearLayout.HORIZONTAL);
    panel.setBackgroundColor(BAR);
    panel.setVisibility(View.GONE);
    panel.addView(key("Tab", KeyEvent.KEYCODE_TAB, t));
    panel.addView(action("Ctrl", new Runnable() { public void run() {
      // sticky: the next letter typed is a control code (F9 for the SDL console)
      t.key(KeyEvent.KEYCODE_F9, true);
      t.key(KeyEvent.KEYCODE_F9, false);
    } }));
    panel.addView(key("\u2190", KeyEvent.KEYCODE_DPAD_LEFT, t));
    panel.addView(key("\u2191", KeyEvent.KEYCODE_DPAD_UP, t));
    panel.addView(key("\u2193", KeyEvent.KEYCODE_DPAD_DOWN, t));
    panel.addView(key("\u2192", KeyEvent.KEYCODE_DPAD_RIGHT, t));
    panel.addView(action("\u22ee", new Runnable() { public void run() { showMore(t); } }));
    panel.addView(action("\u2302", new Runnable() { public void run() { t.start(); } }));
    col.addView(panel, 1, new LinearLayout.LayoutParams(
        ViewGroup.LayoutParams.MATCH_PARENT, px(52)));

    View.OnClickListener toggle = new View.OnClickListener() {
      @Override public void onClick(View v) {
        panel.setVisibility(panel.getVisibility() == View.VISIBLE ? View.GONE : View.VISIBLE);
      }
    };
    burger.setOnClickListener(toggle);
    name.setOnClickListener(toggle);

    // Edge to edge: keep the bar below the status bar, and the content above the keyboard
    root.setOnApplyWindowInsetsListener(new View.OnApplyWindowInsetsListener() {
      @Override public WindowInsets onApplyWindowInsets(View v, WindowInsets in) {
        v.setPadding(in.getSystemWindowInsetLeft(), in.getSystemWindowInsetTop(),
                     in.getSystemWindowInsetRight(), in.getSystemWindowInsetBottom());
        return in;
      }
    });
    return root;
  }
}
