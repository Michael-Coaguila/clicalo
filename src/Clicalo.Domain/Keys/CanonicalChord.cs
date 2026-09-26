using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Keys;

/// <summary>
/// The canonical key of a combination (REP-001): modifiers as a set with their side, main keys by canonical identity
/// and in order (aliases are already one <see cref="KeyId"/>). Used for repeated shortcuts, «already added», «already
/// there» and blocked combinations; persisted in <c>dupIgnored</c> through <see cref="ToStableString"/>.
/// </summary>
/// <remarks>
/// Main keys are compared without distinguishing case (docs/03 §9): <see cref="TryFrom"/> lowers them with the
/// invariant culture, so <c>char:Ñ</c> and <c>char:ñ</c> are one key.
/// </remarks>
/// <param name="Modifiers">The modifiers as a set.</param>
/// <param name="Main">The non-modifier keys, in order.</param>
public readonly record struct CanonicalChord(ChordModifiers Modifiers, ValueList<KeyId> Main)
{
    private const char Separator = '+';
    private const char Escape = '%';

    /// <summary>The modifier tokens of <see cref="ToStableString"/>, in their fixed order.</summary>
    private static readonly (ChordModifiers Flag, string Token)[] ModifierTokens =
    [
        (ChordModifiers.Ctrl, "ctrl"),
        (ChordModifiers.LeftCtrl, "lctrl"),
        (ChordModifiers.RightCtrl, "rctrl"),
        (ChordModifiers.Alt, "alt"),
        (ChordModifiers.LeftAlt, "lalt"),
        (ChordModifiers.RightAlt, "altgr"),
        (ChordModifiers.Shift, "shift"),
        (ChordModifiers.LeftShift, "lshift"),
        (ChordModifiers.RightShift, "rshift"),
        (ChordModifiers.Win, "win"),
        (ChordModifiers.LeftWin, "lwin"),
        (ChordModifiers.RightWin, "rwin"),
    ];

    /// <summary>
    /// The canonical key of <paramref name="chord"/>, or <see langword="false"/> when it has no key at all (only
    /// shortcuts with at least one key have a key, REP-001).
    /// </summary>
    /// <param name="chord">A normalized chord.</param>
    /// <param name="canonical">The canonical key.</param>
    public static bool TryFrom(KeyChord chord, out CanonicalChord canonical)
    {
        ArgumentNullException.ThrowIfNull(chord);
        if (chord.IsEmpty)
        {
            canonical = default;
            return false;
        }

        var modifiers = ChordModifiers.None;
        var main = ImmutableArray.CreateBuilder<KeyId>();
        foreach (var stroke in chord.Strokes)
        {
            if (
                KeyDefinitions.TryGet(stroke.Key, out var definition)
                && definition.Modifier is { } kind
            )
            {
                var side = definition.Side != KeySide.Any ? definition.Side : stroke.Side;
                modifiers |= FlagOf(kind, side);
                continue;
            }

            var key = new KeyId(stroke.Key.Value.ToLowerInvariant());
            if (!main.Contains(key))
            {
                main.Add(key);
            }
        }

        canonical = new CanonicalChord(modifiers, new ValueList<KeyId>(main.ToImmutable()));
        return true;
    }

    /// <summary>
    /// Parses the text written by <see cref="ToStableString"/>; any other spelling of the same key is refused, so a
    /// persisted key has exactly one text.
    /// </summary>
    /// <param name="text">The persisted text.</param>
    /// <param name="canonical">The canonical key.</param>
    public static bool TryParse(string text, out CanonicalChord canonical)
    {
        canonical = default;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var modifiers = ChordModifiers.None;
        var lastModifier = -1;
        var main = ImmutableArray.CreateBuilder<KeyId>();
        foreach (var token in text.Split(Separator))
        {
            if (token.Length == 0)
            {
                return false;
            }

            var modifier = ModifierIndex(token);
            if (modifier >= 0)
            {
                // Modifiers come first, once each and in the fixed order, so every key has one spelling.
                if (main.Count > 0 || modifier <= lastModifier)
                {
                    return false;
                }

                modifiers |= ModifierTokens[modifier].Flag;
                lastModifier = modifier;
                continue;
            }

            if (!TryUnescape(token, out var value))
            {
                return false;
            }

            var key = new KeyId(value.ToLowerInvariant());
            if (main.Contains(key))
            {
                return false;
            }

            main.Add(key);
        }

        var parsed = new CanonicalChord(modifiers, new ValueList<KeyId>(main.ToImmutable()));

        // One spelling per key: an upper-case key or an escape of a character that needs none is refused.
        if (!string.Equals(parsed.ToStableString(), text, StringComparison.Ordinal))
        {
            return false;
        }

        canonical = parsed;
        return true;
    }

    /// <summary>
    /// A stable, culture-independent text (<c>ctrl+shift+s</c>, modifiers first in a fixed order) that round-trips
    /// through <see cref="TryParse"/>. It is a persisted format (<c>dupIgnored</c>, ADR-0007, ADR-0018 point 4).
    /// </summary>
    /// <remarks>
    /// Grammar: tokens joined by <c>+</c>. First the modifiers, each at most once and in this order: <c>ctrl</c>,
    /// <c>lctrl</c>, <c>rctrl</c>, <c>alt</c>, <c>lalt</c>, <c>altgr</c>, <c>shift</c>, <c>lshift</c>, <c>rshift</c>,
    /// <c>win</c>, <c>lwin</c>, <c>rwin</c>. Then the main keys in order, as their <see cref="KeyId"/> in lower case,
    /// with <c>%</c> written <c>%25</c> and <c>+</c> written <c>%2B</c> (<c>ctrl+char:%2B</c>); a main key whose text
    /// equals a modifier token has its first character escaped the same way (<c>%6Cwin</c>), so a token is a modifier
    /// exactly when it is written as one. Escapes are <c>%</c> and two upper-case hexadecimal digits of an ASCII
    /// character.
    /// </remarks>
    public string ToStableString()
    {
        var builder = new StringBuilder();
        foreach (var (flag, token) in ModifierTokens)
        {
            if ((Modifiers & flag) != ChordModifiers.None)
            {
                Append(builder, token);
            }
        }

        foreach (var key in Main)
        {
            Append(builder, EscapeKey(key.Value ?? string.Empty));
        }

        return builder.ToString();
    }

    /// <summary>
    /// The form compared with blocked and special combinations: every modifier counts as its family whatever its side,
    /// because Windows reserves them on both sides (catalog R-08: right Ctrl+Alt+Supr is as reserved as Ctrl+Alt+Supr,
    /// and AltGr+Supr with Ctrl too). So «Ctrl» without side also equals left Ctrl here (REP-001), unlike the comparison
    /// of repeated shortcuts, which keeps the side.
    /// </summary>
    public CanonicalChord ForBlockedComparison()
    {
        var modifiers = Modifiers;
        modifiers = ToFamily(
            modifiers,
            ChordModifiers.Ctrl,
            ChordModifiers.LeftCtrl,
            ChordModifiers.RightCtrl
        );
        modifiers = ToFamily(
            modifiers,
            ChordModifiers.Alt,
            ChordModifiers.LeftAlt,
            ChordModifiers.RightAlt
        );
        modifiers = ToFamily(
            modifiers,
            ChordModifiers.Shift,
            ChordModifiers.LeftShift,
            ChordModifiers.RightShift
        );
        modifiers = ToFamily(
            modifiers,
            ChordModifiers.Win,
            ChordModifiers.LeftWin,
            ChordModifiers.RightWin
        );
        return this with { Modifiers = modifiers };
    }

    private static ChordModifiers ToFamily(
        ChordModifiers modifiers,
        ChordModifiers any,
        ChordModifiers left,
        ChordModifiers right
    )
    {
        var sided = left | right;
        return (modifiers & sided) == ChordModifiers.None ? modifiers : (modifiers & ~sided) | any;
    }

    private static ChordModifiers FlagOf(ModifierKind kind, KeySide side)
    {
        var shift = side switch
        {
            KeySide.Left => 1,
            KeySide.Right => 2,
            _ => 0,
        };
        var family = kind switch
        {
            ModifierKind.Ctrl => ChordModifiers.Ctrl,
            ModifierKind.Alt => ChordModifiers.Alt,
            ModifierKind.Shift => ChordModifiers.Shift,
            _ => ChordModifiers.Win,
        };
        return (ChordModifiers)((int)family << shift);
    }

    private static int ModifierIndex(string token)
    {
        for (var i = 0; i < ModifierTokens.Length; i++)
        {
            if (string.Equals(ModifierTokens[i].Token, token, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static void Append(StringBuilder builder, string token)
    {
        if (builder.Length > 0)
        {
            builder.Append(Separator);
        }

        builder.Append(token);
    }

    private static string EscapeKey(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character is Separator or Escape)
            {
                AppendEscaped(builder, character);
            }
            else
            {
                builder.Append(character);
            }
        }

        var escaped = builder.ToString();
        if (ModifierIndex(escaped) >= 0)
        {
            var first = new StringBuilder();
            AppendEscaped(first, escaped[0]);
            escaped = first.Append(escaped, 1, escaped.Length - 1).ToString();
        }

        return escaped;
    }

    private static void AppendEscaped(StringBuilder builder, char character) =>
        builder
            .Append(Escape)
            .Append(((int)character).ToString("X2", CultureInfo.InvariantCulture));

    private static bool TryUnescape(string token, out string value)
    {
        var builder = new StringBuilder(token.Length);
        for (var i = 0; i < token.Length; i++)
        {
            var character = token[i];
            if (character != Escape)
            {
                builder.Append(character);
                continue;
            }

            if (i + 2 >= token.Length || !IsUpperHex(token[i + 1]) || !IsUpperHex(token[i + 2]))
            {
                value = string.Empty;
                return false;
            }

            var code = int.Parse(
                token.AsSpan(i + 1, 2),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture
            );
            if (code > 0x7F)
            {
                value = string.Empty;
                return false;
            }

            builder.Append((char)code);
            i += 2;
        }

        value = builder.ToString();
        return value.Length > 0;
    }

    private static bool IsUpperHex(char character) =>
        character is >= '0' and <= '9' or >= 'A' and <= 'F';
}
