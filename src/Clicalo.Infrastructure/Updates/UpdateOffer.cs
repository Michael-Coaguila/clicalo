namespace Clicalo.Infrastructure.Updates;

/// <summary>A version a channel offers.</summary>
/// <param name="Version">Its version.</param>
/// <param name="IsDowngrade">Whether it is older than the running one.</param>
/// <param name="Notes">Its release notes (Markdown), or null.</param>
/// <param name="Handle">What the client needs to download and apply it (Velopack's update info).</param>
internal sealed record UpdateOffer(string Version, bool IsDowngrade, string? Notes, object? Handle);
