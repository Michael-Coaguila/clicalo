using System.Collections.Frozen;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;

namespace Clicalo.Domain.Guides;

/// <summary>
/// Which guides were finished or skipped (R-39, ADR-0034): the ids of <see cref="UserSettings.GuidesDone"/>, and
/// <see cref="DockSettings.CoachDone"/> for the guide of the Tab view, which the document has kept since 1.0.
/// </summary>
public static class GuideProgress
{
    private static readonly FrozenDictionary<GuideId, string> Ids = new Dictionary<GuideId, string>
    {
        [GuideId.Panel] = "panel",
        [GuideId.Bubble] = "bubble",
        [GuideId.Tab] = "tab",
        [GuideId.Shortcuts] = "shortcuts",
        [GuideId.Templates] = "templates",
        [GuideId.General] = "general",
        [GuideId.Touch] = "touch",
        [GuideId.System] = "system",
        [GuideId.About] = "about",
    }.ToFrozenDictionary();

    /// <summary>The id of <paramref name="guide"/> in <c>guidesDone</c>.</summary>
    /// <param name="guide">The guide.</param>
    public static string Id(GuideId guide) => Ids[guide];

    /// <summary>Whether <paramref name="guide"/> was finished or skipped.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="guide">The guide.</param>
    public static bool IsDone(UserSettings settings, GuideId guide)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return guide == GuideId.Tab
            ? settings.Dock.CoachDone
            : settings.GuidesDone.Contains(Id(guide), StringComparer.Ordinal);
    }

    /// <summary>
    /// <paramref name="done"/> with <paramref name="guide"/> in it, in the order they were done; the same list when it
    /// was already there. Not for the Tab view, whose guide keeps <see cref="DockSettings.CoachDone"/>.
    /// </summary>
    /// <param name="done">The ids of <c>guidesDone</c>.</param>
    /// <param name="guide">The guide just finished or skipped.</param>
    public static ValueList<string> WithDone(ValueList<string> done, GuideId guide)
    {
        var id = Id(guide);
        return done.Contains(id, StringComparer.Ordinal) ? done : [.. done, id];
    }
}
