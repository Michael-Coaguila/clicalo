namespace Clicalo.Domain.Settings;

/// <summary>
/// Where the Tab handle was on one edge of one monitor (PES-016, user decision D9), in percent along that edge, inside
/// <see cref="SettingsSchema.DockHandlePosition"/>. Placement: never undoable.
/// </summary>
/// <param name="MonitorId">
/// The stable identifier of the monitor (see <see cref="MonitorHandlePositions"/>); opaque to the Domain, never empty.
/// </param>
/// <param name="Side">The screen edge.</param>
/// <param name="Position">Position along the edge, in percent.</param>
public sealed record MonitorHandlePosition(string MonitorId, DockSide Side, int Position);
