using Clicalo.Analyzers.Windowing;
using Verify = Clicalo.Generators.Tests.Analyzers.AnalyzerVerifier<Clicalo.Analyzers.Windowing.NonActivatingWindowAnalyzer>;

namespace Clicalo.Generators.Tests.Analyzers;

[Trait("Req", "REG-01")]
public sealed class NonActivatingWindowAnalyzerTests
{
    [Fact]
    public Task Activating_calls_inside_a_surface_are_errors() =>
        Verify.VerifyAsync(
            """
            using Clicalo.UI.Wpf.Windowing;

            namespace Clicalo.UI.Wpf.Surfaces;

            public sealed class PanelWindow : NonActivatingWindow
            {
                public void Open()
                {
                    {|CLC0001:Show()|};
                    {|CLC0001:this.Activate()|};
                    {|CLC0001:base.Focus()|};
                    _ = {|CLC0001:ShowDialog()|};
                }
            }
            """,
            Stubs.Windowing
        );

    [Fact]
    public Task Activating_calls_on_surface_instances_are_errors_even_through_a_cast() =>
        Verify.VerifyAsync(
            """
            using System.Windows;
            using Clicalo.UI.Wpf.Windowing;

            public sealed class PanelWindow : NonActivatingWindow { }

            public static class Host
            {
                public static void Open(PanelWindow panel, NonActivatingWindow any)
                {
                    {|CLC0001:panel.Show()|};
                    {|CLC0001:any.Activate()|};
                    {|CLC0001:((Window)panel).Show()|};
                }

                public static void OpenGeneric<T>(T surface) where T : NonActivatingWindow => {|CLC0001:surface.Focus()|};
            }
            """,
            Stubs.Windowing
        );

    [Fact]
    public Task Taking_an_activating_method_as_a_delegate_is_an_error() =>
        Verify.VerifyAsync(
            """
            using System;
            using Clicalo.UI.Wpf.Windowing;

            public sealed class PanelWindow : NonActivatingWindow { }

            public static class Host
            {
                public static Action Later(PanelWindow panel) => {|CLC0001:panel.Show|};
            }
            """,
            Stubs.Windowing
        );

    [Fact]
    public Task Showing_activated_or_visible_through_properties_is_an_error() =>
        Verify.VerifyAsync(
            """
            using System.Windows;
            using Clicalo.UI.Wpf.Windowing;

            public sealed class PanelWindow : NonActivatingWindow
            {
                public PanelWindow(bool activate)
                {
                    {|CLC0001:ShowActivated = true|};
                    {|CLC0001:ShowActivated = activate|};
                    {|CLC0001:Visibility = Visibility.Visible|};
                }
            }

            public static class Host
            {
                public static PanelWindow Create() => new PanelWindow(false) { {|CLC0001:ShowActivated = true|} };
            }
            """,
            Stubs.Windowing
        );

    [Fact]
    public Task Passive_api_and_hiding_are_allowed() =>
        Verify.VerifyAsync(
            """
            using System.Windows;
            using Clicalo.UI.Wpf.Windowing;

            public sealed class PanelWindow : NonActivatingWindow
            {
                public PanelWindow()
                {
                    ShowActivated = false;
                    Visibility = Visibility.Collapsed;
                }

                public void Open()
                {
                    ShowPassive();
                    HidePassive();
                    Hide();
                    Visibility = Visibility.Hidden;
                }
            }
            """,
            Stubs.Windowing
        );

    [Fact]
    public Task Ordinary_windows_are_not_this_rules_concern() =>
        // Window.Activate and Window.Focus on any window are banned separately (BannedSymbols); CLC0001 guards surfaces.
        Verify.VerifyAsync(
            """
            using System.Windows;

            public sealed class Dialog : Window
            {
                public void Open()
                {
                    ShowActivated = true;
                    Show();
                    Activate();
                }
            }
            """,
            Stubs.Windowing
        );

    [Fact]
    public Task Without_the_contract_type_the_rule_stays_silent() =>
        Verify.VerifyAsync(
            """
            public sealed class Window
            {
                public void Show() { }
                public void Open() => Show();
            }
            """
        );

    [Fact]
    public Task A_justified_suppression_silences_the_rule() =>
        Verify.VerifyAsync(
            """
            using System.Diagnostics.CodeAnalysis;
            using Clicalo.UI.Wpf.Windowing;

            public sealed class PanelWindow : NonActivatingWindow
            {
                [SuppressMessage("Clicalo.Windowing", "CLC0001", Justification = "Creates the HWND before the first ShowPassive.")]
                public void EnsureHandle() => Show();
            }
            """,
            Stubs.Windowing
        );

    [Fact]
    public async Task Reports_the_method_and_the_surface_in_the_message()
    {
        var test = Verify.Create(
            """
            using Clicalo.UI.Wpf.Windowing;

            public sealed class PanelWindow : NonActivatingWindow
            {
                public void Open() => {|#0:Show()|};
            }
            """,
            Stubs.Windowing
        );
        test.ExpectedDiagnostics.Add(
            new Microsoft.CodeAnalysis.Testing.DiagnosticResult(
                Clicalo.Analyzers.Descriptors.ActivatingCall
            )
                .WithLocation(0)
                .WithArguments("Show", "PanelWindow")
        );

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Declares_both_descriptors_under_one_id() =>
        new NonActivatingWindowAnalyzer()
            .SupportedDiagnostics.Select(d => d.Id)
            .Distinct(StringComparer.Ordinal)
            .ShouldBe(["CLC0001"]);
}
