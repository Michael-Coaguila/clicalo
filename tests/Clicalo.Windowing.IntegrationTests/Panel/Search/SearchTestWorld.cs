using System.Collections.Immutable;
using Clicalo.Application.Foreground;
using Clicalo.Application.Ports;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;

namespace Clicalo.Windowing.IntegrationTests.SearchPanel;

/// <summary>
/// The data of the search and suggestion view model tests: Always visible «Dictar», General «Deshacer», Word with
/// «Negrita» and «Dictado», Chrome with «Pestaña nueva»; a foreground owner that refuses every lease (so the view
/// models are tested without a desktop), and a backup service that keeps nothing.
/// </summary>
internal static class SearchTestWorld
{
    public static readonly ProfileId Word = new("word");

    public static ShortcutLibrary Library() =>
        ShortcutLibrary
            .CreateValidated(
                [Shortcut("dict", "Dictar", "Dictate")],
                [
                    Profile(
                        ProfileId.General,
                        "General",
                        null,
                        Shortcut("undo", "Deshacer", "Undo")
                    ),
                    Profile(
                        Word,
                        "Word",
                        "winword.exe",
                        Shortcut("bold", "Negrita", "Bold"),
                        Shortcut("dictw", "Dictado", "Dictation")
                    ),
                    Profile(
                        new ProfileId("chrome"),
                        "Navegador",
                        "chrome.exe",
                        Shortcut("tab", "Pestaña nueva", "New tab")
                    ),
                ]
            )
            .Value;

    public static UserDocument Document() =>
        UserDocument.Create(Library(), SettingsSchema.Defaults);

    private static Shortcut Shortcut(string id, string es, string en) =>
        new(
            new ShortcutId(id),
            new LocalizedText([new(LangCode.Es, es), new(LangCode.En, en)]),
            new IconRef("bolt"),
            AutoIcon: false,
            new CategoryId("edit"),
            new TapAction(KeyChord.Empty, []),
            new ShortcutOptions(Confirm: false, new HoldLimit.InheritGlobal(), IsPrivate: false),
            Origin: null,
            PinnedFrom: null
        );

    private static Profile Profile(
        ProfileId id,
        string name,
        string? process,
        params Shortcut[] shortcuts
    ) =>
        new(
            id,
            LocalizedText.Same(name, LangCode.Es, LangCode.En),
            new IconRef("apps"),
            AutoIcon: false,
            process is null
                ? new AppBinding.Manual()
                : new AppBinding.Processes([new ProcessName(process)]),
            InjectionMode.VirtualKey,
            [.. shortcuts],
            Origin: null
        );

    /// <summary>Refuses every lease, as Windows does when Clícalo has no foreground right.</summary>
    internal sealed class RefusingForeground : IForegroundOrchestrator
    {
        public ForegroundSnapshot Current => ForegroundSnapshot.Empty;

        public int Requests { get; private set; }

        public ValueTask<LeaseResult> AcquireAsync(
            LeaseRequest request,
            CancellationToken cancellationToken
        )
        {
            Requests++;
            return ValueTask.FromResult<LeaseResult>(
                new LeaseResult.Denied(ForegroundDenialReason.RightsRefused)
            );
        }
    }

    /// <summary>Keeps no backup.</summary>
    internal sealed class NoBackups : IBackupService
    {
        public void SnapshotNow(UserDocument document, BackupKind kind) { }

        public Task<Result<int>> WriteSnapshotsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Results.Ok(0));

        public Task<Result<BackupInfo>> CreateAsync(
            UserDocument document,
            BackupKind kind,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();

        public Task<ImmutableArray<BackupInfo>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ImmutableArray<BackupInfo>.Empty);

        public Task<Result<UserDocument>> ReadAsync(
            BackupId id,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();
    }
}
