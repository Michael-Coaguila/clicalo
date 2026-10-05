using Clicalo.Domain.Catalog;
using Clicalo.Domain.Messages;

namespace Clicalo.Domain.Templates;

/// <summary>
/// A choice of the welcome step «Which apps do you use most?» (<c>data/content/starter.json</c>, BIE-006). A template
/// option takes its name, icon and processes from the template; «Basics» names itself with product texts.
/// </summary>
/// <param name="Id">The option id; for a template, the template id.</param>
/// <param name="Kind">What it installs.</param>
/// <param name="SelectedByDefault">
/// Whether it is marked by default and applied when the welcome is skipped (user decision D2: only «Basics»).
/// </param>
/// <param name="Icon">The icon of «Basics»; <see langword="null"/> for a template.</param>
/// <param name="Label">The chip text of «Basics» (<c>kitBasics</c>); <see langword="null"/> for a template.</param>
/// <param name="Description">What «Basics» installs (<c>kitBasicsD</c>); <see langword="null"/> for a template.</param>
public sealed record StarterOption(
    string Id,
    StarterOptionKind Kind,
    bool SelectedByDefault,
    IconRef? Icon = null,
    MessageKey? Label = null,
    MessageKey? Description = null
);
