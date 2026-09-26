namespace Clicalo.Domain.Migration.V1;

/// <summary>
/// What Macro Quick Access actually did with a combination (catalog §7.5): it split the text on every <c>+</c>, trimmed
/// each piece and pressed the names it knew, so <c>ctrl++</c> sent Ctrl alone, <c>ctrl+num+</c> and <c>ctrl+num-</c>
/// sent nothing useful and <c>ctrl+k ctrl+c</c> sent Ctrl+C. Those shortcuts never worked in v1; the migration gives
/// them the meaning of their name and marks them «Revisar» (MIG-007, PQ-40).
/// </summary>
internal static class V1LegacyHotkey
{
    /// <summary>Whether v1 could not send <paramref name="hotkey"/> as written.</summary>
    /// <param name="hotkey">The combination text.</param>
    public static bool NeverWorked(string hotkey)
    {
        ArgumentNullException.ThrowIfNull(hotkey);
        if (string.IsNullOrWhiteSpace(hotkey))
        {
            return false;
        }

        foreach (var piece in hotkey.ToLowerInvariant().Split('+'))
        {
            var token = piece.Trim();
            if (token.Length == 0 || token.Any(char.IsWhiteSpace))
            {
                // An empty piece (ctrl++) or two chords in one piece (k ctrl): v1 lost a key.
                return true;
            }

            if (V1KeyTokens.UnsentByV1.Contains(token))
            {
                return true;
            }
        }

        return false;
    }
}
