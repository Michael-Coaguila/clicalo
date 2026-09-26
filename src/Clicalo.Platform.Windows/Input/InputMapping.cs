using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;
using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Core.KeyLedger;

namespace Clicalo.Platform.Windows.Input;

/// <summary>
/// The Domain's keys and events as Platform.Core's (which depends on the BCL only): the same key, the same mode, so
/// the release recorded by the ledger is the one of the press (INV-12).
/// </summary>
public static class InputMapping
{
    private const ushort VkReturn = 0x0D;
    private const ushort ScanReturn = 0x1C;

    /// <summary>Enter, as a line break of a text is sent (EJE-008).</summary>
    public static PhysicalKey Enter { get; } = new(VkReturn, ScanReturn, LedgerKeyAttributes.None);

    /// <summary>The physical key of a Domain key.</summary>
    /// <param name="key">The key as the engine resolved it.</param>
    public static PhysicalKey ToPhysical(InjectedKey key) =>
        new(
            key.Vk,
            key.Scan,
            (key.Extended ? LedgerKeyAttributes.Extended : LedgerKeyAttributes.None)
                | (
                    key.Mode == InjectionMode.ScanCode
                        ? LedgerKeyAttributes.ScanCodeMode
                        : LedgerKeyAttributes.None
                )
        );

    /// <summary>The Domain key of a physical key.</summary>
    /// <param name="key">The physical key.</param>
    public static InjectedKey ToInjected(PhysicalKey key) =>
        new(
            key.Vk,
            key.Scan,
            (key.Attributes & LedgerKeyAttributes.Extended) != LedgerKeyAttributes.None,
            (key.Attributes & LedgerKeyAttributes.ScanCodeMode) != LedgerKeyAttributes.None
                ? InjectionMode.ScanCode
                : InjectionMode.VirtualKey
        );

    /// <summary>One mouse button as the ledger stores it.</summary>
    /// <param name="button">One button.</param>
    public static LedgerMouseButtons ToLedger(MouseButtons button) =>
        (LedgerMouseButtons)(byte)button;

    /// <summary>How many low-level inputs <paramref name="events"/> become.</summary>
    /// <param name="events">The engine's events.</param>
    public static int CountOf(ReadOnlySpan<InjectedEvent> events)
    {
        var count = 0;
        foreach (var e in events)
        {
            count += e.Kind == InjectedEventKind.MenuMask ? 2 : 1;
        }

        return count;
    }

    /// <summary>
    /// Writes the low-level inputs of <paramref name="events"/> into <paramref name="target"/> (sized with
    /// <see cref="CountOf"/>): the menu mask becomes a press and release of <c>VK 0xE8</c>.
    /// </summary>
    /// <param name="events">The engine's events.</param>
    /// <param name="target">Where to write.</param>
    public static void Fill(ReadOnlySpan<InjectedEvent> events, Span<LowLevelInput> target)
    {
        var at = 0;
        foreach (var e in events)
        {
            switch (e.Kind)
            {
                case InjectedEventKind.KeyDown:
                    target[at++] = LowLevelInput.KeyDown(ToPhysical(e.Key));
                    break;
                case InjectedEventKind.KeyUp:
                    target[at++] = LowLevelInput.KeyUp(ToPhysical(e.Key));
                    break;
                case InjectedEventKind.MenuMask:
                    target[at++] = LowLevelInput.KeyDown(LedgerRelease.MenuMask);
                    target[at++] = LowLevelInput.KeyUp(LedgerRelease.MenuMask);
                    break;
                case InjectedEventKind.MouseDown:
                    target[at++] = LowLevelInput.ButtonDown(ToLedger(e.Button));
                    break;
                case InjectedEventKind.MouseUp:
                    target[at++] = LowLevelInput.ButtonUp(ToLedger(e.Button));
                    break;
            }
        }
    }

    /// <summary>
    /// The low-level inputs of a text (EJE-008): each UTF-16 unit as Unicode, a line break (LF, CR or CR LF) as Enter
    /// pressed and released.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="target">Where to write; at least <c>text.Length * 2</c> long.</param>
    /// <returns>How many inputs were written.</returns>
    public static int FillText(ReadOnlySpan<char> text, Span<LowLevelInput> target)
    {
        var at = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                continue;
            }

            if (c is '\r' or '\n')
            {
                target[at++] = LowLevelInput.KeyDown(Enter);
                target[at++] = LowLevelInput.KeyUp(Enter);
            }
            else
            {
                target[at++] = LowLevelInput.Unicode(c);
            }
        }

        return at;
    }
}
