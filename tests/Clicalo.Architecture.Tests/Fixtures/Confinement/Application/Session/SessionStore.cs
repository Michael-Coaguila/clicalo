namespace Clicalo.Architecture.Tests.Fixtures.Confinement.Application.Session;

/// <summary>Fixture: a store with a single writer.</summary>
public sealed class SessionStore
{
    public event EventHandler? Changed;

    public int Current { get; private set; }

    public void Dispatch(int value)
    {
        Current = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
