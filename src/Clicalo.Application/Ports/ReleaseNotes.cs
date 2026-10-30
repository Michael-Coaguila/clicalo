using System.Collections.Immutable;

namespace Clicalo.Application.Ports;

/// <summary>«Novedades» of one version (ACT-004): served with the package, never written in the code.</summary>
/// <param name="Version">The version.</param>
/// <param name="Date">Its release date, when the notes carry one.</param>
/// <param name="IsNew">Whether it is the version found and not installed yet ([newBadge]).</param>
/// <param name="Items">The changes per language code (<c>es</c>, <c>en</c>).</param>
public sealed record ReleaseNotes(
    string Version,
    DateOnly? Date,
    bool IsNew,
    ImmutableDictionary<string, ImmutableArray<string>> Items
)
{
    /// <summary>The changes in <paramref name="language"/>, or in Spanish, or in any language there is.</summary>
    /// <param name="language">The language of the interface.</param>
    public ImmutableArray<string> In(string language)
    {
        if (Items.TryGetValue(language, out var items))
        {
            return items;
        }

        return Items.TryGetValue("es", out var spanish)
            ? spanish
            : Items.Values.FirstOrDefault(ImmutableArray<string>.Empty);
    }
}
