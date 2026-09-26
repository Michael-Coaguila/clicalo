namespace Clicalo.Architecture.Tests.Fixtures.Layers.Presentation;

/// <summary>Fixture: a view model that uses WPF, which Presentation must never do.</summary>
public sealed class WindowAwareViewModel
{
    public System.Windows.Visibility Visibility { get; set; }
}
