using System.Globalization;
using System.Runtime.InteropServices;
using Clicalo.TestKit.Windows.Input;

namespace Clicalo.Tools.SpikeLab.Input;

/// <summary>
/// A shortcut of the laboratory: left modifiers plus one ordinary key. <see cref="ToBatch"/> always produces a
/// balanced batch (every press released in the same batch, modifiers first and released last).
/// </summary>
/// <param name="Modifiers">Left modifiers held around the key.</param>
/// <param name="Key">The key; never a modifier key.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct LabChord(LabModifiers Modifiers, VirtualKeyCode Key)
{
    private const ushort F24 = 0x87;
    private const ushort Home = 0x24;
    private const ushort End = 0x23;

    /// <summary>The order in which modifiers are pressed (and released in reverse).</summary>
    private static readonly (LabModifiers Modifier, VirtualKeyCode Key)[] ModifierKeys =
    [
        (LabModifiers.Control, VirtualKeyCode.LeftControl),
        (LabModifiers.Alt, VirtualKeyCode.LeftMenu),
        (LabModifiers.Shift, VirtualKeyCode.LeftShift),
        (LabModifiers.Windows, VirtualKeyCode.LeftWindows),
    ];

    /// <summary>Virtual key of F24, the key of the reserved rights chord (blueprint §3.6).</summary>
    public static VirtualKeyCode F24Key => (VirtualKeyCode)F24;

    /// <summary>Virtual key of Home.</summary>
    public static VirtualKeyCode HomeKey => (VirtualKeyCode)Home;

    /// <summary>Virtual key of End.</summary>
    public static VirtualKeyCode EndKey => (VirtualKeyCode)End;

    /// <summary>Ctrl+<paramref name="key"/>.</summary>
    public static LabChord Ctrl(VirtualKeyCode key) => Of(LabModifiers.Control, key);

    /// <summary>
    /// Creates a chord, refusing modifier keys as the key (a chord of modifiers alone could be read as AltGr or leave
    /// a modifier meaning behind).
    /// </summary>
    public static LabChord Of(LabModifiers modifiers, VirtualKeyCode key)
    {
        if (IsModifierKey(key) || key == VirtualKeyCode.None)
        {
            throw new ArgumentException(
                "A lab chord needs an ordinary key; modifiers go in the modifier flags.",
                nameof(key)
            );
        }

        return new LabChord(modifiers, key);
    }

    /// <summary>True for Shift, Ctrl, Alt and Windows keys, generic or side-specific.</summary>
    public static bool IsModifierKey(VirtualKeyCode key) =>
        key
            is VirtualKeyCode.Shift
                or VirtualKeyCode.Control
                or VirtualKeyCode.Menu
                or VirtualKeyCode.LeftShift
                or VirtualKeyCode.RightShift
                or VirtualKeyCode.LeftControl
                or VirtualKeyCode.RightControl
                or VirtualKeyCode.LeftMenu
                or VirtualKeyCode.RightMenu
                or VirtualKeyCode.LeftWindows
                or VirtualKeyCode.RightWindows;

    /// <summary>The same chord with <paramref name="extra"/> modifiers added (latched Shift or Ctrl).</summary>
    public LabChord With(LabModifiers extra) => this with { Modifiers = Modifiers | extra };

    /// <summary>
    /// The balanced batch: left modifiers pressed in a fixed order, the key pressed and released, the modifiers
    /// released in reverse order. Only left-hand modifier keys ever appear.
    /// </summary>
    public IReadOnlyList<KeyStroke> ToBatch()
    {
        if (IsModifierKey(Key) || Key == VirtualKeyCode.None)
        {
            throw new InvalidOperationException("A lab chord needs an ordinary key.");
        }

        var keys = new List<VirtualKeyCode>(ModifierKeys.Length + 1);
        foreach (var (modifier, key) in ModifierKeys)
        {
            if (Modifiers.HasFlag(modifier))
            {
                keys.Add(key);
            }
        }

        keys.Add(Key);
        return KeyStrokes.Chord([.. keys]);
    }

    /// <summary>The chord as the maintainer reads it («Ctrl+Mayús+B»).</summary>
    public string Describe()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(LabModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(LabModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(LabModifiers.Shift))
        {
            parts.Add("Mayús");
        }

        if (Modifiers.HasFlag(LabModifiers.Windows))
        {
            parts.Add("Windows");
        }

        parts.Add(KeyName(Key));
        return string.Join('+', parts);
    }

    private static string KeyName(VirtualKeyCode key) =>
        (ushort)key switch
        {
            F24 => "F24",
            Home => "Inicio",
            End => "Fin",
            >= (ushort)VirtualKeyCode.A and <= (ushort)VirtualKeyCode.Z => new string((char)key, 1),
            _ => "0x" + ((ushort)key).ToString("X2", CultureInfo.InvariantCulture),
        };
}
