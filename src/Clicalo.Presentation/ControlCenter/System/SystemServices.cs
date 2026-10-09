using Clicalo.Application.Ports;
using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.SystemSection;

/// <summary>What «Sistema» works with besides the services of the Control Center; the composition root builds it.</summary>
/// <param name="Updates">The updates of the installed copy.</param>
/// <param name="Backups">The backups, export and import.</param>
/// <param name="Startup">«Iniciar con Windows».</param>
/// <param name="Elevation">«Reabrir como administrador».</param>
/// <param name="Ids">New ids for what an import merges (DAT-004).</param>
/// <param name="EndForHandover">Ends this instance cleanly once the elevated one started (release all, flush).</param>
public sealed record SystemServices(
    IUpdateService Updates,
    ISystemBackups Backups,
    IStartupRegistration Startup,
    IElevatedRelaunch Elevation,
    IIdGenerator Ids,
    Func<Task> EndForHandover
);
