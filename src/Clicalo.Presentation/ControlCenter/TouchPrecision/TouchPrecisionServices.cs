using Clicalo.Application.Localization;
using Clicalo.Application.Store;

namespace Clicalo.Presentation.ControlCenter.TouchPrecision;

/// <summary>What «Precisión táctil» works with (Workspace role); the composition root builds it.</summary>
/// <param name="Store">The document whose touch filter it shows and writes.</param>
/// <param name="Localization">The interface language.</param>
/// <param name="Time">The clock that stamps the touches of the test zone.</param>
/// <param name="Post">Runs an action on the UI thread, after the current work.</param>
public sealed record TouchPrecisionServices(
    DocumentStore Store,
    ILocalizationContext Localization,
    TimeProvider Time,
    Action<Action> Post
);
