# Regenerating the website screenshots

Run `tools/capture-screenshots.ps1` (PowerShell, `-ExecutionPolicy Bypass` if scripts are blocked) after a
`dotnet build src/MacShortcuts`. It rewrites the six images in `docs/images/` (`shortcuts`, `excluded`,
`settings`, each `-light` and `-dark`). It backs up and restores your `settings.json`, and kills any running
MacShortcuts, so relaunch the app afterwards.

## Learnings

- **High-DPI:** the images are saved at the window's native physical resolution (about 1980×1209) and shown at
  1216 CSS px wide (`width`/`height` on the `<img>` in `docs/index.html`). That is roughly 1.6× density, which
  stays sharp on 4K and retina screens. Don't downscale to the display size: that is what made them look soft.
  If you change the output size, update `width`/`height` on `#shot-light` to the new aspect ratio (CSS size =
  pixels ÷ ~1.63).
- **Capture method:** `PrintWindow` with `PW_RENDERFULLCONTENT` (flag `2`) on the window handle, then crop to
  `DWMWA_EXTENDED_FRAME_BOUNDS` (attribute 9) to drop the invisible resize borders (otherwise black edges show).
- **DPI awareness:** the script must call `SetProcessDpiAwarenessContext(-4)` (per-monitor v2), matching the app.
  A system-DPI-aware or unaware script gets virtualised coordinates and wrong window sizes.
- **Window size:** the app has a minimum width (about 1564 px on this machine) and its height clamps to about
  1219 px, so you can't pick the aspect ratio freely; choose the width instead. At ≤1800 px the Action column
  truncates ("Paste without format…") now that shortcuts are drawn as keycaps, so 2000 px is used. The aspect
  ratio is therefore about 1.64:1.
- **Navigation:** pages are switched by clicking the rail icons with `SetCursorPos` + `mouse_event`. The
  offsets in the script are physical px for the scale the window opens at; if the machine's scale changes,
  re-check them from a screenshot.
- **Theme and content:** the script writes a settings file with `Theme` set to `Light`/`Dark`, default shortcut
  states and `mstsc.exe` + `vmconnect.exe` excluded, then relaunches per theme (switching theme recreates the
  window, so relaunching is simpler than driving the dropdown).
- **Counts:** the "47 of 56 on" in the shortcuts image is the default state of the catalog. Also update the
  numbers in `docs/index.html` (hero "And N more", "N shortcuts in all", "Show all N shortcuts", per-category
  counts) when `ShortcutCatalog.cs` changes.
