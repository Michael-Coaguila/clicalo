using Clicalo.Architecture.Tests.Fixtures.Confinement.Application.Ports;
using Clicalo.Architecture.Tests.Fixtures.Confinement.Application.Session;
using Clicalo.Architecture.Tests.Fixtures.Confinement.Domain.Library;

namespace Clicalo.Architecture.Tests.Fixtures.Confinement.Presentation.Panel;

/// <summary>
/// Fixture: a panel view model. It belongs to the Surfaces role, so it may write the session store, but it must
/// neither inject input, nor take the foreground, nor reveal secrets.
/// </summary>
public sealed class PanelViewModel(
    IInputInjector injector,
    IForegroundControl control,
    SessionStore store
)
{
    public void Tap(SecretText text)
    {
        store.Dispatch(text.Length);
        injector.Inject("a");
        control.TrySetForeground(0);
        text.WithRevealed(0, (span, _) => span.ToString());
    }
}
