using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.SystemSection;

/// <summary>Everything «Sistema» shows (docs/05 §5, SIS-001): the title, the three tabs and the content of the one in view.</summary>
/// <param name="Title">[sysTitle].</param>
/// <param name="Subtitle">[sysSub].</param>
/// <param name="Tabs">The three tabs with their state.</param>
/// <param name="Tab">The tab in view.</param>
/// <param name="Updates">«Actualizaciones».</param>
/// <param name="Backups">«Copias de seguridad».</param>
/// <param name="Start">«Inicio y estabilidad».</param>
public sealed record SystemScreen(
    string Title,
    string Subtitle,
    ValueList<SystemTabModel> Tabs,
    SystemTab Tab,
    UpdatesModel Updates,
    BackupsModel Backups,
    StartModel Start
);
