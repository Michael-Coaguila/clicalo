using Clicalo.Architecture.Tests.Fixtures.Confinement.Application.Session;

namespace Clicalo.Architecture.Tests.Fixtures.Confinement.Presentation.ControlCenter;

/// <summary>Fixture: a Workspace view model that only reads the session store, which is allowed.</summary>
public sealed class StatusViewModel
{
    public StatusViewModel(SessionStore store)
    {
        Last = store.Current;
        store.Changed += (_, _) => Last = store.Current;
    }

    public int Last { get; private set; }
}
