using Clicalo.Domain.Catalog;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Migration.V1;
using Clicalo.Domain.Primitives;
using static Clicalo.Domain.Tests.Migration.V1Docs;
using MonitorPosition = Clicalo.Domain.Settings.MonitorPosition;
using PanelSize = Clicalo.Domain.Settings.PanelSize;

namespace Clicalo.Domain.Tests.Migration;

/// <summary>
/// The whole v1 conversion into a Clícalo document (blueprint §6.6, catalog §7.4). These checks need the model
/// factories of the domain package and are skipped until it is merged (<see cref="DomainPending"/>).
/// </summary>
public sealed class V1ConverterTests
{
    private static V1Document Sample() =>
        V1File(
            Profile(
                "General",
                string.Empty,
                Hotkey("Copiar", "ctrl+c"),
                Hotkey("Bloquear", "win+l", "#7F8C8D"),
                Separator(),
                Url("Correo", "https://mail.example.com")
            ),
            Profile(
                "VS Code",
                "code.exe",
                Hotkey("Copiar", "ctrl+c"),
                Hotkey("Comentar", "ctrl+k ctrl+c"),
                App("Notas", "notepad.exe"),
                Hotkey("Guardar", "ctrl+s"),
                Hotkey("Guardar todo", "ctrl+s")
            )
        ) with
        {
            ActiveProfile = "VS Code",
            WindowOpacity = 0.68,
            ButtonSize = new V1Pair(55, 40),
            WindowPosition = new V1Pair(743, 46),
        };

    [Fact]
    [Trait("Req", "MIG-004")]
    public void The_counts_of_the_output_equal_the_counts_of_the_input() =>
        DomainPending.Run(() =>
        {
            var conversion = V1Converter.Convert(Sample(), V1Baseline.Context(Primary)).Value;

            conversion.Report.Input.ShouldBe(new V1Counts(2, 9, 1, 1, 1));
            conversion.Report.Output.ShouldBe(conversion.Report.Input);
            conversion.Document.Library.Profiles.Sum(static p => p.Shortcuts.Count).ShouldBe(8);
        });

    [Fact]
    [Trait("Req", "MIG-003")]
    [Trait("Req", "MIG-007")]
    public void One_chord_is_a_tap_several_chords_a_macro_and_win_l_the_lock_action() =>
        DomainPending.Run(() =>
        {
            var library = V1Converter
                .Convert(Sample(), V1Baseline.Context(Primary))
                .Value.Document.Library;
            var general = library.Profiles[0];
            var code = library.Profiles[1];

            general
                .Shortcuts[0]
                .Action.ShouldBe(
                    new TapAction(KeyChord.Create([Key(KeyIds.Ctrl), Key(KeyIds.C)]), [])
                );
            general.Shortcuts[1].Action.ShouldBe(new SystemAction(new SystemCommandId("lock")));
            general
                .Shortcuts[2]
                .Action.ShouldBe(
                    new UrlAction(new UrlTarget.Valid(new Uri("https://mail.example.com")))
                );
            code.Shortcuts[1]
                .Action.ShouldBe(
                    new MacroAction([
                        new KeysStep(KeyChord.Create([Key(KeyIds.Ctrl), Key(KeyIds.K)])),
                        new KeysStep(KeyChord.Create([Key(KeyIds.Ctrl), Key(KeyIds.C)])),
                    ])
                );
            code.Shortcuts[2]
                .Action.ShouldBe(
                    new AppAction(new AppTarget.Executable("notepad.exe", string.Empty))
                );
        });

    [Fact]
    [Trait("Req", "MIG-004")]
    public void Profiles_keep_their_order_process_and_names_in_both_languages() =>
        DomainPending.Run(() =>
        {
            var library = V1Converter
                .Convert(Sample(), V1Baseline.Context(Primary))
                .Value.Document.Library;

            library.AlwaysVisible.ShouldBeEmpty();
            library
                .Profiles.Select(static p => p.Id)
                .ShouldBe([ProfileId.General, new ProfileId("p1")]);
            var code = library.Profiles[1];
            code.Name.ShouldBe(LocalizedText.Same("VS Code", LangCode.Es, LangCode.En));
            code.Binding.ShouldBe(new AppBinding.Processes([new ProcessName("code.exe")]));
            code.Injection.ShouldBe(InjectionMode.VirtualKey);
            code.AutoIcon.ShouldBeTrue();
            code.Shortcuts.ShouldAllBe(static s => s.AutoIcon);

            // General keeps the name and icon of a new installation, with the v1 buttons instead of the seed.
            library.Profiles[0].Icon.ShouldBe(V1Baseline.GeneralIcon);
            library
                .Profiles[0]
                .Shortcuts.Select(static s => s.Name.Get(LangCode.Es, LangCode.En))
                .ShouldBe(["Copiar", "Bloquear", "Correo"]);
        });

    [Fact]
    [Trait("Req", "MIG-006")]
    public void The_settings_with_an_equivalent_are_converted_and_the_rest_keep_the_defaults() =>
        DomainPending.Run(() =>
        {
            var context = V1Baseline.Context(Primary);
            var settings = V1Converter.Convert(Sample(), context).Value.Document.Settings;

            settings.Opacity.ShouldBe(0.70);
            settings.Size.ShouldBe(PanelSize.Small);
            settings.LastProfile.ShouldBe(new ProfileId("p1"));
            settings.PanelPositions.ShouldBe([new MonitorPosition(Primary.Id, 1486, 92)]);
            settings.Theme.ShouldBe(context.Baseline.Settings.Theme);
            settings.Language.ShouldBe(context.Baseline.Settings.Language);
        });

    [Fact]
    [Trait("Req", "MIG-008")]
    public void Repeated_combinations_created_by_the_import_go_to_its_fine_and_the_report() =>
        DomainPending.Run(() =>
        {
            var conversion = V1Converter.Convert(Sample(), V1Baseline.Context(Primary)).Value;
            CanonicalChord
                .TryFrom(KeyChord.Create([Key(KeyIds.Ctrl), Key(KeyIds.C)]), out var copy)
                .ShouldBeTrue();
            CanonicalChord
                .TryFrom(KeyChord.Create([Key(KeyIds.Ctrl), Key(KeyIds.S)]), out var save)
                .ShouldBeTrue();

            // «Copiar» in two profiles (same name) and «Guardar» / «Guardar todo» in one list are repeated.
            conversion.Document.Duplicates.Ignored.ShouldBe([copy, save]);
            conversion
                .Report.Notes.Where(static n => n.Kind == MigrationNoteKind.DuplicateIgnored)
                .Select(static n => n.Original)
                .ShouldBe([copy.ToStableString(), save.ToStableString()]);
        });

    [Fact]
    [Trait("Req", "MIG-004")]
    public void The_conversion_is_idempotent() =>
        DomainPending.Run(() =>
        {
            var first = V1Converter.Convert(Sample(), V1Baseline.Context(Primary));
            var second = V1Converter.Convert(Sample(), V1Baseline.Context(Primary));

            first.Value.Document.ShouldBe(second.Value.Document);
            first.Value.Report.ShouldBe(second.Value.Report);
        });

    [Fact]
    [Trait("Req", "EDI-005")]
    public void Suggested_icons_are_used_and_otherwise_the_fallback_icons() =>
        DomainPending.Run(() =>
        {
            var plain = V1Converter
                .Convert(Sample(), V1Baseline.Context(Primary))
                .Value.Document.Library;
            plain.Profiles[1].Icon.ShouldBe(new IconRef("apps"));
            plain.Profiles[1].Shortcuts[0].Icon.ShouldBe(new IconRef("bolt"));
            plain.Profiles[0].Shortcuts[1].Icon.ShouldBe(new IconRef("lock"));

            var context = V1Baseline.Context(Primary) with
            {
                SuggestIcon = static (name, keys) =>
                    string.Equals(name, "Copiar", StringComparison.Ordinal) && keys is not null
                        ? new IconRef("content_copy")
                        : null,
            };
            var suggested = V1Converter.Convert(Sample(), context).Value.Document.Library;
            suggested.Profiles[1].Shortcuts[0].Icon.ShouldBe(new IconRef("content_copy"));
            suggested.Profiles[1].Shortcuts[0].AutoIcon.ShouldBeTrue();
        });

    [Fact]
    [Trait("Req", "MIG-004")]
    public void Without_General_the_created_one_is_not_counted_as_imported() =>
        DomainPending.Run(() =>
        {
            var document = V1File(Profile("Chrome", "chrome.exe", Hotkey("Atrás", "alt+left")));

            var conversion = V1Converter.Convert(document, V1Baseline.Context(Primary)).Value;

            conversion.Document.Library.Profiles.Count.ShouldBe(2);
            conversion.Document.Library.Profiles[0].Shortcuts.ShouldBeEmpty();
            conversion.Report.Output.ShouldBe(new V1Counts(1, 1, 0, 0, 0));
        });
}
