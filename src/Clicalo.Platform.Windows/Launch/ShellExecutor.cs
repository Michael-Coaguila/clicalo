using System.Diagnostics.CodeAnalysis;
using Clicalo.Application.Ports;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Platform.Windows.SystemCommands;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Clicalo.Platform.Windows.Launch;

/// <summary>
/// The <see cref="IShellExecutor"/> of the product (EJE-011, EJE-016, blueprint §3.2): every launch and system command
/// runs on its own <see cref="ShellThread"/>, and the result goes back to the engine's inbox. Apps, Store apps,
/// documents and web addresses start with <c>ShellExecute</c>, never through a command interpreter (LOG-008, checked
/// again with <see cref="LaunchSafety"/>); when Clícalo runs elevated they start unelevated through the desktop shell
/// (<see cref="DesktopShellLauncher"/>), and when that is not possible they do not start at all. The feedback email
/// of «Acerca de» is its own entry (<see cref="OpenMailAsync"/>, ADR-0029): no shortcut can reach it.
/// </summary>
public sealed class ShellExecutor : IShellExecutor, IDisposable
{
    private const string StoreAppsFolder = "shell:AppsFolder\\";
    private const long FirstSuccessfulInstance = 32;

    private readonly ShellThread _thread;
    private readonly bool _selfElevated;
    private readonly Action<Exception>? _onFailure;

    /// <summary>Starts the Shell thread.</summary>
    /// <param name="selfElevated">Clícalo runs elevated: launches go through the desktop shell, unelevated.</param>
    /// <param name="onFailure">Hears an unexpected failure (the caller logs its type); the engine still gets an answer.</param>
    public ShellExecutor(bool selfElevated, Action<Exception>? onFailure = null)
    {
        _selfElevated = selfElevated;
        _onFailure = onFailure;
        _thread = new ShellThread(onFailure);
    }

    /// <inheritdoc />
    public void Launch(EffectId effect, LaunchRequest request, IEngineInbox replyTo)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(replyTo);
        _thread.Post(() =>
        {
            EngineEvent result;
            try
            {
                result = Start(effect, request);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _onFailure?.Invoke(ex);
                result = new EngineEvent.LaunchFailed(
                    effect,
                    Failed("launch.failed", L.ActionFailed(DisplayName(request)))
                );
            }

            _ = replyTo.Post(result);
        });
    }

    /// <inheritdoc />
    public void Run(EffectId effect, SystemCommandId command, IEngineInbox replyTo)
    {
        ArgumentNullException.ThrowIfNull(replyTo);
        _thread.Post(() =>
        {
            var succeeded = false;
            try
            {
                succeeded = SystemCommandRunner.Run(command);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _onFailure?.Invoke(ex);
            }

            _ = replyTo.Post(new EngineEvent.SystemCommandCompleted(effect, succeeded));
        });
    }

    /// <summary>
    /// Opens the email app with the feedback message of «Acerca de» (ACE-004, ADR-0029), on the Shell thread and
    /// unelevated: only a <c>mailto:</c> address towards <paramref name="projectMail"/>, with a subject and a body and
    /// nothing else (<see cref="LaunchSafety.CheckMail"/>). Anything else is refused without touching the shell.
    /// </summary>
    /// <param name="address">The <c>mailto:</c> address.</param>
    /// <param name="projectMail">The fixed contact address of the project; without one nothing opens.</param>
    /// <param name="cancellationToken">Stops waiting for the answer.</param>
    /// <returns>Whether Windows started an email app.</returns>
    public Task<bool> OpenMailAsync(
        Uri address,
        string? projectMail,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(address);
        if (LaunchSafety.CheckMail(address, projectMail) != LaunchVerdict.Allowed)
        {
            return Task.FromResult(false);
        }

        var done = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var file = address.AbsoluteUri;
        _thread.Post(() =>
        {
            try
            {
                _ = done.TrySetResult(
                    _selfElevated
                        ? DesktopShellLauncher.TryStart(file, string.Empty, string.Empty)
                        : ShellOpen(file, string.Empty, string.Empty)
                );
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _onFailure?.Invoke(ex);
                _ = done.TrySetResult(false);
            }
        });
        return done.Task.WaitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose() => _thread.Dispose();

    /// <summary>What a request starts with <c>ShellExecute</c>: the file, its arguments and its folder.</summary>
    /// <param name="request">The request.</param>
    internal static (string File, string Arguments, string Directory) Target(
        LaunchRequest request
    ) =>
        request switch
        {
            LaunchRequest.OpenUrl url => (url.Address.AbsoluteUri, string.Empty, string.Empty),
            LaunchRequest.StartApp { Target: AppTarget.Executable exe } => (
                Unquoted(exe.Path),
                exe.Arguments ?? string.Empty,
                FolderOf(Unquoted(exe.Path))
            ),
            LaunchRequest.StartApp { Target: AppTarget.Document document } => (
                Unquoted(document.Path),
                string.Empty,
                FolderOf(Unquoted(document.Path))
            ),
            LaunchRequest.StartApp { Target: AppTarget.StoreApp store } => (
                StoreAppsFolder + store.AppUserModelId,
                string.Empty,
                string.Empty
            ),
            _ => (string.Empty, string.Empty, string.Empty),
        };

    /// <summary>How the notices name what a request starts: the host of an address, the file name of an app.</summary>
    /// <param name="request">The request.</param>
    internal static string DisplayName(LaunchRequest request) =>
        request switch
        {
            LaunchRequest.OpenUrl url => url.Address.Host,
            LaunchRequest.StartApp { Target: AppTarget.Executable exe } =>
                Path.GetFileNameWithoutExtension(Unquoted(exe.Path)),
            LaunchRequest.StartApp { Target: AppTarget.Document document } => Path.GetFileName(
                Unquoted(document.Path)
            ),
            LaunchRequest.StartApp { Target: AppTarget.StoreApp store } => store
                .AppUserModelId.Split('!')[0]
                .Split('_')[0],
            _ => string.Empty,
        };

    /// <summary>Starts <paramref name="request"/> on the calling thread (the Shell thread) and says how it went.</summary>
    internal EngineEvent Start(EffectId effect, LaunchRequest request)
    {
        var name = DisplayName(request);

        // The engine already refused what is unsafe, a network path without confirmation included; this is the last
        // line before ShellExecute (LOG-008).
        if (LaunchSafety.Check(request, confirmed: true) != LaunchVerdict.Allowed)
        {
            return new EngineEvent.LaunchFailed(
                effect,
                Failed("launch.unsafe", L.LaunchUnsafe(name))
            );
        }

        var (file, arguments, directory) = Target(request);
        var started = _selfElevated
            ? DesktopShellLauncher.TryStart(file, arguments, directory)
            : ShellOpen(file, arguments, directory);
        return started
            ? new EngineEvent.LaunchCompleted(effect)
            : new EngineEvent.LaunchFailed(effect, Failed("launch.failed", L.ActionFailed(name)));
    }

    [SuppressMessage(
        "ApiDesign",
        "RS0030:Do not use banned APIs",
        Justification = "ILauncher: ShellExecute on the Shell thread, without a command interpreter (LOG-008, EJE-011)."
    )]
    private static unsafe bool ShellOpen(string file, string arguments, string directory)
    {
        fixed (char* fileText = file)
        fixed (char* argumentsText = arguments)
        fixed (char* directoryText = directory)
        {
            // HINSTANCE is not a module here: a value above 32 means success (ShellExecute's documented contract).
            var instance = PInvoke.ShellExecute(
                HWND.Null,
                default,
                fileText,
                arguments.Length == 0 ? default : argumentsText,
                directory.Length == 0 ? default : directoryText,
                SHOW_WINDOW_CMD.SW_SHOWNORMAL
            );
            return (long)(nint)instance.Value > FirstSuccessfulInstance;
        }
    }

    private static Failure Failed(string code, Message message) =>
        new(
            code,
            message,
            FailureSeverity.Warning,
            FailureRecovery.None,
            FailureAnnouncement.Assertive
        );

    private static string Unquoted(string path) => (path ?? string.Empty).Trim().Trim('"');

    private static string FolderOf(string path) =>
        Path.IsPathFullyQualified(path)
            ? Path.GetDirectoryName(path) ?? string.Empty
            : string.Empty;
}
