using Clicalo.Platform.Windows.Tray;

namespace Clicalo.Platform.IntegrationTests.Foreground;

/// <summary>
/// The tray shows the icon of Clícalo, not the stock icon of Windows, and shows it at 55 % while the panel is hidden
/// or Clícalo is paused (BUR-003, BUR-004). Headless: no icon is added to the notification area.
/// </summary>
public sealed class TrayIconImagesTests
{
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    [InlineData(false, true, true)]
    [Trait("Req", "BUR-003")]
    [Trait("Req", "BUR-004")]
    public void The_icon_is_dimmed_while_the_panel_is_hidden_or_Clicalo_is_paused(
        bool panelVisible,
        bool paused,
        bool dimmed
    )
    {
        var state = new TrayState(panelVisible, AnythingHeld: false, paused);

        TrayMenuModel.IsDimmed(state).ShouldBe(dimmed);

        // The look changes exactly when the accessible text does.
        (TrayMenuModel.Tooltip(state) != Clicalo.Domain.Messages.L.AppName).ShouldBe(dimmed);
    }

    [Theory]
    [InlineData(16, 16)]
    [InlineData(20, 20)]
    [InlineData(24, 24)]
    [InlineData(32, 32)]
    [InlineData(18, 20)]
    [InlineData(1, 16)]
    [Trait("Req", "BUR-003")]
    public void Both_icons_carry_the_image_of_every_size_of_the_notification_area(
        int wanted,
        int chosen
    )
    {
        foreach (
            var resource in new[] { TrayIconImages.NormalResource, TrayIconImages.DimResource }
        )
        {
            var file = TrayIconImages.Read(resource);

            file.ShouldNotBeEmpty();
            var entry = IconFileEntry.Best(file, wanted).ShouldNotBeNull();
            entry.Size.ShouldBe(chosen);
            (entry.Offset + entry.Length).ShouldBeLessThanOrEqualTo(file.Length);
        }
    }

    [Fact]
    [Trait("Req", "BUR-003")]
    public void A_size_larger_than_every_image_takes_the_largest_one()
    {
        IconFileEntry
            .Best(TrayIconImages.Read(TrayIconImages.NormalResource), 1000)
            .ShouldNotBeNull()
            .Size.ShouldBe(256);
        IconFileEntry
            .Best(TrayIconImages.Read(TrayIconImages.DimResource), 1000)
            .ShouldNotBeNull()
            .Size.ShouldBe(64);
    }

    [Fact]
    [Trait("Req", "BUR-003")]
    public void Bytes_that_are_not_an_icon_file_give_no_image()
    {
        IconFileEntry.Best([], 16).ShouldBeNull();
        IconFileEntry.Best("not an icon file"u8, 16).ShouldBeNull();

        // A directory that points past the end of the file.
        byte[] cut = [0, 0, 1, 0, 1, 0, 16, 16, 0, 0, 1, 0, 32, 0, 0, 4, 0, 0, 22, 0, 0, 0];
        IconFileEntry.Best(cut, 16).ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "BUR-003")]
    [Trait("Req", "BUR-004")]
    public void Windows_makes_an_icon_of_each_image_and_neither_is_the_stock_one()
    {
        using var images = new TrayIconImages();

        var normal = images.Handle(dimmed: false);
        var dim = images.Handle(dimmed: true);

        normal.ShouldNotBe(0);
        dim.ShouldNotBe(0);

        // The stock icon of Windows, the fallback, would be one and the same handle for both.
        dim.ShouldNotBe(normal);
        images.Handle(dimmed: false).ShouldBe(normal);
    }
}
