using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Messages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.ControlCenter.About;

/// <summary>
/// «Acerca de y contacto» (docs/05 §6, ACE-001 to ACE-005): the creator's story, the app card with its real version,
/// the feedback form and the direct contact. Nothing leaves the computer without a tap (LOG-002):
/// <list type="bullet">
/// <item>[shareShort] copies the address of the repository and says [shared];</item>
/// <item>[logPvT] shows the log exactly as it would be attached, already redacted (ACE-003);</item>
/// <item>[Enviar por correo] saves that log as a file and opens its folder when it is attached (a <c>mailto:</c> link
/// cannot attach files), then opens the email app with «[Clícalo] {kind}» and the body, only ever towards the contact
/// email of the project (ADR-0029); without that address or without an email app the message is copied and
/// [fbMailCopied] says so (ACE-004).</item>
/// </list>
/// The LinkedIn button and the email row are hidden while their address is empty (user decision D11).
/// The message is kept while the window lives; the composition root calls <see cref="Refresh"/> when the language
/// changes.
/// </summary>
public sealed class AboutViewModel : ObservableObject
{
    private static readonly (FeedbackKind Kind, string Icon, Message Label)[] KindOptions =
    [
        (FeedbackKind.Suggestion, "lightbulb", L.TSug),
        (FeedbackKind.Bug, "bug_report", L.TBug),
        (FeedbackKind.Idea, "add_circle", L.TIdea),
        (FeedbackKind.Thanks, "favorite", L.TThanks),
    ];

    private readonly AboutServices _s;

    private string? Email => string.IsNullOrWhiteSpace(_s.Links.Email) ? null : _s.Links.Email.Trim();

    private FeedbackKind _kind = FeedbackKind.Suggestion;
    private string _message = string.Empty;
    private bool _attachLog = true;
    private bool _includeSystem = true;
    private bool _previewOpen;
    private Message? _previewProblem;
    private string? _log;
    private bool _sending;
    private AboutScreen? _screen;

    /// <summary>Creates the section.</summary>
    /// <param name="services">What it works with.</param>
    public AboutViewModel(AboutServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _s = services;
        Refresh();
    }

    /// <summary>What the section shows.</summary>
    public AboutScreen Screen
    {
        get => _screen!;
        private set => SetProperty(ref _screen, value);
    }

    /// <summary>[GitHub]: the repository in the browser.</summary>
    public Task OpenRepositoryAsync() => OpenAsync(_s.Links.Repository);

    /// <summary>«Reportar en GitHub»: the issues in the browser.</summary>
    public Task OpenIssuesAsync() => OpenAsync(_s.Links.Issues);

    /// <summary>«Colaborar con el código»: the repository in the browser.</summary>
    public Task OpenContributeAsync() => OpenAsync(_s.Links.Repository);

    /// <summary>«Guía de usuario»: the guide in the browser.</summary>
    public Task OpenGuideAsync() => OpenAsync(_s.Links.Guide);

    /// <summary>[LinkedIn]: the profile in the browser; nothing while there is none (ACE-001, D11).</summary>
    public Task OpenLinkedInAsync() =>
        _s.Links.LinkedIn is { } profile ? OpenAsync(profile) : Task.CompletedTask;

    /// <summary>[shareShort]: copies the address of the repository (ACE-001).</summary>
    public void Share()
    {
        _s.Copy(_s.Links.Repository.AbsoluteUri);
        _s.Notify(new WorkspaceNotice(L.Shared, "link", false, false));
    }

    /// <summary>Copies the contact email ([copiedEmail], ACE-005); nothing while there is none.</summary>
    public void CopyEmail()
    {
        if (Email is not { } email)
        {
            return;
        }

        _s.Copy(email);
        _s.Notify(new WorkspaceNotice(L.CopiedEmail, "content_copy", false, false));
    }

    /// <summary>A kind of the 2 × 2 grid (ACE-002).</summary>
    /// <param name="kind">The kind.</param>
    public void SetKind(FeedbackKind kind)
    {
        _kind = kind;
        Refresh();
    }

    /// <summary>The person typed or dictated in the message.</summary>
    /// <param name="message">The text.</param>
    public void SetMessage(string message)
    {
        _message = message ?? string.Empty;
        Refresh();
    }

    /// <summary>🎤 of the message: Windows dictation in the field (the view focuses it first, ACC-011).</summary>
    public void Dictate() => _ = _s.Dictate(CancellationToken.None).AsTask();

    /// <summary>[fbLog] (ACE-003).</summary>
    public void ToggleLog()
    {
        _attachLog = !_attachLog;
        Refresh();
    }

    /// <summary>[fbSys] (ACE-003).</summary>
    public void ToggleSystem()
    {
        _includeSystem = !_includeSystem;
        Refresh();
    }

    /// <summary>[logPvT]: opens the log exactly as it would be attached, or closes it (ACE-003).</summary>
    public async Task TogglePreviewAsync()
    {
        _previewOpen = !_previewOpen;
        Refresh();
        if (_previewOpen)
        {
            await LoadLogAsync().ConfigureAwait(true);
            Refresh();
        }
    }

    /// <summary>[Enviar por correo] (ACE-004).</summary>
    public async Task SendAsync()
    {
        if (_sending)
        {
            return;
        }

        _sending = true;
        Refresh();
        try
        {
            string? logLine = null;
            if (_attachLog)
            {
                var log = await LoadLogAsync().ConfigureAwait(true);
                var file = log is { Length: > 0 }
                    ? await _s.SaveLog(log, CancellationToken.None).ConfigureAwait(true)
                    : null;
                if (file is not null)
                {
                    logLine = T(L.FbBodyLog(name: file));
                }
                else if (log is not { Length: 0 })
                {
                    _s.Notify(new WorkspaceNotice(L.LogPvFailed, "warning", false, true));
                }
            }

            var subject = T(L.FbSubject(name: KindLabel(_kind)));
            var body = FeedbackMail.Body(
                _message,
                _includeSystem ? T(L.FbBodySys(version: _s.Version, name: _s.System)) : null,
                logLine
            );
            // ACE-004, ADR-0029: the email app opens only towards the contact email of the project; without one the
            // message is copied.
            var opened =
                Email is { } email
                && await _s.Open(
                        FeedbackMail.Address(email, subject, body),
                        CancellationToken.None
                    )
                    .ConfigureAwait(true);
            if (opened)
            {
                _s.Notify(new WorkspaceNotice(L.SentMail, "send", false, false));
            }
            else
            {
                _s.Copy(subject + "\n\n" + body);
                _s.Notify(new WorkspaceNotice(L.FbMailCopied, "content_copy", false, true));
            }
        }
        finally
        {
            _sending = false;
            Refresh();
        }
    }

    /// <summary>Projects everything again (the language changed, or the section).</summary>
    public void Refresh()
    {
        var email = Email;
        Screen = new AboutScreen(
            T(L.AboutTitle),
            T(L.AboutSub),
            T(L.Story1),
            T(L.Story2),
            T(L.CreatorName),
            T(L.CreatorInitials),
            T(L.CreatorRole),
            T(L.AppName),
            T(L.AboutVersion(version: _s.Version, name: L.OpenSource)),
            T(L.GitHub),
            _s.Links.LinkedIn is null ? null : T(L.LinkedIn),
            T(L.ShareShort),
            T(L.FbTitle),
            [
                .. KindOptions.Select(option => new AboutOption(
                    option.Kind,
                    option.Icon,
                    T(option.Label),
                    option.Kind == _kind
                )),
            ],
            T(L.FbMsg),
            T(L.FbPh),
            _message,
            T(L.SearchDictate),
            T(L.FbLog),
            T(L.FbLogFile),
            _attachLog,
            T(L.FbSys),
            T(L.FbSysD),
            _includeSystem,
            T(L.LogPvT),
            _previewOpen,
            PreviewText(),
            T(L.FbSend),
            T(
                email is null ? L.FbSendCopyD
                : _attachLog ? L.FbSendLogD
                : L.FbSendD
            ),
            _sending,
            T(L.FbDirect),
            email,
            T(L.Copy),
            T(L.FbPromise),
            T(L.UserGuide),
            T(L.UserGuideD),
            T(L.FbGh),
            T(L.FbGhD),
            T(L.FbContrib),
            T(L.FbContribD)
        );
    }

    private string PreviewText() =>
        _previewProblem is { } problem ? T(problem)
        : _log is null ? string.Empty
        : _log.Length == 0 ? T(L.LogPvEmpty)
        : _log;

    private async Task<string?> LoadLogAsync()
    {
        var log = await _s.ReadLog(CancellationToken.None).ConfigureAwait(true);
        _log = log;
        _previewProblem = log is null ? L.LogPvFailed : null;
        return log;
    }

    private async Task OpenAsync(Uri address) =>
        _ = await _s.Open(address, CancellationToken.None).ConfigureAwait(true);

    private string KindLabel(FeedbackKind kind) =>
        T(KindOptions.First(option => option.Kind == kind).Label);

    private string T(Message message) => _s.Localization.Current.Format(message);
}
