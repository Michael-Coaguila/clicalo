using System.Runtime.CompilerServices;

namespace Clicalo.Performance;

/// <summary>
/// A measurement of <c>cl perf</c>: skipped unless <see cref="PerfEnvironment.IsEnabled"/>. Mark the class with
/// <c>[Trait("Requires", "Desktop")]</c> and <c>[Trait("Category", "Perf")]</c>, so <c>cl check</c> and <c>cl desk</c>
/// leave it out and <c>cl perf</c> selects it.
/// </summary>
public sealed class PerfFactAttribute : FactAttribute
{
    public PerfFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1
    )
        : base(sourceFilePath, sourceLineNumber)
    {
        Skip = PerfEnvironment.SkipReason;
        SkipUnless = nameof(PerfEnvironment.IsEnabled);
        SkipType = typeof(PerfEnvironment);
    }
}
