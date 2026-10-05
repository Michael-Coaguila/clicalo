using System.Globalization;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Tests.Generators;

/// <summary>A deterministic <see cref="IIdGenerator"/>: <c>np1</c>, <c>ns2</c>… never used by the generators.</summary>
internal sealed class SequentialIds : IIdGenerator
{
    private int _next;

    /// <inheritdoc />
    public ProfileId NewProfileId() => new("np" + Next());

    /// <inheritdoc />
    public ShortcutId NewShortcutId() => new("ns" + Next());

    private string Next() =>
        Interlocked.Increment(ref _next).ToString(CultureInfo.InvariantCulture);
}
