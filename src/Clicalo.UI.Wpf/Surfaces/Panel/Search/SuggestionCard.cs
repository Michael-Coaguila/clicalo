using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using Clicalo.Presentation.Panel.Search;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;

namespace Clicalo.UI.Wpf.Surfaces.Panel.Search;

/// <summary>
/// The suggestion card of the panel (PER-009, docs/04 §6): accentWash with an accent outline, «<b>{app}</b>
/// [suggest]» and the buttons [Crear perfil] (main) and [Ahora no] side by side, 40 px drawn inside 44 px targets
/// (REG-02). Margin 0 12 10 12 and padding 10 12 as the prototype. It shows only while
/// <see cref="SuggestionViewModel.IsVisible"/>.
/// </summary>
/// <remarks>
/// The panel registers <see cref="CreateButton"/> and <see cref="NotNowButton"/> as tap targets of its pointer layer
/// and calls the view model; UI Automation Invoke reaches the same methods through <c>Click</c>.
/// </remarks>
public sealed class SuggestionCard : Border
{
    /// <summary>Drawn height of the two buttons (prototype: 40 px).</summary>
    public const double ButtonHeight = 40;

    private readonly SuggestionViewModel _viewModel;
    private readonly Run _app;
    private readonly Run _message;
    private readonly Card _card;

    /// <summary>Creates the card.</summary>
    /// <param name="viewModel">The suggestion.</param>
    public SuggestionCard(SuggestionViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        Margin = new Thickness(12, 0, 12, 10);

        _app = new Run { FontWeight = FontWeights.Bold };
        _message = new Run();
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap, LineHeight = 19 };
        text.Inlines.Add(_app);
        text.Inlines.Add(new Run(" "));
        text.Inlines.Add(_message);
        text.SetResourceReference(TextBlock.FontFamilyProperty, ThemeKeys.UiFont);
        text.SetResourceReference(TextBlock.FontSizeProperty, ThemeKeys.TextSize(14));
        AutomationProperties.SetLiveSetting(text, AutomationLiveSetting.Polite);

        CreateButton = new TouchButton
        {
            Appearance = ButtonAppearance.Accent,
            Height = ButtonHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Focusable = false,
            IsTabStop = false,
        };
        CreateButton.Click += (_, _) => _viewModel.Create();
        NotNowButton = new TouchButton
        {
            Appearance = ButtonAppearance.Outline,
            Height = ButtonHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FontWeight = FontWeights.Normal,
            Focusable = false,
            IsTabStop = false,
        };
        NotNowButton.Click += (_, _) => _viewModel.NotNow();

        var buttons = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(NotNowButton, 2);
        buttons.Children.Add(CreateButton);
        buttons.Children.Add(NotNowButton);

        var layout = new StackPanel();
        layout.Children.Add(text);
        layout.Children.Add(buttons);
        _card = new Card
        {
            Tone = CardTone.Accent,
            Padding = new Thickness(12, 10, 12, 10),
            Content = layout,
        };
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
    }
}
