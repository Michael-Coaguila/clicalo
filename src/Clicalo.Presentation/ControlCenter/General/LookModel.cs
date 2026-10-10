using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;

namespace Clicalo.Presentation.ControlCenter.General;

/// <summary>
/// The 2 × 2 grid of cards of equal height at the top (GEN-001): Idioma, Tema, Tamaño and Vista, in this order.
/// </summary>
/// <param name="LanguageTitle">«Idioma · Language».</param>
/// <param name="Languages">ES/Español and EN/English, two big buttons (GEN-002).</param>
/// <param name="ThemeTitle">[theme].</param>
/// <param name="Themes">Como Windows, Oscuro, Claro and Alto contraste, each with its swatch (GEN-003).</param>
/// <param name="SizeTitle">[size].</param>
/// <param name="Sizes">S, M and L with a miniature (GEN-004).</param>
/// <param name="TextSizeTitle">[textSize].</param>
/// <param name="TextSize">The text scale, «100%».</param>
/// <param name="TextSmallerName">The accessible name of −.</param>
/// <param name="TextBiggerName">The accessible name of +.</param>
/// <param name="CanTextSmaller">Whether − applies (above 100 %).</param>
/// <param name="CanTextBigger">Whether + applies (below 150 %).</param>
/// <param name="ViewTitle">[view].</param>
/// <param name="Views">Completa, Compacta and Pestaña with a miniature (GEN-005).</param>
/// <param name="ViewDescription">[dFullD], [dCompactD] or [dDockD], of the current view.</param>
public sealed record LookModel(
    string LanguageTitle,
    ValueList<LanguageOption> Languages,
    string ThemeTitle,
    ValueList<SettingOption<ThemeChoice>> Themes,
    string SizeTitle,
    ValueList<SettingOption<PanelSize>> Sizes,
    string TextSizeTitle,
    string TextSize,
    string TextSmallerName,
    string TextBiggerName,
    bool CanTextSmaller,
    bool CanTextBigger,
    string ViewTitle,
    ValueList<SettingOption<PanelDensity>> Views,
    string ViewDescription
);
