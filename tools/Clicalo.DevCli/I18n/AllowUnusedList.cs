namespace Clicalo.DevCli.I18n;

/// <summary>
/// Parser of <c>data/i18n/allow-unused.txt</c>: one base key per line; <c>#</c> starts a comment and blank lines
/// are ignored.
/// </summary>
internal static class AllowUnusedList
{
    public static List<AllowUnusedEntry> Parse(string text)
    {
        var entries = new List<AllowUnusedEntry>();
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var comment = line.IndexOf('#', StringComparison.Ordinal);
            var key = (comment < 0 ? line : line[..comment]).Trim();
            if (key.Length > 0)
            {
                entries.Add(new AllowUnusedEntry(key, i + 1));
            }
        }

        return entries;
    }
}
