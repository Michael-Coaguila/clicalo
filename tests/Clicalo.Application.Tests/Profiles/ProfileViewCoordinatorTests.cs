using Clicalo.Application.Profiles;
using Clicalo.Application.Tests.Store;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Document;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.ProfileResolution;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Tests.Generators;

namespace Clicalo.Application.Tests.Profiles;

/// <summary>
/// The profile the panel shows as the active app, the ★ button, the selector and Auto/Fixed change (PER-001 to PER-008,
/// docs/03 §2): the coordinator applies the domain rules, raises the page reset and the notice, and keeps Auto/Fixed and
/// the last profile in the settings. Sample: General; Word (winword.exe); Navegador (chrome.exe).
/// </summary>
public sealed class ProfileViewCoordinatorTests
{
    private static readonly ProfileId Word = new("word");
    private static readonly ProfileId Browser = new("browser");
    private static readonly ProcessName WinWord = new("winword.exe");
    private static readonly ProcessName Chrome = new("chrome.exe");
    private static readonly ProcessName TaskManager = new("taskmgr.exe");

    private readonly StoreHarness _harness = new(Document());
    private readonly List<ProfileViewChangedEventArgs> _changes = [];
    private readonly ProfileViewCoordinator _coordinator;

    public ProfileViewCoordinatorTests()
    {
        _coordinator = new ProfileViewCoordinator(_harness.Store);
        _coordinator.Changed += (_, change) => _changes.Add(change);
    }

    [Fact]
    [Trait("Req", "PER-001")]
    public void It_starts_on_the_last_profile_with_the_saved_Auto_or_Fixed()
    {
        var document = Document() with
        {
            Settings = SettingsSchema.Defaults with { LockProfile = true, LastProfile = Browser },
        };

        var coordinator = new ProfileViewCoordinator(new StoreHarness(document).Store);

        coordinator.State.ShouldBe(
            new ProfileState(new ViewTarget.Profile(Browser), true, Browser)
        );
        coordinator.ActiveApp.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "PER-003")]
    public void In_Auto_the_view_follows_the_app_and_returns_to_page_1()
    {
        _coordinator.OnActiveApp(WinWord).ShouldBeTrue();
        _coordinator.State.View.ShouldBe(new ViewTarget.Profile(Word));
        _coordinator.OnActiveApp(TaskManager);

        _coordinator.State.View.ShouldBe(new ViewTarget.Profile(ProfileId.General));
        var last = _changes[^1];
        last.ResetPage.ShouldBeTrue();
        last.AppChanged.ShouldBeTrue();
        last.Notice.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "PER-005")]
    public void A_manual_choice_lasts_until_the_app_really_changes()
    {
        _coordinator.OnActiveApp(WinWord);
        _coordinator.Choose(Browser);
        _changes.Clear();

        // The monitor reports Word again after the tray menu or the Control Center: not a change.
        _coordinator.OnActiveApp(new ProcessName("WINWORD.EXE")).ShouldBeFalse();

        _coordinator.State.View.ShouldBe(new ViewTarget.Profile(Browser));
        _changes.ShouldBeEmpty();
        _harness.Store.Current.Settings.LastProfile.ShouldBe(Browser);
    }

    [Fact]
    [Trait("Req", "PER-003")]
    [Trait("Req", "FRE-003")]
    public void Frequents_and_Fixed_never_change_by_themselves_but_the_app_change_is_still_reported()
    {
        _coordinator.ShowFrequents();
        _coordinator.OnActiveApp(Chrome);

        _coordinator.State.View.ShouldBe(new ViewTarget.Frequents());
        var change = _changes[^1];
        change.AppChanged.ShouldBeTrue();
        change.ResetPage.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "PER-006")]
    [Trait("Req", "CAB-003")]
    public void Auto_to_Fixed_fixes_the_profile_shown_saves_it_and_says_so()
    {
        _coordinator.OnActiveApp(WinWord);

        _coordinator.ToggleLock();
        _coordinator.OnActiveApp(Chrome);

        _coordinator.State.View.ShouldBe(new ViewTarget.Profile(Word));
        _harness.Store.Current.Settings.LockProfile.ShouldBeTrue();
        _harness.Store.Current.Settings.LastProfile.ShouldBe(Word);
        _changes
            .Select(static c => c.Notice)
            .OfType<ProfileNotice.Locked>()
            .ShouldHaveSingleItem()
            .Profile.ShouldBe(Word);
    }

    [Fact]
    [Trait("Req", "PER-006")]
    public void Fixed_to_Auto_jumps_to_the_profile_of_the_active_app_on_page_1()
    {
        _coordinator.OnActiveApp(WinWord);
        _coordinator.ToggleLock();
        _coordinator.OnActiveApp(Chrome);
        _changes.Clear();

        _coordinator.ToggleLock();

        _coordinator.State.View.ShouldBe(new ViewTarget.Profile(Browser));
        _harness.Store.Current.Settings.LockProfile.ShouldBeFalse();
        var change = _changes.ShouldHaveSingleItem();
        change.ResetPage.ShouldBeTrue();
        change.Notice.ShouldBe(new ProfileNotice.FollowingApp(Browser));
    }

    [Fact]
    [Trait("Req", "PER-004")]
    [Trait("Req", "SEL-002")]
    public void The_profile_button_in_Frequents_returns_to_the_return_profile()
    {
        _coordinator.OnActiveApp(Chrome);
        _coordinator.ShowFrequents();
        _coordinator.ProfileButtonTarget.ShouldBe(Browser);

        _coordinator.ReturnFromFrequents().ShouldBeTrue();

        _coordinator.State.View.ShouldBe(new ViewTarget.Profile(Browser));
        _coordinator.ReturnFromFrequents().ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "PER-008")]
    public void Deleting_the_profile_shown_goes_to_General_and_stays_Fixed()
    {
        _coordinator.Choose(Browser);
        _coordinator.ToggleLock();

        _harness.Dispatch(new DeleteProfile(Browser)).IsSuccess.ShouldBeTrue();
        _coordinator.OnDocumentChanged();

        _coordinator.State.ShouldBe(
            new ProfileState(new ViewTarget.Profile(ProfileId.General), true, ProfileId.General)
        );
        _changes[^1].ResetPage.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "CAB-002")]
    public void The_profile_of_the_active_app_is_known_only_when_it_has_one()
    {
        _coordinator.OnActiveApp(WinWord);
        _coordinator.ActiveAppProfile.ShouldBe(Word);

        _coordinator.OnActiveApp(TaskManager);
        _coordinator.ActiveAppProfile.ShouldBeNull();
    }

    private static UserDocument Document() =>
        UserDocument.Create(
            ShortcutLibrary
                .CreateValidated(
                    [],
                    [
                        DomainGen.General(
                            StoreSamples.Tap("undo", "Deshacer", KeyIds.Ctrl, KeyIds.Z)
                        ),
                        Profile(Word, "Word", WinWord),
                        Profile(Browser, "Navegador", Chrome),
                    ]
                )
                .Value,
            SettingsSchema.Defaults
        );

    private static Profile Profile(ProfileId id, string name, ProcessName process) =>
        new(
            id,
            LocalizedText.Same(name, LangCode.Es, LangCode.En),
            new IconRef("description"),
            true,
            new AppBinding.Processes([process]),
            InjectionMode.VirtualKey,
            [StoreSamples.Tap(id.Value + "-a", name, KeyIds.Ctrl, KeyIds.A)],
            null
        );
}
