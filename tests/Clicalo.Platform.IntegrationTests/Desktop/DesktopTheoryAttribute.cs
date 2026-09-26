using System.Runtime.CompilerServices;
using Clicalo.TestKit.Windows;

namespace Clicalo.Platform.IntegrationTests.Desktop;

/// <summary>A theory that needs an interactive desktop; see <see cref="DesktopFactAttribute"/>.</summary>
public sealed class DesktopTheoryAttribute : TheoryAttribute
{
    public DesktopTheoryAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1
    )
        : base(sourceFilePath, sourceLineNumber)
    {
        Skip = DesktopTestEnvironment.SkipReason;
        SkipUnless = nameof(DesktopTestEnvironment.IsEnabled);
        SkipType = typeof(DesktopTestEnvironment);
    }
}
