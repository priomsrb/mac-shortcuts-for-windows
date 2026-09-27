namespace MacShortcuts.Shortcuts;

public static class ShortcutCatalog
{
    public const string MouseCtrlClickId = "mouse.ctrlClick";

    public static IReadOnlyList<ShortcutDef> All { get; } = Build();

    static List<ShortcutDef> Build()
    {
        var list = new List<ShortcutDef>();

        void Add(string id, string cat, string trigger, string sends, string desc, Binding[] bindings, bool on = true) =>
            list.Add(new ShortcutDef(id, cat, trigger, sends, desc, on, bindings));

        // Alt(+Shift)+Key -> Ctrl(+Shift)+Key
        void Same(string id, string cat, Keys key, string desc, bool shift = false)
        {
            string s = shift ? "Shift+" : "";
            string name = KeyName(key);
            Add(id, cat, $"Alt+{s}{name}", $"Ctrl+{s}{name}", desc,
                [Map(key, shift, new Chord(key, Mods.Ctrl | (shift ? Mods.Shift : Mods.None)))]);
        }

        const string Editing = "Editing";
        Same("edit.copy", Editing, Keys.C, "Copy");
        Same("edit.paste", Editing, Keys.V, "Paste");
        Same("edit.cut", Editing, Keys.X, "Cut");
        Same("edit.undo", Editing, Keys.Z, "Undo");
        Add("edit.redo", Editing, "Alt+Shift+Z", "Ctrl+Y", "Redo",
            [Map(Keys.Z, true, new Chord(Keys.Y, Mods.Ctrl))]);
        Same("edit.pastePlain", Editing, Keys.V, "Paste without formatting", shift: true);
        Same("edit.selectAll", Editing, Keys.A, "Select all");
        Same("edit.find", Editing, Keys.F, "Find");
        Add("edit.findNext", Editing, "Alt+G / Alt+Shift+G", "F3 / Shift+F3", "Find next / previous",
            [Map(Keys.G, false, new Chord(Keys.F3)), Map(Keys.G, true, new Chord(Keys.F3, Mods.Shift))]);
        Same("edit.bold", Editing, Keys.B, "Bold");
        Same("edit.italic", Editing, Keys.I, "Italic");
        Same("edit.underline", Editing, Keys.U, "Underline");
        Same("edit.link", Editing, Keys.K, "Insert link");
        Add("edit.comment", Editing, "Alt+/", "Ctrl+/", "Toggle comment (code editors)",
            [Map(Keys.OemQuestion, false, new Chord(Keys.OemQuestion, Mods.Ctrl))]);

        const string File = "File";
        Same("file.new", File, Keys.N, "New window / document");
        Same("file.newShift", File, Keys.N, "New incognito window / new folder", shift: true);
        Same("file.open", File, Keys.O, "Open");
        Same("file.save", File, Keys.S, "Save");
        Same("file.saveAs", File, Keys.S, "Save as", shift: true);
        Same("file.print", File, Keys.P, "Print");

        const string Text = "Text navigation";
        Add("nav.lineEnds", Text, "Alt+← / Alt+→", "Home / End", "Go to start / end of line",
            [Map(Keys.Left, false, new Chord(Keys.Home)), Map(Keys.Right, false, new Chord(Keys.End))]);
        Add("nav.docEnds", Text, "Alt+↑ / Alt+↓", "Ctrl+Home / Ctrl+End", "Go to top / bottom of document",
            [Map(Keys.Up, false, new Chord(Keys.Home, Mods.Ctrl)), Map(Keys.Down, false, new Chord(Keys.End, Mods.Ctrl))]);
        Add("nav.selectLine", Text, "Alt+Shift+← / →", "Shift+Home / Shift+End", "Select to start / end of line",
            [Map(Keys.Left, true, new Chord(Keys.Home, Mods.Shift)), Map(Keys.Right, true, new Chord(Keys.End, Mods.Shift))]);
        Add("nav.selectDoc", Text, "Alt+Shift+↑ / ↓", "Ctrl+Shift+Home / End", "Select to top / bottom of document",
            [Map(Keys.Up, true, new Chord(Keys.Home, Mods.Ctrl | Mods.Shift)), Map(Keys.Down, true, new Chord(Keys.End, Mods.Ctrl | Mods.Shift))]);
        Add("nav.deleteLine", Text, "Alt+Backspace", "Shift+Home, Backspace", "Delete to start of line",
            [Map(Keys.Back, false, new Chord(Keys.Home, Mods.Shift), new Chord(Keys.Back))]);

        const string Browser = "Browser & tabs";
        Same("tab.new", Browser, Keys.T, "New tab");
        Same("tab.close", Browser, Keys.W, "Close tab / window");
        Same("tab.reopen", Browser, Keys.T, "Reopen closed tab", shift: true);
        Add("tab.number", Browser, "Alt+1 … Alt+9, Alt+0", "Ctrl+1 … Ctrl+9, Ctrl+0", "Go to tab 1–8 / last tab (Alt+0 resets zoom)",
            Enumerable.Range(0, 10).Select(i => Map(Keys.D0 + i, false, new Chord(Keys.D0 + i, Mods.Ctrl))).ToArray());
        Add("tab.prevNext", Browser, "Alt+{ / Alt+}", "Ctrl+PgUp / Ctrl+PgDn", "Previous / next tab",
            [Map(Keys.OemOpenBrackets, true, new Chord(Keys.PageUp, Mods.Ctrl)), Map(Keys.OemCloseBrackets, true, new Chord(Keys.PageDown, Mods.Ctrl))]);
        Add("browser.backForward", Browser, "Alt+[ / Alt+]", "Browser Back / Forward", "Go back / forward",
            [Map(Keys.OemOpenBrackets, false, new Chord(Keys.BrowserBack)), Map(Keys.OemCloseBrackets, false, new Chord(Keys.BrowserForward))]);
        Same("browser.address", Browser, Keys.L, "Focus address bar");
        Same("browser.reload", Browser, Keys.R, "Reload");
        Same("browser.hardReload", Browser, Keys.R, "Hard reload (ignore cache)", shift: true);
        Add("browser.zoom", Browser, "Alt++ / Alt+-", "Ctrl++ / Ctrl+-", "Zoom in / out",
            [
                Map(Keys.Oemplus, false, new Chord(Keys.Oemplus, Mods.Ctrl)),
                Map(Keys.Oemplus, true, new Chord(Keys.Oemplus, Mods.Ctrl)),
                Map(Keys.OemMinus, false, new Chord(Keys.OemMinus, Mods.Ctrl)),
                Map(Keys.Add, false, new Chord(Keys.Add, Mods.Ctrl)),
                Map(Keys.Subtract, false, new Chord(Keys.Subtract, Mods.Ctrl)),
            ]);
        Same("browser.bookmark", Browser, Keys.D, "Bookmark page");
        Add("browser.history", Browser, "Alt+Y", "Ctrl+H", "Show history",
            [Map(Keys.Y, false, new Chord(Keys.H, Mods.Ctrl))]);

        const string Window = "Window & system";
        Add("win.quit", Window, "Alt+Q", "Alt+F4", "Quit app / close window",
            [Map(Keys.Q, false, new Chord(Keys.F4, Mods.Alt))]);
        Add("win.minimize", Window, "Alt+M", "Minimize", "Minimize window",
            [new Binding(new Trigger(Keys.M, false), new MinimizeWindowAction())]);
        Add("win.hide", Window, "Alt+H", "Minimize", "Hide app (minimizes the window)",
            [new Binding(new Trigger(Keys.H, false), new MinimizeWindowAction())]);
        Add("win.prefs", Window, "Alt+,", "Ctrl+,", "Preferences (apps that support Ctrl+,)",
            [Map(Keys.Oemcomma, false, new Chord(Keys.Oemcomma, Mods.Ctrl))]);
        Add("win.screenshot", Window, "Alt+Shift+3 / Alt+Shift+4", "Win+PrtScn / Win+Shift+S", "Screenshot full screen / region",
            [Map(Keys.D3, true, new Chord(Keys.PrintScreen, Mods.Win)), Map(Keys.D4, true, new Chord(Keys.S, Mods.Win | Mods.Shift))]);
        Add("win.search", Window, "Alt+Space", "Win+S", "Spotlight → Windows Search (overrides Alt+Space window menu / PowerToys)",
            [Map(Keys.Space, false, new Chord(Keys.S, Mods.Win))], on: false);

        const string Mouse = "Mouse";
        Add(MouseCtrlClickId, Mouse, "Alt+Click", "Ctrl+Click", "Open link in new tab, multi-select, go to definition", []);

        // Catch-alls cover every letter not already claimed above, so they never override a specific shortcut.
        var used = list.SelectMany(s => s.Bindings).Select(b => b.Trigger).ToHashSet();
        Binding[] OtherLetters(bool shift) => Enumerable.Range('A', 26)
            .Select(c => (Keys)c)
            .Where(k => !used.Contains(new Trigger(k, shift)))
            .Select(k => Map(k, shift, new Chord(k, Mods.Ctrl | (shift ? Mods.Shift : Mods.None))))
            .ToArray();

        const string Other = "Catch-all";
        Add("other.letters", Other, "Alt+any other letter", "Ctrl+same letter", "Remap every remaining Alt+letter", OtherLetters(false), on: false);
        Add("other.shiftLetters", Other, "Alt+Shift+any other letter", "Ctrl+Shift+same letter", "Remap every remaining Alt+Shift+letter", OtherLetters(true), on: false);

        return list;
    }

    static Binding Map(Keys from, bool shift, params Chord[] to) =>
        new(new Trigger(from, shift), new SendKeysAction(to));

    static string KeyName(Keys key) => key is >= Keys.D0 and <= Keys.D9
        ? ((int)(key - Keys.D0)).ToString()
        : key.ToString();
}
