using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;

namespace Clicalo.Presentation.ControlCenter.General;

/// <summary>«Modo pestaña» (GEN-010, PES-003).</summary>
/// <param name="Caption">[secBar].</param>
/// <param name="Explain">[barExplain].</param>
/// <param name="Sides">Izquierda, Arriba, Abajo and Derecha with a miniature; choosing one does not change the view.</param>
/// <param name="Gutter">Whether the miniatures leave the scroll bar free ([gutter]).</param>
/// <param name="HandleTitle">[handlePos].</param>
/// <param name="HandleDescription">[handlePosD].</param>
/// <param name="Vertical">Whether the side is vertical: ↑/↓, or ←/→ on the top and bottom edges.</param>
/// <param name="HandleBackName">The accessible name of ↑ or ←.</param>
/// <param name="HandleForwardName">The accessible name of ↓ or →.</param>
/// <param name="CanBack">Whether ↑ or ← applies (above 8 %).</param>
/// <param name="CanForward">Whether ↓ or → applies (below 92 %).</param>
/// <param name="HandleLock">[handleLock] (PES-003).</param>
/// <param name="PerPageTitle">[dockCount].</param>
/// <param name="PerPage">4, 5, 6 and 8.</param>
/// <param name="AutoHide">[autoHide], the inverse of <c>pinOpen</c>.</param>
/// <param name="KeepScrollbar">[gutter].</param>
public sealed record DockModel(
    string Caption,
    string Explain,
    ValueList<SettingOption<DockSide>> Sides,
    bool Gutter,
    string HandleTitle,
    string HandleDescription,
    bool Vertical,
    string HandleBackName,
    string HandleForwardName,
    bool CanBack,
    bool CanForward,
    SwitchItem HandleLock,
    string PerPageTitle,
    ValueList<SettingOption<int>> PerPage,
    SwitchItem AutoHide,
    SwitchItem KeepScrollbar
);
