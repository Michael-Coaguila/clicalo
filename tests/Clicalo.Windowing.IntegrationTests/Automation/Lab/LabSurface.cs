using System.Windows;
using Clicalo.Application.Ports;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.Windowing.IntegrationTests.Automation.Lab;

/// <summary>
/// A real non-activatable surface (<see cref="NonActivatingWindow"/>, owned by the <see cref="OwnerAnchor"/> of its
/// registry) that hosts a <see cref="TileLab"/>, as the S3 panel of SpikeLab does. It is only ever shown with
/// <see cref="NonActivatingWindow.ShowPassive"/>.
/// </summary>
public sealed class LabSurface : NonActivatingWindow
{
    /// <summary>Logical position and size of the surface: on the primary monitor, clear of the corners.</summary>
    public static readonly Rect Bounds = new(120, 120, 340, 330);

    /// <summary>The window title, which is also its UI Automation name.</summary>
    public const string SurfaceTitle = "Panel de prueba de S3";

    /// <summary>Creates the surface <paramref name="id"/> in <paramref name="registry"/> with <paramref name="lab"/> as content.</summary>
    public LabSurface(SurfaceId id, SurfaceRegistry registry, TileLab lab)
        : base(id, registry)
    {
        ArgumentNullException.ThrowIfNull(lab);
        Lab = lab;
        Title = SurfaceTitle;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = Bounds.Left;
        Top = Bounds.Top;
        Width = Bounds.Width;
        Height = Bounds.Height;
        Content = lab.Root;
    }

    /// <summary>The hosted lab.</summary>
    public TileLab Lab { get; }

    /// <summary>Raised on the UI thread each time WPF applies the window's template (again after a system theme change).</summary>
    public event EventHandler? TemplateApplied;

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        TemplateApplied?.Invoke(this, EventArgs.Empty);
    }
}
