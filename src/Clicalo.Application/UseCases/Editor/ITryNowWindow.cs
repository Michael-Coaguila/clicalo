using Clicalo.Application.Ports;

namespace Clicalo.Application.UseCases.Editor;

/// <summary>The Control Center window as «Probar ahora» hides and shows it (PRB-004).</summary>
public interface ITryNowWindow
{
    /// <summary>The window, for the foreground lease that brings it back.</summary>
    WindowToken Window { get; }

    /// <summary>Hides it while the app is tried.</summary>
    void Hide();

    /// <summary>Shows it again without activating it; the lease brings it to the front.</summary>
    void Show();
}
