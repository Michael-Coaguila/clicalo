using System.Collections.Frozen;
using System.Globalization;
using Clicalo.Domain.Keys;

namespace Clicalo.Domain.Migration.V1;

/// <summary>
/// The token table of the v1 combination grammar (catalog §7.3): every v1 token and the catalog key it means. The
/// tokens are compared in lower case; anything else is «Revisar» (MIG-005). The keys come from the generated
/// <see cref="KeyIds"/>, so a key missing from <c>data/catalogs/keys.json</c> does not compile.
/// </summary>
internal static class V1KeyTokens
{
    /// <summary>The minus sign U+2212, accepted as the minus key.</summary>
    public const string MinusSign = "\u2212";

    /// <summary>
    /// Tokens of the table that Macro Quick Access could not send (its <c>pyautogui</c> key names do not include them),
    /// used to spot the shortcuts that never worked in v1 (catalog §7.5, PQ-40).
    /// </summary>
    public static FrozenSet<string> UnsentByV1 { get; } =
        new[] { "num+", "num-", "num*", "num/", "num" + MinusSign, MinusSign, "ñ" }.ToFrozenSet(
            StringComparer.Ordinal
        );

    /// <summary>The table, token → stroke (a modifier with its side, or a key).</summary>
    public static FrozenDictionary<string, KeyStroke> Table { get; } = Build();

    /// <summary>The catalog key of a v1 token, already in lower case.</summary>
    /// <param name="token">The token, trimmed and in lower case.</param>
    /// <param name="stroke">The key and, for a modifier, its side.</param>
    public static bool TryResolve(string token, out KeyStroke stroke) =>
        Table.TryGetValue(token, out stroke);

    private static FrozenDictionary<string, KeyStroke> Build()
    {
        var table = new Dictionary<string, KeyStroke>(StringComparer.Ordinal);

        // Modifiers and their sides; «win» and «winleft» are both the generic Win key (catalog §7.3).
        Add(table, KeyIds.Ctrl, KeySide.Any, "ctrl");
        Add(table, KeyIds.Ctrl, KeySide.Left, "ctrlleft");
        Add(table, KeyIds.Ctrl, KeySide.Right, "ctrlright");
        Add(table, KeyIds.Alt, KeySide.Any, "alt");
        Add(table, KeyIds.Alt, KeySide.Left, "altleft");
        Add(table, KeyIds.Alt, KeySide.Right, "altright");
        Add(table, KeyIds.Shift, KeySide.Any, "shift");
        Add(table, KeyIds.Shift, KeySide.Left, "shiftleft");
        Add(table, KeyIds.Shift, KeySide.Right, "shiftright");
        Add(table, KeyIds.Win, KeySide.Any, "win", "winleft");
        Add(table, KeyIds.Win, KeySide.Right, "winright");

        // Letters, Ñ and digits.
        KeyId[] letters =
        [
            KeyIds.A,
            KeyIds.B,
            KeyIds.C,
            KeyIds.D,
            KeyIds.E,
            KeyIds.F,
            KeyIds.G,
            KeyIds.H,
            KeyIds.I,
            KeyIds.J,
            KeyIds.K,
            KeyIds.L,
            KeyIds.M,
            KeyIds.N,
            KeyIds.O,
            KeyIds.P,
            KeyIds.Q,
            KeyIds.R,
            KeyIds.S,
            KeyIds.T,
            KeyIds.U,
            KeyIds.V,
            KeyIds.W,
            KeyIds.X,
            KeyIds.Y,
            KeyIds.Z,
        ];
        foreach (var letter in letters)
        {
            Add(table, letter, letter.Value);
        }

        Add(table, KeyIds.NTilde, "ñ");
        KeyId[] digits =
        [
            KeyIds.D0,
            KeyIds.D1,
            KeyIds.D2,
            KeyIds.D3,
            KeyIds.D4,
            KeyIds.D5,
            KeyIds.D6,
            KeyIds.D7,
            KeyIds.D8,
            KeyIds.D9,
        ];
        foreach (var digit in digits)
        {
            Add(table, digit, digit.Value);
        }

        // Function keys F1 to F24.
        KeyId[] functions =
        [
            KeyIds.F1,
            KeyIds.F2,
            KeyIds.F3,
            KeyIds.F4,
            KeyIds.F5,
            KeyIds.F6,
            KeyIds.F7,
            KeyIds.F8,
            KeyIds.F9,
            KeyIds.F10,
            KeyIds.F11,
            KeyIds.F12,
            KeyIds.F13,
            KeyIds.F14,
            KeyIds.F15,
            KeyIds.F16,
            KeyIds.F17,
            KeyIds.F18,
            KeyIds.F19,
            KeyIds.F20,
            KeyIds.F21,
            KeyIds.F22,
            KeyIds.F23,
            KeyIds.F24,
        ];
        foreach (var function in functions)
        {
            Add(table, function, function.Value);
        }

        // Editing and navigation.
        Add(table, KeyIds.Tab, "tab");
        Add(table, KeyIds.Enter, "enter", "return");
        Add(table, KeyIds.Escape, "esc", "escape");
        Add(table, KeyIds.Space, "space");
        Add(table, KeyIds.Delete, "delete", "del");
        Add(table, KeyIds.Backspace, "backspace");
        Add(table, KeyIds.Insert, "insert");
        Add(table, KeyIds.Home, "home");
        Add(table, KeyIds.End, "end");
        Add(table, KeyIds.PageUp, "pageup", "pgup");
        Add(table, KeyIds.PageDown, "pagedown", "pgdn");
        Add(table, KeyIds.Left, "left");
        Add(table, KeyIds.Right, "right");
        Add(table, KeyIds.Up, "up");
        Add(table, KeyIds.Down, "down");
        Add(table, KeyIds.PrintScreen, "printscreen", "prtsc");
        Add(table, KeyIds.Pause, "pause");
        Add(table, KeyIds.CapsLock, "capslock");
        Add(table, KeyIds.NumLock, "numlock");
        Add(table, KeyIds.ScrollLock, "scrolllock");
        Add(table, KeyIds.Menu, "apps");

        // Symbols: «+» as a key comes out of the grammar (ctrl++), U+2212 is the minus key (MIG-003).
        Add(table, KeyIds.Plus, "+", "plus");
        Add(table, KeyIds.Minus, "-", "minus", MinusSign);
        Add(table, KeyIds.EqualsSign, "=");
        Add(table, KeyIds.Comma, ",");
        Add(table, KeyIds.Period, ".");
        Add(table, KeyIds.Semicolon, ";");
        Add(table, KeyIds.Slash, "/", "slash");

        // The keys added to the catalog for the migration (MIG-005).
        Add(table, KeyIds.Grave, "grave", "`");
        Add(table, KeyIds.Backslash, "backslash", "\\");
        Add(table, KeyIds.LeftBracket, "[");
        Add(table, KeyIds.RightBracket, "]");
        Add(table, KeyIds.Apostrophe, "'");
        Add(table, KeyIds.Hash, "#");

        // Numeric keypad: «num+» and «num-» are single tokens of the grammar.
        KeyId[] keypad =
        [
            KeyIds.Num0,
            KeyIds.Num1,
            KeyIds.Num2,
            KeyIds.Num3,
            KeyIds.Num4,
            KeyIds.Num5,
            KeyIds.Num6,
            KeyIds.Num7,
            KeyIds.Num8,
            KeyIds.Num9,
        ];
        for (var i = 0; i < keypad.Length; i++)
        {
            Add(table, keypad[i], "num" + i.ToString(CultureInfo.InvariantCulture));
        }

        Add(table, KeyIds.NumAdd, "num+", "add");
        Add(table, KeyIds.NumSubtract, "num-", "subtract", "num" + MinusSign);
        Add(table, KeyIds.NumMultiply, "num*", "multiply");
        Add(table, KeyIds.NumDivide, "num/", "divide");
        Add(table, KeyIds.NumDecimal, "decimal");

        // Media.
        Add(table, KeyIds.VolumeUp, "volumeup");
        Add(table, KeyIds.VolumeDown, "volumedown");
        Add(table, KeyIds.VolumeMute, "volumemute");
        Add(table, KeyIds.MediaPlayPause, "playpause");
        Add(table, KeyIds.MediaNext, "nexttrack");
        Add(table, KeyIds.MediaPrevious, "prevtrack");
        Add(table, KeyIds.MediaStop, "stop");

        return table.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static void Add(
        Dictionary<string, KeyStroke> table,
        KeyId key,
        params ReadOnlySpan<string> tokens
    ) => Add(table, key, KeySide.Any, tokens);

    private static void Add(
        Dictionary<string, KeyStroke> table,
        KeyId key,
        KeySide side,
        params ReadOnlySpan<string> tokens
    )
    {
        foreach (var token in tokens)
        {
            table.Add(token, new KeyStroke(key, side));
        }
    }
}
