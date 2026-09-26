namespace Clicalo.DevCli.I18n;

/// <summary>Where a key of <c>data/i18n</c> is first used by product code or data (1-based position).</summary>
internal sealed record KeyUsage(string Key, string Path, int Line, int Column);
