using System.Collections.Immutable;

namespace Clicalo.Platform.Core.Injection;

/// <summary>
/// Releases whatever Windows reports down (ADR-0023): every key from <c>0x01</c> to <c>0xFE</c> and every mouse
/// button. The user of Clícalo does not use a physical keyboard, so anything down was pressed by Clícalo. Used by
/// Sentinel after the main process died, by the engine after an exception, by «Soltar todo» of the tray (it works
/// with a hung engine) and by the preventive release of the start (SEG-006, REG-03).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Buttons go first, then the keys, the modifiers last; Alt and Win get the menu mask first (<c>VK 0xE8</c>
/// pressed and released), so neither the Start menu nor a menu bar opens (blueprint §7.7).</item>
/// <item>Keys go up in virtual key mode with the scan code Windows maps, and <c>KEYEVENTF_EXTENDEDKEY</c> when that
/// scan code is extended.</item>
/// <item>The generic Shift, Ctrl and Alt (<c>0x10</c> to <c>0x12</c>) go up only when neither side is down: the side
/// keys cover them.</item>
/// <item>Windows may report the left and right buttons swapped (<c>SM_SWAPBUTTON</c>): either one down releases
/// both, and an extra button up is harmless.</item>
/// </list>
/// </remarks>
public static class PressedInputRelease
{
    private const byte VkLeftButton = 0x01;
    private const byte VkRightButton = 0x02;
    private const byte VkCancel = 0x03;
    private const byte VkMiddleButton = 0x04;
    private const byte VkX1Button = 0x05;
    private const byte VkX2Button = 0x06;
    private const byte VkShift = 0x10;
    private const byte VkControl = 0x11;
    private const byte VkMenu = 0x12;
    private const byte VkLeftWin = 0x5B;
    private const byte VkRightWin = 0x5C;
    private const byte VkLeftShift = 0xA0;
    private const byte VkRightShift = 0xA1;
    private const byte VkLeftControl = 0xA2;
    private const byte VkRightControl = 0xA3;
    private const byte VkLeftMenu = 0xA4;
    private const byte VkRightMenu = 0xA5;
    private const byte LastKey = 0xFE;

    /// <summary>The menu mask key: an unassigned virtual key (<c>0xE8</c>) pressed and released.</summary>
    public static PhysicalKey MenuMask { get; } =
        new(LowLevelInjector.MenuMaskVirtualKey, 0, PhysicalKeyAttributes.None);

    /// <summary>The modifiers, in the order they go up: Shift, Ctrl, then Alt and Win (each after the mask).</summary>
    private static ReadOnlySpan<byte> Modifiers =>
        [
            VkLeftShift,
            VkRightShift,
            VkShift,
            VkLeftControl,
            VkRightControl,
            VkControl,
            VkLeftMenu,
            VkRightMenu,
            VkMenu,
            VkLeftWin,
            VkRightWin,
        ];

    /// <summary>The releases of everything <paramref name="keys"/> reports down; empty when nothing is.</summary>
    /// <param name="keys">The key state.</param>
    public static ImmutableArray<LowLevelInput> BuildBatch(IKeyStateReader keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var batch = ImmutableArray.CreateBuilder<LowLevelInput>();
        if (keys.IsDown(VkLeftButton) || keys.IsDown(VkRightButton))
        {
            batch.Add(LowLevelInput.ButtonUp(LowLevelMouseButtons.Left));
            batch.Add(LowLevelInput.ButtonUp(LowLevelMouseButtons.Right));
        }

        AddButton(batch, keys, VkMiddleButton, LowLevelMouseButtons.Middle);
        AddButton(batch, keys, VkX1Button, LowLevelMouseButtons.X1);
        AddButton(batch, keys, VkX2Button, LowLevelMouseButtons.X2);
        for (int vk = VkCancel; vk <= LastKey; vk++)
        {
            var key = (byte)vk;
            if (
                key is not (VkMiddleButton or VkX1Button or VkX2Button)
                && key != LowLevelInjector.MenuMaskVirtualKey
                && !Modifiers.Contains(key)
                && keys.IsDown(key)
            )
            {
                batch.Add(LowLevelInput.KeyUp(KeyOf(keys, key)));
            }
        }

        foreach (var key in Modifiers)
        {
            if (!keys.IsDown(key) || GenericCoveredBySide(keys, key))
            {
                continue;
            }

            var physical = KeyOf(keys, key);
            if (PhysicalKeyKinds.IsAltOrWin(physical))
            {
                batch.Add(LowLevelInput.KeyDown(MenuMask));
                batch.Add(LowLevelInput.KeyUp(MenuMask));
            }

            batch.Add(LowLevelInput.KeyUp(physical));
        }

        return batch.ToImmutable();
    }

    /// <summary>
    /// Reads the key state and sends its releases in one call; nothing is sent when the state cannot be read (the
    /// secure desktop has the input) or nothing is down.
    /// </summary>
    /// <param name="keys">The key state.</param>
    /// <param name="sender">Sends the releases.</param>
    public static ReleaseOutcome ReleaseOnce(IKeyStateReader keys, ILowLevelSender sender)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(sender);
        if (!keys.CanRead)
        {
            return new ReleaseOutcome(false, 0, default);
        }

        var batch = BuildBatch(keys);
        return batch.IsEmpty
            ? new ReleaseOutcome(true, 0, default)
            : new ReleaseOutcome(true, batch.Length, sender.Send(batch.AsSpan()));
    }

    /// <summary>The key of <paramref name="virtualKey"/> in virtual key mode, extended when its scan code is.</summary>
    /// <param name="keys">The key state, for the scan code.</param>
    /// <param name="virtualKey">The virtual key.</param>
    public static PhysicalKey KeyOf(IKeyStateReader keys, byte virtualKey)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var scan = keys.ScanCode(virtualKey);
        var prefix = scan >> 8;
        return new PhysicalKey(
            virtualKey,
            (ushort)(scan & 0xFF),
            prefix is 0xE0 or 0xE1 ? PhysicalKeyAttributes.Extended : PhysicalKeyAttributes.None
        );
    }

    private static void AddButton(
        ImmutableArray<LowLevelInput>.Builder batch,
        IKeyStateReader keys,
        byte virtualKey,
        LowLevelMouseButtons button
    )
    {
        if (keys.IsDown(virtualKey))
        {
            batch.Add(LowLevelInput.ButtonUp(button));
        }
    }

    private static bool GenericCoveredBySide(IKeyStateReader keys, byte virtualKey) =>
        virtualKey switch
        {
            VkShift => keys.IsDown(VkLeftShift) || keys.IsDown(VkRightShift),
            VkControl => keys.IsDown(VkLeftControl) || keys.IsDown(VkRightControl),
            VkMenu => keys.IsDown(VkLeftMenu) || keys.IsDown(VkRightMenu),
            _ => false,
        };
}
