using System;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Clicalo.Generators.Catalogs;

/// <summary>
/// Snapshot of a catalog file with value equality on its path and content, so the incremental pipeline
/// only re-runs an output when one of its own files changes.
/// </summary>
internal sealed class CatalogFile : AdditionalText, IEquatable<CatalogFile>
{
    public CatalogFile(string path, string text)
    {
        Path = path;
        Text = text;
        FileName = System.IO.Path.GetFileName(path);
    }

    public override string Path { get; }

    /// <summary>Full content of the file.</summary>
    public string Text { get; }

    /// <summary>File name without directory, used to pick a catalog.</summary>
    public string FileName { get; }

    public static CatalogFile? From(AdditionalText file, CancellationToken cancellationToken)
    {
        var text = file.GetText(cancellationToken)?.ToString();
        return text is null ? null : new CatalogFile(file.Path, text);
    }

    public override SourceText GetText(CancellationToken cancellationToken = default) =>
        SourceText.From(Text);

    public bool Equals(CatalogFile? other) =>
        other is not null
        && string.Equals(Path, other.Path, StringComparison.Ordinal)
        && string.Equals(Text, other.Text, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as CatalogFile);

    public override int GetHashCode() =>
        unchecked(
            (StringComparer.Ordinal.GetHashCode(Path) * 397)
            ^ StringComparer.Ordinal.GetHashCode(Text)
        );
}
