namespace Clicalo.Domain.Settings;

/// <summary>What a setting affects, which decides whether it is undoable (DAT-006, blueprint §6.3).</summary>
public enum SettingScope
{
    /// <summary>How things look (theme, size, opacity): reverted with the same control, not undoable.</summary>
    Presentation,

    /// <summary>How things behave: undoable.</summary>
    Behavior,

    /// <summary>Where things are (panel and handle positions): never undoable.</summary>
    Placement,
}
