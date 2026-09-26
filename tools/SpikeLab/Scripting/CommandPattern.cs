namespace Clicalo.Tools.SpikeLab.Scripting;

/// <summary>
/// The UI Automation pattern that carried a command, mirroring <c>Clicalo.UI.Wpf.Automation.ShortcutTilePattern</c>
/// without depending on WPF, so the script engine stays pure.
/// </summary>
internal enum CommandPattern
{
    /// <summary><c>IInvokeProvider.Invoke</c>.</summary>
    Invoke,

    /// <summary><c>IToggleProvider.Toggle</c>.</summary>
    Toggle,

    /// <summary><c>IExpandCollapseProvider.Expand</c> or <c>Collapse</c>.</summary>
    ExpandCollapse,
}
