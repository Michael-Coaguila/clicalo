using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Clicalo.Application.Localization;
using Clicalo.Application.Ports;
using Clicalo.Application.Store;
using Clicalo.Application.UseCases.Ai;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Application.UseCases.Templates;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Templates;
using Clicalo.Domain.Timing;
using Clicalo.Infrastructure.Ai;
using Clicalo.Infrastructure.Sharing;
using Clicalo.Platform.Windows.Secrets;
using Clicalo.Presentation.ControlCenter.Templates;
using Microsoft.Win32;

namespace Clicalo.App.Composition;

/// <summary>
/// The pieces of the section Plantillas and of sharing a profile (docs/05 §2, DAT-007): the preview, the AI with the
/// person's own key (ADR-0014, user decision D5), the shared profile files and the file dialogs. With isolated data
/// (<c>--data</c>, <c>cl run</c>) the key lives only in memory, so a trial run never touches the Credential Manager.
/// </summary>
internal sealed class TemplatesComposition
{
    private readonly DocumentStore _store;
    private readonly ILocalizationContext _localization;
    private readonly IAtomicFileWriter _writer;
    private readonly TemplatePreviewSession _preview;
    private readonly AiAssistant _ai;
    private readonly ProfileSharing _sharing;

    /// <summary>Creates the pieces.</summary>
    /// <param name="store">The document.</param>
    /// <param name="localization">The interface language, for the file dialogs.</param>
    /// <param name="time">The clock.</param>
    /// <param name="ids">New ids for imported profiles.</param>
    /// <param name="writer">The only writer of files.</param>
    /// <param name="isolatedData">Whether the app runs with isolated data.</param>
    public TemplatesComposition(
        DocumentStore store,
        ILocalizationContext localization,
        TimeProvider time,
        IIdGenerator ids,
        IAtomicFileWriter writer,
        bool isolatedData
    )
    {
        _store = store;
        _localization = localization;
        _writer = writer;
        IAiKeyStore keys = isolatedData
            ? new MemoryKeyStore(AiServices.CredentialTarget)
            : new CredentialKeyStore(AiServices.CredentialTarget);
        _preview = new TemplatePreviewSession(store);
        _ai = new AiAssistant(store, AiServices.CreateGenerator(keys), keys, time);
        _sharing = new ProfileSharing(time, ids);
    }

    /// <summary>The services of the section, with the status bar and the owner of the dialogs.</summary>
    /// <param name="notify">Shows a message in the status bar.</param>
    /// <param name="owner">The Control Center window.</param>
    public TemplatesServices Services(Action<WorkspaceNotice> notify, Func<Window?> owner) =>
        new(
            _preview,
            _ai,
            _sharing,
            () => KeyboardLayouts.Detect(InputLanguageManager.Current?.CurrentInputLanguage?.Name),
            () => KeyboardLayouts.DetectAppsLanguage(CultureInfo.InstalledUICulture.Name),
            cancellationToken => PickImport(owner(), Filter(), cancellationToken),
            (name, content, cancellationToken) =>
                SaveShareAsync(owner(), name, content, cancellationToken),
            notify
        );

    private string Filter() => _localization.Current.Format(L.AppName) + " (*.json)|*.json";

    private static async ValueTask<ReadOnlyMemory<byte>?> PickImport(
        Window? owner,
        string filter,
        CancellationToken cancellationToken
    )
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false,
        };
        if (dialog.ShowDialog(owner) != true)
        {
            return null;
        }

        // The UI thread never reads files (blueprint §3.2): the dialog runs here, the read on the pool.
        var path = dialog.FileName;
        return await Task.Run(() => Read(path), cancellationToken).ConfigureAwait(true);
    }

    private static ReadOnlyMemory<byte> Read(string path)
    {
        try
        {
            // A file over the limit is rejected by the codec as too large (LOG-006) without reading it all.
            return new FileInfo(path).Length > Timings.Import.ShareMaxBytes
                ? new byte[Timings.Import.ShareMaxBytes + 1]
                : File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
    }

    private async ValueTask<bool?> SaveShareAsync(
        Window? owner,
        string name,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken
    )
    {
        var dialog = new SaveFileDialog
        {
            FileName = name,
            Filter = Filter(),
            AddExtension = true,
            DefaultExt = ".json",
            OverwritePrompt = true,
        };
        if (dialog.ShowDialog(owner) != true)
        {
            return null;
        }

        var written = await _writer
            .WriteAsync(dialog.FileName, content, cancellationToken)
            .ConfigureAwait(true);
        return written.IsSuccess;
    }
}
