using Clicalo.Domain.Execution;
using Clicalo.Domain.Library;

namespace Clicalo.Domain.Tests.Execution;

/// <summary>EJE-011 and LOG-008: what the Shell thread may start, never through a command interpreter.</summary>
[Trait("Req", "EJE-011")]
[Trait("Req", "LOG-008")]
public sealed class LaunchSafetyTests
{
    private static LaunchRequest.StartApp Exe(string path) =>
        new LaunchRequest.StartApp(new AppTarget.Executable(path, string.Empty));

    private static LaunchRequest.StartApp Document(string path) =>
        new LaunchRequest.StartApp(new AppTarget.Document(path));

    private const string ProjectMail = "contacto@clicalo.example";

    [Theory]
    [Trait("Req", "ACE-004")]
    [InlineData("mailto:contacto@clicalo.example")]
    [InlineData("mailto:contacto@clicalo.example?subject=%5BCl%C3%ADcalo%5D%20Idea")]
    [InlineData(
        "mailto:Contacto@Clicalo.Example?subject=a&body=l%C3%ADnea%201%0D%0Al%C3%ADnea%202"
    )]
    public void The_feedback_email_opens_only_towards_the_project_address(string address) =>
        LaunchSafety.CheckMail(new Uri(address), ProjectMail).ShouldBe(LaunchVerdict.Allowed);

    [Theory]
    [Trait("Req", "ACE-004")]
    [InlineData("mailto:otra@persona.example?subject=a&body=b")]
    [InlineData("mailto:?subject=a&body=b")]
    [InlineData("mailto:contacto@clicalo.example,otra@persona.example?subject=a")]
    [InlineData("mailto:contacto@clicalo.example%2Cotra@persona.example?subject=a")]
    [InlineData("mailto:contacto@clicalo.example?cc=otra@persona.example")]
    [InlineData("mailto:contacto@clicalo.example?subject=a&bcc=otra@persona.example")]
    [InlineData("mailto:contacto@clicalo.example?to=otra@persona.example")]
    [InlineData("mailto:contacto@clicalo.example?subject=a&attach=C:%5Csecreto.txt")]
    [InlineData("mailto:contacto@clicalo.example?subject=a&subject=b")]
    [InlineData("mailto:contacto@clicalo.example?body=a&body=b")]
    [InlineData("https://clicalo.example/?subject=a")]
    [InlineData("file:///C:/Windows/notepad.exe")]
    public void Any_other_recipient_header_or_scheme_is_refused(string address)
    {
        // An address .NET cannot even parse (two recipients) never becomes a Uri, so it never reaches the shell.
        if (Uri.TryCreate(address, UriKind.Absolute, out var parsed))
        {
            LaunchSafety.CheckMail(parsed, ProjectMail).ShouldBe(LaunchVerdict.Invalid);
        }
    }

    [Theory]
    [Trait("Req", "ACE-004")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Without_a_project_address_no_email_opens(string? projectMail) =>
        LaunchSafety
            .CheckMail(new Uri("mailto:contacto@clicalo.example?subject=a"), projectMail)
            .ShouldBe(LaunchVerdict.Invalid);

    [Fact]
    [Trait("Req", "ACE-004")]
    public void A_shortcut_never_opens_an_email_address() =>
        LaunchSafety
            .Check(
                new LaunchRequest.OpenUrl(new Uri("mailto:contacto@clicalo.example")),
                confirmed: true
            )
            .ShouldBe(LaunchVerdict.Invalid);

    [Theory]
    [InlineData(@"C:\Program Files\Microsoft Office\root\Office16\WINWORD.EXE")]
    [InlineData("notepad.exe")]
    [InlineData("calc")]
    [InlineData(@"C:\Users\Public\Desktop\Spotify.lnk")]
    public void Executables_and_shortcuts_start(string path) =>
        LaunchSafety.Check(Exe(path), confirmed: false).ShouldBe(LaunchVerdict.Allowed);

    [Theory]
    [InlineData(@"C:\Windows\System32\cmd.exe")]
    [InlineData("CMD")]
    [InlineData("powershell.exe")]
    [InlineData("pwsh")]
    [InlineData(@"C:\Windows\System32\wscript.exe")]
    [InlineData("mshta.exe")]
    [InlineData("wsl.exe")]
    [InlineData(@"C:\tools\backup.bat")]
    [InlineData(@"C:\tools\backup.CMD")]
    [InlineData(@"C:\tools\run.ps1")]
    [InlineData(@"C:\tools\run.vbs")]
    [InlineData("\"C:\\tools\\run.js\"")]
    public void Interpreters_and_scripts_never_start(string path) =>
        LaunchSafety.Check(Exe(path), confirmed: true).ShouldBe(LaunchVerdict.Interpreter);

    [Theory]
    [InlineData(@"C:\Users\Public\Documents\report.docx", LaunchVerdict.Allowed)]
    [InlineData(@"C:\tools\setup.bat", LaunchVerdict.Interpreter)]
    [InlineData(@"C:\tools\cmd.pdf", LaunchVerdict.Allowed)]
    public void Documents_open_unless_they_are_scripts(string path, LaunchVerdict expected) =>
        LaunchSafety.Check(Document(path), confirmed: false).ShouldBe(expected);

    [Theory]
    [InlineData(@"\\server\share\tool.exe")]
    [InlineData("//server/share/tool.exe")]
    [InlineData(@"\\?\UNC\server\share\tool.exe")]
    public void A_network_path_needs_the_confirmation_of_its_shortcut(string path)
    {
        LaunchSafety.IsNetworkPath(path).ShouldBeTrue();
        LaunchSafety.Check(Exe(path), confirmed: false).ShouldBe(LaunchVerdict.NetworkPath);
        LaunchSafety.Check(Exe(path), confirmed: true).ShouldBe(LaunchVerdict.Allowed);
    }

    [Theory]
    [InlineData(@"\\?\C:\tools\app.exe")]
    [InlineData(@"C:\tools\app.exe")]
    public void Local_paths_are_not_network_paths(string path) =>
        LaunchSafety.IsNetworkPath(path).ShouldBeFalse();

    [Theory]
    [InlineData("https://example.com", LaunchVerdict.Allowed)]
    [InlineData("http://example.com/a?b=c", LaunchVerdict.Allowed)]
    [InlineData("ftp://example.com", LaunchVerdict.Invalid)]
    [InlineData("file:///C:/Windows/System32/cmd.exe", LaunchVerdict.Invalid)]
    public void Only_http_and_https_addresses_open(string address, LaunchVerdict expected) =>
        LaunchSafety
            .Check(new LaunchRequest.OpenUrl(new Uri(address)), confirmed: false)
            .ShouldBe(expected);

    [Theory]
    [InlineData("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", LaunchVerdict.Allowed)]
    [InlineData("", LaunchVerdict.Invalid)]
    [InlineData(@"..\cmd.exe", LaunchVerdict.Invalid)]
    public void Store_apps_start_by_their_id(string aumid, LaunchVerdict expected) =>
        LaunchSafety
            .Check(new LaunchRequest.StartApp(new AppTarget.StoreApp(aumid)), confirmed: false)
            .ShouldBe(expected);

    [Fact]
    public void Raw_text_never_starts() =>
        LaunchSafety
            .Check(new LaunchRequest.StartApp(new AppTarget.Raw("cmd /c del")), confirmed: true)
            .ShouldBe(LaunchVerdict.Invalid);
}
