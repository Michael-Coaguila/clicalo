namespace Clicalo.TestKit.Windows.Input;

/// <summary>Builders for balanced batches: every press is released within the same batch.</summary>
public static class KeyStrokes
{
    /// <summary>
    /// Presses <paramref name="keys"/> in the given order and releases them in reverse order (EJE-003), in
    /// virtual-key mode.
    /// </summary>
    public static IReadOnlyList<KeyStroke> Chord(params ReadOnlySpan<VirtualKeyCode> keys)
    {
        if (keys.IsEmpty)
        {
            throw new ArgumentException("A chord needs at least one key.", nameof(keys));
        }

        var strokes = new List<KeyStroke>(keys.Length * 2);
        foreach (var key in keys)
        {
            strokes.Add(KeyStroke.Press(key));
        }

        for (var i = keys.Length - 1; i >= 0; i--)
        {
            strokes.Add(KeyStroke.Release(keys[i]));
        }

        return strokes;
    }

    /// <summary>Presses and releases one key identified by its scan code (compatible mode, <c>wVk = 0</c>).</summary>
    public static IReadOnlyList<KeyStroke> ScanCodeTap(ushort scanCode, bool extended) =>
        [
            KeyStroke.PressScanCode(scanCode, extended),
            KeyStroke.ReleaseScanCode(scanCode, extended),
        ];

    /// <summary>
    /// Types <paramref name="text"/> with <c>KEYEVENTF_UNICODE</c>, one press and release per UTF-16 code unit
    /// (a character outside the BMP, such as an emoji, is its two surrogates in order).
    /// </summary>
    public static IReadOnlyList<KeyStroke> UnicodeText(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        var strokes = new List<KeyStroke>(text.Length * 2);
        foreach (var unit in text)
        {
            strokes.Add(KeyStroke.PressUnicode(unit));
            strokes.Add(KeyStroke.ReleaseUnicode(unit));
        }

        return strokes;
    }
}
