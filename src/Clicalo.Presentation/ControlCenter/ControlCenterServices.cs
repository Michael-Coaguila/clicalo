using System.Collections.Immutable;
using Clicalo.Application.Confirmation;
using Clicalo.Application.Localization;
using Clicalo.Application.Ports;
using Clicalo.Application.Store;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Presentation.ControlCenter.SystemSection;
using Clicalo.Presentation.ControlCenter.Templates;

namespace Clicalo.Presentation.ControlCenter;

/// <summary>
/// What the view models of the Control Center work with (Workspace role): the document, the use cases of «Atajos»,
/// the language, the two-tap confirmation, the clock and the ports of the desktop. The composition root builds it.
/// </summary>
/// <param name="Store">The document.</param>
/// <param name="Shortcuts">The «Atajos» section.</param>
/// <param name="Profiles">The profile of the list in view.</param>
/// <param name="Localization">The interface language.</param>
/// <param name="Catalogs">Icons, library, templates, key labels and mouse actions, once loaded.</param>
/// <param name="Confirm">The two-tap confirmation of destructive operations (REG-04).</param>
/// <param name="Time">The clock of the animations and the armed buttons.</param>
/// <param name="Post">Runs an action on the UI thread, after the current work.</param>
/// <param name="ActiveAppProfile">The profile of the app in front, for «Tu panel muestra» (ATJ-001).</param>
/// <param name="OpenApps">Reads the open apps (ATJ-006, PRB-003).</param>
/// <param name="LastApp">The last app in front that is not Clícalo, the default of «Probar en» (PRB-003).</param>
/// <param name="Dictate">Starts Windows dictation (Win+H) for the focused field (ACC-011).</param>
/// <param name="TryNow">«Probar ahora» (PRB-004): hides the Control Center, tries and brings it back.</param>
/// <param name="OpenTemplates">«+ Nuevo perfil»: the Plantillas section (ATJ-002).</param>
/// <param name="System">The services of «Sistema» (docs/05 §5); null shows its marker.</param>
/// <param name="Templates">The services of Plantillas and of sharing a profile; null where there are none.</param>
public sealed record ControlCenterServices(
    DocumentStore Store,
    ShortcutsWorkspace Shortcuts,
    ProfileWorkspace Profiles,
    ILocalizationContext Localization,
    Func<EditorCatalogs> Catalogs,
    TwoStepConfirm Confirm,
    TimeProvider Time,
    Action<Action> Post,
    Func<Profile?> ActiveAppProfile,
    Func<CancellationToken, ValueTask<ImmutableArray<OpenApp>>> OpenApps,
    Func<ProcessName?> LastApp,
    Func<CancellationToken, ValueTask<bool>> Dictate,
    Func<Shortcut, OpenApp, CancellationToken, ValueTask<TryNowOutcome>> TryNow,
    Action OpenTemplates,
    SystemServices? System = null,
    TemplatesServices? Templates = null
);
