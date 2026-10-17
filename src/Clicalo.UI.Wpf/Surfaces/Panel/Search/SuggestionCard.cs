using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using Clicalo.Presentation.Panel.Search;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel.Search;

/// <summary>
/// The profile suggestion (PER-009) in direction C (D12): a card of the accent tone, shown as a layer at the foot of the
/// panel's body, over the tiles, without moving them (it no longer pushes the grid). «<b>{app}</b> [suggest]» in
/// <c>bodySmall</c> and the two buttons side by side, <see cref="Space.S2"/> apart: [Crear perfil] primary and [Ahora
/// no] secondary, both bold and 44 high. It shows only while <see cref="SuggestionViewModel.IsVisible"/>.
/// </summary>
/// <remarks>
/// The panel registers <see cref="CreateButton"/> and <see cref="NotNowButton"/> as tap targets of its pointer layer
/// and calls the view model; UI Automation Invoke reaches the same methods through <c>Click</c>.
/// </remarks>
public sealed class SuggestionCard : Border
{
    private readonly SuggestionViewModel _viewModel;
    private readonly Run _app;
    private readonly Run _message;
    private readonly TextBlock _text;
    private readonly Card _card;
    private string _announced = string.Empty;

    /// <summary>Creates the card.</summary>
    /// <param name="viewModel">The suggestion.</param>
    public SuggestionCard(SuggestionViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;

        _app = new Run { FontWeight = FontWeights.Bold };
        _message = new Run();
        var text = new StyledText(string.Empty, TextStyleToken.BodySmall) { MaxLines = 0 };
        _text = text;
        text.Inlines.Add(_app);
        text.Inlines.Add(new Run(" "));
        text.Inlines.Add(_message);
        AutomationProperties.SetLiveSetting(text, AutomationLiveSetting.Polite);

        CreateButton = new TouchButton
        {
            Variant = ButtonVariant.Primary,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Focusable = false,
            IsTabStop = false,
        };
        CreateButton.Click += (_, _) => _viewModel.Create();
        NotNowButton = new TouchButton
        {
            Variant = ButtonVariant.Secondary,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Focusable = false,
            IsTabStop = false,
        };
        NotNowButton.Click += (_, _) => _viewModel.NotNow();

        var buttons = new Grid { Margin = new Thickness(0, Space.S3, 0, 0) };
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Space.S2) });
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(NotNowButton, 2);
        buttons.Children.Add(CreateButton);
        buttons.Children.Add(NotNowButton);

        var layout = new StackPanel();
        layout.Children.Add(text);
        layout.Children.Add(buttons);
        _card = new Card { Tone = CardTone.Accent, Content = layout };
        Child = _card;

        _viewModel.PropertyChanged += OnViewModelChanged;
        Apply();
    }

    /// <summary>[Crear perfil]; the panel registers it as a tap target.</summary>
    public TouchButton CreateButton { get; }

    /// <summary>[Ahora no]; the panel registers it as a tap target.</summary>
    public TouchButton NotNowButton { get; }

    /// <summary>Detaches from the view model when the panel closes.</summary>
    public void Detach() => _viewModel.PropertyChanged -= OnViewModelChanged;

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs change) => Apply();

    private void Apply()
    {
        Visibility = _viewModel.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        _app.Text = _viewModel.AppName;
        _message.Text = _viewModel.Message;
        CreateButton.Content = _viewModel.CreateText;
        NotNowButton.Content = _viewModel.NotNowText;
        AutomationProperties.SetName(_card, _viewModel.AppName + " " + _viewModel.Message);
        Announce();
    }

    /// <summary>
    /// Says the suggestion once when it appears for an app (ACC-001): the live setting alone raises nothing in WPF, and
    /// readers ignore live regions of an inactive window, so it also goes as a notification (as <see cref="LiveAnnouncer"/>).
    /// </summary>
    private void Announce()
    {
        var spoken = _viewModel.IsVisible
            ? _viewModel.AppName + " " + _viewModel.Message
            : string.Empty;
        if (string.Equals(spoken, _announced, StringComparison.Ordinal))
        {
            return;
        }

        _announced = spoken;
        if (spoken.Length == 0)
        {
            return;
        }

        var peer =
            UIElementAutomationPeer.FromElement(_text)
            ?? UIElementAutomationPeer.CreatePeerForElement(_text);
        if (peer is null)
        {
            return;
        }

        peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        peer.RaiseNotificationEvent(
            AutomationNotificationKind.Other,
            AutomationNotificationProcessing.MostRecent,
            LiveAnnouncer.ForUiaBstr(spoken),
            LiveAnnouncer.ForUiaBstr(LiveAnnouncer.ActivityId)
        );
    }
}
