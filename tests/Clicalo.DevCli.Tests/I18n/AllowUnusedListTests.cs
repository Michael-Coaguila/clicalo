using Clicalo.DevCli.I18n;

namespace Clicalo.DevCli.Tests.I18n;

public sealed class AllowUnusedListTests
{
    [Fact]
    public void Comments_and_blank_lines_are_ignored_and_lines_are_one_based()
    {
        var entries = AllowUnusedList.Parse(
            "# header\r\n\r\ndStrip\r\n  mFollow   # trailing comment\n#only comment\nrSingle"
        );

        entries.ShouldBe([
            new AllowUnusedEntry("dStrip", 3),
            new AllowUnusedEntry("mFollow", 4),
            new AllowUnusedEntry("rSingle", 6),
        ]);
    }

    [Fact]
    public void An_empty_file_has_no_entries() =>
        AllowUnusedList.Parse(string.Empty).ShouldBeEmpty();
}
