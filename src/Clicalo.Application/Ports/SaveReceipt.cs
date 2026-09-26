namespace Clicalo.Application.Ports;

/// <summary>A successful save.</summary>
/// <param name="Seq">The envelope sequence number written.</param>
/// <param name="WrittenAt">When.</param>
public sealed record SaveReceipt(long Seq, DateTimeOffset WrittenAt);
