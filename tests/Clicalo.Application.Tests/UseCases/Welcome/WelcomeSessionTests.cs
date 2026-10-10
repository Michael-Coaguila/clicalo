using Clicalo.Application.UseCases.Welcome;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Document;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Templates;
using Clicalo.Domain.Tests.Generators;
using Microsoft.Extensions.Time.Testing;
using PanelSize = Clicalo.Domain.Settings.PanelSize;

namespace Clicalo.Application.Tests.UseCases.Welcome;

/// <summary>
/// The welcome (docs/06): its steps, what each one applies and when, and what [Empezar] and [Omitir] install
/// (BIE-003, BIE-006, BIE-009) on a first start and when it is repeated from General (BIE-010).
/// </summary>
public sealed class WelcomeSessionTests
{
    private static readonly ProcessName Chrome = new("chrome.exe");
    private static readonly ProcessName Edge = new("msedge.exe");

    [Fact]
    [Trait("Req", "BIE-003")]
    [Trait("Req", "BIE-005")]
    [Trait("Req", "BIE-006")]
    public void A_first_welcome_starts_on_step_0_with_nothing_marked_but_basics()
    {
        var session = new WelcomeSession(
            WelcomeTestData.Store(WelcomeTestData.FirstStart()),
            WelcomeTestData.Content,
            repeat: false
        );

        session.Step.ShouldBe(0);
        session.Uses.ShouldBeEmpty();
        session.Kit.Chosen.ShouldBe(["basics"]);
        session.Back();
        session.Step.ShouldBe(0, "there is no step before the first");
    }

    [Fact]
    [Trait("Req", "BIE-004")]
    [Trait("Req", "BIE-007")]
    [Trait("Req", "BIE-008")]
    public void Language_view_size_and_theme_apply_at_once()
    {
        var store = WelcomeTestData.Store(WelcomeTestData.FirstStart());
        var session = new WelcomeSession(store, WelcomeTestData.Content, repeat: false);

        session.SetLanguage(LangCode.En);
        session.SetDensity(PanelDensity.Dock);
        session.SetSize(PanelSize.Small);
        session.SetTheme(ThemeChoice.HighContrast);

        var settings = store.Current.Settings;
        settings.Language.ShouldBe(LangCode.En);
        settings.Density.ShouldBe(PanelDensity.Dock);
        settings.Size.ShouldBe(PanelSize.Small);
        settings.Theme.ShouldBe(ThemeChoice.HighContrast);
        store.CanUndo.ShouldBeFalse("presentation settings do not enter the undo history");
    }

    [Fact]
    [Trait("Req", "BIE-005")]
    public void The_answers_apply_when_leaving_step_1_and_going_back_recalculates_them()
    {
        var store = WelcomeTestData.Store(WelcomeTestData.FirstStart());
        var session = new WelcomeSession(store, WelcomeTestData.Content, repeat: false);
        session.Next();
        session.ToggleUse(WelcomeUse.Tremor);
        session.ToggleUse(WelcomeUse.Voice);
        store.Current.Settings.VoiceNumbers.ShouldBeFalse(
            "nothing applies before leaving the step"
        );

        session.Next();

        session.Step.ShouldBe(2);
        store.Current.Settings.Touch.Preset.ShouldBe(TouchPresets.StrongTremor.Id);
        store.Current.Settings.Size.ShouldBe(PanelSize.Large);
        store.Current.Settings.VoiceNumbers.ShouldBeTrue();

        session.Back();
        session.ToggleUse(WelcomeUse.Tremor);
        session.ToggleUse(WelcomeUse.NoKeyboard);
        session.Next();

        store.Current.Settings.Touch.Preset.ShouldBe(TouchPresets.MildTremor.Id);
        store.Current.Settings.NoKeyboardUser.ShouldBeTrue();
        store.Current.Settings.Size.ShouldBe(
            SettingsSchema.Defaults.Size,
            "without tremor the size the welcome had made L goes back (EC-BIE-01)"
        );
        store.Undo().IsSuccess.ShouldBeTrue();
        store.CanUndo.ShouldBeFalse("both passes are one undo step");
        store.Current.Settings.NoKeyboardUser.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "BIE-006")]
    [Trait("Req", "BIE-009")]
    public void Finishing_without_touching_anything_leaves_basics_only()
    {
        var store = WelcomeTestData.Store(WelcomeTestData.FirstStart());
        var session = new WelcomeSession(store, WelcomeTestData.Content, repeat: false);
        WelcomeEnd? end = null;
        session.Ended += (_, e) => end = e.End;

        Finish(session);

        end.ShouldBe(WelcomeEnd.Finished);
        session.HasEnded.ShouldBeTrue();
        var document = store.Current;
        document.Onboarding.Completed.ShouldBeTrue();
        document.Library.AlwaysVisible.Count.ShouldBe(2);
        document.Library.Profiles.ShouldHaveSingleItem().Shortcuts.Count.ShouldBe(1);
        document.Validate().ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "BIE-006")]
    public void Everything_unmarked_leaves_general_and_always_visible_empty()
    {
        var store = WelcomeTestData.Store(WelcomeTestData.FirstStart());
        var session = new WelcomeSession(store, WelcomeTestData.Content, repeat: false);
        session.ToggleKit("basics");

        Finish(session);

        store.Current.Library.AlwaysVisible.ShouldBeEmpty();
        store.Current.Library.Profiles.ShouldHaveSingleItem().Shortcuts.ShouldBeEmpty();
        store.Current.Onboarding.Completed.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "BIE-006")]
    [Trait("Req", "DAT-006")]
    public void A_marked_template_is_one_profile_for_all_its_programs_in_one_undo_step()
    {
        var store = WelcomeTestData.Store(WelcomeTestData.FirstStart());
        var session = new WelcomeSession(store, WelcomeTestData.Content, repeat: false);
        session.ToggleKit("browser");

        Finish(session);

        var library = store.Current.Library;
        library.Profiles.Count.ShouldBe(2);
        library.ProfileFor(Chrome).ShouldNotBeNull().Id.ShouldBe(library.ProfileFor(Edge)!.Id);
        store.Undo().IsSuccess.ShouldBeTrue();
        store.Current.Library.Profiles.Count.ShouldBe(1, "the install is a single step");
        store.Current.Library.AlwaysVisible.ShouldBeEmpty();
        store.Current.Onboarding.Completed.ShouldBeTrue("finishing the welcome is not undone");
    }

    [Fact]
    [Trait("Req", "BIE-003")]
    public void Skipping_keeps_what_was_applied_and_installs_basics_only()
    {
        var store = WelcomeTestData.Store(WelcomeTestData.FirstStart());
        var session = new WelcomeSession(store, WelcomeTestData.Content, repeat: false);
        WelcomeEnd? end = null;
        session.Ended += (_, e) => end = e.End;
        session.SetLanguage(LangCode.En);
        session.Next();
        session.ToggleUse(WelcomeUse.Voice);
        session.Next();
        session.ToggleKit("word");

        session.Skip();

        end.ShouldBe(WelcomeEnd.Skipped);
        var document = store.Current;
        document.Settings.Language.ShouldBe(LangCode.En);
        document.Settings.VoiceNumbers.ShouldBeTrue();
        document.Library.Profiles.ShouldHaveSingleItem();
        document.Library.AlwaysVisible.Count.ShouldBe(2);
        document.Onboarding.Completed.ShouldBeTrue();
        session.Next();
        store.Current.ShouldBeSameAs(document, "an ended welcome does nothing more");
    }

    [Fact]
    [Trait("Req", "BIE-010")]
    public void A_repeated_welcome_shows_what_is_installed_and_never_uninstalls()
    {
        var first = WelcomeTestData.Store(WelcomeTestData.FirstStart());
        var firstSession = new WelcomeSession(first, WelcomeTestData.Content, repeat: false);
        firstSession.ToggleKit("word");
        Finish(firstSession);
        var store = WelcomeTestData.Store(first.Current);

        var session = new WelcomeSession(store, WelcomeTestData.Content, repeat: true);

        session.Installed.Chosen.ShouldBe(["basics", "word"], ignoreOrder: true);
        session.Kit.Chosen.ShouldBe(["basics", "word"], ignoreOrder: true);
        session.ToggleKit("basics");
        session.ToggleKit("word");
        session.ToggleKit("browser");
        Finish(session);
        var library = store.Current.Library;
        library.Profiles.Count.ShouldBe(3, "Word stays and Navegador is added");
        library.AlwaysVisible.Count.ShouldBe(2, "basics stays");
        library.General.Shortcuts.Count.ShouldBe(1);
    }

    [Fact]
    [Trait("Req", "BIE-010")]
    public void A_repeated_welcome_installs_basics_again_without_repeating_them()
    {
        var first = WelcomeTestData.Store(WelcomeTestData.FirstStart());
        Finish(new WelcomeSession(first, WelcomeTestData.Content, repeat: false));
        var store = WelcomeTestData.Store(first.Current);
        var session = new WelcomeSession(store, WelcomeTestData.Content, repeat: true);

        Finish(session);

        store.Current.Library.AlwaysVisible.Count.ShouldBe(2);
        store.Current.Library.General.Shortcuts.Count.ShouldBe(1);
        store.CanUndo.ShouldBeFalse("nothing was missing, so nothing changed");
    }

    [Fact]
    [Trait("Req", "BIE-010")]
    public void A_repeated_welcome_starts_from_the_recorded_answers_and_keeps_what_was_changed_by_hand()
    {
        var first = WelcomeTestData.Store(WelcomeTestData.FirstStart());
        var firstSession = new WelcomeSession(first, WelcomeTestData.Content, repeat: false);
        firstSession.Next();
        firstSession.ToggleUse(WelcomeUse.Tremor);
        firstSession.ToggleUse(WelcomeUse.Touch);
        Finish(firstSession);
        var recorded = first.Current.Onboarding.Answers.ShouldNotBeNull();
        recorded.Uses.ShouldBe([WelcomeAnswer.Touch, WelcomeAnswer.Tremor]);
        recorded.Kit.ShouldBe(["basics"]);
        recorded.Baseline.ShouldBe(
            new WelcomeBaseline(TouchPresets.StrongTremor.Id, PanelSize.Large, false, false)
        );

        // Afterwards the person makes the panel M by hand.
        var store = WelcomeTestData.Store(
            first.Current with
            {
                Settings = first.Current.Settings with { Size = PanelSize.Medium },
            }
        );
        var session = new WelcomeSession(store, WelcomeTestData.Content, repeat: true);
        session.Uses.ShouldBe(
            [WelcomeUse.Touch, WelcomeUse.Tremor],
            ignoreOrder: true,
            "touch leaves no trace in the settings: it comes from the recorded answers"
        );

        session.Next();
        session.PendingChanges.ShouldBeNull("the same answers change nothing");
        session.Next();
        store.Current.Settings.Size.ShouldBe(PanelSize.Medium);

        session.Back();
        session.ToggleUse(WelcomeUse.Voice);
        var plan = session.PendingChanges.ShouldNotBeNull();
        plan.Changes.ShouldBe([WelcomeSetting.VoiceNumbers]);
        plan.Kept.ShouldBe([WelcomeSetting.Size], "the size was changed by hand");
        session.Next();

        store.Current.Settings.VoiceNumbers.ShouldBeTrue();
        store.Current.Settings.Size.ShouldBe(PanelSize.Medium, "what was changed by hand stays");
        Finish(session);
        var again = store.Current.Onboarding.Answers.ShouldNotBeNull();
        again.Uses.ShouldBe([WelcomeAnswer.Touch, WelcomeAnswer.Voice, WelcomeAnswer.Tremor]);
        again.Baseline.Size.ShouldBe(
            PanelSize.Large,
            "the baseline keeps what the welcome set, so the hand change is still recognized"
        );
        again.Baseline.VoiceNumbers.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "BIE-010")]
    [Trait("Req", "EC-BIE-01")]
    public void Going_back_to_the_answers_it_opened_with_recalculates_the_effects_too()
    {
        var first = WelcomeTestData.Store(WelcomeTestData.FirstStart());
        var firstSession = new WelcomeSession(first, WelcomeTestData.Content, repeat: false);
        firstSession.Next();
        firstSession.ToggleUse(WelcomeUse.Voice);
        Finish(firstSession);
        var store = WelcomeTestData.Store(first.Current);
        var session = new WelcomeSession(store, WelcomeTestData.Content, repeat: true);
        session.Next();

        session.ToggleUse(WelcomeUse.Voice);
        session.Next();
        store.Current.Settings.VoiceNumbers.ShouldBeFalse();
        session.Back();
        session.ToggleUse(WelcomeUse.Voice);
        session.Next();

        store.Current.Settings.VoiceNumbers.ShouldBeTrue(
            "the answers it opened with apply again after another pass changed them"
        );
    }

    [Fact]
    [Trait("Req", "BIE-010")]
    public void A_document_without_recorded_answers_shows_what_its_settings_reflect()
    {
        // A document written before schema 1.1: the welcome finished, no answers.
        var old = WelcomeTestData.FirstStart() with
        {
            Onboarding = new OnboardingState(true),
            Settings = WelcomeEffects.Apply(
                SettingsSchema.Defaults,
                new HashSet<WelcomeUse> { WelcomeUse.Voice }
            ),
        };
        var store = WelcomeTestData.Store(old);
        var session = new WelcomeSession(store, WelcomeTestData.Content, repeat: true);

        session.Uses.ShouldBe([WelcomeUse.Voice]);
        session.Skip();

        var answers = store.Current.Onboarding.Answers.ShouldNotBeNull();
        answers.Uses.ShouldBe([WelcomeAnswer.Voice]);
        answers.Baseline.ShouldBe(WelcomeBaseline.Of(store.Current.Settings));
    }

    [Fact]
    [Trait("Req", "BIE-003")]
    [Trait("Req", "BIE-010")]
    public void Skipping_records_the_default_kit_and_the_settings_as_they_are()
    {
        var store = WelcomeTestData.Store(WelcomeTestData.FirstStart());
        var session = new WelcomeSession(store, WelcomeTestData.Content, repeat: false);

        session.Skip();

        var answers = store.Current.Onboarding.Answers.ShouldNotBeNull();
        answers.Uses.ShouldBeEmpty();
        answers.Kit.ShouldBe(["basics"]);
        answers.Baseline.ShouldBe(WelcomeBaseline.Of(store.Current.Settings));
    }

    [Fact]
    [Trait("Req", "NFR-010")]
    [Trait("Req", "REG-04")]
    public void A_reinstallation_keeps_the_data_unless_starting_from_scratch_is_tapped_twice()
    {
        var first = WelcomeTestData.Store(WelcomeTestData.FirstStart());
        var firstSession = new WelcomeSession(first, WelcomeTestData.Content, repeat: false);
        firstSession.ToggleKit("word");
        firstSession.Next();
        firstSession.ToggleUse(WelcomeUse.Tremor);
        Finish(firstSession);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 9, 10, 0, 0, TimeSpan.Zero));
        var fresh = WelcomeFreshStart.Of(() => WelcomeTestData.Content, new SequentialIds(), time);

        // Kept: the default. Finishing changes nothing of what was there.
        var kept = WelcomeTestData.Store(first.Current);
        var keeping = new WelcomeSession(kept, WelcomeTestData.Content, repeat: true, fresh);
        keeping.OffersFreshStart.ShouldBeTrue();
        keeping.StartFromScratch();
        keeping.FreshStartArmedUntil.ShouldNotBeNull("the first tap only arms");
        kept.Current.ShouldBeSameAs(first.Current);
        time.Advance(TimeSpan.FromSeconds(10));
        keeping.FreshStartArmedUntil.ShouldBeNull("the window passed");
        Finish(keeping);
        kept.Current.Library.Profiles.Count.ShouldBe(2, "General and Word are still there");

        // From scratch: two taps, the document of a new installation, and the welcome goes on as a first one.
        var store = WelcomeTestData.Store(first.Current);
        var session = new WelcomeSession(store, WelcomeTestData.Content, repeat: true, fresh);
        session.SetLanguage(LangCode.En);
        session.StartFromScratch();
        session.StartFromScratch();

        session.StartedFresh.ShouldBeTrue();
        session.OffersFreshStart.ShouldBeFalse();
        session.Repeat.ShouldBeFalse();
        session.Uses.ShouldBeEmpty();
        session.Kit.Chosen.ShouldBe(["basics"]);
        var empty = store.Current;
        empty.Onboarding.Completed.ShouldBeFalse();
        empty.Library.AlwaysVisible.ShouldBeEmpty();
        empty.Library.Profiles.ShouldHaveSingleItem().Shortcuts.ShouldBeEmpty();
        empty.Settings.Language.ShouldBe(LangCode.En, "the language in use is kept");
        empty.Settings.Size.ShouldBe(SettingsSchema.Defaults.Size);
        empty.Validate().ShouldBeEmpty();
        store.CanUndo.ShouldBeTrue("starting from scratch can be undone");
        Finish(session);
        store.Current.Onboarding.Completed.ShouldBeTrue();
        store.Current.Library.AlwaysVisible.Count.ShouldBe(2, "Basics, as on a first start");
    }

    [Theory]
    [Trait("Req", "NFR-010")]
    [InlineData(true, false, true, true)]
    [InlineData(false, false, true, false)]
    [InlineData(true, true, true, false)]
    [InlineData(true, false, false, false)]
    public void Only_the_first_start_of_an_installation_that_found_finished_data_asks(
        bool firstRunAfterInstall,
        bool newData,
        bool welcomed,
        bool asks
    )
    {
        var document = WelcomeTestData.FirstStart() with
        {
            Onboarding = new OnboardingState(welcomed),
        };

        WelcomeFreshStart.ShouldAsk(firstRunAfterInstall, newData, document).ShouldBe(asks);
    }

    [Fact]
    [Trait("Req", "BIE-003")]
    public void Skipping_a_repeated_welcome_installs_nothing()
    {
        var store = WelcomeTestData.Store(WelcomeTestData.FirstStart());
        var session = new WelcomeSession(store, WelcomeTestData.Content, repeat: true);

        session.Skip();

        store.Current.Library.AlwaysVisible.ShouldBeEmpty();
        store.Current.Onboarding.Completed.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "BIE-006")]
    public void Without_content_the_welcome_still_finishes()
    {
        var store = WelcomeTestData.Store(WelcomeTestData.FirstStart());
        var session = new WelcomeSession(store, null, repeat: false);

        session.Kit.ShouldBe(StarterSelection.Empty);
        Finish(session);

        store.Current.Onboarding.Completed.ShouldBeTrue();
        store.Current.Library.AlwaysVisible.ShouldBeEmpty();
    }

    private static void Finish(WelcomeSession session)
    {
        while (!session.HasEnded)
        {
            var step = session.Step;
            session.Next();
            if (!session.HasEnded)
            {
                session.Step.ShouldBe(step + 1);
            }
        }
    }
}
