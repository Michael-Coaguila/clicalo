using Clicalo.Domain.Keys;
using Clicalo.Domain.Migration.V1;
using Clicalo.Domain.Primitives;
using Clicalo.Infrastructure.Migration;

namespace Clicalo.Infrastructure.Tests.Migration;

/// <summary>
/// Fidelity on the user's real files (catalog §7.1 and §7.5): every file is read with its real counts, every
/// combination tokenizes without an empty or unresolved token, and the 8 + 2 special shortcuts get the meaning of
/// their name. The full conversion (210 → 210) needs the domain package and runs once it is merged.
/// </summary>
public sealed class V1RealFilesTests
{
    [Theory]
    [Trait("Req", "MIG-002")]
    [MemberData(nameof(V1Fixtures.Counts), MemberType = typeof(V1Fixtures))]
    public void Every_real_file_is_read_with_its_real_counts(
        string fixture,
        int profiles,
        int buttons
    )
    {
        var counts = V1Counts.Of(V1Fixtures.Read(fixture));

        // All real buttons are variant (a): combinations without type, no separators, addresses or apps.
        counts.ShouldBe(new V1Counts(profiles, buttons, 0, 0, 0));
    }

    [Fact]
    [Trait("Req", "MIG-004")]
    public void The_file_in_use_has_the_14_profiles_and_210_buttons_of_the_catalog()
    {
        var document = V1Fixtures.Read(V1Fixtures.InUse);

        document
            .Profiles.Select(static p => (p.Name, p.Buttons.Count))
            .ShouldBe([
                ("General", 27),
                ("VS Code", 21),
                ("Navegador", 10),
                ("Chrome", 19),
                ("Firefox", 18),
                ("Word", 13),
                ("Excel", 12),
                ("PowerPoint", 12),
                ("Zoom", 9),
                ("Teams", 9),
                ("Photoshop", 18),
                ("Notepad++", 12),
                ("Explorador", 9),
                ("Antigravity", 21),
            ]);
        document.Profiles.Count(static p => p.Process.Length > 0).ShouldBe(12);
        document.ActiveProfile.ShouldBe("Chrome");
        document.PinnedProfile.ShouldBe("General");
        document.WindowOpacity.ShouldBe(0.68);
        document.ButtonSize.ShouldBe(new V1Pair(55, 40));
    }

    [Theory]
    [Trait("Req", "MIG-003")]
    [InlineData(V1Fixtures.InUse)]
    [InlineData(V1Fixtures.Dist)]
    [InlineData(V1Fixtures.Root)]
    [InlineData(V1Fixtures.BackupEs)]
    [InlineData(V1Fixtures.BackupEn)]
    public void Every_real_combination_tokenizes_without_empty_or_unresolved_tokens(string fixture)
    {
        foreach (var profile in V1Fixtures.Read(fixture).Profiles)
        {
            foreach (var button in profile.Buttons)
            {
                var scan = V1ComboTokenizer.Scan(button.Hotkey ?? string.Empty);

                scan.Unresolved.ShouldBeEmpty($"{profile.Name} · {button.Label}");
                scan.Chords.ShouldNotBeEmpty($"{profile.Name} · {button.Label}");
                scan.Chords.ShouldAllBe(static chord => !chord.IsEmpty);
            }
        }
    }

    /// <summary>Catalog §7.5: the 8 + 2 real shortcuts with a special conversion.</summary>
    public static TheoryData<string, string, string, KeyStroke[][]> Special() =>
        new()
        {
            {
                "Firefox",
                "Acercar",
                "ctrl++",
                [
                    [Key(KeyIds.Ctrl), Key(KeyIds.Plus)],
                ]
            },
            {
                "Photoshop",
                "Acercar",
                "ctrl++",
                [
                    [Key(KeyIds.Ctrl), Key(KeyIds.Plus)],
                ]
            },
            {
                "Excel",
                "Inser. fila",
                "ctrl+num+",
                [
                    [Key(KeyIds.Ctrl), Key(KeyIds.NumAdd)],
                ]
            },
            {
                "Excel",
                "Elim. fila",
                "ctrl+num-",
                [
                    [Key(KeyIds.Ctrl), Key(KeyIds.NumSubtract)],
                ]
            },
            {
                "VS Code",
                "Comentar",
                "ctrl+k ctrl+c",
                [
                    [Key(KeyIds.Ctrl), Key(KeyIds.K)],
                    [Key(KeyIds.Ctrl), Key(KeyIds.C)],
                ]
            },
            {
                "VS Code",
                "Descomentar",
                "ctrl+k ctrl+u",
                [
                    [Key(KeyIds.Ctrl), Key(KeyIds.K)],
                    [Key(KeyIds.Ctrl), Key(KeyIds.U)],
                ]
            },
            {
                "Antigravity",
                "Comentar",
                "ctrl+k ctrl+c",
                [
                    [Key(KeyIds.Ctrl), Key(KeyIds.K)],
                    [Key(KeyIds.Ctrl), Key(KeyIds.C)],
                ]
            },
            {
                "Antigravity",
                "Descomentar",
                "ctrl+k ctrl+u",
                [
                    [Key(KeyIds.Ctrl), Key(KeyIds.K)],
                    [Key(KeyIds.Ctrl), Key(KeyIds.U)],
                ]
            },
            {
                "General",
                "Bloquear",
                "win+l",
                [
                    [Key(KeyIds.Win), Key(KeyIds.L)],
                ]
            },
            {
                "General",
                "Admin. tar.",
                "ctrl+shift+esc",
                [
                    [Key(KeyIds.Ctrl), Key(KeyIds.Shift), Key(KeyIds.Escape)],
                ]
            },
        };

    [Theory]
    [Trait("Req", "MIG-003")]
    [Trait("Req", "MIG-007")]
    [MemberData(nameof(Special))]
    public void The_special_real_shortcuts_get_the_meaning_of_their_name(
        string profile,
        string label,
        string hotkey,
        KeyStroke[][] chords
    )
    {
        var button = V1Fixtures
            .Read(V1Fixtures.InUse)
            .Profiles.Single(p => string.Equals(p.Name, profile, StringComparison.Ordinal))
            .Buttons.Single(b => string.Equals(b.Label, label, StringComparison.Ordinal));

        button.Hotkey.ShouldBe(hotkey);
        V1ComboTokenizer
            .Scan(hotkey)
            .Chords.ShouldBe([.. chords.Select(static c => new ValueList<KeyStroke>([.. c]))]);
    }

    [Fact]
    [Trait("Req", "MIG-009")]
    public void The_language_backups_are_recognized_and_keep_their_note()
    {
        V1Importer
            .DetectKind(V1Fixtures.BackupEs, V1Fixtures.Bytes(V1Fixtures.BackupEs))
            .ShouldBe(V1SourceKind.LanguageBackup);
        V1Importer
            .DetectKind(V1Fixtures.BackupEn, V1Fixtures.Bytes(V1Fixtures.BackupEn))
            .ShouldBe(V1SourceKind.LanguageBackup);
        V1Importer
            .DetectKind(V1Fixtures.InUse, V1Fixtures.Bytes(V1Fixtures.InUse))
            .ShouldBe(V1SourceKind.ProfilesJson);
        V1Fixtures.Read(V1Fixtures.BackupEs).UnknownKeys.ShouldBe(["_nota"]);
        V1Fixtures.Read(V1Fixtures.BackupEn).UnknownKeys.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "MIG-009")]
    public void The_real_zip_backup_passes_the_safe_reader_with_its_three_files()
    {
        V1Importer
            .DetectKind(V1Fixtures.Zip, V1Fixtures.Bytes(V1Fixtures.Zip))
            .ShouldBe(V1SourceKind.Zip);

        var entries = new SafeZipReader(SafeZipLimits.Default)
            .ReadJsonEntries(
                new MemoryStream(V1Fixtures.Bytes(V1Fixtures.Zip)),
                TestContext.Current.CancellationToken
            )
            .Value;

        entries
            .Select(static e => e.Name)
            .ShouldBe(["profiles.backup.en.json", "profiles.backup.es.json", "profiles.json"]);
        entries
            .Select(static e => V1Counts.Of(V1Reader.Read(e.Content.Span).Value).Buttons)
            .ShouldBe([160, 163, 192]);
    }

    [Theory]
    [Trait("Req", "MIG-004")]
    [Trait("Req", "MIG-003")]
    [MemberData(nameof(V1Fixtures.Counts), MemberType = typeof(V1Fixtures))]
    public void Every_real_file_converts_without_losses(string fixture, int profiles, int buttons)
    {
        var conversion = V1Converter.Convert(V1Fixtures.Read(fixture), V1Context.Create()).Value;

        conversion.Report.Input.ShouldBe(new V1Counts(profiles, buttons, 0, 0, 0));
        conversion.Report.Output.ShouldBe(conversion.Report.Input);
        conversion.Document.Library.Profiles.Sum(static p => p.Shortcuts.Count).ShouldBe(buttons);
        conversion.Report.Notes.ShouldNotContain(static n =>
            n.Kind == MigrationNoteKind.UnresolvedToken
        );
        conversion.Report.Notes.ShouldNotContain(static n =>
            n.Kind == MigrationNoteKind.MissingAction
        );
    }

    [Fact]
    [Trait("Req", "MIG-007")]
    public void The_file_in_use_reports_the_8_that_never_worked_and_the_2_special_ones()
    {
        var notes = V1Converter
            .Convert(V1Fixtures.Read(V1Fixtures.InUse), V1Context.Create())
            .Value.Report.Notes;

        notes.Count(static n => n.Kind == MigrationNoteKind.NeverWorkedInV1).ShouldBe(8);
        notes.ShouldContain(static n =>
            n.Kind == MigrationNoteKind.LockBecameSystemAction && n.Button == "Bloquear"
        );
        notes.ShouldContain(static n =>
            n.Kind == MigrationNoteKind.SpecialCombination && n.Button == "Admin. tar."
        );
        notes
            .Where(static n => n.Kind == MigrationNoteKind.HexColor)
            .Select(static n => n.Original)
            .ShouldBe(["#55ff00", "#55ff00", "#5500ff", "#00ffff"], ignoreOrder: true);
    }

    private static KeyStroke Key(KeyId key) => new(key);
}
