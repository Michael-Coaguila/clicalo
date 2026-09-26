namespace Clicalo.Build.Tests;

public sealed class CSharpierReportTests
{
    [Fact]
    public void Lists_each_unformatted_file_once_with_forward_slashes()
    {
        const string Output = """
            Error .\build\Program.cs - Was not formatted.
              ----------------------------- Expected: Around Line 21 -----------------------------
                          var application = new ClApplication(
              ----------------------------- Actual: Around Line 21 -----------------------------
            Error ./src/Clicalo.Domain/Foo.cs - Was not formatted due to syntax errors.
            Error .\build\Program.cs - Was not formatted.
            Checked 53 files in 1096ms.
            """;

        CSharpierReport
            .UnformattedFiles(Output)
            .ShouldBe(["build/Program.cs", "src/Clicalo.Domain/Foo.cs"]);
    }

    [Fact]
    public void A_clean_run_lists_nothing() =>
        CSharpierReport.UnformattedFiles("Checked 53 files in 761ms.").ShouldBeEmpty();
}
