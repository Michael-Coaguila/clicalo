using Clicalo.App.Lifecycle;

namespace Clicalo.App.Composition;

/// <summary>The guardian and the preventive release of <c>--no-input</c>: nothing is pressed, so there is nothing to guard.</summary>
internal sealed class NoGuardian : IGuardian, IStartupRelease
{
    /// <inheritdoc />
    public bool IsRunning => false;

    /// <inheritdoc />
    /// <remarks>Never raised: nothing is pressed, so there is nothing to guard.</remarks>
    public event EventHandler? Unstable
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public int ReleaseStuckModifiers() => 0;
}
