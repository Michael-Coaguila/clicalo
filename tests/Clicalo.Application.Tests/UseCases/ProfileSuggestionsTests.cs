using System.Collections.Immutable;
using Clicalo.Application.Tests.Store;
using Clicalo.Application.UseCases;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Document;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.ProfileResolution;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Templates;
using Clicalo.Domain.Tests.Generators;

namespace Clicalo.Application.Tests.UseCases;

/// <summary>
/// The profile suggestion of the panel (PER-009, EC-PER-02): Excel has a template and no profile; Chrome has a profile;
/// Paint has neither.
/// </summary>
[Trait("Req", "PER-009")]
public sealed class ProfileSuggestionsTests
{
    private static readonly ProcessName Excel = new("excel.exe");
    private static readonly ProcessName Chrome = new("chrome.exe");
    private static readonly ProcessName Paint = new("mspaint.exe");

    private static readonly StarterContent Content = new(
        new StarterKit(1, [new StarterOption("excel", StarterOptionKind.Template, false)]),
        new SeedContent(
            1,
            [],
            LocalizedText.Same("General", LangCode.Es, LangCode.En),
            new IconRef("apps"),
            []
        ),
        [
            new ProfileTemplate(
                "excel",
                3,
                new LocalizedText([
                    new(LangCode.Es, "Hoja Excel"),
                    new(LangCode.En, "Excel sheet"),
                ]),
                new IconRef("table"),
                [Excel, new ProcessName("excelonline.exe")],
                [
                    new TemplateShortcut(
                        "bold",
                        LocalizedText.Same("Negrita", LangCode.Es, LangCode.En),
                        new IconRef("format_bold"),
                        new CategoryId("fmt"),
                        new TapAction(
                            KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.N),
                            [
                                new ChordVariant(
                                    LangCode.En,
                                    KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.B)
                                ),
                            ]
                        ),
                        false
                    ),
                ]
            ),
        ]
    );

    private static readonly ProfileState Auto = new(
        new ViewTarget.Profile(ProfileId.General),
        LockProfile: false,
        LastProfile: null
    );

    private static ShortcutLibrary Library() =>
        ShortcutLibrary
            .CreateValidated(
                [],
                [
                    DomainGen.General(),
                    new Profile(
                        new ProfileId("chrome"),
                        LocalizedText.Same("Navegador", LangCode.Es, LangCode.En),
                        new IconRef("public"),
                        false,
                        new AppBinding.Processes([Chrome]),
                        InjectionMode.VirtualKey,
                        [],
                        null
                    ),
                ]
            )
            .Value;

    private static ProfileSuggestion? For(
        ProcessName app,
        bool autoSuggest = true,
        bool searching = false,
        StarterContent? content = null,
        params ProcessName[] dismissed
    ) =>
        ProfileSuggestions.For(
            app,
            Library(),
            content ?? Content,
            autoSuggest,
            [.. dismissed],
            searching
        );

    [Fact]
    public void An_app_without_a_profile_whose_process_a_template_lists_gets_the_card()
    {
        var suggestion = For(Excel).ShouldNotBeNull();

        suggestion.Process.ShouldBe(Excel);
        suggestion.Template.Id.ShouldBe("excel");
        For(new ProcessName("EXCELONLINE.EXE"))
            .ShouldNotBeNull("any process of the template counts");
    }

    [Fact]
    public void There_is_no_card_when_a_condition_fails()
    {
        For(Excel, autoSuggest: false).ShouldBeNull("«Detectar» is off");
        For(Excel, searching: true).ShouldBeNull("a search with text hides it (PAN-008)");
        For(Excel, dismissed: new ProcessName("EXCEL.EXE")).ShouldBeNull("«Ahora no» this session");
        For(Chrome).ShouldBeNull("the app already has a profile");
        For(new ProcessName(string.Empty)).ShouldBeNull("no app known");
        ProfileSuggestions
            .For(Excel, Library(), null, true, ImmutableHashSet<ProcessName>.Empty, false)
            .ShouldBeNull("the templates could not be read");
    }

    [Fact]
    [Trait("Req", "EC-PER-02")]
    public void An_app_without_a_template_never_gets_a_card() => For(Paint).ShouldBeNull();

    [Fact]
    [Trait("Req", "PER-007")]
    [Trait("Req", "PLA-009")]
    public void Creating_the_profile_installs_the_template_in_the_programs_language_and_shows_it_in_auto()
    {
        var harness = Harness(LangCode.En);
        var suggestion = For(Excel).ShouldNotBeNull();

        var accepted = ProfileSuggestions
            .Accept(harness.Store, suggestion, Auto, LangCode.Es, new SequentialIds())
            .Value;

        var created = harness.Store.Current.Library.ProfileFor(Excel).ShouldNotBeNull();
        accepted.Profile.ShouldBe(created.Id);
        created.Origin.ShouldNotBeNull().Source.ShouldBe("excel");
        created
            .Shortcuts.ShouldHaveSingleItem()
            .Action.ShouldBe(new TapAction(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.B), []));
        accepted.Transition.State.View.ShouldBe(new ViewTarget.Profile(created.Id));
        accepted.Notice.ShouldBe(L.InstalledApp(app: "Hoja Excel"));
        For(Excel).ShouldNotBeNull("the fixture library is unchanged");
        ProfileSuggestions
            .For(Excel, harness.Store.Current.Library, Content, true, [], false)
            .ShouldBeNull("Excel has its profile now");
    }

    [Fact]
    [Trait("Req", "REG-07")]
    public void Creating_the_profile_can_be_undone()
    {
        var harness = Harness(LangCode.Es);

        _ = ProfileSuggestions
            .Accept(harness.Store, For(Excel)!, Auto, LangCode.Es, new SequentialIds())
            .Value;
        harness.Store.Undo().IsSuccess.ShouldBeTrue();

        harness.Store.Current.Library.ProfileFor(Excel).ShouldBeNull();
    }

    [Theory]
    [Trait("Req", "PER-007")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void In_fixed_or_in_frequents_the_view_stays(bool locked, bool frequents)
    {
        var harness = Harness(LangCode.Es);
        var state = new ProfileState(
            frequents ? new ViewTarget.Frequents() : new ViewTarget.Profile(ProfileId.General),
            locked,
            null
        );

        var accepted = ProfileSuggestions
            .Accept(harness.Store, For(Excel)!, state, LangCode.Es, new SequentialIds())
            .Value;

        accepted.Transition.State.View.ShouldBe(state.View);
    }

    private static StoreHarness Harness(LangCode appsLanguage)
    {
        var defaults = SettingsSchema.Defaults;
        var settings = defaults with
        {
            Keyboard = defaults.Keyboard with { AppsLanguage = appsLanguage },
        };
        return new StoreHarness(UserDocument.Create(Library(), settings));
    }
}
