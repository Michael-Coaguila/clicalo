using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Tests.Execution;

/// <summary>
/// <see cref="ProcessName.AppName"/> (EJE-003): how the notices name an app whose executable has no description yet.
/// </summary>
public sealed class ProcessNameAppNameTests
{
    [Theory]
    [InlineData("regedit.exe", "Regedit")]
    [InlineData("WINWORD.EXE", "WINWORD")]
    [InlineData("Taskmgr.exe", "Taskmgr")]
    [InlineData("notepad", "Notepad")]
    [InlineData("", "")]
    [Trait("Req", "EJE-003")]
    public void Without_its_description_an_app_is_its_executable_with_a_capital_letter(
        string process,
        string expected
    ) => new ProcessName(process).AppName.ShouldBe(expected);
}
