using System.Text.RegularExpressions;

namespace Clicalo.Windowing.IntegrationTests.Automation.Rules;

/// <summary>Names with the voice number prefix «{n} » (ACC-009).</summary>
public static partial class VoiceNames
{
    /// <summary>The name without a leading voice number, as a person would say it after «clic».</summary>
    public static string Strip(string name) => Prefix().Replace(name, string.Empty, 1);

    /// <summary>True when <paramref name="name"/> starts with a voice number.</summary>
    public static bool HasNumber(string name) => Prefix().IsMatch(name);

    [GeneratedRegex(@"^\d+ ", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Prefix();
}
