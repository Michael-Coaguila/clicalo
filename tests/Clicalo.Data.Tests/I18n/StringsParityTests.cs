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
                // Key-group and category labels of data/catalogs (keys.json, categories.json).
                // Texts of the M3 actions: what each action did and why a launch did not start (catalog §6.1, R-21).
                "aboutVersion",
                "actionFailed",
                "actionUnavailable",
                "activeCount",
                "addedCountToProf",
                "addedToProf",
                "adminActive",
                "adminCancelled",
                "adminFailed",
                "adminNotInstalled",
                "adminRestarting",
                // Texts of the M4 Templates section and of sharing a profile (catalog §6.1, R-26).
                "aiPrivacy4",
                "aiUse",
                "aiUseD",
                "alreadyAdded",
                "alwaysOn",
                "appGone",
                "appName",
                "autoBackupD",
                "backToProfile",
                // Texts of the M2 integration: failures, undo labels and notices of the five packages (catalog §6.1).
                "autoReleaseNever",
                "autoReleaseSwitchOff",
                "autoReleaseSwitchOn",
                "autoReleaseTimed",
                "backupDamaged",
                "backupFailed",
                "backupNone",
                "bakAuto",
                "bakManual",
                "bakMeta",
                "bakPreChange",
                "bakPreMigrate",
                "bakPreUpdate",
                "blockedAltLock",
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
                "changeKeys",
                "coachStep",
                "columnsN",
                "confirmRun",
                "confirmRunD",
                "consentD4",
                "consentGive",
                "consentGiven",
                "consentNotGiven",
                "consentRevoke",
                "contactPending",
                "crashRecoveryD",
                "createWithN",
                "creatorInitials",
                "creatorName",
                "creatorRole",
                "dangerMark",
                "dataUnreadable",
                "delA",
                "dockNotices",
                "dockPageOf",
                "dockScrollDown",
                "dockScrollUp",
                "dupReviewN",
                "dupSummaryN",
                "elevatedRefused",
                "engineFault",
                "errInvD",
                "errInvT",
                "errKeyD",
                "errKeyT",
                "errNoKeyD",
                "errNoKeyT",
                "errUnavD",
                "errUnavT",
                "exitApp",
                "exportDone",
                "exportFailed",
                "fbBodyLog",
                "fbBodySys",
                "fbLogFile",
                "fbMailCopied",
                "fbSendLogD",
                "fbSubject",
                "freqEmptyS",
                "freqEmptyT",
                "generalFixed",
                "gitHub",
                "globalHotkeyD",
                "globalHotkeyT",
                "globalHotkeyTaken",
                "guardianUnstable",
                // Texts of the M4 Control Center: «General y panel» and «Precisión táctil» (catalog §6.1, R-27).
                "handleDown",
                "handleLeft",
                "handleLock",
                "handleLockD",
                "handleRight",
                "handleUp",
                "hideA",
                "hidePanel",
                "holdAsGeneral",
                "holdMinutes",
                "holdSeconds",
                "holdingKeys",
                "impSummary",
                "impTextsLost",
                "importInvalid",
                "importTooLarge",
                "incompleteTap",
                "installSelN",
                "installedApp",
                "itemGone",
                "kbAppsEn",
                "kbAppsEs",
                "kbEnInt",
                "kbEnUs",
                "kbEsEs",
                "kbEsLa",
                "kbForLine",
                "keyDelete",
                "keyDeleted",
                "keyMissing",
                "keyNone",
                "keyNotSaved",
                "keyPaste",
                "keySaved",
                "keysNameOnly",
                "keysWith",
                "kgFn",
                "kgMods",
                "kitBasics",
                "kitBasicsD",
                "langBoth",
                "lastBackupAt",
                "lastCheckAt",
                "latchedName",
                "launchUnsafe",
                "lessOf",
                "linkedToApp",
                "logPvEmpty",
                "logPvFailed",
                "macroCancelled",
                "macroRanName",
                "macroRunning",
                "macroStep",
                "moreOf",
                "mouseRan",
                "msValue",
                "noOpenApps",
                "notCheckedYet",
                "obKbLine",
                "obStep",
                "opLess",
                "opMore",
                "openedName",
                "opensWithApps",
                "orderHintDrag",
                "pauseApp",
                "pinFolds",
                "pinLimit",
                "positionOf",
                "processTaken",
                "profAutoOn",
                "profLockedOn",
                "profShared",
                "programsInstalled",
                "pxValue",
                "recordedKeys",
                "releasedKeys",
                "releasedOnLock",
                "removeKeyN",
                "reopenAdmin",
                "reopenAdminD",
                "reopenBtn",
                "resumeApp",
                "riskyMark",
                "rollingBack",
                "saveFailD",
                "saveFailT",
                "saveReadOnly",
                "schemaNewer",
                "searchDenied",
                "searchDictate",
                "searchNoReturn",
                "secA11yData",
                "secAi",
                "seeCoach",
                "seeCoachD",
                "settingInvalid",
                "settingOption",
                "shareWithTexts",
                "sharedTextsExcluded",
                "shortcutsN",
                "showTabsNote",
                "startFailed",
                "startNotInstalled",
                "stepDeleted",
                "stepPress",
                "stepType",
                "stepWait",
                "stepsN",
                "suggestionDot",
                "szL",
                "szM",
                "szS",
                "tMoved",
                "takeOverYes",
                "tapSent",
                "testReset",
                "textBigger",
                "textSmaller",
                "textTyped",
                "textTypedPrivate",
                "textUnavailable",
                "timeMultiplierD",
                "timeMultiplierT",
                "timesN",
                "tmLeft",
                "trayHidden",
                "trayPaused",
                "uNokbD",
                "undoEditsIn",
                "unlatchedName",
                "updErrDamaged",
                "updErrOffline",
                "updErrSpace",
                "updErrStopped",
                // Texts of the M5 Sistema section: updates, backups and startup (catalog §6.1, R-25).
                "updErrT",
                "updUnavailD",
                "updUnavailT",
                "updateVariantEn",
                "updateVariantEs",
                "variantUpdatedN",
                "voiceNumsT",
                "voiceSay",
                "waitLonger",
                "waitShorter",
                "welcomeTitle",
                "whenDate",
                "whenToday",
                "whenYesterday",
            ]);
        entries
            .Where(static e => I18nData.Category(e.Key) is not null)
            .Select(static e => I18nData.BaseKey(e.Key))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ShouldBe([
                "addMissing",
                "addedCountToProf",
                "bakMeta",
                "columnsN",
                "comboN",
                "createWithN",
                "dupHead",
                "dupReviewN",
                "dupSummaryN",
                "impSummary",
                "impTextsLost",
                "instNoteSome",
                "installSelN",
                "pinLimit",
                "sharedTextsExcluded",
                "shortcutsN",
                "stepsN",
                "sugLine",
                "tmLeft",
                "twMacro",
                "variantUpdatedN",
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
