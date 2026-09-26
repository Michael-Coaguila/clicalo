using System.Globalization;

namespace Clicalo.Generators.Localization;

/// <summary>
/// A data error found in <c>data/i18n</c>, with its 1-based position in the offending file.
/// Roslyn-free so that the generator and <c>cl i18n-check</c> share the exact same validation.
/// </summary>
internal sealed class LocalizationIssue(
    string id,
    string message,
    string? path,
    int line,
    int column
)
{
    /// <summary>Stable identifier (see <see cref="LocalizationIds"/>).</summary>
    public string Id { get; } = id;

    /// <summary>English, developer-facing description.</summary>
    public string Message { get; } = message;

    /// <summary>Path of the file as given to the analysis, or <c>null</c> when the problem is a missing file.</summary>
    public string? Path { get; } = path;

    /// <summary>1-based line, or 0 when the issue has no position.</summary>
    public int Line { get; } = line;

    /// <summary>1-based column (UTF-16 code units), or 0 when the issue has no position.</summary>
    public int Column { get; } = column;

    /// <summary>MSBuild canonical format, understood by editors and CI problem matchers.</summary>
    public override string ToString() =>
        Path is null
            ? string.Format(CultureInfo.InvariantCulture, "error {0}: {1}", Id, Message)
            : string.Format(
                CultureInfo.InvariantCulture,
                "{0}({1},{2}): error {3}: {4}",
                Path,
                Line,
                Column,
                Id,
                Message
            );
}
