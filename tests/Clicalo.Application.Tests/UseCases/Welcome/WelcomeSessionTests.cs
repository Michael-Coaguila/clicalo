using Clicalo.Application.UseCases.Welcome;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Templates;
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
    public void A_repeated_welcome_changes_the_settings_only_when_the_answers_change()
    {
        var first = WelcomeTestData.Store(WelcomeTestData.FirstStart());
        var firstSession = new WelcomeSession(first, WelcomeTestData.Content, repeat: false);
        firstSession.Next();
        firstSession.ToggleUse(WelcomeUse.Tremor);
        Finish(firstSession);
        var store = WelcomeTestData.Store(
            first.Current with
            {
                Settings = first.Current.Settings with { Size = PanelSize.Medium },
            }
        );
        var session = new WelcomeSession(store, WelcomeTestData.Content, repeat: true);
        session.Uses.ShouldBe([WelcomeUse.Tremor]);

        session.Next();
        session.Next();

        store.Current.Settings.Size.ShouldBe(PanelSize.Medium, "the same answers change nothing");
        session.Back();
        session.ToggleUse(WelcomeUse.Voice);
        session.Next();
        store.Current.Settings.VoiceNumbers.ShouldBeTrue();
        store.Current.Settings.Size.ShouldBe(PanelSize.Large);
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
