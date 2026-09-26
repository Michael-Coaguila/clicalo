using Clicalo.Domain.Document;
using Clicalo.Infrastructure.Persistence;
using Clicalo.Infrastructure.Tests.Persistence;

namespace Clicalo.Infrastructure.Tests.Backup;

/// <summary>Where backups live and how they are named and kept (blueprint §6.5, §6.8, COP-003).</summary>
[Trait("Req", "COP-003")]
public sealed class BackupLayoutTests
{
    [Theory]
    [InlineData(BackupKind.Auto, "auto", 12)]
    [InlineData(BackupKind.Manual, "manual", null)]
    [InlineData(BackupKind.PreUpdate, "pre-update", 10)]
    [InlineData(BackupKind.PreMigrate, "pre-migrate", 10)]
    [InlineData(BackupKind.PreRestore, "pre-restore", 10)]
    [InlineData(BackupKind.PreImportReplace, "pre-import", 10)]
    [InlineData(BackupKind.PreResetFrequents, "pre-reset", 10)]
    [InlineData(BackupKind.PreRepair, "pre-repair", 10)]
    public void Each_kind_has_its_folder_and_retention(BackupKind kind, string folder, int? kept)
    {
        BackupLayout.Folder(kind).ShouldBe(folder);
        BackupLayout.Retention(kind).ShouldBe(kept);
    }

    [Fact]
    [Trait("Req", "MIG-004")]
    public void The_v1_original_is_kept_forever_at_the_root()
    {
        BackupLayout.Retention(BackupKind.V1Original).ShouldBeNull();
        BackupLayout
            .V1FileName(new DateTimeOffset(2026, 9, 25, 10, 31, 2, TimeSpan.Zero), 0)
            .ShouldBe("v1-original-20260925T103102Z.json");
    }

    [Fact]
    public void Ids_round_trip()
    {
        var at = new DateTimeOffset(2026, 9, 25, 10, 31, 2, TimeSpan.Zero);
        var id = BackupLayout.Id(BackupKind.PreUpdate, BackupLayout.FileName(at, 42));

        id.ShouldBe("pre-update/clicalo.20260925T103102Z.000042.json");
        BackupLayout.TryParse(id, out var entry).ShouldBeTrue();
        entry.ShouldBe(new BackupLayout.Entry(BackupKind.PreUpdate, at, 42, id));
    }

    [Theory]
    [Trait("Req", "LOG-006")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("../clicalo.json")]
    [InlineData("auto/../../clicalo.json")]
    [InlineData("auto/clicalo.20260925T103102Z.000042.json/../x")]
    [InlineData(@"auto\clicalo.20260925T103102Z.000042.json")]
    [InlineData("C:/Windows/clicalo.20260925T103102Z.000042.json")]
    [InlineData("quarantine/clicalo.20260925T103102Z.000042.json")]
    [InlineData("auto/clicalo.20260925T103102Z.42.json")]
    public void Only_names_of_this_layout_are_accepted(string? id) =>
        BackupLayout.TryParse(id, out _).ShouldBeFalse();

    [Fact]
    public void Scanning_finds_every_backup_and_ignores_other_files()
    {
        using var folder = new TempFolder();
        var at = new DateTimeOffset(2026, 9, 25, 10, 31, 2, TimeSpan.Zero);
        Put(folder, "auto", BackupLayout.FileName(at, 1));
        Put(folder, "manual", BackupLayout.FileName(at, 2));
        Put(folder, "auto", BackupLayout.FileName(at, 3) + ".tmp");
        Put(folder, "auto", "notes.txt");

        BackupLayout
            .Scan(folder.Locations, AtomicFile.Disk)
            .Select(e => (e.Kind, e.Seq))
            .Order()
            .ShouldBe([(BackupKind.Auto, 1L), (BackupKind.Manual, 2L)]);
    }

    private static void Put(TempFolder folder, string kind, string name)
    {
        var path = Path.Combine(folder.Locations.Backups, kind, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}");
    }
}
