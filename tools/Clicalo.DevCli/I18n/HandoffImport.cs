namespace Clicalo.DevCli.I18n;

/// <summary>Result of converting the handoff strings: the entries per language, in file order, or the errors.</summary>
internal sealed class HandoffImport
{
    /// <summary>Language → physical entries (<c>comboN_one</c>, <c>comboN_other</c>…) in output order.</summary>
    public Dictionary<string, List<KeyValuePair<string, string>>> Entries { get; } =
        new(StringComparer.Ordinal);

    /// <summary>Problems of the handoff or of the recipe; nothing is written when there is any.</summary>
    public List<string> Errors { get; } = [];

    /// <summary>Number of keys of the handoff (669), the retired ones included.</summary>
    public int HandoffKeyCount { get; set; }
}
