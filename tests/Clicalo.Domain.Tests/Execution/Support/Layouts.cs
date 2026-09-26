using System.Collections.Immutable;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Keys;

namespace Clicalo.Domain.Tests.Execution.Support;

/// <summary>Keyboard layout snapshots as the platform captures them (<c>VkKeyScanEx</c> and <c>MapVirtualKeyEx</c>).</summary>
internal static class Layouts
{
    /// <summary>es-ES (0000040A): ñ is its own key, + is unshifted, # needs AltGr.</summary>
    public static KeyboardLayoutSnapshot Spanish { get; } =
        new(
            new KeyboardLayoutId(0x040A_040A),
            ImmutableDictionary<KeyId, LayoutKey>
                .Empty.Add(
                    new KeyId("char:ñ"),
                    new LayoutKey(0xC0, 0x27, false, NeedsShift: false, NeedsAltGr: false)
                )
                .Add(new KeyId("char:+"), new LayoutKey(0xBB, 0x1B, false, false, false))
                .Add(new KeyId("char:#"), new LayoutKey(0x33, 0x04, false, false, NeedsAltGr: true))
                .Add(new KeyId("char:%"), new LayoutKey(0x35, 0x06, false, NeedsShift: true, false))
        );

    /// <summary>en-US (00000409): no ñ, + needs Shift.</summary>
    public static KeyboardLayoutSnapshot English { get; } =
        new(
            new KeyboardLayoutId(0x0409_0409),
            ImmutableDictionary<KeyId, LayoutKey>
                .Empty.Add(
                    new KeyId("char:+"),
                    new LayoutKey(0xBB, 0x0D, false, NeedsShift: true, false)
                )
                .Add(new KeyId("char:#"), new LayoutKey(0x33, 0x04, false, NeedsShift: true, false))
                .Add(new KeyId("char:%"), new LayoutKey(0x35, 0x06, false, NeedsShift: true, false))
        );
}
