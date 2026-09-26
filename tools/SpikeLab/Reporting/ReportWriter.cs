using System.Globalization;
using System.IO;
using System.Text;
using Clicalo.Tools.SpikeLab.Scripting;

namespace Clicalo.Tools.SpikeLab.Reporting;

/// <summary>
/// Writes the report of one run as <c>S&lt;n&gt;-&lt;fecha&gt;.json</c> and <c>.md</c> in the reports folder
/// (<see cref="DefaultDirectory"/>), replacing both files atomically after every change, so a crash never loses more
/// than the last repetition. Writes are serialized and never run on the UI thread.
/// </summary>
internal sealed class ReportWriter : IDisposable
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Prepares the files of a run of <paramref name="spike"/> started at <paramref name="startedAt"/>.</summary>
    /// <param name="directory">The reports folder; created if needed.</param>
    /// <param name="spike">The spike.</param>
    /// <param name="startedAt">Start of the run; its local date and time name the files.</param>
    public ReportWriter(string directory, SpikeId spike, DateTimeOffset startedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory = directory;
        var name = BaseName(spike, startedAt);
        var candidate = name;
        for (
            var suffix = 2;
            File.Exists(Path.Combine(directory, candidate + ".json"))
                || File.Exists(Path.Combine(directory, candidate + ".md"));
            suffix++
        )
        {
            candidate = name + "-" + suffix.ToString(CultureInfo.InvariantCulture);
        }

        JsonPath = Path.Combine(directory, candidate + ".json");
        MarkdownPath = Path.Combine(directory, candidate + ".md");
    }

    /// <summary><c>%LOCALAPPDATA%\Clicalo.SpikeLab\reports</c>.</summary>
    public static string DefaultDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Clicalo.SpikeLab",
            "reports"
        );

    /// <summary>The reports folder.</summary>
    public string Directory { get; }

    /// <summary>The JSON report.</summary>
    public string JsonPath { get; }

    /// <summary>The Markdown summary.</summary>
    public string MarkdownPath { get; }

    /// <summary><c>S1-2026-09-26-101530</c>: the spike and the local start time.</summary>
    public static string BaseName(SpikeId spike, DateTimeOffset startedAt) =>
        spike.ToString()
        + "-"
        + startedAt.ToLocalTime().ToString("yyyy-MM-dd-HHmmss", CultureInfo.InvariantCulture);

    /// <summary>Writes both files (each replaced atomically), in order with the previous writes.</summary>
    public async Task WriteAsync(string json, string markdown, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(markdown);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(
                    async () =>
                    {
                        System.IO.Directory.CreateDirectory(Directory);
                        await ReplaceAsync(JsonPath, json, cancellationToken).ConfigureAwait(false);
                        await ReplaceAsync(MarkdownPath, markdown, cancellationToken)
                            .ConfigureAwait(false);
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    private static async Task ReplaceAsync(
        string path,
        string content,
        CancellationToken cancellationToken
    )
    {
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, content, Utf8, cancellationToken)
            .ConfigureAwait(false);
        File.Move(temporary, path, overwrite: true);
    }
}
