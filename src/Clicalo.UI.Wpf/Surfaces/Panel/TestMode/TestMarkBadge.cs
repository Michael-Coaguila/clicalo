using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.Domain.Primitives;
using Clicalo.Presentation.Panel.TestMode;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel.TestMode;

/// <summary>
/// The test mode mark over a tile (TAC-008, CUA-009, prototype line 214): it covers the tile with its radius of 12 for
/// 700 ms, white ✓ (<c>check</c>, 30 px) on <c>success</c> when the touch counted, ⊘ (<c>block</c>) on <c>danger</c>
/// when the filter ignored it. Its item status says [tmOk], [tShort] or [tDouble], so the state is never color alone.
/// It only projects <see cref="TestModeViewModel"/> for one tile and lets touches pass through.
/// </summary>
/// <remarks>The tile control lays it over its content (same cell, on top).</remarks>
public sealed class TestMarkBadge : Border
{
    private const double GlyphPx = 30;

    private readonly TestModeViewModel _testMode;
    private readonly ShortcutId _shortcut;
    private readonly SymbolIcon _glyph = new()
    {
        Size = GlyphPx,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>Creates the mark of a tile.</summary>
    /// <param name="testMode">Test mode.</param>
    /// <param name="shortcut">The tile's shortcut.</param>
    public TestMarkBadge(TestModeViewModel testMode, ShortcutId shortcut)
    {
        ArgumentNullException.ThrowIfNull(testMode);
        _testMode = testMode;
        _shortcut = shortcut;
        CornerRadius = new CornerRadius(Radii.Tile);
        IsHitTestVisible = false;
        Child = _glyph;
        _testMode.MarkChanged += OnMarkChanged;
        Refresh();
    }

    /// <summary>Stops following test mode when the tile goes away.</summary>
    public void Detach() => _testMode.MarkChanged -= OnMarkChanged;

    private void OnMarkChanged(object? sender, TestMarkChangedEventArgs change)
    {
        if (change.Shortcut == _shortcut)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        if (_testMode.MarkOf(_shortcut) is not { } mark)
        {
            Visibility = Visibility.Collapsed;
            AutomationProperties.SetItemStatus(this, string.Empty);
            return;
        }

        Visibility = Visibility.Visible;
        _glyph.Symbol = TestModeViewModel.MarkIcon(mark);
        SetResourceReference(
            BackgroundProperty,
            ThemeBrushKey.For(mark.Accepted ? ColorToken.Success : ColorToken.Danger)
        );
        _glyph.SetResourceReference(
            SymbolIcon.ForegroundProperty,
            ThemeBrushKey.For(mark.Accepted ? ColorToken.OnSuccess : ColorToken.OnDanger)
        );
        AutomationProperties.SetItemStatus(this, _testMode.MarkText(mark));
    }
}
