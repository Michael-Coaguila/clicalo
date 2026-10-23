using Clicalo.Domain.Primitives;

namespace Clicalo.Application.Coordinators;

/// <summary>The name of an app in front was read (EJE-003): the notices say it instead of its executable.</summary>
/// <param name="process">Its executable.</param>
/// <param name="name">The description of its executable («Microsoft Word»).</param>
public sealed class AppNamedEventArgs(ProcessName process, string name) : EventArgs
{
    /// <summary>Its executable.</summary>
    public ProcessName Process { get; } = process;

    /// <summary>The description of its executable.</summary>
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
}
