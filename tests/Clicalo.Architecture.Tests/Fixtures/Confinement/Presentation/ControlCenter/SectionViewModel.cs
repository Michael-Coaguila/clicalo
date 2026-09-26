using Clicalo.Architecture.Tests.Fixtures.Confinement.Application.Session;

namespace Clicalo.Architecture.Tests.Fixtures.Confinement.Presentation.ControlCenter;

/// <summary>Fixture: a Workspace view model that writes the session store, which only Surfaces may do.</summary>
public sealed class SectionViewModel(SessionStore store)
{
    public void Reset() => store.Dispatch(0);
}
