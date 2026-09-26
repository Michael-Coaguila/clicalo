using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.ProfileResolution;
using static Clicalo.Domain.Tests.Library.LibraryBuilder;

namespace Clicalo.Domain.Tests.ProfileResolution;

/// <summary>
/// The profile the panel shows, as the full table of view × Auto/Fixed × active app (PER-001 to PER-008, docs/03 §2).
/// Sample library: General; Word (winword.exe); Navegador (chrome.exe).
/// </summary>
public sealed class ProfileResolverTests
{
    private static readonly ShortcutLibrary Library = Sample();
    private static readonly ProfileId Word = new("word");
    private static readonly ProfileId Chrome = new("chrome");
    private static readonly ProfileId General = ProfileId.General;
    private static readonly ViewTarget Frequents = new ViewTarget.Frequents();

    public static TheoryData<string, bool, string?, string, bool> AppChanges =>
        new()
        {
            // view, fixed, new app → view after, page reset
            { "word", false, "chrome.exe", "chrome", true },
            { "word", false, "taskmgr.exe", "general", true },
            { "word", false, "WINWORD.EXE", "word", false },
            { "general", false, "winword.exe", "word", true },
            { "chrome", false, "", "general", true },
            { "word", true, "chrome.exe", "word", false },
            { "word", true, "taskmgr.exe", "word", false },
            { "freq", false, "chrome.exe", "freq", false },
            { "freq", true, "chrome.exe", "freq", false },
        };

    [Theory]
    [Trait("Req", "PER-003")]
    [Trait("Req", "PER-005")]
    [MemberData(nameof(AppChanges))]
    public void An_app_change_moves_the_view_only_in_Auto_and_outside_Frequents(
        string view,
        bool locked,
        string? app,
        string expected,
        bool resetPage
    )
    {
        var state = new ProfileState(View(view), locked, LastProfile: null);

        var transition = ProfileResolver.OnAppChanged(
            state,
            Library,
            new ProcessName(app ?? string.Empty)
        );

        transition.State.View.ShouldBe(View(expected));
        transition.State.LockProfile.ShouldBe(locked);
        transition.ResetPage.ShouldBe(resetPage);
        transition.Notice.ShouldBeNull();
    }

    public static TheoryData<bool, string?, string?, string> ReturnProfiles =>
        new()
        {
            // fixed, last profile, active app → return profile
            { false, "chrome", "winword.exe", "word" },
            { false, "chrome", "taskmgr.exe", "chrome" },
            { false, "ghost", "taskmgr.exe", "general" },
            { false, null, null, "general" },
            { true, "chrome", "winword.exe", "chrome" },
            { true, "ghost", "winword.exe", "word" },
            { true, null, "taskmgr.exe", "general" },
        };

    [Theory]
    [Trait("Req", "PER-004")]
    [MemberData(nameof(ReturnProfiles))]
    public void The_return_profile_follows_its_four_rules_in_order(
        bool locked,
        string? last,
        string? app,
        string expected
    )
    {
        var state = new ProfileState(Frequents, locked, last is null ? null : new ProfileId(last));
        var process = app is null ? (ProcessName?)null : new ProcessName(app);

        ProfileResolver.ReturnProfile(state, Library, process).ShouldBe(new ProfileId(expected));

        var back = ProfileResolver.ReturnFromFrequents(state, Library, process);
        back.State.View.ShouldBe(View(expected));
        back.ResetPage.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "PER-001")]
    public void Frequents_is_a_view_of_its_own_and_choosing_a_profile_remembers_it()
    {
        var state = new ProfileState(View("word"), LockProfile: false, LastProfile: null);

        var frequents = ProfileResolver.ShowFrequents(state);
        frequents.State.View.ShouldBe(Frequents);
        frequents.ResetPage.ShouldBeTrue();
        ProfileResolver.ShowFrequents(frequents.State).ResetPage.ShouldBeFalse();

        var chosen = ProfileResolver.Choose(frequents.State, Library, Chrome);
        chosen.State.ShouldBe(new ProfileState(View("chrome"), false, Chrome));
        ProfileResolver
            .Choose(chosen.State, Library, new ProfileId("ghost"))
            .State.ShouldBe(chosen.State);
    }

    [Fact]
    [Trait("Req", "PER-006")]
    public void Auto_to_Fixed_fixes_the_shown_profile()
    {
        var state = new ProfileState(View("chrome"), LockProfile: false, LastProfile: Word);

        var transition = ProfileResolver.ToggleLock(state, Library, new ProcessName("winword.exe"));

        transition.State.ShouldBe(new ProfileState(View("chrome"), true, Chrome));
        transition.Notice.ShouldBe(new ProfileNotice.Locked(Chrome));
        transition.ResetPage.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "PER-006")]
    public void Auto_to_Fixed_in_Frequents_saves_the_return_profile_and_stays()
    {
        var state = new ProfileState(Frequents, LockProfile: false, LastProfile: Chrome);

        var transition = ProfileResolver.ToggleLock(state, Library, new ProcessName("winword.exe"));

        transition.State.ShouldBe(new ProfileState(Frequents, true, Word));
        transition.Notice.ShouldBe(new ProfileNotice.Locked(Word));
    }

    [Fact]
    [Trait("Req", "PER-006")]
    public void Fixed_to_Auto_jumps_to_the_active_app_on_page_one()
    {
        var state = new ProfileState(View("word"), LockProfile: true, LastProfile: Word);

        var transition = ProfileResolver.ToggleLock(state, Library, new ProcessName("taskmgr.exe"));

        transition.State.ShouldBe(new ProfileState(View("general"), false, Word));
        transition.ResetPage.ShouldBeTrue();
        transition.Notice.ShouldBe(new ProfileNotice.FollowingApp(General));
        ProfileResolver
            .ToggleLock(
                state with
                {
                    View = View("chrome"),
                },
                Library,
                new ProcessName("chrome.exe")
            )
            .ResetPage.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "PER-006")]
    public void Fixed_to_Auto_in_Frequents_stays_in_Frequents()
    {
        var state = new ProfileState(Frequents, LockProfile: true, LastProfile: Word);

        var transition = ProfileResolver.ToggleLock(state, Library, new ProcessName("chrome.exe"));

        transition.State.ShouldBe(new ProfileState(Frequents, false, Word));
        transition.Notice.ShouldBe(new ProfileNotice.FollowingApp(Chrome));
    }

    [Theory]
    [Trait("Req", "PER-007")]
    [InlineData("word", false, "installed")]
    [InlineData("word", true, "word")]
    [InlineData("freq", false, "freq")]
    public void Installing_a_template_shows_it_only_in_Auto_outside_Frequents(
        string view,
        bool locked,
        string expected
    )
    {
        var state = new ProfileState(View(view), locked, LastProfile: null);

        ProfileResolver
            .OnTemplateInstalled(state, new ProfileId("installed"))
            .State.View.ShouldBe(View(expected));
    }

    [Fact]
    [Trait("Req", "PER-008")]
    public void Deleting_the_shown_or_last_profile_goes_to_General_and_keeps_Fixed()
    {
        var shown = new ProfileState(View("word"), LockProfile: true, LastProfile: Word);

        var transition = ProfileResolver.OnProfileRemoved(shown, Word);

        transition.State.ShouldBe(new ProfileState(View("general"), true, General));
        transition.ResetPage.ShouldBeTrue();

        var other = ProfileResolver.OnProfileRemoved(
            new ProfileState(Frequents, false, Chrome),
            Chrome
        );
        other.State.ShouldBe(new ProfileState(Frequents, false, General));
        ProfileResolver.OnProfileRemoved(shown, Chrome).State.ShouldBe(shown);
    }

    [Fact]
    [Trait("Req", "PER-008")]
    public void Reconciling_after_an_undo_or_a_restore_drops_missing_profiles()
    {
        var state = new ProfileState(
            View("ghost"),
            LockProfile: false,
            LastProfile: new ProfileId("gone")
        );

        ProfileResolver
            .Reconcile(state, Library)
            .State.ShouldBe(new ProfileState(View("general"), false, General));
        var valid = new ProfileState(View("word"), false, Chrome);
        ProfileResolver.Reconcile(valid, Library).State.ShouldBeSameAs(valid);
    }

    private static ViewTarget View(string value) =>
        string.Equals(value, "freq", StringComparison.Ordinal)
            ? new ViewTarget.Frequents()
            : new ViewTarget.Profile(new ProfileId(value));
}
