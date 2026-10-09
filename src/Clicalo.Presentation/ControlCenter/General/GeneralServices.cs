using Clicalo.Application.Confirmation;
using Clicalo.Application.Localization;
using Clicalo.Application.Store;

namespace Clicalo.Presentation.ControlCenter.General;

/// <summary>What «General y panel» works with (Workspace role); the composition root builds it.</summary>
/// <param name="Store">The document whose settings it shows and writes.</param>
/// <param name="Localization">The interface language.</param>
/// <param name="Confirm">The two taps of «Reiniciar Frecuentes» (REG-04).</param>
/// <param name="Time">The clock of the armed state.</param>
/// <param name="Post">Runs an action on the UI thread, after the current work.</param>
/// <param name="OpenWelcome">«Ver la bienvenida otra vez»: closes the Control Center and opens the welcome at step 0 (GEN-014).</param>
public sealed record GeneralServices(
    DocumentStore Store,
    ILocalizationContext Localization,
    TwoStepConfirm Confirm,
    TimeProvider Time,
    Action<Action> Post,
    Action OpenWelcome
);
