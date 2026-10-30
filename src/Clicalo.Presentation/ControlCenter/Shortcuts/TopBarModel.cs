using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Shortcuts;

/// <summary>The top bar of «Atajos»: what the panel shows and the chip of repeated combinations (ATJ-001, REP-003).</summary>
/// <param name="Title">[panelShows].</param>
/// <param name="Layers">Always visible, the profile of the active app and Frequents.</param>
/// <param name="Duplicates">«N [dupSummary]», or null when nothing is repeated.</param>
/// <param name="ReviewText">[review]: the button of the notice.</param>
/// <param name="ReviewName">«N [dupSummary] · [review]»: the accessible name of that button.</param>
public sealed record TopBarModel(
    string Title,
    ValueList<LayerChip> Layers,
    string? Duplicates,
    string ReviewText = "",
    string ReviewName = ""
);
