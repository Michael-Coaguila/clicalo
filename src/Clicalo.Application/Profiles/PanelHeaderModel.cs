using Clicalo.Domain.Catalog;
using Clicalo.Domain.Messages;

namespace Clicalo.Application.Profiles;

/// <summary>
/// What the header of the full and compact views shows (CAB-001 to CAB-003), as <see cref="PanelHeaderProjection"/>
/// computes it: immutable and compared by value.
/// </summary>
/// <param name="Kind">What the title names.</param>
/// <param name="Title">The title as a message (Frecuentes, Buscar) or <see langword="null"/> for a profile.</param>
/// <param name="ProfileName">The profile name in the interface language (user data); empty for the other kinds.</param>
/// <param name="Icon">The icon before the title: the profile's, <c>star</c> or <c>search</c>.</param>
/// <param name="ShowsActiveAppDot">The profile shown is that of the active app, without a search (the «auto» dot).</param>
/// <param name="IsFixed">Fixed (red, <c>lock</c>) or Auto (blue, <c>autorenew</c>).</param>
/// <param name="ShowsAutoFixed">Whether the Auto/Fixed button is visible: hidden while the search has text.</param>
public sealed record PanelHeaderModel(
    HeaderTitleKind Kind,
    Message? Title,
    string ProfileName,
    IconRef Icon,
    bool ShowsActiveAppDot,
    bool IsFixed,
    bool ShowsAutoFixed
)
{
    /// <summary>The Material Symbols icon of Auto (CAB-003).</summary>
    public static IconRef AutoIcon { get; } = new("autorenew");

    /// <summary>The Material Symbols icon of Fixed (CAB-003).</summary>
    public static IconRef FixedIcon { get; } = new("lock");

    /// <summary>The icon of the Auto/Fixed button for this state.</summary>
    public IconRef AutoFixedIcon => IsFixed ? FixedIcon : AutoIcon;

    /// <summary>The accessible name and tooltip of the Auto/Fixed button: the state in words, not only color.</summary>
    public Message AutoFixedName => IsFixed ? L.LockA : L.AutoA;
}
