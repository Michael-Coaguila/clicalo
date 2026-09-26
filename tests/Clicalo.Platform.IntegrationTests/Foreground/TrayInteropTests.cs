using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Clicalo.Platform.Windows.Tray;

namespace Clicalo.Platform.IntegrationTests.Foreground;

/// <summary>
/// The hand-written <c>NOTIFYICONDATAW</c> (CsWin32 cannot generate it for AnyCPU) must match the 64-bit Windows
/// layout byte for byte, or <c>Shell_NotifyIcon</c> rejects it. Headless: nothing is shown.
/// </summary>
public sealed class TrayInteropTests
{
    [Fact]
    [Trait("Req", "BUR-003")]
    public void NotifyIconData_has_the_size_and_offsets_of_the_64_bit_NOTIFYICONDATAW()
    {
        Environment.Is64BitProcess.ShouldBeTrue("Clícalo ships x64 and ARM64 only");
        Unsafe.SizeOf<NotifyIconData>().ShouldBe(ShellNotifyIcon.ExpectedSize);
        Offset(nameof(NotifyIconData.Window)).ShouldBe(8);
        Offset(nameof(NotifyIconData.Icon)).ShouldBe(32);
        Offset(nameof(NotifyIconData.Tip)).ShouldBe(40);
        Offset(nameof(NotifyIconData.State)).ShouldBe(296);
        Offset(nameof(NotifyIconData.TimeoutOrVersion)).ShouldBe(816);
        Offset(nameof(NotifyIconData.Item)).ShouldBe(952);
        Offset(nameof(NotifyIconData.BalloonIcon)).ShouldBe(968);
    }

    [Fact]
    [Trait("Req", "BUR-003")]
    public void A_menu_item_carries_its_localized_text_and_state()
    {
        var item = new TrayMenuItem(3, "Soltar todo", IsEnabled: false);

        item.Id.ShouldBe(3);
        item.Text.ShouldBe("Soltar todo");
        item.IsEnabled.ShouldBeFalse();
        new TrayMenuItem(1, "Salir").IsEnabled.ShouldBeTrue();
    }

    private static int Offset(string field) => (int)Marshal.OffsetOf<NotifyIconData>(field);
}
