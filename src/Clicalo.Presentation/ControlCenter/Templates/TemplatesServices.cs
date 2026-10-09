using Clicalo.Application.Ports;
using Clicalo.Application.UseCases.Ai;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Application.UseCases.Templates;
using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Templates;

/// <summary>
/// What the section Plantillas and the share of «Atajos» work with, besides <see cref="ControlCenterServices"/>: the
/// preview, the AI, the shared profiles, what Windows suggests for the keyboard line and the file dialogs. The
/// composition root builds it.
/// </summary>
/// <param name="Preview">The preview and its install (PLA-013 to PLA-017).</param>
/// <param name="Ai">«Crear con IA» with the person's own key (PLA-002 to PLA-008, D5).</param>
/// <param name="Sharing">Export and import of one profile (DAT-007).</param>
/// <param name="DetectedLayout">The layout of the keyboard in use ([detected], PLA-009).</param>
/// <param name="DetectedAppsLanguage">The programs language Windows suggests ([detected], PLA-009).</param>
/// <param name="PickImport">Asks for a shared profile file and reads it; null when the person cancels.</param>
/// <param name="SaveShare">
/// Asks where to save <c>clicalo-perfil-&lt;id&gt;.json</c> and writes it; null when the person cancels, false when it failed.
/// </param>
/// <param name="Notify">Shows a message in the status bar for its time (CCM-003).</param>
public sealed record TemplatesServices(
    TemplatePreviewSession Preview,
    AiAssistant Ai,
    IProfileSharing Sharing,
    Func<string> DetectedLayout,
    Func<LangCode> DetectedAppsLanguage,
    Func<CancellationToken, ValueTask<ReadOnlyMemory<byte>?>> PickImport,
    Func<string, ReadOnlyMemory<byte>, CancellationToken, ValueTask<bool?>> SaveShare,
    Action<WorkspaceNotice> Notify
);
