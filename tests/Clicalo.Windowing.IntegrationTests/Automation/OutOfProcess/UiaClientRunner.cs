using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Clicalo.TestKit.Windows.Rendering;

namespace Clicalo.Windowing.IntegrationTests.Automation.OutOfProcess;

/// <summary>
/// The out-of-process half of <see cref="OutOfProcessUiaTests"/>: a UI Automation client in another process, as Voice
/// access, Narrator or a script are. <see cref="UiaClientProcess"/> starts this test executable again with only this
/// test selected and the target in environment variables; this test then reads one pattern call per line from its
/// standard input, calls it on the target window and answers on its standard output. Without those variables it is
/// skipped.
/// </summary>
/// <remarks>
/// With <see cref="InFrontVariable"/> it first shows a window of its own and reports its handle: the parent test puts
/// that window in front, so that the client is the foreground process when it calls the pattern (the case of the S3
/// finding with the managed client).
/// </remarks>
[Trait("Requires", "Desktop")]
public sealed class UiaClientRunner
{
    /// <summary>Handle of the target window, in decimal.</summary>
    public const string WindowVariable = "CLICALO_UIA_TARGET_WINDOW";

    /// <summary>The client, a name of <see cref="UiaClientKind"/>.</summary>
    public const string ClientVariable = "CLICALO_UIA_CLIENT";

    /// <summary>«1» when the client shows a window of its own for the parent to put in front.</summary>
    public const string InFrontVariable = "CLICALO_UIA_CLIENT_IN_FRONT";

    /// <summary>First word of every line of the protocol on the standard output.</summary>
    public const string Marker = "clicalo-uia";

    /// <summary>The line that ends the session.</summary>
    public const string Quit = "quit";

    /// <summary>True when a parent test asked for a client.</summary>
    public static bool IsRequested =>
        Environment.GetEnvironmentVariable(ClientVariable) is { Length: > 0 };

    [Fact(
        Skip = "Only runs when OutOfProcessUiaTests starts it with a target window.",
        SkipUnless = nameof(IsRequested),
        SkipType = typeof(UiaClientRunner)
    )]
    public void Serve_the_parent_test()
    {
        var window = nint.Parse(
            Environment.GetEnvironmentVariable(WindowVariable)!,
            CultureInfo.InvariantCulture
        );
        var kind = Enum.Parse<UiaClientKind>(Environment.GetEnvironmentVariable(ClientVariable)!);
        using IUiaPatternClient client =
            kind == UiaClientKind.Uia2
                ? new ManagedUiaPatternClient(window)
                : new FlaUiPatternClient(window);
        var own = string.Equals(
            Environment.GetEnvironmentVariable(InFrontVariable),
            "1",
            StringComparison.Ordinal
        )
            ? WpfThread.Invoke(ShowOwnWindow)
            : null;
        // FlaUI turns an unknown COM failure into a Win32Exception that keeps the message and loses the HRESULT: keep
        // the last COMException of the call to report it.
        COMException? lastCom = null;
        EventHandler<FirstChanceExceptionEventArgs> record = (_, e) =>
        {
            if (e.Exception is COMException com)
            {
                Volatile.Write(ref lastCom, com);
            }
        };
        AppDomain.CurrentDomain.FirstChanceException += record;
        try
        {
            Say("ready " + (own is null ? "0" : WpfThread.Invoke(() => Handle(own))));
            while (
                Console.ReadLine() is { } line
                && !string.Equals(line, Quit, StringComparison.Ordinal)
            )
            {
                var parts = line.Split(' ', 2);
                Volatile.Write(ref lastCom, null);
                try
                {
                    client.Run(parts[0], parts.Length > 1 ? parts[1] : string.Empty);
                    Say("ok");
                }
                catch (Exception ex)
                {
                    Say("error " + Describe(ex, Volatile.Read(ref lastCom)));
                }
            }
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= record;
            if (own is not null)
            {
                WpfThread.Invoke(own.Close);
            }
        }
    }

    /// <summary>One line: the exception, its HRESULT, the COM failure under it and where it was thrown.</summary>
    private static string Describe(Exception exception, COMException? com) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{exception.GetType().Name} (HRESULT 0x{exception.HResult:X8}): {exception.Message.ReplaceLineEndings(" ")}"
        )
        + (
            com is null
                ? string.Empty
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"; COM HRESULT 0x{com.HResult:X8}: {com.Message.ReplaceLineEndings(" ")}"
                )
        )
        + "; at "
        + string.Join(
            " | ",
            (exception.StackTrace ?? string.Empty)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(8)
        );

    private static void Say(string text) => Console.Out.WriteLine(Marker + " " + text);

    private static string Handle(Window window) =>
        new WindowInteropHelper(window).Handle.ToString(CultureInfo.InvariantCulture);

    private static Window ShowOwnWindow()
    {
        var window = new Window
        {
            Title = "Cliente UIA de prueba",
            Width = 320,
            Height = 120,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ShowActivated = false,
        };
        window.Show();
        return window;
    }
}
