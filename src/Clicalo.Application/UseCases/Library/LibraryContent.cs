using Clicalo.Domain.Primitives;

namespace Clicalo.Application.UseCases.Library;

/// <summary>
/// The «Añadir atajo» library (<c>data/content/library.json</c>, ATJ-010), read and validated by Infrastructure since
/// content is untrusted (LOG-006).
/// </summary>
/// <param name="Version">The version of the file; part of the catalog reference of what it installs (DAT-004).</param>
/// <param name="Sections">The sections in order.</param>
public sealed record LibraryContent(int Version, ValueList<LibrarySection> Sections)
{
    /// <summary>The source of the catalog reference of every library shortcut (DAT-004).</summary>
    public const string Source = "library";

    /// <summary>No library: only «Crear el mío» and the profile's own category are offered.</summary>
    public static LibraryContent Empty { get; } = new(0, []);
}
