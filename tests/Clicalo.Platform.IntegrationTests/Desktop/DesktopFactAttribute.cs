using System.Runtime.CompilerServices;
using Clicalo.TestKit.Windows;

namespace Clicalo.Platform.IntegrationTests.Desktop;

/// <summary>
/// A fact that needs an interactive desktop. xUnit v3 skips it at run time unless
/// <c>CLICALO_DESKTOP_TESTS=1</c> (<see cref="DesktopTestEnvironment"/>). Mark the class with
/// <c>[Trait("Requires", "Desktop")]</c> and <c>[Collection(DesktopCollectionDefinition.Name)]</c>.
/// </summary>
public sealed class DesktopFactAttribute : FactAttribute
{
    public DesktopFactAttribute(
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
