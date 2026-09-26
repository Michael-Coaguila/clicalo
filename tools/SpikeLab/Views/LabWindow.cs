using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Clicalo.Tools.SpikeLab.Composition;
using Clicalo.Tools.SpikeLab.Scripting;
using Clicalo.Tools.SpikeLab.Session;
using Clicalo.UI.Wpf.Automation;

namespace Clicalo.Tools.SpikeLab.Views;

/// <summary>
/// The control window of the laboratory: a normal, ACTIVATABLE window to prepare the cycles (choose the spike, show
/// the surfaces, open the probe, switch voice numbers and key sending), to open the reports and to see which real
/// pieces already work (the ones that are not ready are summed up next to the buttons). Touching it takes the
/// foreground, so during a cycle the maintainer uses the guide strip instead; its buttons have names of their own, so a
/// voice command never has to choose between this window and a surface. It opens centered, never larger than the work
/// area, and its scroll bar is a 44-pixel touch target (WPF's touch panning is off in the laboratory).
/// </summary>
internal sealed class LabWindow : Window
{
    /// <summary>Width of the vertical scroll bar, in logical pixels (REG-02).</summary>
    public const double ScrollBarWidth = 44;

    private readonly LabApp _app;
    private readonly StackPanel _chooser = new() { Margin = new Thickness(0, 8, 0, 8) };
    private readonly StackPanel _controls = new() { Visibility = Visibility.Collapsed };
    private readonly TextBlock _spikeLine = Line(18, FontWeights.SemiBold);
    private readonly TextBlock _reportLine = Line(15, FontWeights.Normal);
    private readonly TextBlock _piecesSummary = Line(16, FontWeights.SemiBold);
    private readonly StackPanel _components = new();
    private readonly TextBlock _refusal = Line(16, FontWeights.SemiBold);
    private readonly CheckBox _voiceNumbers = Check(
        "Números de voz (el nombre de cada ficha empieza por su número)"
    );
    private bool _showingVoiceNumbers;
    private bool _closing;

    /// <summary>Creates the window for <paramref name="app"/>; <paramref name="argumentError"/> explains a bad command line.</summary>
    public LabWindow(LabApp app, string? argumentError)
    {
        _app = app;
        Title = "Clícalo SpikeLab";
        var area = SystemParameters.WorkArea;
        Width = Math.Min(1040, area.Width);
        Height = Math.Min(820, area.Height);
        MaxWidth = area.Width;
        MaxHeight = area.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontSize = 16;
        Resources[SystemParameters.VerticalScrollBarWidthKey] = ScrollBarWidth;

        var layout = new StackPanel { Margin = new Thickness(16) };
        layout.Children.Add(Line(26, FontWeights.Bold, "Clícalo SpikeLab"));
        layout.Children.Add(
            Line(
                16,
                FontWeights.Normal,
                "Laboratorio de los spikes S1, S3 y S4 (docs/testing/spikes/README.md). Esta ventana SÍ se activa al "
                    + "tocarla: úsala para preparar. Durante los ciclos usa la tira-guía de abajo, que no quita el foco."
            )
        );
        if (argumentError is not null)
        {
            var error = Line(16, FontWeights.SemiBold, argumentError);
            error.Foreground = Brushes.DarkRed;
            layout.Children.Add(error);
        }

        BuildChooser();
        layout.Children.Add(_chooser);
        BuildControls();
        layout.Children.Add(_controls);
        layout.Children.Add(Line(20, FontWeights.Bold, "Piezas del producto"));
        layout.Children.Add(_components);
        Content = new ScrollViewer
        {
            Content = layout,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        app.Host.Board.Changed += (_, _) => _ = Dispatcher.BeginInvoke(ShowComponents);
        app.SessionStarted = () => _ = Dispatcher.BeginInvoke(OnSessionStarted);
        app.StartRefused = reason => _ = Dispatcher.BeginInvoke(() => _refusal.Text = reason);
        app.ExitRequested = () => _ = Dispatcher.BeginInvoke(Close);
        ShowComponents();
    }

    /// <inheritdoc />
    protected override void OnClosing(CancelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!_closing)
        {
            // Save the report and release everything before the process ends.
            _closing = true;
            e.Cancel = true;
            _ = ShutdownAsync();
        }

        base.OnClosing(e);
    }

    private async Task ShutdownAsync()
    {
        await _app.DisposeAsync();
        System.Windows.Application.Current.Shutdown();
    }

    private void BuildChooser()
    {
        _chooser.Children.Add(Line(20, FontWeights.Bold, "Elige el spike"));
        var buttons = new WrapPanel();
        foreach (var script in (SpikeScript[])[SpikeScripts.S1, SpikeScripts.S3, SpikeScripts.S4])
        {
            buttons.Children.Add(
                Button(script.Title, () => _app.StartSpike(script.Id), width: 300)
            );
        }

        _chooser.Children.Add(buttons);
        _refusal.Foreground = Brushes.DarkRed;
        _chooser.Children.Add(_refusal);
    }

    private void BuildControls()
    {
        _controls.Children.Add(_spikeLine);
        _controls.Children.Add(_reportLine);
        _controls.Children.Add(_piecesSummary);
        var buttons = new WrapPanel { Margin = new Thickness(0, 6, 0, 6) };
        buttons.Children.Add(Button("Mostrar superficies", () => Session?.ShowSurfaces()));
        buttons.Children.Add(Button("Ocultar superficies", () => Session?.HideSurfaces()));
        buttons.Children.Add(Button("Empezar ciclo", () => Session?.Repeat()));
        buttons.Children.Add(Button("Paso anterior", () => Session?.Previous()));
        buttons.Children.Add(
            Button(
                "Forzar activación desde la ventana de control",
                () => Run(Session?.ForceActivationAsync())
            )
        );
        buttons.Children.Add(
            Button("Soltar todo desde la ventana de control", () => Session?.ReleaseAll())
        );
        buttons.Children.Add(
            Button(
                "Aviso cortés desde la ventana de control",
                () => Session?.AnnounceTest(AnnouncementUrgency.Polite)
            )
        );
        buttons.Children.Add(
            Button(
                "Aviso urgente desde la ventana de control",
                () => Session?.AnnounceTest(AnnouncementUrgency.Assertive)
            )
        );
        buttons.Children.Add(Button("Abrir sonda", () => Run(Session?.OpenProbeAsync())));
        buttons.Children.Add(
            Button(
                "Abrir el CC desde la ventana de control",
                () => Run(Session?.OpenControlCenterAsync())
            )
        );
        buttons.Children.Add(
            Button("Escribir el informe ahora", () => Run(Session?.SaveReportAsync()))
        );
        buttons.Children.Add(Button("Abrir carpeta de informes", OpenReportsFolder));
        buttons.Children.Add(Button("Abrir resumen", OpenSummary));
        buttons.Children.Add(Button("Cerrar SpikeLab", Close));
        _controls.Children.Add(buttons);

        _voiceNumbers.Checked += (_, _) => SetVoiceNumbers(on: true);
        _voiceNumbers.Unchecked += (_, _) => SetVoiceNumbers(on: false);
        _controls.Children.Add(_voiceNumbers);

        var sendKeys = Check(
            "Enviar teclas a la app de delante (desactivado en los guiones: las fichas no escriben)"
        );
        sendKeys.Checked += (_, _) => SetSendKeys(on: true);
        sendKeys.Unchecked += (_, _) => SetSendKeys(on: false);
        _controls.Children.Add(sendKeys);
    }

    private LabSession? Session => _app.Session;

    private void OnSessionStarted()
    {
        if (Session is not { } session)
        {
            return;
        }

        _chooser.Visibility = Visibility.Collapsed;
        _controls.Visibility = Visibility.Visible;
        _spikeLine.Text = session.Script.Title + " · guion: " + session.Script.Document;
        _reportLine.Text =
            "Informe: "
            + session.ReportPath
            + " (y su resumen .md al lado). Ábrelos con «Abrir carpeta de informes» o «Abrir resumen».";
        session.VoiceNumbersChanged = on => _ = Dispatcher.BeginInvoke(() => ShowVoiceNumbers(on));
    }

    private void ShowComponents()
    {
        _components.Children.Clear();
        foreach (var component in _app.Host.Board.Components)
        {
            var line = Line(
                15,
                FontWeights.Normal,
                (
                    component.State switch
                    {
                        LabComponentState.Ready => "Lista",
                        LabComponentState.Pending => "Pendiente",
                        _ => "Con error",
                    }
                )
                    + " · "
                    + component.Name
                    + ": "
                    + component.Detail
            );
            if (component.State != LabComponentState.Ready)
            {
                line.Foreground = Brushes.DarkRed;
            }

            _components.Children.Add(line);
        }

        var notReady = _app
            .Host.Board.Components.Where(component => component.State != LabComponentState.Ready)
            .Select(component => component.Name)
            .ToArray();
        _piecesSummary.Text =
            notReady.Length == 0
                ? "Todas las piezas del producto están listas."
                : notReady.Length
                    + (
                        notReady.Length == 1
                            ? " pieza no está lista: "
                            : " piezas no están listas: "
                    )
                    + string.Join(", ", notReady)
                    + " (el motivo, al final de esta ventana).";
        _piecesSummary.Foreground = notReady.Length == 0 ? Brushes.DarkGreen : Brushes.DarkRed;
    }

    private void SetVoiceNumbers(bool on)
    {
        if (!_showingVoiceNumbers && Session is { } session)
        {
            session.VoiceNumbers = on;
        }
    }

    private void ShowVoiceNumbers(bool on)
    {
        _showingVoiceNumbers = true;
        try
        {
            _voiceNumbers.IsChecked = on;
        }
        finally
        {
            _showingVoiceNumbers = false;
        }
    }

    private void SetSendKeys(bool on)
    {
        if (Session is { } session)
        {
            session.SendsKeys = on;
        }
    }

    private void OpenReportsFolder()
    {
        if (Session is not { } session)
        {
            return;
        }

        // Explorer opens the folder with the report selected (the folder is hidden in %LOCALAPPDATA%).
        var report = session.ReportPath;
        Open(
            File.Exists(report)
                ? "/select,\"" + report + "\""
                : "\"" + Path.GetDirectoryName(report) + "\""
        );
    }

    private void OpenSummary()
    {
        if (Session is not { } session)
        {
            return;
        }

        if (!File.Exists(session.SummaryPath))
        {
            _app.Host.Measurements.Notice(
                "El resumen aún no está escrito: toca «Escribir el informe ahora»."
            );
            return;
        }

        // The summary opens with the app Windows has for .md files (it asks once if there is none).
        Open("\"" + session.SummaryPath + "\"");
    }

    private void Open(string arguments)
    {
        var explorer = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "explorer.exe"
        );
        try
        {
            using var process = Process.Start(
                new ProcessStartInfo(explorer, arguments) { UseShellExecute = false }
            );
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            _app.Host.Measurements.Notice("No se pudo abrir el Explorador: " + ex.Message);
        }
    }

    private void Run(Task? task)
    {
        if (task is null)
        {
            return;
        }

        _ = task.ContinueWith(
            failed =>
                _app.Host.Measurements.Notice(
                    "La orden falló: " + failed.Exception?.GetBaseException().Message
                ),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default
        );
    }

    private static Button Button(string text, Action click, double width = 240)
    {
        var button = new Button
        {
            Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
            MinHeight = 52,
            MinWidth = width,
            MaxWidth = 300,
            Margin = new Thickness(4),
            Padding = new Thickness(10, 4, 10, 4),
            FontSize = 17,
        };
        AutomationProperties.SetName(button, text);
        button.Click += (_, _) => click();
        return button;
    }

    private static CheckBox Check(string text)
    {
        var check = new CheckBox
        {
            Content = text,
            MinHeight = 44,
            Margin = new Thickness(4),
            FontSize = 17,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(check, text);
        return check;
    }

    private static TextBlock Line(double size, FontWeight weight, string text = "") =>
        new()
        {
            Text = text,
            FontSize = size,
            FontWeight = weight,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 4),
        };
}
