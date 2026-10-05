using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Templates;

/// <summary>
/// Everything a new document is built from: the kit, the «Basics» content and the templates the kit offers
/// (<c>data/content</c>). Loaded and validated by Infrastructure, since content is untrusted (LOG-006).
/// </summary>
/// <param name="Kit">The options of the welcome.</param>
/// <param name="Seed">The content of «Basics».</param>
/// <param name="Templates">The templates, in the order of the kit.</param>
public sealed record StarterContent(
    StarterKit Kit,
    SeedContent Seed,
    ValueList<ProfileTemplate> Templates
)
{
    /// <summary>The template <paramref name="id"/>, or <see langword="null"/>.</summary>
    /// <param name="id">A template id.</param>
    public ProfileTemplate? Template(string id) =>
        Templates.Items.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// The template that binds <paramref name="process"/>, compared without case (PER-009, PLA-011: «there is a
    /// template» for an app); <see langword="null"/> when none does. Any of the processes of a template counts.
    /// </summary>
    /// <param name="process">The executable in the foreground.</param>
    public ProfileTemplate? TemplateFor(ProcessName process) =>
        process.IsEmpty
            ? null
            : Templates.Items.FirstOrDefault(t => t.Processes.Items.Contains(process));
}
