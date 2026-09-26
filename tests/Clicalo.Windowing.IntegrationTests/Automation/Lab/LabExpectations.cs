using Clicalo.UI.Wpf.Automation;
using Clicalo.Windowing.IntegrationTests.Automation.Rules;
using FlaUI.Core.Definitions;

namespace Clicalo.Windowing.IntegrationTests.Automation.Lab;

/// <summary>
/// What the view model of the S3 lab says UI Automation must expose, computed from the test's own model of the
/// state (never read back from the tiles), so a snapshot taken through UI Automation is checked against it.
/// </summary>
public static class LabExpectations
{
    /// <summary>The expectations for one state of the lab.</summary>
    /// <param name="voiceNumbers">«Numbers for voice» on (tile N is «N name»).</param>
    /// <param name="shift">State of the three-state Shift tile.</param>
    /// <param name="holdCtrl">State of the Hold Ctrl tile.</param>
    /// <param name="profileExpanded">Whether the profile tile is expanded.</param>
    /// <param name="notice">Text of the notice bar.</param>
    /// <param name="noticeLive">Live setting of the notice bar.</param>
    public static UiaExpectations Create(
        bool voiceNumbers,
        ToggleState shift = ToggleState.Off,
        ToggleState holdCtrl = ToggleState.Off,
        bool profileExpanded = false,
        string notice = TileLab.IdleNotice,
        LiveSetting noticeLive = LiveSetting.Polite
    )
    {
        var elements = LabTiles
            .All.Select(
                (spec, index) =>
                    Tile(
                        spec,
                        voiceNumbers ? index + 1 : null,
                        spec.Id switch
                        {
                            "tile.shift" => shift,
                            "tile.holdCtrl" => holdCtrl,
                            _ => ToggleState.Off,
                        },
                        profileExpanded
                    )
            )
            .Append(
                new UiaExpectation(
                    TileLab.NoticeId,
                    notice,
                    null,
                    ControlType.Text,
                    UiaPatterns.None
                )
                {
                    LiveSetting = noticeLive,
                }
            );
        return UiaExpectations.For(elements);
    }

    /// <summary>The state that follows <paramref name="state"/> when the tile is toggled (the lab's view model).</summary>
    public static ToggleState Next(ToggleState state, bool threeStates) =>
        (state, threeStates) switch
        {
            (ToggleState.Off, _) => ToggleState.On,
            (ToggleState.On, true) => ToggleState.Indeterminate,
            _ => ToggleState.Off,
        };

    /// <summary>The item status the lab's view model gives <paramref name="state"/>.</summary>
    public static string StateText(ToggleState state) =>
        state switch
        {
            ToggleState.On => LabTiles.OnState,
            ToggleState.Indeterminate => LabTiles.LockedState,
            _ => string.Empty,
        };

    private static UiaExpectation Tile(
        LabTileSpec spec,
        int? voiceNumber,
        ToggleState toggle,
        bool expanded
    )
    {
        var expectation = new UiaExpectation(
            spec.Id,
            spec.Name,
            voiceNumber,
            ControlType.Button,
            spec.Pattern switch
            {
                ShortcutTilePattern.Toggle => UiaPatterns.Toggle,
                ShortcutTilePattern.ExpandCollapse => UiaPatterns.ExpandCollapse,
                _ => UiaPatterns.Invoke,
            }
        )
        {
            HelpText = spec.HelpText,
        };
        return spec.Pattern switch
        {
            ShortcutTilePattern.Toggle => expectation with
            {
                ToggleState = toggle,
                ItemStatus = StateText(toggle),
            },
            ShortcutTilePattern.ExpandCollapse => expectation with
            {
                ExpandCollapseState = expanded
                    ? ExpandCollapseState.Expanded
                    : ExpandCollapseState.Collapsed,
                ItemStatus = string.Empty,
            },
            _ => expectation with { ItemStatus = string.Empty },
        };
    }
}
