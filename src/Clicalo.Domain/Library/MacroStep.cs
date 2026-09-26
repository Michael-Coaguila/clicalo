namespace Clicalo.Domain.Library;

/// <summary>
/// One step of a macro (EJE-010): <see cref="KeysStep"/>, <see cref="WaitStep"/>, <see cref="TextStep"/> or
/// <see cref="MouseStep"/>. A closed hierarchy, so a step always has a valid type (invariant I6).
/// </summary>
public abstract record MacroStep
{
    private protected MacroStep() { }
}
