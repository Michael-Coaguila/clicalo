using Clicalo.UI.Wpf.Automation;

namespace Clicalo.Windowing.IntegrationTests.Automation.Lab;

/// <summary>One tile of the S3 lab panel: what the view model would give the tile.</summary>
/// <param name="Id">UI Automation id (<c>AutomationProperties.AutomationId</c>).</param>
/// <param name="Name">Localized name, without the voice number.</param>
/// <param name="Pattern">Pattern of its action.</param>
/// <param name="HelpText">Key combination with localized key names; empty when the action has no keys.</param>
/// <param name="ThreeStates">For a Toggle tile: a sticky key with the third, locked state.</param>
public sealed record LabTileSpec(
    string Id,
    string Name,
    ShortcutTilePattern Pattern,
    string HelpText,
    bool ThreeStates = false
);
