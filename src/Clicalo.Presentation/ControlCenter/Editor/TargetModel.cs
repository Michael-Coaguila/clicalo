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
public sealed record TargetModel(
    string Label,
    string Value,
    bool Invalid,
    string InvalidText,
    string PickLabel,
    ValueList<AppChip> Picks,
    string DictateName
);
