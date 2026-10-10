using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Library;
using Clicalo.Platform.Windows.Launch.InstalledApps;

namespace Clicalo.Platform.IntegrationTests.Actions;

/// <summary>
/// «Elegir programa» against the real shell (EDI-014): the Applications folder of this computer is listed, never
/// opened. It needs a desktop session with the shell running, so it is not part of <c>cl check</c>.
/// </summary>
[Trait("Requires", "Desktop")]
public sealed class InstalledAppsReaderTests
{
    [Fact]
    [Trait("Req", "EDI-014")]
    public async Task The_installed_programs_are_listed_by_name_with_a_target_the_app_field_understands()
    {
        var programs = await InstalledAppsReader.ListAsync(TestContext.Current.CancellationToken);

        programs.ShouldNotBeEmpty("Windows always has apps in its Start menu");
        programs.ShouldAllBe(p => p.Name.Length > 0);
        programs
            .Select(p => p.Name)
            .ShouldBe(programs.Select(p => p.Name).Order(StringComparer.CurrentCultureIgnoreCase));
        // Every target is one the launcher opens, without a command interpreter: through the Applications folder, or
        // by the path of the program when Windows names it by its path.
        programs.ShouldAllBe(p =>
            LaunchSafety.Check(
                new LaunchRequest.StartApp(Targets.ParseApp(p.Target)),
                confirmed: false
            ) == LaunchVerdict.Allowed
        );
        programs.ShouldContain(p => Targets.ParseApp(p.Target) is AppTarget.StoreApp);
        programs.ShouldContain(
            p => Targets.ParseApp(p.Target) is AppTarget.Executable,
            "the Windows tools of the Start menu are named by their path"
        );
        programs
            .Select(p => p.Target)
            .Distinct(StringComparer.Ordinal)
            .Count()
            .ShouldBe(programs.Length);
    }
}
