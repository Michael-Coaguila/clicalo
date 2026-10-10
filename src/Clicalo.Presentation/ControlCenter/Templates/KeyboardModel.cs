using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Templates;

/// <summary>The keyboard line of Plantillas and of the preview (PLA-009).</summary>
/// <param name="Line">«Para {distribución} · {programas}».</param>
/// <param name="Why">[kbWhy].</param>
/// <param name="ActionText">[change] or [done].</param>
/// <param name="Open">Whether the options show.</param>
/// <param name="LayoutTitle">[kbLayout].</param>
/// <param name="Layouts">The four layouts.</param>
/// <param name="AppsTitle">[kbApps].</param>
/// <param name="AppsLanguages">The two programs languages.</param>
/// <param name="DetectedText">[detected].</param>
public sealed record KeyboardModel(
    string Line,
    string Why,
    string ActionText,
    bool Open,
    string LayoutTitle,
    ValueList<KbOption> Layouts,
    string AppsTitle,
    ValueList<KbOption> AppsLanguages,
    string DetectedText
);
