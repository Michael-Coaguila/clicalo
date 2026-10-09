using Clicalo.UI.Wpf.Theming;

namespace Clicalo.Windowing.IntegrationTests.Theming;

/// <summary>A system theme the test sets: dark apps, no contrast theme and animations on unless told otherwise.</summary>
internal sealed class FakeSystemTheme : ISystemThemeSource
{
    /// <summary>What <see cref="Read"/> returns.</summary>
    public SystemThemeState State { get; set; } =
        new(AppsUseLightTheme: false, HighContrast: false, ClientAreaAnimation: true);

    /// <summary>How many handlers listen to <see cref="Changed"/>.</summary>
    public int Listeners => Changed?.GetInvocationList().Length ?? 0;

    public event EventHandler? Changed;

    public SystemThemeState Read() => State;

    /// <summary>Sets the state and raises <see cref="Changed"/>, as <c>WM_SETTINGCHANGE</c> would.</summary>
    public void Change(SystemThemeState state)
    {
        State = state;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
