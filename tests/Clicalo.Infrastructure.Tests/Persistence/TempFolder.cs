using Clicalo.Infrastructure.Persistence;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>A real, empty data folder under the temporary folder, deleted afterwards (blueprint §10.1).</summary>
public sealed class TempFolder : IDisposable
{
    /// <summary>Creates the folder.</summary>
    public TempFolder()
    {
        Root = Path.Combine(Path.GetTempPath(), "clicalo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Locations = new DataLocations(Path.Combine(Root, "roaming"))
        {
            LocalRoot = Path.Combine(Root, "local"),
        };
    }

    /// <summary>The folder.</summary>
    public string Root { get; }

    /// <summary>Data locations inside it (roaming and local apart, like production).</summary>
    public DataLocations Locations { get; }

    /// <summary>A path inside the folder.</summary>
    /// <param name="parts">Relative parts.</param>
    public string PathOf(params string[] parts) => Path.Combine([Root, .. parts]);

    /// <summary>Every file below the folder, relative, sorted, with forward slashes.</summary>
    public IReadOnlyList<string> Files() =>
        [
            .. Directory
                .GetFiles(Root, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(Root, f).Replace('\\', '/'))
                .Order(StringComparer.Ordinal),
        ];

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A handle still open by a failed test; the temporary folder is cleaned by the system.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above.
        }
    }
}
