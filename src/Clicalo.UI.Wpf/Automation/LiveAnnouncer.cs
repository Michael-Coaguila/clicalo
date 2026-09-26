using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace Clicalo.UI.Wpf.Automation;

/// <summary>
/// Announces notices to screen readers from a non-activatable surface (blueprint §8.6, ACC-001, REG-06, S3): it
/// sets the text of a live region element (<c>AutomationProperties.LiveSetting</c> Polite or Assertive), raises
/// <c>LiveRegionChanged</c> on its peer and, for readers that ignore live regions on inactive windows, also
/// <c>RaiseNotificationEvent</c> with <see cref="ActivityId"/>. Runs on the UI thread of the region.
/// </summary>
/// <remarks>The text arrives localized (a notice's <c>MessageKey</c> rendered by the localizer); never a literal.</remarks>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality",
    Justification = "M1 contract stub: the automation package implements it (docs/testing/spikes/M1-ownership.md)."
)]
public sealed class LiveAnnouncer
{
    /// <summary>Activity id of every notification, so readers can group or replace Clícalo's notices.</summary>
    public const string ActivityId = "Clicalo.Notice";

    /// <summary>Creates an announcer that speaks through <paramref name="region"/>.</summary>
    /// <param name="region">
    /// The element that shows the notice (the notice bar or status bar text); it becomes the live region.
    /// </param>
    public LiveAnnouncer(FrameworkElement region)
    {
        ArgumentNullException.ThrowIfNull(region);
        Region = region;
    }

    /// <summary>The live region element.</summary>
    public FrameworkElement Region { get; }

    /// <summary>The last text announced; empty before the first announcement.</summary>
    public string LastText => throw new NotImplementedException("M1 automation package.");

    /// <summary>Shows and announces <paramref name="text"/> with <paramref name="urgency"/>.</summary>
    /// <param name="text">The localized notice text.</param>
    /// <param name="urgency">Polite or assertive.</param>
    public void Announce(string text, AnnouncementUrgency urgency) =>
        throw new NotImplementedException("M1 automation package.");
}
