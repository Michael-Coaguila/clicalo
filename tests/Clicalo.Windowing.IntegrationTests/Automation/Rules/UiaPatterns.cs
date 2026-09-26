namespace Clicalo.Windowing.IntegrationTests.Automation.Rules;

/// <summary>The control patterns the UIA rules look at (blueprint §8.6, ACC-001).</summary>
[Flags]
public enum UiaPatterns
{
    /// <summary>No pattern.</summary>
    None = 0,

    /// <summary>Invoke: tiles and buttons.</summary>
    Invoke = 1 << 0,

    /// <summary>Toggle: Auto/Fixed, switches, sticky keys, lock and Toggle shortcuts.</summary>
    Toggle = 1 << 1,

    /// <summary>ExpandCollapse: profile button, quick settings, collapsibles.</summary>
    ExpandCollapse = 1 << 2,

    /// <summary>SelectionItem: card groups and segments.</summary>
    SelectionItem = 1 << 3,

    /// <summary>RangeValue: sliders.</summary>
    RangeValue = 1 << 4,

    /// <summary>Value: editable text.</summary>
    Value = 1 << 5,

    /// <summary>The patterns that make an element actionable by voice.</summary>
    Actionable = Invoke | Toggle | ExpandCollapse | SelectionItem | RangeValue,

    /// <summary>The patterns of a button: exactly one of them (ACC-001).</summary>
    ButtonActions = Invoke | Toggle | ExpandCollapse,
}
