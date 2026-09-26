using Clicalo.Domain.Migration.V1;
using Clicalo.Infrastructure.Migration;
using Clicalo.TestKit;

namespace Clicalo.Infrastructure.Tests.Migration;

/// <summary>
/// The user's real Macro Quick Access files, anonymized with <c>anonymize-v1</c> (Fixtures/v1/README.md): the
/// combinations, colours, counts and structure are the real ones.
/// </summary>
internal static class V1Fixtures
{
    /// <summary><c>dist\MacroQuickAccess\profiles.json</c>, the file in use: 14 profiles and 210 buttons (MIG-001).</summary>
    public const string InUse = "profiles.dist-app.json";

    /// <summary><c>dist\profiles.json</c>: 14 profiles and 204 buttons.</summary>
    public const string Dist = "profiles.dist.json";

    /// <summary><c>profiles.json</c> at the root of the v1 folder: 13 profiles and 192 buttons.</summary>
    public const string Root = "profiles.root.json";

    /// <summary><c>profiles.backup.es.json</c>, with <c>_nota</c>: 12 profiles and 163 buttons.</summary>
    public const string BackupEs = "profiles.backup.es.json";

    /// <summary><c>profiles.backup.en.json</c>: 12 profiles and 160 buttons.</summary>
    public const string BackupEn = "profiles.backup.en.json";

    /// <summary><c>profiles.backup.zip</c>: the root <c>profiles.json</c> of March and both language backups.</summary>
    public const string Zip = "profiles.backup.zip";

    public static string PathOf(string name) =>
        RepoPaths.Combine("tests", "Clicalo.Infrastructure.Tests", "Fixtures", "v1", name);

    public static byte[] Bytes(string name) => File.ReadAllBytes(PathOf(name));

    public static V1Document Read(string name) => V1Reader.Read(Bytes(name)).Value;

    /// <summary>Every JSON fixture with its real counts (profiles, buttons).</summary>
    public static TheoryData<string, int, int> Counts() =>
        new()
        {
            { InUse, 14, 210 },
            { Dist, 14, 204 },
            { Root, 13, 192 },
            { BackupEs, 12, 163 },
            { BackupEn, 12, 160 },
        };
}
