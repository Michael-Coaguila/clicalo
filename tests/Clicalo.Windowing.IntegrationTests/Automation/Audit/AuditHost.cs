using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using Clicalo.UI.Wpf.Theming;
using Clicalo.Windowing.IntegrationTests.Automation.Rules;

namespace Clicalo.Windowing.IntegrationTests.Automation.Audit;

/// <summary>
/// A real view of the product on a presentation source of its own, so WPF visibility, the UI Automation control view
/// and the bounding rectangles are the real ones. The window of the source is created without <c>WS_VISIBLE</c> and is
/// never shown, moved or activated: nothing reaches the desktop. Create, use and dispose it on the WPF thread.
/// </summary>
internal sealed class AuditHost : IDisposable
{
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private readonly HwndSource _source;
    private readonly UserControl _frame;
    private Action? _restore;

    /// <summary>Hosts <paramref name="view"/> at its own size, or at the size given.</summary>
    /// <param name="view">The view; it must have no parent.</param>
    /// <param name="width">Width in device-independent pixels; <see langword="null"/> is the width the view asks for.</param>
    /// <param name="height">Height in device-independent pixels; <see langword="null"/> is the height it asks for.</param>
    public AuditHost(FrameworkElement view, double? width = null, double? height = null)
        : this(view, theme: null, width, height) { }

    /// <summary>
    /// Hosts <paramref name="view"/> painted by <paramref name="theme"/>. The theme goes on the host, as it goes on the
    /// window in the app, never on the view itself: on the view it would set the text color the view gives itself (the
    /// bold <c>onDanger</c> of «Soltar todo» came out in the color of plain text).
    /// </summary>
    private AuditHost(FrameworkElement view, ThemeService? theme, double? width, double? height)
    {
        ArgumentNullException.ThrowIfNull(view);
        // A UserControl has an automation peer, so the snapshot has one root whatever the view is. The adorner layer is
        // the one every window of the product has in its template: the ring of a guide is drawn on it (R-39).
        _frame = new UserControl
        {
            Content = new AdornerDecorator { Child = view },
            Width = width ?? double.NaN,
            Height = height ?? double.NaN,
            Focusable = false,
            IsTabStop = false,
        };
        FixesWidth = width is not null;
        FixesHeight = height is not null;
        if (theme is not null)
        {
            theme.Attach(_frame);
            Theme = theme;
        }

        _source = new HwndSource(
            new HwndSourceParameters("clicalo-audit")
            {
                WindowStyle = WsPopup,
                ExtendedWindowStyle = WsExToolWindow | WsExNoActivate,
            }
        )
        {
            SizeToContent = SizeToContent.WidthAndHeight,
            RootVisual = _frame,
        };
        LayOut();
    }

    /// <summary>
    /// Hosts the content of <paramref name="window"/>, which is never shown: a window that is not on screen is
    /// collapsed for WPF, so its content is audited outside it, under the theme of the window, and goes back when the
    /// host is disposed.
    /// </summary>
    /// <param name="window">A window of the product, built but not shown.</param>
    /// <param name="theme">The theme service of the window.</param>
    /// <param name="width">Width in device-independent pixels; <see langword="null"/> is the width the window fixes or, when it fixes none, the one its content asks for.</param>
    /// <param name="height">Height in device-independent pixels; <see langword="null"/> is the height the window fixes or, when it fixes none, the one its content asks for.</param>
    public static AuditHost OfWindow(
        Window window,
        ThemeService theme,
        double? width = null,
        double? height = null
    )
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(theme);
        var content = (FrameworkElement)window.Content;
        window.Content = null;
        return new AuditHost(
            content,
            theme,
            width ?? Own(window.Width),
            height ?? Own(window.Height)
        )
        {
            Window = window,
            _restore = () => window.Content = content,
        };
    }

    /// <summary>
    /// Hosts <paramref name="view"/> painted by <paramref name="theme"/>, so the gallery can draw it in every theme.
    /// </summary>
    /// <param name="view">The view; it must have no parent.</param>
    /// <param name="theme">The theme service of the thread.</param>
    /// <param name="width">Width in device-independent pixels; <see langword="null"/> is the width the view asks for.</param>
    public static AuditHost OfView(FrameworkElement view, ThemeService theme, double? width = null)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return new AuditHost(view, theme, width, height: null);
    }

    /// <summary>The size a window fixes for itself; <see langword="null"/> when it takes the size of its content.</summary>
    private static double? Own(double size) => double.IsNaN(size) ? null : size;

    /// <summary>Physical pixels per device-independent pixel of the source.</summary>
    public double Scale => VisualTreeHelper.GetDpi(_frame).DpiScaleX;

    /// <summary>The root of the hosted tree.</summary>
    public FrameworkElement Root => _frame;

    /// <summary>The theme service that paints the host, when it came from <see cref="OfWindow"/> or <see cref="OfView"/>.</summary>
    public ThemeService? Theme { get; private init; }

    /// <summary>The window whose content is hosted, when it came from <see cref="OfWindow"/>: the gallery paints its frame.</summary>
    public Window? Window { get; private init; }

    /// <summary>
    /// Whether the width of the host is fixed (by the window or the test): the window is then that wide with its border
    /// inside, while a window that takes the width of its content is its content and its border.
    /// </summary>
    public bool FixesWidth { get; }

    /// <summary>Whether the height of the host is fixed, as <see cref="FixesWidth"/> for the width.</summary>
    public bool FixesHeight { get; }

    /// <summary>Runs the pending layout and bindings, after the view model changed.</summary>
    public void LayOut()
    {
        Clicalo.TestKit.Windows.Rendering.WpfThread.DrainPendingWork();
        _frame.UpdateLayout();
    }

    /// <summary>The UI Automation control view of the hosted view now.</summary>
    /// <param name="name">The name of the synthetic root, for messages.</param>
    public UiaNode Snapshot(string name)
    {
        LayOut();
        var root = PeerSnapshot.Capture(
            UIElementAutomationPeer.CreatePeerForElement(_frame),
            Scale
        );
        return root with { Name = name };
    }

    /// <summary>
    /// The peer of the element of the control view named <paramref name="name"/> (and of <paramref name="type"/>, when
    /// given), to ask it for a pattern or a state the snapshot does not carry. Fails when there is none or more than
    /// one.
    /// </summary>
    public AutomationPeer Peer(string name, AutomationControlType? type = null)
    {
        LayOut();
        var found = new List<AutomationPeer>();
        Collect(UIElementAutomationPeer.CreatePeerForElement(_frame));
        return found.Count == 1
            ? found[0]
            : throw new Xunit.Sdk.XunitException(
                string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"{found.Count} elements of the control view are named «{name}»; one expected."
                )
            );

        void Collect(AutomationPeer peer)
        {
            peer.ResetChildrenCache();
            foreach (var child in peer.GetChildren() ?? [])
            {
                if (
                    child.IsControlElement()
                    && string.Equals(child.GetName(), name, StringComparison.Ordinal)
                    && (type is null || child.GetAutomationControlType() == type)
                )
                {
                    found.Add(child);
                }

                Collect(child);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _source.RootVisual = null;
        if (_frame.Content is AdornerDecorator adorners)
        {
            adorners.Child = null;
        }

        _frame.Content = null;
        _source.Dispose();
        Theme?.Detach(_frame);
        _restore?.Invoke();
    }
}
