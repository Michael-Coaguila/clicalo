using Clicalo.Domain.Keys;

namespace Clicalo.Infrastructure.Persistence.Mappers;

/// <summary>
/// A persisted key stroke: the canonical key id (<c>ctrl</c>, <c>a</c>, <c>num.add</c>, <c>char:ñ</c>), with
/// <c>@left</c> or <c>@right</c> when the stroke names a side (EDI-009). A key id never contains <c>@</c> followed by a
/// word (a character key is <c>char:</c> and one character), so the suffix is unambiguous.
/// </summary>
internal static class KeyStrokeFormat
{
    private const string LeftSuffix = "@left";
    private const string RightSuffix = "@right";

    /// <summary>The persisted text of <paramref name="stroke"/>.</summary>
    /// <param name="stroke">A stroke.</param>
    public static string Format(KeyStroke stroke) =>
        stroke.Side switch
        {
            KeySide.Left => stroke.Key.Value + LeftSuffix,
            KeySide.Right => stroke.Key.Value + RightSuffix,
            _ => stroke.Key.Value,
        };

    /// <summary>Reads a persisted stroke; the key id itself is validated by <c>KeyChord.Create</c>.</summary>
    /// <param name="text">The persisted text.</param>
    /// <param name="stroke">The stroke.</param>
    public static bool TryParse(string? text, out KeyStroke stroke)
    {
        stroke = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var side = KeySide.Any;
        var key = text;
        if (text.Length > LeftSuffix.Length && text.EndsWith(LeftSuffix, StringComparison.Ordinal))
        {
            side = KeySide.Left;
            key = text[..^LeftSuffix.Length];
        }
        else if (
            text.Length > RightSuffix.Length
            && text.EndsWith(RightSuffix, StringComparison.Ordinal)
        )
        {
            side = KeySide.Right;
            key = text[..^RightSuffix.Length];
        }

        stroke = new KeyStroke(new KeyId(key), side);
        return true;
    }
}
