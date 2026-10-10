using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Clicalo.Application.Localization;
using Clicalo.Application.Ports;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Library;
using Clicalo.Infrastructure.Persistence;
using Clicalo.Presentation.ControlCenter.About;

namespace Clicalo.App.Composition;

/// <summary>
/// Builds what «Acerca de y contacto» works with (docs/05 §6, ACE-001 to ACE-005) from the adapters of the app:
/// <list type="bullet">
/// <item>the real version of the app and of Windows;</item>
/// <item>addresses open on the Shell thread through <see cref="IShellExecutor"/>, which only starts http and https
/// (LOG-008); the feedback email goes through its own entry, which opens the email app only towards the contact
/// address of the project (ACE-004, ADR-0029), and without that entry (a start with <c>--no-input</c>) or without an
/// address the view model copies the message instead;</item>
/// <item>the log preview is the tail of <c>clicalo.log</c>, already redacted when it was written (LOG-001), and the
/// copy to attach is written with <see cref="IAtomicFileWriter"/> next to it as <c>clicalo-registro.log</c>, whose
/// folder Explorer opens with the file selected.</item>
/// </list>
/// Logs nothing: no address, path or text of the person leaves through here (LOG-001, LOG-002).
/// </summary>
internal static class AboutServicesFactory
{
    /// <summary>The file the feedback asks to attach (ACE-004).</summary>
    public const string AttachmentName = "clicalo-registro.log";

    /// <summary>The most of the log the preview shows and the attachment holds: its newest part.</summary>
    private const int LogTailBytes = 64 * 1024;

    private static long _nextEffect = long.MinValue / 2;

    /// <summary>Creates the services.</summary>
    /// <param name="localization">The interface language.</param>
    /// <param name="locations">The data folders (the log).</param>
    /// <param name="shell">The Shell thread.</param>
    /// <param name="files">The atomic writer.</param>
    /// <param name="ui">The UI dispatcher (the clipboard).</param>
    /// <param name="dictate">Windows dictation for the focused field.</param>
    /// <param name="notify">The status bar of the Control Center.</param>
    /// <param name="openMail">
    /// Opens the email app for a <c>mailto:</c> address (<c>ShellExecutor.OpenMailAsync</c>); null where nothing may
    /// reach outside Clícalo.
    /// </param>
    public static AboutServices Create(
        ILocalizationContext localization,
        DataLocations locations,
        IShellExecutor shell,
        IAtomicFileWriter files,
        Dispatcher ui,
        Func<CancellationToken, ValueTask<bool>> dictate,
        Action<WorkspaceNotice> notify,
        Func<Uri, CancellationToken, Task<bool>>? openMail = null
    )
    {
        ArgumentNullException.ThrowIfNull(locations);
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(ui);
        return new AboutServices(
            localization,
            DocumentFormats.AppVersion,
            WindowsVersion(),
            AboutLinks.Current,
            (address, cancellationToken) => OpenAsync(shell, openMail, address, cancellationToken),
            text => Copy(ui, text),
            dictate,
            cancellationToken => ReadLogAsync(locations.LogFile, cancellationToken),
            (text, cancellationToken) =>
                SaveLogAsync(locations, shell, files, text, cancellationToken),
            notify
        );
    }

    /// <summary>«Windows 11 (26100)»: the name by its build and the build number.</summary>
    internal static string WindowsVersion()
    {
        var version = Environment.OSVersion.Version;
        var name = version.Major == 10 && version.Build >= 22000 ? "Windows 11" : "Windows 10";
        return name
            + " ("
            + version.Build.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ")";
    }

    /// <summary>The newest part of the log, from the start of a line; empty without a log; null if unreadable.</summary>
    [SuppressMessage(
        "ApiDesign",
        "RS0030:Do not use banned APIs",
        Justification = "log-preview: reads clicalo.log while the sink holds it open (FileShare.ReadWrite), read-only."
    )]
    internal static async ValueTask<string?> ReadLogAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (!File.Exists(path))
            {
                return string.Empty;
            }

            await using var stream = new FileStream(
                path,
                new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.Read,
                    Share = FileShare.ReadWrite | FileShare.Delete,
                    Options = FileOptions.Asynchronous,
                }
            );
            var start = Math.Max(0, stream.Length - LogTailBytes);
            stream.Position = start;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            if (start > 0)
            {
                var firstLine = text.IndexOf('\n', StringComparison.Ordinal);
                text = firstLine < 0 ? string.Empty : text[(firstLine + 1)..];
            }

            return text.TrimEnd();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static async ValueTask<string?> SaveLogAsync(
        DataLocations locations,
        IShellExecutor shell,
        IAtomicFileWriter files,
        string text,
        CancellationToken cancellationToken
    )
    {
        var path = Path.Combine(locations.Logs, AttachmentName);
        var written = await files
            .WriteAsync(path, Encoding.UTF8.GetBytes(text), cancellationToken)
            .ConfigureAwait(true);
        if (!written.IsSuccess)
        {
            return null;
        }

        _ = await StartAsync(
                shell,
                new LaunchRequest.StartApp(
                    new AppTarget.Executable("explorer.exe", "/select,\"" + path + "\"")
                ),
                cancellationToken
            )
            .ConfigureAwait(true);
        return AttachmentName;
    }

    /// <summary>A web address through the Shell thread; a <c>mailto:</c> address through its own entry, if any.</summary>
    internal static async ValueTask<bool> OpenAsync(
        IShellExecutor shell,
        Func<Uri, CancellationToken, Task<bool>>? openMail,
        Uri address,
        CancellationToken cancellationToken
    )
    {
        if (!string.Equals(address.Scheme, Uri.UriSchemeMailto, StringComparison.Ordinal))
        {
            return await StartAsync(shell, new LaunchRequest.OpenUrl(address), cancellationToken)
                .ConfigureAwait(true);
        }

        return openMail is not null
            && await openMail(address, cancellationToken).ConfigureAwait(true);
    }

    private static async ValueTask<bool> StartAsync(
        IShellExecutor shell,
        LaunchRequest request,
        CancellationToken cancellationToken
    )
    {
        var reply = new Reply();
        shell.Launch(new EffectId(Interlocked.Increment(ref _nextEffect)), request, reply);
        return await reply.Done.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
    }

    private static void Copy(Dispatcher ui, string text)
    {
        _ = ui.BeginInvoke(() =>
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch (ExternalException)
            {
                // Another app holds the clipboard: nothing is copied, as when the person copies by hand.
            }
        });
    }

    /// <summary>The answer of the Shell thread to one launch.</summary>
    private sealed class Reply : IEngineInbox
    {
        public TaskCompletionSource<bool> Done { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Post(EngineEvent engineEvent) =>
            engineEvent switch
            {
                EngineEvent.LaunchCompleted => Done.TrySetResult(true),
                EngineEvent.LaunchFailed => Done.TrySetResult(false),
                _ => false,
            };
    }
}
