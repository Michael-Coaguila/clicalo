using System.Collections.Immutable;
using System.Globalization;
using Clicalo.Application.Ports;

namespace Clicalo.UI.Wpf.Windowing.Internal;

/// <summary>
/// One immutable snapshot of <see cref="SurfaceRegistry"/>: published with a compare-and-swap, so any thread reads a
/// consistent view without locks (blueprint §3.2 rule 1).
/// </summary>
/// <param name="Surfaces">The live surfaces, in registration order.</param>
/// <param name="Windows">The window of each live surface.</param>
/// <param name="ByWindow">The surface of each live window.</param>
/// <param name="Allowed">Surfaces a lease currently lets activate.</param>
internal sealed record RegistryState(
    ImmutableArray<NonActivatingWindow> Surfaces,
    ImmutableDictionary<SurfaceId, nint> Windows,
    ImmutableDictionary<nint, SurfaceId> ByWindow,
    ImmutableHashSet<SurfaceId> Allowed
)
{
    /// <summary>No surface.</summary>
    public static RegistryState Empty { get; } =
        new(
            [],
            ImmutableDictionary<SurfaceId, nint>.Empty,
            ImmutableDictionary<nint, SurfaceId>.Empty,
            []
        );

    /// <summary>
    /// The state with <paramref name="surface"/> added. Throws when another live surface has its id; adding the same
    /// surface twice returns the state unchanged.
    /// </summary>
    public RegistryState With(NonActivatingWindow surface)
    {
        if (Surfaces.Contains(surface))
        {
            return this;
        }

        if (Windows.ContainsKey(surface.Id))
        {
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Another live surface already has the id {surface.Id}."
                )
            );
        }

        var window = surface.SurfaceWindow.Handle;
        return this with
        {
            Surfaces = Surfaces.Add(surface),
            Windows = Windows.Add(surface.Id, window),
            ByWindow = ByWindow.SetItem(window, surface.Id),
        };
    }

    /// <summary>The state without <paramref name="surface"/> (and without its activation allowance).</summary>
    public RegistryState Without(NonActivatingWindow surface)
    {
        if (!Surfaces.Contains(surface))
        {
            return this;
        }

        var byWindow = Windows.TryGetValue(surface.Id, out var window)
            ? ByWindow.Remove(window)
            : ByWindow;
        return this with
        {
            Surfaces = Surfaces.Remove(surface),
            Windows = Windows.Remove(surface.Id),
            ByWindow = byWindow,
            Allowed = Allowed.Remove(surface.Id),
        };
    }
}
