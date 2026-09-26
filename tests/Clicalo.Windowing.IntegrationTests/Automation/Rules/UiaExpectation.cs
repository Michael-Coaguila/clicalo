using System.Globalization;
using FlaUI.Core.Definitions;

namespace Clicalo.Windowing.IntegrationTests.Automation.Rules;

/// <summary>What the view model says one element must expose (UIA001–UIA004, UIA006).</summary>
/// <param name="AutomationId">The element.</param>
/// <param name="Name">Localized name, without the voice number.</param>
/// <param name="VoiceNumber">Voice number when «Numbers for voice» is on; null when off.</param>
/// <param name="ControlType">Expected control type.</param>
/// <param name="Patterns">Expected actionable patterns, exactly.</param>
public sealed record UiaExpectation(
    string AutomationId,
    string Name,
    int? VoiceNumber,
    ControlType ControlType,
    UiaPatterns Patterns
)
{
    /// <summary>Expected ToggleState, when the view model has one.</summary>
    public ToggleState? ToggleState { get; init; }

    /// <summary>Expected ExpandCollapseState, when the view model has one.</summary>
    public ExpandCollapseState? ExpandCollapseState { get; init; }

    /// <summary>Expected LiveSetting of a notice region; null when the element is not a live region.</summary>
    public LiveSetting? LiveSetting { get; init; }

    /// <summary>Expected HelpText (the key combination); null when not checked.</summary>
    public string? HelpText { get; init; }

    /// <summary>Expected ItemStatus (the accessible state); null when not checked.</summary>
    public string? ItemStatus { get; init; }

    /// <summary>The full name UI Automation must report: «{n} {name}» with a voice number (UIA001).</summary>
    public string FullName =>
        VoiceNumber is { } number
            ? string.Create(CultureInfo.InvariantCulture, $"{number} {Name}")
            : Name;
}
