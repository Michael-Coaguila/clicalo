using Clicalo.Domain.Library;

namespace Clicalo.Application.UseCases.Templates;

/// <summary>A row of the preview of Plantillas (PLA-015).</summary>
/// <param name="Index">Its position in the preview.</param>
/// <param name="Shortcut">The shortcut as it would be installed, with the edited name.</param>
/// <param name="AlreadyIn">Whether the installed profile already has it («ya está»): dimmed and fixed.</param>
/// <param name="Checked">Whether it will be installed.</param>
/// <param name="Risky">A Web, App, Macro or Text shortcut of a shared profile (LOG-008).</param>
/// <param name="Dangerous">A combination that closes or deletes at once (Alt+F4, Ctrl+W, Supr).</param>
/// <param name="Renamed">Whether the person changed its name.</param>
public sealed record PreviewRow(
    int Index,
    Shortcut Shortcut,
    bool AlreadyIn,
    bool Checked,
    bool Risky,
    bool Dangerous,
    bool Renamed
);
