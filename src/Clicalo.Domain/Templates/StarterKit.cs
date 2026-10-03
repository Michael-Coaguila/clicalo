using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Templates;

/// <summary>
/// The starter kit (<c>data/content/starter.json</c>, user decision D2 of 2026-10-03): the options of the welcome step
/// «Which apps do you use most?», in display order, and which are marked by default.
/// </summary>
/// <param name="Version">Increases with every change of the kit.</param>
/// <param name="Options">The options in display order; ids are unique.</param>
public sealed record StarterKit(int Version, ValueList<StarterOption> Options)
{
    /// <summary>
    /// The options marked by default: what the welcome shows first, what «Skip» applies (BIE-003) and what a first
    /// start without the welcome installs.
    /// </summary>
    public StarterSelection DefaultSelection =>
        StarterSelection.Of(
            Options.Items.Where(static o => o.SelectedByDefault).Select(static o => o.Id)
        );

    /// <summary>The option <paramref name="id"/>, or <see langword="null"/>.</summary>
    /// <param name="id">An option id.</param>
    public StarterOption? Find(string id) =>
        Options.Items.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.Ordinal));
}
