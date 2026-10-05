using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;
using Clicalo.Platform.Windows.Input;
using Clicalo.Platform.Windows.SentinelHost;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>
/// The stand-in main process of the chaos tests of S9: this test executable started again with
/// <see cref="Flag"/>. It starts the real Sentinel with its one handle through the real supervisor, presses keys into
/// the probe through the real injector (Ctrl+Shift held, a drag, a macro in the middle of a step), says «ready» and
/// waits to be killed. Sentinel then releases and tries to relaunch <c>Clicalo.exe</c>, which is not next to it in the
/// build output, so nothing is relaunched. It refuses to run outside the nightly run on a CI runner.
/// </summary>
internal static class ChaosMain
{
    public const string Flag = "--clicalo-chaos-main";

    public const string Ready = "ready";

    [ModuleInitializer]
    [SuppressMessage(
        "Usage",
        "CA2255:The 'ModuleInitializer' attribute should not be used in libraries",
        Justification = "A test executable that doubles as the stand-in main process of the S9 chaos tests."
    )]
    internal static void Initialize()
    {
        var arguments = Environment.GetCommandLineArgs();
        var at = Array.IndexOf(arguments, Flag);
        if (at >= 0)
        {
            Environment.Exit(Run(arguments[(at + 1)..]));
        }
    }

    private static int Run(string[] arguments)
    {
        if (!ChaosEnvironment.IsEnabled || arguments.Length != 3)
        {
            return 2;
        }

        var sentinel = arguments[0];
        var scenario = arguments[1];
        var probe = (nint)long.Parse(arguments[2], CultureInfo.InvariantCulture);
        var sender = new GuardedProbeSender(probe, allowUnbalanced: true);
        var injector = new InputInjector(sender, Platform.Core.Injection.SystemKeyState.Instance);
        var supervisor = new SentinelSupervisor(
            sentinel,
            TimeProvider.System,
            NullLogger<SentinelSupervisor>.Instance
        );
        supervisor.Start();
        if (supervisor.ProcessId is null)
        {
            return 3;
        }

        var ctrl = new InjectedKey(0xA2, 0x1D, false, InjectionMode.VirtualKey);
        var shift = new InjectedKey(0xA0, 0x2A, false, InjectionMode.VirtualKey);
        var c = new InjectedKey(0x43, 0x2E, false, InjectionMode.VirtualKey);
        switch (scenario)
        {
            case "hold":
                injector.Send([InjectedEvent.KeyDown(ctrl), InjectedEvent.KeyDown(shift)]);
                break;
            case "drag":
                injector.Mouse(MouseOp.Drag, target: null);
                injector.Send([InjectedEvent.MouseDown(MouseButtons.Left)]);
                break;
            case "macro":
                injector.Send([InjectedEvent.KeyDown(ctrl), InjectedEvent.KeyDown(c)]);
                break;
            default:
                return 4;
        }

        Console.Out.WriteLine(
            Ready + " " + supervisor.ProcessId.Value.ToString(CultureInfo.InvariantCulture)
        );
        Console.Out.Flush();
        Thread.Sleep(Timeout.Infinite);
        return 0;
    }
}
