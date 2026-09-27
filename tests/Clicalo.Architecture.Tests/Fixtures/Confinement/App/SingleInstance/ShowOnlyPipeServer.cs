namespace Clicalo.Architecture.Tests.Fixtures.Confinement.App.SingleInstance;

/// <summary>Fixture: a single-instance pipe server that only raises «show», as D13 allows.</summary>
public sealed class ShowOnlyPipeServer
{
    public event EventHandler? ShowRequested;

    public void Handle() => ShowRequested?.Invoke(this, EventArgs.Empty);
}
