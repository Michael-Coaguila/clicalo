using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Migration.V1;

/// <summary>A v1 profile: its name is its identity (catalog §7.2).</summary>
/// <param name="Name">The visible name, unique and case sensitive.</param>
/// <param name="Process">The executable without path, or empty for manual.</param>
/// <param name="ButtonsPerPage">v1 page size (1 to 50), reported only.</param>
/// <param name="Buttons">The buttons, in order.</param>
public sealed record V1Profile(
    string Name,
    string Process,
    int? ButtonsPerPage,
    ValueList<V1Button> Buttons
);
