using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Clicalo.Platform.Windows.Tray;

namespace Clicalo.Platform.IntegrationTests.Foreground;

/// <summary>
/// The hand-written <c>NOTIFYICONDATAW</c> (CsWin32 cannot generate it for AnyCPU) must match the 64-bit Windows
/// layout byte for byte, or <c>Shell_NotifyIcon</c> rejects it; the popup menu is built as the model groups it.
/// Headless: nothing is shown.
/// </summary>
public sealed class TrayInteropTests
{
    private const uint ByCommand = 0x0000;
    private const uint ByPosition = 0x0400;
    private const uint Grayed = 0x0001;
    private const uint Separator = 0x0800;

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
        item.StartsGroup.ShouldBeFalse();
        item.IsDefault.ShouldBeFalse();
        new TrayMenuItem(1, "Salir").IsEnabled.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "BUR-003")]
    public void The_menu_draws_a_separator_before_each_group_and_its_default_in_bold()
    {
        TrayMenuItem[] items =
        [
            new(1, "Ocultar panel", IsDefault: true),
            new(4, "Centro de control"),
            new(2, "Soltar todo", IsEnabled: false, StartsGroup: true),
            new(5, "Pausar"),
            new(3, "Salir", StartsGroup: true),
        ];

        var menu = TrayMenuHost.CreateMenu(items);
        try
        {
            var rows = Enumerable.Range(0, GetMenuItemCount(menu)).ToList();
            rows.Select(row => IsSeparator(menu, row))
                .ShouldBe([false, false, true, false, false, true, false]);
            rows.Where(row => !IsSeparator(menu, row))
                .Select(row => GetMenuItemID(menu, row))
                .ShouldBe([1u, 4u, 2u, 5u, 3u]);
            GetMenuDefaultItem(menu, 0, 0)
                .ShouldBe(1u, "a click on the icon shows or hides the panel");
            (GetMenuState(menu, 2, ByCommand) & Grayed).ShouldNotBe(0u, "nothing is held");
        }
        finally
        {
            DestroyMenu(menu).ShouldBeTrue();
        }
    }

    [Fact]
    [Trait("Req", "BUR-003")]
    public void A_menu_never_starts_with_a_separator()
    {
        var menu = TrayMenuHost.CreateMenu([new(3, "Salir", StartsGroup: true)]);
        try
        {
            GetMenuItemCount(menu).ShouldBe(1);
            GetMenuItemID(menu, 0).ShouldBe(3u);
        }
        finally
        {
            DestroyMenu(menu).ShouldBeTrue();
        }
    }

    private static int Offset(string field) => (int)Marshal.OffsetOf<NotifyIconData>(field);

    private static bool IsSeparator(nint menu, int row) =>
        (GetMenuState(menu, (uint)row, ByPosition) & Separator) != 0;

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetMenuItemCount(nint menu);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetMenuItemID(nint menu, int position);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetMenuState(nint menu, uint item, uint flags);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetMenuDefaultItem(nint menu, uint byPosition, uint flags);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(nint menu);
}
