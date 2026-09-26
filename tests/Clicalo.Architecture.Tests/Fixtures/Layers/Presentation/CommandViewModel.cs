using System.Windows.Input;

namespace Clicalo.Architecture.Tests.Fixtures.Layers.Presentation;

/// <summary>Fixture: a view model that exposes the portable ICommand, which Presentation may use.</summary>
public sealed class CommandViewModel
{
    public ICommand? Save { get; set; }
}
