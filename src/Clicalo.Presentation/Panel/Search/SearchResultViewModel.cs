using Clicalo.Application.Coordinators;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Touch;

namespace Clicalo.Presentation.Panel.Search;

/// <summary>
/// One result of the panel search (BUS-005): a tile like the normal ones whose second line names its profile of origin
/// («Siempre visible» for a global one) instead of its keys. It decides nothing: what happens on it goes to
/// <see cref="SearchViewModel"/>, which gives the foreground back before it runs (BUS-002 c).
/// </summary>
public sealed class SearchResultViewModel
{
    private readonly SearchViewModel _owner;

    /// <summary>Creates the result.</summary>
    /// <param name="owner">The search it belongs to.</param>
    /// <param name="binding">What it runs: the shortcut, its origin profile and that profile's injection mode.</param>
    /// <param name="name">The shortcut name in the interface language (user data, shown as is).</param>
    /// <param name="origin">The name of its profile, or «Siempre visible», in the interface language.</param>
    internal SearchResultViewModel(
        SearchViewModel owner,
        TileBinding binding,
        string name,
        string origin
    )
    {
        _owner = owner;
        Binding = binding;
        AccessibleName = name;
        Origin = origin;
    }

    /// <summary>The shortcut id.</summary>
    public ShortcutId Id => Binding.Shortcut.Id;

    /// <summary>What the result runs.</summary>
    public TileBinding Binding { get; }

    /// <summary>
    /// How the tile reacts to the finger. A Hold result is a tap that latches (the view makes it a tap target):
    /// <see cref="Application.UseCases.PanelSearch.RunTappedAsync"/>.
    /// </summary>
    public TileBehavior Behavior =>
        PanelProjector.BehaviorOf(Binding.Shortcut.Action) == TileBehavior.Toggle
            ? TileBehavior.Toggle
            : TileBehavior.Tap;

    /// <summary>The name shown on the tile and exposed to UI Automation (REG-06).</summary>
    public string AccessibleName { get; }

    /// <summary>The profile of origin, shown under the name in place of the keys.</summary>
    public string Origin { get; }

    /// <summary>The Material Symbols name of the icon (CUA-007).</summary>
    public string Icon => Binding.Shortcut.Icon.Name;

    /// <summary>The color category id (TEM-003).</summary>
    public string Category => Binding.Shortcut.Category.Value;

    /// <summary>An accepted tap lifted on the result: it runs once the app is back in front.</summary>
    /// <param name="contactId">The pointer id.</param>
    /// <param name="device">Finger, pen or mouse.</param>
    /// <param name="summary">Duration, displacement and palm.</param>
    /// <param name="at">When it lifted.</param>
    public void Tapped(
        uint contactId,
        PointerKind device,
        ContactSummary summary,
        DateTimeOffset at
    ) => _ = _owner.RunTappedAsync(this, contactId, device, summary, at);

    /// <summary>A UI Automation Invoke or Toggle (voice, keyboard or switch, EJE-005).</summary>
    public void Invoke() => _ = _owner.RunInvokedAsync(this);
}
