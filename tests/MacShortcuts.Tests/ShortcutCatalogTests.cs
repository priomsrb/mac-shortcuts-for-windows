using MacShortcuts.Shortcuts;

namespace MacShortcuts.Tests;

public class ShortcutCatalogTests
{
    [Fact]
    public void Ids_AreUnique()
    {
        var duplicates = ShortcutCatalog.All.GroupBy(s => s.Id).Where(g => g.Count() > 1).Select(g => g.Key);
        Assert.Empty(duplicates);
    }

    [Fact]
    public void Triggers_AreUnique()
    {
        // The remap table keeps the first binding for a trigger, so a duplicate would silently shadow another shortcut.
        var duplicates = ShortcutCatalog.All
            .SelectMany(s => s.Bindings, (s, b) => (s.Id, b.Trigger))
            .GroupBy(x => x.Trigger)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(x => x.Id))}");
        Assert.Empty(duplicates);
    }

    [Fact]
    public void EveryKeyboardShortcut_HasBindings()
    {
        var empty = ShortcutCatalog.All
            .Where(s => s != ShortcutCatalog.CtrlClick && s != ShortcutCatalog.CtrlScroll && s.Bindings.Count == 0)
            .Select(s => s.Id);
        Assert.Empty(empty);
    }
}
