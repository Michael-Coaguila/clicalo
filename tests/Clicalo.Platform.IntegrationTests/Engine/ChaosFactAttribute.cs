using System.Runtime.CompilerServices;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>A fact that runs only where <see cref="ChaosEnvironment.IsEnabled"/> (continuous integration).</summary>
public sealed class ChaosFactAttribute : FactAttribute
{
    public ChaosFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1
    )
        : base(sourceFilePath, sourceLineNumber)
    {
        Skip = ChaosEnvironment.SkipReason;
        SkipUnless = nameof(ChaosEnvironment.IsEnabled);
        SkipType = typeof(ChaosEnvironment);
    }
}
