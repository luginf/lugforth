---
name: add-web-page
description: Add a page or demo to the web build and the website; use when a new ports/web/*.html or demo is created
---

Three files must stay in step:
1. `ports/web/BUILD`: `Copy('$dst/web/<name>.html', '$src/ports/web/<name>.html')` and the `.fs` it loads.
2. `tools/make_pages.py`: the demo list (the tuple of names copied into `out/pages/demo`).
3. `.github/workflows/pages.yml`: the `ninja` targets (the `out/web/...` list on the build line).

Then `ninja web`, serve `out/web` (`python3 -m http.server`), test in headless Chromium. JS inside `r~ ... ~` blocks of
`ports/web/*.fs` must not contain `~` or a backtick (see CLAUDE.md, "Web page"). Cells are 32 bits there.
Bump the version (skill `bump-version`).
