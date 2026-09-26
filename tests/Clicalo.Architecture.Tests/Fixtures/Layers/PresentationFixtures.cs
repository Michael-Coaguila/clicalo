using System.Windows.Input;

namespace Clicalo.Architecture.Tests.Fixtures.Layers.Presentation;

/// <summary>Fixture: a view model that uses WPF, which Presentation must never do.</summary>
public sealed class WindowAwareViewModel
{
    public System.Windows.Visibility Visibility { get; set; }
}

/// <summary>Fixture: a view model that exposes the portable ICommand, which Presentation may use.</summary>
public sealed class CommandViewModel
{
    public ICommand? Save { get; set; }
}
