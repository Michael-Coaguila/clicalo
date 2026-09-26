using System.Security.Cryptography;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Migration.V1;
using Clicalo.Infrastructure.Migration;

namespace Clicalo.Infrastructure.Tests.Migration;

/// <summary>
/// The v1 import stage (blueprint §6.6, MIG-004, MIG-009): the byte copy is kept before converting, a failure converts
/// nothing and offers «Retry migration», the source is only read and the log only carries codes and counts.
/// </summary>
public sealed class V1ImporterTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "clicalo-v1-import-" + Guid.NewGuid().ToString("N")
    );
    private readonly RecordingLogger<V1Importer> _log = new();
    private readonly V1Importer _importer;

    public V1ImporterTests()
    {
        Directory.CreateDirectory(_folder);
        _importer = new V1Importer(new SafeZipReader(SafeZipLimits.Default), _log);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder must not fail a test.
        }
    }

    [Fact]
    [Trait("Req", "MIG-004")]
    public async Task A_damaged_file_converts_nothing_keeps_only_its_byte_copy_and_offers_retry()
    {
        var real = V1Fixtures.Bytes(V1Fixtures.InUse);
        var damaged = Write("profiles.json", real[..(real.Length / 2)]);
        var before = Hash(damaged);
        var backups = new RecordingBackupService();

        var result = await _importer.MigrateAsync(
            damaged,
            Unused(),
            backups,
            TestContext.Current.CancellationToken
        );

        result.Failure.Code.ShouldBe(V1ImportFailures.DamagedCode);
        result.Failure.Recovery.ShouldBe(FailureRecovery.Retry);
        backups.KeptOriginals.ShouldHaveSingleItem().ShouldBe(real[..(real.Length / 2)]);
        Hash(damaged).ShouldBe(before);
        _log.Lines.ShouldContain(line =>
            line.Contains(V1ImportFailures.DamagedCode, StringComparison.Ordinal)
        );
    }

    [Fact]
    [Trait("Req", "MIG-004")]
    public async Task Without_the_byte_copy_nothing_is_converted()
    {
        var path = Write("profiles.json", V1Fixtures.Bytes(V1Fixtures.InUse));
        var backups = new RecordingBackupService(fail: true);

        // Converting would need the domain package; the copy fails first, so the conversion never starts.
        var result = await _importer.MigrateAsync(
            path,
            Unused(),
            backups,
            TestContext.Current.CancellationToken
        );

        result.Failure.ShouldBe(RecordingBackupService.Refused);
        backups.KeptOriginals.ShouldHaveSingleItem().ShouldBe(V1Fixtures.Bytes(V1Fixtures.InUse));
    }

    [Fact]
    [Trait("Req", "MIG-009")]
    public async Task A_missing_file_is_unreadable_and_nothing_is_kept()
    {
        var backups = new RecordingBackupService();

        var result = await _importer.MigrateAsync(
            Path.Combine(_folder, "missing.json"),
            Unused(),
            backups,
            TestContext.Current.CancellationToken
        );

        result.Failure.Code.ShouldBe(V1ImportFailures.UnreadableCode);
        backups.KeptOriginals.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "MIG-009")]
    public async Task A_zip_without_profiles_json_is_refused()
    {
        var path = Write("backup.zip", Zips.Create(("notes.json", "{}")));

        var result = await _importer.PreviewAsync(
            path,
            Unused(),
            TestContext.Current.CancellationToken
        );

        result.Failure.Code.ShouldBe(V1ImportFailures.ZipWithoutProfilesCode);
    }

    [Fact]
    [Trait("Req", "MIG-009")]
    public async Task A_hostile_zip_is_refused_before_anything_is_read()
    {
        var path = Write("backup.zip", Zips.Create(("../profiles.json", "{}")));

        var result = await _importer.PreviewAsync(
            path,
            Unused(),
            TestContext.Current.CancellationToken
        );

        result.Failure.Code.ShouldBe(V1ImportFailures.ZipUnsafePathCode);
    }

    [Theory]
    [Trait("Req", "MIG-009")]
    [InlineData("profiles.json", V1SourceKind.ProfilesJson)]
    [InlineData("profiles.backup.es.json", V1SourceKind.LanguageBackup)]
    [InlineData("PROFILES.BACKUP.EN.JSON", V1SourceKind.LanguageBackup)]
    [InlineData("perfiles_backup.json", V1SourceKind.ProfilesJson)]
    public void The_kind_of_a_json_file_comes_from_its_name(string name, V1SourceKind kind) =>
        V1Importer.DetectKind(name, "{}"u8).ShouldBe(kind);

    [Fact]
    [Trait("Req", "MIG-009")]
    public void A_zip_is_recognized_by_its_signature_whatever_its_name() =>
        V1Importer
            .DetectKind("profiles.json", Zips.Create(("profiles.json", "{}")))
            .ShouldBe(V1SourceKind.Zip);

    [Fact]
    [Trait("Req", "MIG-004")]
    public Task The_first_run_migration_keeps_the_original_then_converts_210_to_210() =>
        DomainPending.RunAsync(async () =>
        {
            var original = V1Fixtures.Bytes(V1Fixtures.InUse);
            var path = Write("profiles.json", original);
            var backups = new RecordingBackupService();

            var preview = (
                await _importer.MigrateAsync(
                    path,
                    V1Context.Create(),
                    backups,
                    TestContext.Current.CancellationToken
                )
            ).Value;

            backups.KeptOriginals.ShouldHaveSingleItem().ShouldBe(original);
            preview.Source.ShouldBe(V1SourceKind.ProfilesJson);
            preview.Original.ToArray().ShouldBe(original);
            preview.Conversion.Report.Input.Buttons.ShouldBe(210);
            preview.Conversion.Report.Output.ShouldBe(preview.Conversion.Report.Input);
        });

    [Fact]
    [Trait("Req", "MIG-004")]
    public Task Migrating_twice_gives_the_same_document_and_never_changes_the_source() =>
        DomainPending.RunAsync(async () =>
        {
            var path = Write("profiles.json", V1Fixtures.Bytes(V1Fixtures.InUse));
            var before = Hash(path);

            var first = await _importer.MigrateAsync(
                path,
                V1Context.Create(),
                new RecordingBackupService(),
                TestContext.Current.CancellationToken
            );
            var second = await _importer.MigrateAsync(
                path,
                V1Context.Create(),
                new RecordingBackupService(),
                TestContext.Current.CancellationToken
            );

            first.Value.Conversion.ShouldBe(second.Value.Conversion);
            Hash(path).ShouldBe(before);
        });

    [Fact]
    [Trait("Req", "MIG-009")]
    public Task The_real_zip_backup_imports_its_profiles_json() =>
        DomainPending.RunAsync(async () =>
        {
            var path = Write("profiles.backup.zip", V1Fixtures.Bytes(V1Fixtures.Zip));

            var preview = (
                await _importer.PreviewAsync(
                    path,
                    V1Context.Create(),
                    TestContext.Current.CancellationToken
                )
            ).Value;

            preview.Source.ShouldBe(V1SourceKind.Zip);
            preview.Original.ToArray().ShouldBe(V1Fixtures.Bytes(V1Fixtures.Zip));
            preview.Conversion.Report.Input.ShouldBe(new V1Counts(13, 192, 0, 0, 0));
        });

    [Fact]
    [Trait("Req", "LOG-001")]
    public Task The_log_carries_counts_never_labels_combinations_or_paths() =>
        DomainPending.RunAsync(async () =>
        {
            var path = Write("profiles.json", V1Fixtures.Bytes(V1Fixtures.InUse));

            await _importer.PreviewAsync(
                path,
                V1Context.Create(),
                TestContext.Current.CancellationToken
            );

            _log.Lines.ShouldNotBeEmpty();
            foreach (var line in _log.Lines)
            {
                line.ShouldNotContain(_folder, Case.Insensitive);
                line.ShouldNotContain("Copiar");
                line.ShouldNotContain("ctrl+");
            }
        });

    private static V1ConversionContext Unused() => V1Context.BeforeConversion();

    private static string Hash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private string Write(string name, byte[] bytes)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
