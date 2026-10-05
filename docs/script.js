(() => {
  // ---------- Screenshot tour ----------
  const shots = {
    shortcuts: {
      caption: "Every shortcut, grouped by category, with a filter box. Tick or untick each one.",
      alt: "The Shortcuts page, listing Mac-style shortcuts such as Alt+C next to what they send, such as Ctrl+C",
    },
    excluded: {
      caption: "Apps where nothing is remapped, like Remote Desktop. Type a name, pick a running app or browse for an .exe.",
      alt: "The Excluded apps page, with mstsc.exe and vmconnect.exe in the list",
    },
    settings: {
      caption: "Start with Windows, which Alt keys act as ⌘, the theme and restarting as administrator.",
      alt: "The Settings page, with options for starting with Windows, the Cmd key, the theme and administrator mode",
    },
  };

  const tabs = document.querySelectorAll(".tour-tabs button");
  const light = document.getElementById("shot-light");
  const dark = document.getElementById("shot-dark");
  const caption = document.getElementById("tour-caption");

  // Warm the cache so switching tabs is instant.
  for (const name of Object.keys(shots)) {
    const scheme = matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
    new Image().src = `images/${name}-${scheme}.png`;
  }

  function select(tab) {
    const name = tab.dataset.shot;
    tabs.forEach(t => {
      const on = t === tab;
      t.setAttribute("aria-selected", on);
      t.tabIndex = on ? 0 : -1;
    });
    light.src = `images/${name}-light.png`;
    dark.srcset = `images/${name}-dark.png`;
    light.alt = shots[name].alt;
    caption.textContent = shots[name].caption;
  }

  tabs.forEach((tab, i) => {
    tab.tabIndex = i === 0 ? 0 : -1;
    tab.addEventListener("click", () => select(tab));
    tab.addEventListener("keydown", e => {
      const step = e.key === "ArrowRight" ? 1 : e.key === "ArrowLeft" ? -1 : 0;
      if (!step) return;
      const next = tabs[(i + step + tabs.length) % tabs.length];
      next.focus();
      select(next);
    });
  });
})();
