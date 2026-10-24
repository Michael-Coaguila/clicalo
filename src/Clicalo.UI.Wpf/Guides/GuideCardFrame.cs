using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using Clicalo.Presentation.Guides;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Guides;

/// <summary>
/// The guide of a section inside the Control Center (R-39): the <see cref="GuideCard"/> on a layer of the window, in
/// <c>win</c> with its line, as the guide window beside the panel. UI Automation reads a Group named as the guide. The
/// window places it in the corner away from the control the step points at, so it never covers it.
/// </summary>
public sealed class GuideCardFrame : Border
{
    private readonly GuideViewModel _viewModel;
    private readonly GuideCard _card;

    /// <summary>Creates the frame of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The guide of the section in view.</param>
    public GuideCardFrame(GuideViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        Width = GuideCard.CardWidth;
        CornerRadius = new CornerRadius(Radii.Large);
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Win));
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Line));
        SetResourceReference(BorderThicknessProperty, ThemeScope.BorderThicknessKey);
        _card = new GuideCard(viewModel, focusable: true, () => BorderThickness);
        Child = _card;
        Visibility = viewModel.IsShown ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetName(this, viewModel.Name);
        _viewModel.PropertyChanged += OnChanged;
    }

    /// <summary>The card.</summary>
    public GuideCard Card => _card;

    /// <summary>Stops following the guide (the window is closing).</summary>
    public void Detach()
    {
        _viewModel.PropertyChanged -= OnChanged;
        _card.Detach();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new GroupAutomationPeer(this);

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(GuideViewModel.IsShown):
                Visibility = _viewModel.IsShown ? Visibility.Visible : Visibility.Collapsed;
                break;
            case nameof(GuideViewModel.Name):
                AutomationProperties.SetName(this, _viewModel.Name);
                break;
        }
    }
}
