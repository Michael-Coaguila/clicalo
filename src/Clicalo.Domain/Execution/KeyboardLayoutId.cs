namespace Clicalo.Domain.Execution;

/// <summary>A keyboard layout handle (<c>HKL</c>) as a value.</summary>
/// <param name="Value">The layout handle bits.</param>
public readonly record struct KeyboardLayoutId(ulong Value);
