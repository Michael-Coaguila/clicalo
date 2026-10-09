using System.Text;
using Clicalo.Infrastructure.Updates;

namespace Clicalo.Infrastructure.Tests.Updates;

/// <summary>
/// The small formats of the updates: SemVer order (blueprint §11), the release notes that travel with the package
/// (ACT-004) and <c>update.json</c> (ACT-005).
/// </summary>
public sealed class UpdateFormatsTests
{
    [Theory]
    [InlineData("2.0.0", "2.0.1", -1)]
    [InlineData("2.1.0", "2.0.9", 1)]
    [InlineData("2.0.0-beta.1", "2.0.0", -1)]
    [InlineData("2.0.0-beta.2", "2.0.0-beta.10", -1)]
    [InlineData("2.0.0-beta.2", "2.0.0-beta.2", 0)]
    [InlineData("2.0.0+abc", "2.0.0", 0)]
    [InlineData("10.0.0", "9.9.9", 1)]
    [Trait("Req", "NFR-010")]
    public void Versions_follow_semantic_versioning(string left, string right, int expected)
    {
        VersionOrder.Compare(left, right).ShouldBe(expected);
        VersionOrder.Compare(right, left).ShouldBe(-expected);
    }

    [Fact]
    [Trait("Req", "ACT-004")]
    public void Release_notes_are_read_per_language_with_their_date()
    {
        const string markdown =
            "# 2.1.0\r\n<!-- date: 2026-10-09 -->\r\n## es\r\n- Pestaña lateral\r\n- Filas visibles\r\n\r\n## en\r\n- Edge tab\r\n## fr\r\nTexte libre\r\n";

        var notes = ReleaseNotesParser.Parse("2.1.0", markdown, isNew: true);

        notes.Version.ShouldBe("2.1.0");
        notes.IsNew.ShouldBeTrue();
        notes.Date.ShouldBe(new DateOnly(2026, 10, 9));
        notes.In("es").ShouldBe(["Pestaña lateral", "Filas visibles"]);
        notes.In("en").ShouldBe(["Edge tab"]);
        notes.In("pt").ShouldBe(["Pestaña lateral", "Filas visibles"], "falls back to Spanish");
        notes.Items.ContainsKey("fr").ShouldBeTrue();
        notes.In("fr").ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "ACT-004")]
    [Trait("Req", "LOG-006")]
    public void Release_notes_are_untrusted_and_capped()
    {
        var markdown = new StringBuilder("## es\n");
        for (var i = 0; i < ReleaseNotesParser.MaxItems + 5; i++)
        {
            markdown
                .Append("- ")
                .Append(new string('x', ReleaseNotesParser.MaxItemLength + 10))
                .Append('\n');
        }

        var notes = ReleaseNotesParser.Parse("2.0.0", markdown.ToString(), isNew: false);

        notes.In("es").Length.ShouldBe(ReleaseNotesParser.MaxItems);
        notes.In("es")[0].Length.ShouldBe(ReleaseNotesParser.MaxItemLength);
        ReleaseNotesParser.Parse("2.0.0", null, isNew: false).Items.ShouldBeEmpty();
        ReleaseNotesParser
            .Parse("2.0.0", "<!-- date: mañana -->", isNew: false)
            .Date.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "ACT-005")]
    public void The_update_state_round_trips_and_rejects_anything_else()
    {
        var state = new UpdateState(
            "2.1.0",
            "2.0.0",
            new DateTimeOffset(2026, 10, 9, 10, 0, 0, TimeSpan.Zero)
        );

        UpdateStateFile.Parse(UpdateStateFile.Write(state)).ShouldBe(state);
        UpdateStateFile
            .Parse(UpdateStateFile.Write(new UpdateState("2.0.0", null, null)))
            .ShouldBe(new UpdateState("2.0.0", null, null));
        UpdateStateFile
            .Parse(UpdateStateFile.Write(new UpdateState("2.0.0", null, null, "2.1.0")))
            .ShouldBe(new UpdateState("2.0.0", null, null, "2.1.0"));
        UpdateStateFile.Parse("not json"u8).ShouldBeNull();
        UpdateStateFile.Parse("[1,2]"u8).ShouldBeNull();
        UpdateStateFile.Parse("{\"lastRunVersion\":\"\"}"u8).ShouldBeNull();
        UpdateStateFile.Parse("{\"lastRunVersion\":3}"u8).ShouldBeNull();
    }
}
