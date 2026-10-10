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
        // Decision D5 of the user (2026-10-09): no free quota, so its texts are not shown; aiPrivacy and sharedProf
        // give way to aiPrivacy4 and to sharing a file (catalog §6.1, R-26).
        listed.Contains("quotaFree").ShouldBeTrue();
        // About and the welcome show creatorRole and fbLogFile, the catalog §9 corrections of creator and fbLogD.
        listed.Contains("creator").ShouldBeTrue();
        // M6: the active app is marked with activeShort (ATJ-008), so it left the list; the fragments that were joined
        // to a number give way to whole messages with their plural (IDI-004), and autoReleaseD and recorded to the
        // texts that say what the app does (catalog §9).
        listed.Contains("activeShort").ShouldBeFalse();
        listed.Contains("shortcutsW").ShouldBeTrue();
        listed.Contains("autoReleaseD").ShouldBeTrue();
        listed.Count.ShouldBe(72);
    }
}
