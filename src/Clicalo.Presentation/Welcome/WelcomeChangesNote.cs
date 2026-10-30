using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.Welcome;

/// <summary>
/// The notice of step 1 on a repeated welcome (BIE-010): what [Siguiente] changes, and what it leaves alone because the
/// person changed it by hand after the last welcome.
/// </summary>
/// <param name="ChangesTitle">[obChangesT].</param>
/// <param name="Changes">One line per setting that changes, with its new value; may be empty.</param>
/// <param name="KeptTitle">[obKeptT].</param>
/// <param name="Kept">One line per setting that stays, with the value the person left; may be empty.</param>
public sealed record WelcomeChangesNote(
    string ChangesTitle,
    ValueList<string> Changes,
    string KeptTitle,
    ValueList<string> Kept
);
