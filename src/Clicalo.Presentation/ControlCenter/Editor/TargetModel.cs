using Clicalo.Domain.Primitives;
using Clicalo.Presentation.ControlCenter.Shortcuts;

namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>The address of a Web shortcut or the target of an App shortcut (EDI-014).</summary>
/// <param name="Label">[url] or [appPath].</param>
/// <param name="Value">The text of the field.</param>
/// <param name="Invalid">Whether the address is not valid: warn border and [badUrl].</param>
/// <param name="InvalidText">[badUrl].</param>
/// <param name="PickLabel">[openTabs] or [pickProgram].</param>
/// <param name="Picks">The open apps, for an App shortcut.</param>
/// <param name="DictateName">The accessible name of the dictation button.</param>
/// <param name="OpenLabel">[linkOpenApps], over the open apps.</param>
/// <param name="ProgramsLabel">[programsInstalled], over the installed programs.</param>
/// <param name="Programs">
/// The installed programs, Store apps included (EDI-014): all of them, whatever the filter says, and the same list
/// until they are read again.
/// </param>
/// <param name="ProgramsLoading">[programsLoading] while the programs are read for the first time; null otherwise.</param>
/// <param name="ProgramSearch">[programsSearch]: the hint and the accessible name of the filter.</param>
/// <param name="ProgramQuery">What the filter holds; the view shows the programs it matches (<see cref="ProgramFilter"/>).</param>
/// <param name="ProgramsNoMatch">[noResults] when the filter matches no program; null otherwise.</param>
/// <param name="ProgramsVersion">Changes when <paramref name="Programs"/> is another list: the view builds its buttons again only then.</param>
public sealed record TargetModel(
    string Label,
    string Value,
    bool Invalid,
    string InvalidText,
    string PickLabel,
    ValueList<AppChip> Picks,
    string DictateName,
    string OpenLabel,
    string ProgramsLabel,
    ValueList<ProgramChip> Programs,
    string? ProgramsLoading = null,
    string ProgramSearch = "",
    string ProgramQuery = "",
    string? ProgramsNoMatch = null,
    int ProgramsVersion = 0
);
