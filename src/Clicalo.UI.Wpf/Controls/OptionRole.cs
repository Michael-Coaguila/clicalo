namespace Clicalo.UI.Wpf.Controls;

/// <summary>What an <see cref="OptionButton"/> is for UI Automation (ACC-001): one pattern per control, so «clic» has one meaning.</summary>
public enum OptionRole
{
    /// <summary>One choice of a group where exactly one is chosen (segments, cards, sections): a radio button with SelectionItem.</summary>
    Option,

    /// <summary>A state that is on or off by itself (a key of a combination, a row to tick): a button with Toggle.</summary>
    Toggle,

    /// <summary>A header that opens and closes what is under it: a button with ExpandCollapse.</summary>
    Expander,
}
