using Clicalo.Domain.Library;
using Clicalo.Domain.Migration.V1;

namespace Clicalo.Domain.Tests.Migration;

/// <summary>
/// v1 web and app buttons (catalog §7.4): only http and https are addresses, and nothing that needs a command
/// interpreter is ever kept runnable (EJE-011, LOG-008).
/// </summary>
public sealed class V1ActionTargetsTests
{
    [Theory]
    [Trait("Req", "EJE-011")]
    [InlineData("https://mail.example.com/inbox", "https://mail.example.com/inbox")]
    [InlineData("http://example.com", "http://example.com/")]
    [InlineData("  example.com/docs ", "https://example.com/docs")]
    public void Web_addresses_are_kept_or_completed(string action, string address)
    {
        var (target, review) = V1ActionTargets.Url(action);

        target.ShouldBe(new UrlTarget.Valid(new Uri(address)));
        review.ShouldBeNull();
    }

    [Theory]
    [Trait("Req", "LOG-008")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("C:\\Users\\someone\\notes.txt")]
    [InlineData("not an address")]
    public void Anything_that_is_not_http_or_https_is_kept_raw_for_review(string action)
    {
        var (target, review) = V1ActionTargets.Url(action);

        target.ShouldBe(new UrlTarget.Raw(action));
        review.ShouldBe(MigrationNoteKind.NonWebAddress);
    }

    [Theory]
    [Trait("Req", "EJE-011")]
    [InlineData("notepad.exe", "notepad.exe", "")]
    [InlineData("C:\\Program Files\\App\\app.exe", "C:\\Program Files\\App\\app.exe", "")]
    [InlineData(
        "C:\\Program Files\\App\\app.exe --new-window",
        "C:\\Program Files\\App\\app.exe",
        "--new-window"
    )]
    [InlineData(
        "\"C:\\Program Files\\App\\app.exe\" /safe",
        "C:\\Program Files\\App\\app.exe",
        "/safe"
    )]
    [InlineData("calc", "calc", "")]
    public void Executables_keep_their_path_and_arguments(
        string action,
        string path,
        string arguments
    )
    {
        var (target, review) = V1ActionTargets.App(action);

        target.ShouldBe(new AppTarget.Executable(path, arguments));
        review.ShouldBeNull();
    }

    [Fact]
    public void Documents_and_store_apps_are_recognized()
    {
        V1ActionTargets
            .App("C:\\Docs\\my notes.pdf")
            .Target.ShouldBe(new AppTarget.Document("C:\\Docs\\my notes.pdf"));
        V1ActionTargets
            .App("shell:AppsFolder\\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App")
            .Target.ShouldBe(
                new AppTarget.StoreApp("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App")
            );
    }

    [Theory]
    [Trait("Req", "LOG-008")]
    [InlineData("cmd /c start chrome")]
    [InlineData("cmd.exe /k dir")]
    [InlineData("powershell -NoProfile -Command Get-Process")]
    [InlineData("C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe -File x.ps1")]
    [InlineData("pwsh")]
    [InlineData("C:\\Scripts\\backup.bat")]
    [InlineData("run.vbs")]
    [InlineData("notepad.exe & calc.exe")]
    [InlineData("echo %USERNAME%")]
    [InlineData("app.exe > out.txt")]
    [InlineData("ms-settings:display")]
    [InlineData("https://example.com")]
    public void Commands_that_need_an_interpreter_are_kept_raw_and_never_run(string action)
    {
        var (target, review) = V1ActionTargets.App(action);

        target.ShouldBe(new AppTarget.Raw(action));
        review.ShouldBe(MigrationNoteKind.InterpreterCommand);
    }

    [Fact]
    public void Missing_actions_are_incomplete_and_reported()
    {
        V1ActionTargets
            .Url(null)
            .ShouldBe((new UrlTarget.Raw(string.Empty), MigrationNoteKind.MissingAction));
        V1ActionTargets
            .App("  ")
            .ShouldBe((new AppTarget.Raw(string.Empty), MigrationNoteKind.MissingAction));
    }
}
