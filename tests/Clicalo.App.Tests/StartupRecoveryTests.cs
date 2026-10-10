using Clicalo.App.Lifecycle;
using Clicalo.Application.Ports;
using Clicalo.Application.Store;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Document;
using Clicalo.Domain.Duplicates;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;

namespace Clicalo.App.Tests;

/// <summary>
/// SIS-004 and DAT-003: a start that recovered the document says so, and the default document shown when nothing could
/// be read is only written once the person uses it.
/// </summary>
public sealed class StartupRecoveryTests
{
    [Theory]
    [Trait("Req", "SIS-004")]
    [Trait("Req", "DAT-003")]
    [InlineData(DocumentLoadOutcome.RecoveredFromPrevious)]
    [InlineData(DocumentLoadOutcome.RecoveredFromBackup)]
    public void A_recovered_document_is_announced(DocumentLoadOutcome outcome) =>
        StartupRecovery.NoticeFor(outcome, awaitingAcceptance: false).ShouldBe(L.DocRecovered);

    [Fact]
    [Trait("Req", "DAT-003")]
    public void Nothing_usable_asks_to_accept_the_new_panel_and_a_newer_major_says_read_only()
    {
        StartupRecovery
            .NoticeFor(DocumentLoadOutcome.DefaultInMemory, awaitingAcceptance: true)
            .ShouldBe(L.DataUnreadable);
        StartupRecovery
            .NoticeFor(DocumentLoadOutcome.FutureMajorReadOnly, awaitingAcceptance: false)
            .ShouldBe(L.SaveReadOnly);
    }

    [Theory]
    [Trait("Req", "SIS-004")]
    [InlineData(DocumentLoadOutcome.FirstRun)]
    [InlineData(DocumentLoadOutcome.Loaded)]
    [InlineData(DocumentLoadOutcome.EditedExternally)]
    [InlineData(DocumentLoadOutcome.Repaired)]
    [InlineData(DocumentLoadOutcome.RecoveredFromPending)]
    [InlineData(DocumentLoadOutcome.DefaultInMemory)]
    public void A_document_read_as_usual_says_nothing(DocumentLoadOutcome outcome) =>
        StartupRecovery.NoticeFor(outcome, awaitingAcceptance: false).ShouldBeNull();

    [Fact]
    [Trait("Req", "DAT-003")]
    public void Using_the_new_panel_accepts_it_and_what_changes_on_its_own_does_not()
    {
        var document = Document();
        var settings = document with
        {
            Settings = document.Settings with { VoiceNumbers = !document.Settings.VoiceNumbers },
        };
        var welcomed = document with { Onboarding = new OnboardingState(true) };
        var edited = document with
        {
            Library = ShortcutLibrary.CreateValidated([], [General("apps_outage")]).Value,
        };

        StartupRecovery.Accepts(Change(document, settings)).ShouldBeFalse();
        StartupRecovery.Accepts(Change(document, welcomed)).ShouldBeTrue("the welcome ended");
        StartupRecovery.Accepts(Change(document, edited)).ShouldBeTrue("the library changed");
    }

    private static DocumentChangedEventArgs Change(UserDocument before, UserDocument after) =>
        new(before, after, DocumentSlices.None, [], ChangeOrigin.Command);

    private static UserDocument Document() =>
        new(
            0,
            ShortcutLibrary.CreateValidated([], [General("apps")]).Value,
            FrequentsState.Empty,
            DuplicatePolicy.Empty,
            SettingsSchema.Defaults,
            new OnboardingState(false)
        );

    private static Profile General(string icon) =>
        new(
            ProfileId.General,
            new LocalizedText([]),
            new IconRef(icon),
            false,
            new AppBinding.Manual(),
            InjectionMode.VirtualKey,
            [],
            null
        );
}
