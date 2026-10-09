namespace Clicalo.Data.Tests.I18n;

public sealed class StringsParityTests
{
    [Fact]
    [Trait("Req", "IDI-001")]
    public void Spanish_and_English_have_exactly_the_same_keys_in_the_same_order()
    {
        var spanish = I18nData.Strings("es").Select(static e => e.Key).ToList();
        var english = I18nData.Strings("en").Select(static e => e.Key).ToList();

        english.ShouldBe(spanish);
    }

    [Fact]
    [Trait("Req", "IDI-001")]
    public void Every_one_of_the_669_handoff_keys_is_kept_with_its_original_name_unless_retired()
    {
        var handoff = I18nData.Handoff("es").Select(static e => e.Key).ToList();
        var retired = I18nData.RetiredHandoffKeys();
        var imported = I18nData
            .Strings("es")
            .Select(static e => I18nData.BaseKey(e.Key))
            .ToHashSet(StringComparer.Ordinal);

        handoff.Count.ShouldBe(669);
        // Decision D1 of the user (2026-10-03, ADR-0020): no import from Macro Quick Access, so no migration card.
        retired.Order(StringComparer.Ordinal).ShouldBe(["migD", "migT"]);
        handoff.Where(k => !retired.Contains(k) && !imported.Contains(k)).ShouldBeEmpty();
        retired.Where(imported.Contains).ShouldBeEmpty();
    }

    [Fact]
    public void Only_the_reviewed_keys_are_new_and_only_the_catalog_plurals_have_forms()
    {
        var handoff = I18nData
            .Handoff("es")
            .Select(static e => e.Key)
            .ToHashSet(StringComparer.Ordinal);
        var entries = I18nData.Strings("es");

        entries
            .Select(static e => I18nData.BaseKey(e.Key))
            .Where(k => !handoff.Contains(k))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ShouldBe([
                // Texts of the M3 actions: what each action did and why a launch did not start (catalog §6.1, R-21).
                "aboutVersion",
                "actionFailed",
                "actionUnavailable",
                "addedToProf",
                "alreadyAdded",
                "appGone",
                "appName",
                "backupDamaged",
                "catEdit",
                "catFile",
                "catFmt",
                "catHist",
                "catNav",
                "catSel",
                "catText",
                "catVoice",
                "catWeb",
                "catWin",
                "ccSoon",
                "coachStep",
                "confirmRun",
                "confirmRunD",
                "contactPending",
                "creatorInitials",
                "creatorName",
                "creatorRole",
                "dataUnreadable",
                "delA",
                "dockPageOf",
                "dockScrollDown",
                "dockScrollUp",
                "elevatedRefused",
                "engineFault",
                "exitApp",
                "fbBodyLog",
                "fbBodySys",
                "fbLogFile",
                "fbMailCopied",
                "fbSendLogD",
                "fbSubject",
                "generalFixed",
                "gitHub",
                "guardianUnstable",
                "handleLock",
                "hideA",
                "hidePanel",
                "holdAsGeneral",
                "holdMinutes",
                "holdSeconds",
                "holdingKeys",
                "importInvalid",
                "importTooLarge",
                "incompleteTap",
                "installedApp",
                "itemGone",
                "kbAppsEn",
                "kbAppsEs",
                "keyMissing",
                "kgFn",
                "kgMods",
                "kitBasics",
                "kitBasicsD",
                "latchedName",
                "launchUnsafe",
                "linkedToApp",
                "logPvEmpty",
                "logPvFailed",
                "macroCancelled",
                "macroRanName",
                "macroRunning",
                "macroStep",
                "mouseRan",
                "noOpenApps",
                "obKbLine",
                "obStep",
                "opLess",
                "opMore",
                "openedName",
                "orderHintDrag",
                "pinFolds",
                "pinLimit",
                "positionOf",
                "processTaken",
                "profAutoOn",
                "profLockedOn",
                "releasedKeys",
                "releasedOnLock",
                "removeKeyN",
                "saveFailD",
                "saveFailT",
                "saveReadOnly",
                "schemaNewer",
                "searchDenied",
                "searchDictate",
                "searchNoReturn",
                "settingInvalid",
                "sharedTextsExcluded",
                "stepDeleted",
                "stepPress",
                "stepType",
                "stepWait",
                "stepsN",
                "szL",
                "szM",
                "szS",
                "takeOverYes",
                "tapSent",
                "textTyped",
                "textTypedPrivate",
                "textUnavailable",
                "tmLeft",
                "trayHidden",
                "undoEditsIn",
                "unlatchedName",
                "voiceSay",
                "waitLonger",
                "waitShorter",
                "welcomeTitle",
            ]);
        entries
            .Where(static e => I18nData.Category(e.Key) is not null)
            .Select(static e => I18nData.BaseKey(e.Key))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ShouldBe([
                "addMissing",
                "comboN",
                "dupHead",
                "instNoteSome",
                "pinLimit",
                "sharedTextsExcluded",
                "stepsN",
                "sugLine",
                "tmLeft",
                "twMacro",
            ]);
    }

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    [Trait("Req", "IDI-001")]
    public void No_text_is_empty(string language) =>
        I18nData
            .Strings(language)
            .Where(static e => string.IsNullOrWhiteSpace(e.Value))
            .Select(static e => e.Key)
            .ShouldBeEmpty();

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    public void Keys_are_letters_and_digits_with_an_optional_CLDR_plural_suffix(string language)
    {
        foreach (var (key, _) in I18nData.Strings(language))
        {
            var baseKey = I18nData.BaseKey(key);
            (
                baseKey.Length > 0
                && char.IsAsciiLetter(baseKey[0])
                && baseKey.All(char.IsAsciiLetterOrDigit)
            ).ShouldBeTrue(key);
            if (I18nData.Category(key) is { } category)
            {
                I18nData
                    .PluralCategories.Contains(category, StringComparer.Ordinal)
                    .ShouldBeTrue(key);
            }
        }
    }
}
