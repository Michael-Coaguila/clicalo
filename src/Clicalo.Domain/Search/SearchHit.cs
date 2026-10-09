using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Search;

/// <summary>One result of the panel search (BUS-005): a shortcut and where it lives.</summary>
/// <param name="Shortcut">The shortcut found.</param>
/// <param name="Profile">
/// Its profile, which the result shows under its name instead of the keys and with which it runs; <see langword="null"/>
/// for a shortcut of Always visible (shown as «Siempre visible»).
/// </param>
public sealed record SearchHit(Shortcut Shortcut, ProfileId? Profile)
{
    /// <summary>Whether the shortcut belongs to Always visible.</summary>
    public bool IsAlwaysVisible => Profile is null;
}
