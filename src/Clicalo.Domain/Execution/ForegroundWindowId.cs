namespace Clicalo.Domain.Execution;

/// <summary>An opaque window handle of the foreground app, as a value (the Domain never touches handles).</summary>
/// <param name="Value">The handle bits.</param>
public readonly record struct ForegroundWindowId(ulong Value);
