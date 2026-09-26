using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Library;

/// <summary>Which apps a profile follows (PER-002): none (manual) or its processes, unique across profiles (I5).</summary>
public abstract record AppBinding
{
    private AppBinding() { }

    /// <summary>Only chosen by hand; General is always manual (I4).</summary>
    public sealed record Manual : AppBinding;

    /// <summary>Shown automatically when one of these processes is in the foreground.</summary>
    /// <param name="Names">The processes, compared without distinguishing case.</param>
    public sealed record Processes(ValueList<ProcessName> Names) : AppBinding;
}
