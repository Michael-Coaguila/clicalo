using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Library;
using Clicalo.Platform.Windows.Launch.InstalledApps;

namespace Clicalo.Platform.IntegrationTests.Actions;

/// <summary>
/// What «Elegir programa» writes in the App field for each program Windows lists (EDI-014), without asking the shell:
/// a program is only offered with a target the launcher opens. Headless and deterministic.
/// </summary>
[Trait("Req", "EDI-014")]
public sealed class InstalledAppTargetTests
{
    private const string System32 = "{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}";

    private static string? Folder(Guid folder) =>
        folder == Guid.Parse(System32) ? @"C:\Windows\System32\" : null;

    private static void ShouldOpen(string? target) =>
        LaunchSafety
            .Check(new LaunchRequest.StartApp(Targets.ParseApp(target)), confirmed: false)
            .ShouldBe(LaunchVerdict.Allowed);

    [Theory]
    [InlineData("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App")]
    [InlineData("Microsoft.Office.WINWORD.EXE.15")]
    public void A_program_with_an_id_of_its_own_opens_through_the_applications_folder(string id)
    {
        var target = AppsFolder.TargetOf(id, Folder, static _ => false);

        target.ShouldBe(@"shell:AppsFolder\" + id);
        Targets.ParseApp(target).ShouldBe(new AppTarget.StoreApp(id));
        ShouldOpen(target);
    }

    [Fact]
    public void A_program_named_by_its_path_in_a_known_folder_opens_by_that_path()
    {
        var target = AppsFolder.TargetOf(System32 + @"\notepad.exe", Folder, static _ => true);

        target.ShouldBe(@"C:\Windows\System32\notepad.exe");
        Targets.ParseApp(target).ShouldBeOfType<AppTarget.Executable>();
        ShouldOpen(target);
    }

    [Fact]
    public void A_program_named_by_a_full_path_opens_by_that_path()
    {
        var target = AppsFolder.TargetOf(@"D:\Tools\Editor\editor.exe", Folder, static _ => true);

        target.ShouldBe(@"D:\Tools\Editor\editor.exe");
        ShouldOpen(target);
    }

    [Theory]
    // The file is not there any more.
    [InlineData(System32 + @"\gone.exe", false)]
    // A known folder Windows does not have.
    [InlineData(@"{00000000-0000-0000-0000-000000000001}\app.exe", true)]
    [InlineData(@"{not-a-guid}\app.exe", true)]
    // The launcher never starts an interpreter, a script or a console snap-in (EJE-011).
    [InlineData(System32 + @"\cmd.exe", true)]
    [InlineData(System32 + @"\services.msc", true)]
    // Nor a program on another computer without a confirmation.
    [InlineData(@"\\server\share\app.exe", true)]
    [InlineData(@"relative\app.exe", true)]
    public void A_program_the_launcher_would_refuse_is_not_offered(string id, bool exists)
    {
        AppsFolder.TargetOf(id, Folder, _ => exists).ShouldBeNull();
    }
}
