namespace Clicalo.Windowing.IntegrationTests.Automation.Lab;

/// <summary>One tile event, as the surface would forward it with origin <c>UiaInvoke</c>.</summary>
/// <param name="TileId">The tile's automation id.</param>
/// <param name="Kind">The event.</param>
/// <param name="ManagedThreadId">The thread that raised it (the tile's UI thread).</param>
public sealed record LabInvocation(string TileId, LabInvocationKind Kind, int ManagedThreadId);
