using Clicalo.Application.Engine;
using Clicalo.Application.Localization;
using Clicalo.Domain.Messages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Bubble;

/// <summary>
/// The 64 px bubble the panel minimizes to (docs/04 «Burbuja minimizada», BUR-001, BUR-002): its name, whether something
/// is held (the red ring and the floating «Release all» beside it) and what a tap does. Rules and opacity come from the
/// domain; this only shows them and forwards the taps.
/// </summary>
public sealed class BubbleViewModel : ObservableObject
{
    private readonly ILocalizationContext _localization;
    private readonly Action _restore;
    private readonly Action _releaseAll;
    private string _accessibleName = string.Empty;
    private string _releaseAllName = string.Empty;
    private bool _isPanic;

    /// <summary>Creates the bubble.</summary>
    /// <param name="localization">The interface language.</param>
    /// <param name="restore">A tap without dragging: back to the panel (PAN-001 b).</param>
    /// <param name="releaseAll">«Release all» beside it (BUR-002, REG-03).</param>
    public BubbleViewModel(ILocalizationContext localization, Action restore, Action releaseAll)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(restore);
        ArgumentNullException.ThrowIfNull(releaseAll);
        _localization = localization;
        _restore = restore;
        _releaseAll = releaseAll;
        Relocalize();
    }

    /// <summary>[restore] «Mostrar panel».</summary>
    public string AccessibleName
    {
        get => _accessibleName;
        private set => SetProperty(ref _accessibleName, value);
    }

    /// <summary>[releaseAll].</summary>
    public string ReleaseAllName
    {
        get => _releaseAllName;
        private set => SetProperty(ref _releaseAllName, value);
    }

    /// <summary>Something is held or latched: the red ring of 3 and «Release all» beside the bubble (BUR-002).</summary>
    public bool IsPanic
    {
        get => _isPanic;
        private set => SetProperty(ref _isPanic, value);
    }

    /// <summary>Applies what the engine holds.</summary>
    /// <param name="snapshot">The engine snapshot.</param>
    public void ApplyEngine(EngineSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        IsPanic = !snapshot.Held.IsEmpty;
    }

    /// <summary>Formats the texts again in the current language (IDI-001).</summary>
    public void Relocalize()
    {
        var l = _localization.Current;
        AccessibleName = l.Format(L.Restore);
        ReleaseAllName = l.Format(L.ReleaseAll);
    }

    /// <summary>A tap without dragging (or UI Automation Invoke): the panel comes back.</summary>
    public void Restore() => _restore();

    /// <summary>«Release all» beside the bubble.</summary>
    public void ReleaseAll() => _releaseAll();
}
