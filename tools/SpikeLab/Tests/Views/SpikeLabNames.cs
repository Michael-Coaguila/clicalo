using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Clicalo.Tools.SpikeLab.Composition;
using Clicalo.Tools.SpikeLab.Measurement;
using Clicalo.Tools.SpikeLab.Scripting;
using Clicalo.Tools.SpikeLab.Session;
using Clicalo.Tools.SpikeLab.Surfaces;
using Clicalo.Tools.SpikeLab.Views;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.Tools.SpikeLab.Tests.Views;

/// <summary>
/// The UI Automation names a voice command can reach in SpikeLab, taken from the real windows built in process (never
/// shown): the control window, every surface (with the other names of the strip buttons and of the step action), the
/// lab Control Center and the tray menu. Must run on the WPF thread.
/// </summary>
internal static class SpikeLabNames
{
    /// <summary>The name of the dictation buttons, repeated next to every free text field by rule UIA010.</summary>
    public const string Dictation = "Dictar";

    /// <summary>Every name, with the window it belongs to; <paramref name="spike"/> limits them to the ones it can show.</summary>
    public static List<(string Window, string Name)> Collect(SpikeId? spike = null)
    {
        var names = new List<(string Window, string Name)>();
        var app = new LabApp(LabOptions.Default, Dispatcher.CurrentDispatcher);
        Add(names, "ventana de control", new LabWindow(app, argumentError: null));

        // The search and the lab Control Center only open with their leases, in S4 (LeaseFlows).
        var withLeases = spike is null or SpikeId.S4;
        foreach (var surface in Surfaces().All)
        {
            if (withLeases || surface.Id != LabSurfaceIds.Search)
            {
                Add(names, surface.SurfaceName, surface);
            }
        }

        string[] otherGuideNames = [GuideView.UnfoldName, GuideView.ShortenName];
        foreach (
            var name in otherGuideNames.Concat(
                Enum.GetValues<StepAction>()
                    .SelectMany(action =>
                        (string?[])
                            [
                                GuideModelBuilder.ActionName(action, voiceNumbers: false),
                                GuideModelBuilder.ActionName(action, voiceNumbers: true),
                            ]
                    )
                    .OfType<string>()
                    .Distinct(StringComparer.Ordinal)
            )
        )
        {
            names.Add(("Notice#9", name));
        }

        if (withLeases)
        {
            var controlCenter = new LabControlCenter(
                _ => Task.CompletedTask,
                (TextBox _) => { },
                (TextBox _) => { }
            );
            Add(names, "Centro de control de laboratorio", controlCenter);
        }

        names.AddRange(LeaseFlows.TrayMenuItems.Select(item => ("menú de la bandeja", item.Text)));
        return names;
    }

    /// <summary>The lab surfaces, created but never shown.</summary>
    public static LabSurfaces Surfaces()
    {
        var guard = new ActivationGuard(new ArbiterRelay(), TimeProvider.System);
        return new LabSurfaces(
            new SurfaceRegistry(new OwnerAnchor(), guard),
            new LabSurfaceContext(
                TimeProvider.System,
                new SilentSink(),
                new ComponentBoard(),
                new SurfaceDirectory(),
                new LabEventLog(TimeProvider.System)
            )
        );
    }

    private static void Add(
        List<(string Window, string Name)> names,
        string window,
        DependencyObject root
    )
    {
        foreach (var element in Interactive(root))
        {
            if (
                UIElementAutomationPeer.CreatePeerForElement(element)?.GetName() is
                { Length: > 0 } name
            )
            {
                names.Add((window, name));
            }
        }
    }

    private static IEnumerable<UIElement> Interactive(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is ShortcutTile or ButtonBase or TextBox or DragGrip)
            {
                yield return (UIElement)child;
                continue;
            }

            foreach (var element in Interactive(child))
            {
                yield return element;
            }
        }
    }

    private sealed class SilentSink : ILabInputSink
    {
        public void OnTile(TileInput input) { }

        public void OnHandleDrag(
            string surface,
            SurfaceGroup group,
            Clicalo.Domain.Touch.PointerKind? pointer
        ) { }

        public void OnIgnoredTouch(string surface, Clicalo.Domain.Touch.IgnoreReason reason) { }

        public void OnActivationMessage(string surface, string message) { }
    }
}
