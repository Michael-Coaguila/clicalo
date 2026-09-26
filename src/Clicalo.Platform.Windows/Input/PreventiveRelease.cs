using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Core.KeyLedger;
using Windows.Win32;

namespace Clicalo.Platform.Windows.Input;

/// <summary>
/// The preventive release at start-up (SEG-006, blueprint §7.6 «Arranque»): every modifier that
/// <c>GetAsyncKeyState</c> reports down is released, Alt and Win with the menu mask first. It covers the case the
/// guardian cannot: the process died before Sentinel was running, or «End process tree» killed both.
/// </summary>
/// <remarks>
/// Only releases, through the gate, so it never presses anything; a modifier the user holds on a physical keyboard
/// at that instant is released too, which SEG-006 accepts.
/// </remarks>
public static class PreventiveRelease
{
    /// <summary>The modifiers checked, as they are sent: left and right Shift, Ctrl, Alt and Win.</summary>
    public static IReadOnlyList<PhysicalKey> Modifiers { get; } =
    [
        new(0xA0, 0x2A, LedgerKeyAttributes.None),
        new(0xA1, 0x36, LedgerKeyAttributes.None),
        new(0xA2, 0x1D, LedgerKeyAttributes.None),
        new(0xA3, 0x1D, LedgerKeyAttributes.Extended),
        new(0xA4, 0x38, LedgerKeyAttributes.None),
        new(0xA5, 0x38, LedgerKeyAttributes.Extended),
        new(0x5B, 0x5B, LedgerKeyAttributes.Extended),
        new(0x5C, 0x5C, LedgerKeyAttributes.Extended),
    ];

    /// <summary>Releases every modifier the system reports down; returns how many were released.</summary>
    /// <param name="gate">The gate.</param>
    /// <param name="generation">The engine generation.</param>
    public static int Run(InjectionGate gate, ulong generation) =>
        Run(gate, generation, static vk => (PInvoke.GetAsyncKeyState(vk) & 0x8000) != 0);

    /// <summary>Releases every modifier <paramref name="isDown"/> reports; returns how many were released.</summary>
    /// <param name="gate">The gate.</param>
    /// <param name="generation">The engine generation.</param>
    /// <param name="isDown">Whether a virtual key is down.</param>
    public static int Run(InjectionGate gate, ulong generation, Func<int, bool> isDown)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(isDown);
        var batch = new List<LowLevelInput>();
        var released = 0;
        foreach (var key in Modifiers)
        {
            if (!isDown(key.Vk))
            {
                continue;
            }

            if (PhysicalKeyKinds.IsAltOrWin(key))
            {
                batch.Add(LowLevelInput.KeyDown(LedgerRelease.MenuMask));
                batch.Add(LowLevelInput.KeyUp(LedgerRelease.MenuMask));
            }

            batch.Add(LowLevelInput.KeyUp(key));
            released++;
        }

        if (batch.Count > 0)
        {
            gate.TryInject(generation, [.. batch]);
        }

        return released;
    }
}
