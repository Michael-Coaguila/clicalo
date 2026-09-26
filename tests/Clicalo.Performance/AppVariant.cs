namespace Clicalo.Performance;

/// <summary>One publication of <c>Clicalo.exe</c> that <c>cl perf</c> measures.</summary>
/// <param name="Name">
/// <c>sc-r2r</c> (self-contained ReadyToRun), <c>sc-r2r-composite</c> (self-contained, composite ReadyToRun) or
/// <c>fdd</c> (framework-dependent).
/// </param>
/// <param name="Executable">The full path of its <c>Clicalo.exe</c>.</param>
internal sealed record AppVariant(string Name, string Executable)
{
    /// <summary>
    /// Parses <c>CLICALO_PERF_APPS</c>: <c>name=path;name=path</c>. Entries without a name, a path or an existing
    /// executable are ignored.
    /// </summary>
    /// <param name="value">The variable's value; null or empty gives no variant.</param>
    public static IReadOnlyList<AppVariant> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var variants = new List<AppVariant>();
        foreach (
            var entry in value.Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
        )
        {
            var separator = entry.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0 || separator == entry.Length - 1)
            {
                continue;
            }

            variants.Add(
                new AppVariant(entry[..separator].Trim(), entry[(separator + 1)..].Trim())
            );
        }

        return variants;
    }
}
