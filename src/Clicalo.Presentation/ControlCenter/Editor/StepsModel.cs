using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>The steps of a Macro shortcut (EDI-013).</summary>
/// <param name="Label">[steps].</param>
/// <param name="Steps">The steps in order.</param>
/// <param name="Add">The four + buttons.</param>
/// <param name="EditName">The accessible name of the edit button.</param>
/// <param name="UpName">[before].</param>
/// <param name="DownName">[after].</param>
/// <param name="DeleteName">[delete].</param>
/// <param name="ConfirmText">[delConfirm].</param>
/// <param name="LessName">The accessible name of −.</param>
/// <param name="MoreName">The accessible name of +.</param>
/// <param name="DictateName">The accessible name of the dictation button.</param>
public sealed record StepsModel(
    string Label,
    ValueList<StepRow> Steps,
    ValueList<StepAdd> Add,
    string EditName,
    string UpName,
    string DownName,
    string DeleteName,
    string ConfirmText,
    string LessName,
    string MoreName,
    string DictateName
);
