namespace Clicalo.Domain.Execution;

/// <summary>Identity of one run of a macro.</summary>
/// <param name="Value">Sequence number, unique per engine.</param>
public readonly record struct MacroRunId(long Value);
