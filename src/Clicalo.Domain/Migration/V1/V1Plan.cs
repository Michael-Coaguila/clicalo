using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Migration.V1;

/// <summary>
/// Everything the conversion decides from a v1 document alone (catalog §7.4): profiles and shortcuts in order, the
/// settings with an equivalent and the report lines. <see cref="V1DocumentBuilder"/> turns it into a document.
/// </summary>
/// <param name="Profiles">The profiles in display order; a created General goes first.</param>
/// <param name="Settings">The converted settings.</param>
/// <param name="Notes">The report lines, in document order.</param>
/// <param name="Input">The counts read (MIG-004).</param>
internal sealed record V1Plan(
    ValueList<V1ProfilePlan> Profiles,
    V1SettingsPlan Settings,
    ValueList<MigrationNote> Notes,
    V1Counts Input
);
