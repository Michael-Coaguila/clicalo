using System.Collections.Immutable;

namespace Clicalo.Tools.SpikeLab.Composition;

/// <summary>
/// The list of real pieces and their state. A piece that fails to start is shown in red and the laboratory goes on
/// with the rest; a piece that waits for another one that failed is «pendiente».
/// </summary>
internal sealed class ComponentBoard
{
    private ImmutableArray<LabComponent> _components = [];

    /// <summary>Raised after every change.</summary>
    public event EventHandler? Changed;

    /// <summary>The pieces, in the order they were first reported.</summary>
    public ImmutableArray<LabComponent> Components => _components;

    /// <summary>True when every piece is ready.</summary>
    public bool AllReady =>
        _components.All(component => component.State == LabComponentState.Ready);

    /// <summary>Describes an exception thrown by a piece: its type and message.</summary>
    public static LabComponent Describe(string name, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new LabComponent(
            name,
            LabComponentState.Failed,
            exception.GetType().Name + ": " + exception.Message
        );
    }

    /// <summary>Records <paramref name="name"/> as ready.</summary>
    public void Ready(string name, string detail) =>
        Set(new LabComponent(name, LabComponentState.Ready, detail));

    /// <summary>Records <paramref name="name"/> as failed because of <paramref name="exception"/>.</summary>
    public void Fail(string name, Exception exception) => Set(Describe(name, exception));

    /// <summary>Records <paramref name="name"/> as failed with <paramref name="detail"/>.</summary>
    public void Fail(string name, string detail) =>
        Set(new LabComponent(name, LabComponentState.Failed, detail));

    /// <summary>Records <paramref name="name"/> as pending with <paramref name="detail"/>.</summary>
    public void Pending(string name, string detail) =>
        Set(new LabComponent(name, LabComponentState.Pending, detail));

    /// <summary>
    /// Runs <paramref name="action"/> and records the piece as ready, or as failed when it throws. The
    /// exception never escapes: the laboratory keeps working with the pieces that are ready.
    /// </summary>
    /// <returns>True when the action completed.</returns>
    public bool Try(string name, string detail, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            action();
            Ready(name, detail);
            return true;
        }
        catch (Exception ex) when (IsContained(ex))
        {
            Fail(name, ex);
            return false;
        }
    }

    /// <summary>The asynchronous form of <see cref="Try(string, string, Action)"/>.</summary>
    public async Task<bool> TryAsync(string name, string detail, Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            await action();
            Ready(name, detail);
            return true;
        }
        catch (Exception ex) when (IsContained(ex))
        {
            Fail(name, ex);
            return false;
        }
    }

    /// <summary>
    /// The failures a piece may have inside the laboratory without taking it down: everything except the ones that
    /// mean the process itself is broken.
    /// </summary>
    public static bool IsContained(Exception exception) =>
        exception
            is not (OutOfMemoryException or StackOverflowException or AccessViolationException);

    private void Set(LabComponent component)
    {
        var index = -1;
        for (var i = 0; i < _components.Length; i++)
        {
            if (string.Equals(_components[i].Name, component.Name, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        _components =
            index < 0 ? _components.Add(component) : _components.SetItem(index, component);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
