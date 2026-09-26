namespace Clicalo.Build;

/// <summary>A repository-level git setting that <c>cl setup</c> applies.</summary>
/// <param name="Key">The git configuration key.</param>
/// <param name="Value">The value to set.</param>
/// <param name="Reason">Why the project wants it (English, for maintainers reading the code).</param>
internal sealed record GitSetting(string Key, string Value, string Reason);
