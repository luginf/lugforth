// Theme switch: light/dark. Applied from <head> (no flash); the choice is kept in
// localStorage when available, otherwise the OS preference decides.
(function () {
  var root = document.documentElement;
  function stored() {
    try { return localStorage.getItem("theme"); } catch (e) { return null; }
  }
  function system() {
    return window.matchMedia && matchMedia("(prefers-color-scheme: dark)").matches
      ? "dark" : "light";
  }
  root.setAttribute("data-theme", stored() || system());
  document.addEventListener("DOMContentLoaded", function () {
    var btn = document.getElementById("theme-toggle");
    if (!btn) return;
    btn.hidden = false;
    btn.addEventListener("click", function () {
      var next = root.getAttribute("data-theme") === "dark" ? "light" : "dark";
      root.setAttribute("data-theme", next);
      try { localStorage.setItem("theme", next); } catch (e) {}
    });
  });
})();
