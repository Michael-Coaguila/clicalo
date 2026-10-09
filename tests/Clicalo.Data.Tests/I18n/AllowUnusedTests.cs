namespace Clicalo.Data.Tests.I18n;

public sealed class AllowUnusedTests
{
    private static readonly HashSet<string> Keys = I18nData
        .Strings("es")
        .Select(static e => I18nData.BaseKey(e.Key))
        .ToHashSet(StringComparer.Ordinal);

    [Fact]
    [Trait("Req", "IDI-005")]
    public void Every_listed_key_exists_once()
    {
        var entries = I18nData.AllowUnused();

        entries
            .Where(static e => !Keys.Contains(e.Key))
            .Select(static e => e.Key + " (line " + e.Line + ")")
            .ShouldBeEmpty();
        entries
            .GroupBy(static e => e.Key, StringComparer.Ordinal)
            .Where(static g => g.Count() > 1)
            .Select(static g => g.Key)
            .ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "IDI-005")]
    public void The_orphan_keys_of_the_catalog_and_proposal_P2_are_listed()
    {
        var listed = I18nData
            .AllowUnused()
            .Select(static e => e.Key)
            .ToHashSet(StringComparer.Ordinal);

        listed.Contains("rSingle").ShouldBeTrue();
        listed.Contains("dStrip").ShouldBeTrue();
        listed.Contains("sidesA").ShouldBeTrue();
        listed.Contains("useOther").ShouldBeFalse();
        // M4 shows dupTitle (the state of a repeated tile) and dupChangeT (REP-006), so they left the list.
        listed.Contains("dupTitle").ShouldBeFalse();
        listed.Count.ShouldBe(57);
    }
}
