using Clicalo.Infrastructure.Updates;

namespace Clicalo.Infrastructure.Tests.Updates;

/// <summary>
/// Only the copy in the installer's folder is the installed one (SIS-002, LOG-007, ADR-0027): «Iniciar con Windows» and
/// «Reabrir como administrador» never use a portable copy or one unpacked elsewhere.
/// </summary>
public sealed class InstallationTests
{
    private const string LocalAppData = @"C:\Users\ana\AppData\Local";

    [Fact]
    [Trait("Req", "SIS-002")]
    [Trait("Req", "LOG-007")]
    public void The_installer_folder_is_the_installed_copy()
    {
        Installation
            .InFolder(@"C:\Users\ana\AppData\Local\Clicalo.App\current\", LocalAppData)
            .ShouldBe(@"C:\Users\ana\AppData\Local\Clicalo.App\current\Clicalo.exe");
        Installation
            .InFolder(@"c:\users\ana\appdata\local\clicalo.app\CURRENT", LocalAppData)
            .ShouldNotBeNull();
    }

    [Theory]
    [Trait("Req", "SIS-002")]
    [Trait("Req", "LOG-007")]
    [InlineData(@"C:\Users\ana\Downloads\Clicalo\current")]
    [InlineData(@"C:\Users\ana\AppData\Local\Other.App\current")]
    [InlineData(@"C:\Users\ana\AppData\Local\Clicalo.App\current\..\..\Evil\current")]
    [InlineData(null)]
    public void Any_other_folder_is_not_installed(string? folder) =>
        Installation.InFolder(folder, LocalAppData).ShouldBeNull();
}
