(() => {
  // ---------- Hero demo ----------
  // `from` is what follows Alt (⌘); `to` is what Windows receives.
  const demos = [
    { from: ["C"], to: ["Ctrl", "C"], action: "Copy" },
    { from: ["V"], to: ["Ctrl", "V"], action: "Paste" },
    { from: ["T"], to: ["Ctrl", "T"], action: "New tab" },
    { from: ["⇧", "Z"], to: ["Ctrl", "Y"], action: "Redo" },
    { from: ["Q"], to: ["Alt", "F4"], action: "Quit app" },
    { from: ["←"], to: ["Home"], action: "Start of line" },
    { from: ["⇧", "4"], to: ["Win", "Shift", "S"], action: "Screenshot a region" },
    { from: ["W"], to: ["Ctrl", "W"], action: "Close tab" },
  ];

  // Shortcuts the visitor can try by holding Alt on this page, keyed by KeyboardEvent.code.
  const tryable = {
    KeyC: { action: "Copy" },
    KeyV: { action: "Paste", shift: { to: ["Ctrl", "Shift", "V"], action: "Paste without formatting" } },
    KeyX: { action: "Cut" },
    KeyZ: { action: "Undo", shift: { to: ["Ctrl", "Y"], action: "Redo" } },
    KeyA: { action: "Select all" },
    KeyF: { action: "Find" },
    KeyB: { action: "Bold" },
    KeyI: { action: "Italic" },
    KeyU: { action: "Underline" },
    KeyK: { action: "Insert link" },
    KeyT: { action: "New tab", shift: { action: "Reopen closed tab" } },
    KeyW: { action: "Close tab" },
    KeyN: { action: "New window" },
    KeyO: { action: "Open" },
    KeyS: { action: "Save", shift: { action: "Save as" } },
    KeyP: { action: "Print" },
    KeyL: { action: "Focus address bar" },
    KeyR: { action: "Reload", shift: { action: "Hard reload" } },
    KeyD: { action: "Bookmark page" },
    KeyY: { to: ["Ctrl", "H"], action: "Show history" },
    KeyQ: { to: ["Alt", "F4"], action: "Quit app" },
    KeyM: { to: ["Minimize"], action: "Minimize window" },
    KeyH: { to: ["Minimize"], action: "Hide app" },
    KeyG: { to: ["F3"], action: "Find next", shift: { to: ["Shift", "F3"], action: "Find previous" } },
  };
  for (let i = 1; i <= 9; i++) tryable["Digit" + i] = { action: i === 9 ? "Go to last tab" : `Go to tab ${i}` };
  tryable.Digit3.shift = { to: ["Win", "PrtScn"], action: "Screenshot full screen" };
  tryable.Digit4.shift = { to: ["Win", "Shift", "S"], action: "Screenshot a region" };

  const fromEl = document.getElementById("demo-from");
  const toEl = document.getElementById("demo-to");
  const actionEl = document.getElementById("demo-action");
  const reduceMotion = matchMedia("(prefers-reduced-motion: reduce)").matches;

  const key = (label, cmd) => {
    const k = document.createElement("kbd");
    k.className = cmd ? "key key-cmd" : "key";
    k.textContent = label;
    return k;
  };

  function show(demo) {
    fromEl.replaceChildren(key("Alt", true), ...demo.from.map(l => key(l)));
    toEl.replaceChildren(...demo.to.map(l => key(l)));
    actionEl.textContent = demo.action;
    if (reduceMotion) return;
    for (const el of [fromEl, toEl, actionEl]) {
      el.classList.remove("fade");
      void el.offsetWidth;
      el.classList.add("fade");
    }
    // Press the keys briefly so it reads as a keystroke.
    const pressed = fromEl.querySelectorAll(".key");
    setTimeout(() => pressed.forEach(k => k.classList.add("pressed")), 250);
    setTimeout(() => pressed.forEach(k => k.classList.remove("pressed")), 600);
  }

  let index = 0;
  let timer;
  const cycle = () => {
    clearInterval(timer);
    timer = setInterval(() => {
      index = (index + 1) % demos.length;
      show(demos[index]);
    }, reduceMotion ? 4000 : 2600);
  };
  cycle();

  document.addEventListener("keydown", e => {
    if (!e.altKey || e.ctrlKey || e.metaKey) return;
    const entry = tryable[e.code];
    if (!entry) return;
    e.preventDefault();
    const pick = (e.shiftKey && entry.shift) || entry;
    const letter = e.code.replace(/^(Key|Digit)/, "");
    const shift = e.shiftKey && entry.shift;
    show({
      from: shift ? ["⇧", letter] : [letter],
      to: pick.to || (shift ? ["Ctrl", "Shift", letter] : ["Ctrl", letter]),
      action: pick.action,
    });
    cycle(); // give the visitor's shortcut time on screen before the demo resumes
  });

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
