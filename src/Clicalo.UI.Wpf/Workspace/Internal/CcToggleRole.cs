namespace Clicalo.UI.Wpf.Workspace.Internal;

/// <summary>What a <see cref="CcToggle"/> is for UI Automation (ACC-001): one pattern per button, so «clic» has one meaning.</summary>
internal enum CcToggleRole
{
    /// <summary>A state that is on or off by itself (a switch, a key of a combination, a row to tick): Toggle.</summary>
    Toggle,

    /// <summary>
    /// One choice of a group where exactly one is chosen (cards, segments, the sections of the menu): a radio button
    /// with SelectionItem.
    /// </summary>
    Option,

    /// <summary>A header that opens and closes what is under it (a collapsible): ExpandCollapse.</summary>
    Expander,
}
