using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace Clicalo.Windowing.IntegrationTests.Automation.OutOfProcess;

/// <summary>
/// The parent side of <see cref="UiaClientRunner"/>: starts this test executable again as a UI Automation client of one
/// <see cref="UiaClientKind"/> for the target window, and asks it for one pattern call at a time.
/// </summary>
public sealed class UiaClientProcess : IAsyncDisposable
{
    /// <summary><c>IInvokeProvider.Invoke</c>.</summary>
    public const string Invoke = "invoke";

    /// <summary><c>IToggleProvider.Toggle</c>.</summary>
    public const string Toggle = "toggle";

    /// <summary><c>IExpandCollapseProvider.Expand</c>.</summary>
    public const string Expand = "expand";

    /// <summary><c>IExpandCollapseProvider.Collapse</c>.</summary>
    public const string Collapse = "collapse";

    private const string ReadyPrefix = "ready ";

    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(15);

    private readonly Process _process;
    private readonly StringBuilder _printed = new();
    private readonly Task _errors;

    private UiaClientProcess(Process process)
    {
        _process = process;
        _errors = CollectAsync(process.StandardError);
    }

    /// <summary>The window the client showed of its own (zero unless it was asked to).</summary>
    public nint OwnWindow { get; private set; }

    /// <summary>Starts the client and waits until it is ready.</summary>
    public static async Task<UiaClientProcess> StartAsync(
        UiaClientKind kind,
        nint targetWindow,
        bool inFront,
        CancellationToken cancellationToken
    )
    {
        var process =
            Process.Start(StartInfo(kind, targetWindow, inFront))
            ?? throw new InvalidOperationException("The UI Automation client did not start.");
        var client = new UiaClientProcess(process);
        try
        {
            var ready = await client.ReadAnswerAsync(StartTimeout, cancellationToken);
            if (!ready.StartsWith(ReadyPrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(client.Failure("did not start: " + ready));
            }

            client.OwnWindow = nint.Parse(
                ready[ReadyPrefix.Length..],
                CultureInfo.InvariantCulture
            );
            return client;
        }
        catch
        {
            await client.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Calls <paramref name="operation"/> on the element <paramref name="automationId"/> from the client process and
    /// returns once the call has returned there.
    /// </summary>
    public async Task RunAsync(
        string operation,
        string automationId,
        CancellationToken cancellationToken
    )
    {
        await _process.StandardInput.WriteLineAsync(
            (operation + " " + automationId).AsMemory(),
            cancellationToken
        );
        await _process.StandardInput.FlushAsync(cancellationToken);
        var answer = await ReadAnswerAsync(CallTimeout, cancellationToken);
        if (!string.Equals(answer, "ok", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                Failure(operation + " «" + automationId + "» failed: " + answer)
            );
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited)
            {
                await _process.StandardInput.WriteLineAsync(UiaClientRunner.Quit);
                await _process.StandardInput.FlushAsync();
                using var timeout = new CancellationTokenSource(ExitTimeout);
                try
                {
                    await _process.WaitForExitAsync(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
        }
        catch (IOException)
        {
            // The client already ended.
        }
        finally
        {
            await _errors;
            _process.Dispose();
        }
    }

    private static ProcessStartInfo StartInfo(UiaClientKind kind, nint targetWindow, bool inFront)
    {
        var host =
            Environment.ProcessPath ?? throw new InvalidOperationException("No process path.");
        var start = new ProcessStartInfo(host)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        if (
            string.Equals(
                Path.GetFileNameWithoutExtension(host),
                "dotnet",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            start.ArgumentList.Add(typeof(UiaClientRunner).Assembly.Location);
        }

        // The test executable's own xUnit v3 runner: only the client, without logo or colors.
        start.ArgumentList.Add("-method");
        start.ArgumentList.Add(
            typeof(UiaClientRunner).FullName + "." + nameof(UiaClientRunner.Serve_the_parent_test)
        );
        start.ArgumentList.Add("-noLogo");
        start.ArgumentList.Add("-noColor");
        start.Environment[UiaClientRunner.WindowVariable] = targetWindow.ToString(
            CultureInfo.InvariantCulture
        );
        start.Environment[UiaClientRunner.ClientVariable] = kind.ToString();
        start.Environment[UiaClientRunner.InFrontVariable] = inFront ? "1" : "0";
        return start;
    }

    private async Task CollectAsync(StreamReader output)
    {
        while (await output.ReadLineAsync() is { } line)
        {
            Keep(line);
        }
    }

    /// <summary>The next line of the protocol, skipping what the xUnit runner of the client prints around it.</summary>
    private async Task<string> ReadAnswerAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var prefix = UiaClientRunner.Marker + " ";
        while (true)
        {
            string? line;
            try
            {
                line = await _process.StandardOutput.ReadLineAsync(deadline.Token);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(Failure("did not answer in time"), ex);
            }

            if (line is null)
            {
                throw new InvalidOperationException(Failure("ended"));
            }

            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                return line[prefix.Length..];
            }

            Keep(line);
        }
    }

    private void Keep(string line)
    {
        lock (_printed)
        {
            _printed.AppendLine(line);
        }
    }

    private string Failure(string what)
    {
        lock (_printed)
        {
            return "The UI Automation client "
                + what
                + ". What it printed:"
                + Environment.NewLine
                + _printed;
        }
    }
}
