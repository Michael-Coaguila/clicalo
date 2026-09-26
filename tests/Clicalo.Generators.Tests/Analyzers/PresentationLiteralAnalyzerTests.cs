using Microsoft.CodeAnalysis.Testing;
using Verify = Clicalo.Generators.Tests.Analyzers.AnalyzerVerifier<Clicalo.Analyzers.Presentation.PresentationLiteralAnalyzer>;

namespace Clicalo.Generators.Tests.Analyzers;

[Trait("Req", "IDI-002")]
[Trait("Req", "IDI-005")]
public sealed class PresentationLiteralAnalyzerTests
{
    [Fact]
    public Task Literal_text_in_view_model_members_is_an_error() =>
        Verify.VerifyAsync(
            """
            namespace Clicalo.Presentation.Panel;

            public sealed class PanelViewModel
            {
                private string _statusMessage = {|CLC0006:"Listo"|};

                public string Title { get; set; } = {|CLC0006:"Clícalo"|};

                public string Label => {|CLC0006:"Atajo"|};

                public string Description
                {
                    get { return {|CLC0006:"Perfil general"|}; }
                }

                public string ErrorText { get; set; }

                public void Update(string name, bool ok)
                {
                    Title = $"{|CLC0006:Hola |}{name}";
                    ErrorText = ok ? {|CLC0006:"Sí"|} : {|CLC0006:"No"|};
                    _statusMessage += {|CLC0006:" y más"|};
                }
            }
            """
        );

    [Fact]
    public Task Literal_text_composed_with_string_methods_is_an_error() =>
        Verify.VerifyAsync(
            """
            using System.Globalization;

            namespace Clicalo.Presentation.Library;

            public sealed class LibraryViewModel
            {
                public string Subtitle { get; set; }

                public string EditHint { get; set; }

                public string SearchPlaceholder { get; set; } = {|CLC0006:"Buscar"|};

                public void Update(int count, string[] names, string name)
                {
                    Subtitle = string.Format(CultureInfo.CurrentCulture, {|CLC0006:"Tienes {0} atajos"|}, count);
                    Subtitle = string.Format(CultureInfo.CurrentCulture, "{0}", {|CLC0006:"Vacío"|});
                    EditHint = string.Join({|CLC0006:" y "|}, names);
                    EditHint = string.Concat(name, {|CLC0006:" (copia)"|});
                    Subtitle = string.Format(CultureInfo.CurrentCulture, "{0:N0} · {{{1}}}", count, name);
                    EditHint = string.Join(", ", names);
                }
            }
            """
        );

    [Fact]
    public Task Literal_text_passed_to_visible_parameters_is_an_error() =>
        Verify.VerifyAsync(
            """
            namespace Clicalo.Presentation.Common;

            public sealed record NoticeViewModel(string Title, string Message, int Priority);

            public static class Notices
            {
                public static NoticeViewModel Saved() => new NoticeViewModel({|CLC0006:"Guardado"|}, {|CLC0006:"Todo bien"|}, 1);
            }
            """
        );

    [Fact]
    [Trait("Req", "TEM-002")]
    public Task Literal_text_and_colors_in_wpf_code_are_errors() =>
        Verify.VerifyAsync(
            """
            using System.Windows;
            using System.Windows.Automation;
            using System.Windows.Controls;
            using System.Windows.Media;

            namespace Clicalo.UI.Wpf.Surfaces;

            public static class PanelBuilder
            {
                public static void Build(TextBlock block, Button button)
                {
                    block.Text = {|CLC0006:"Hola"|};
                    button.Content = {|CLC0006:"Guardar"|};
                    button.ToolTip = {|CLC0006:"Guarda el perfil"|};
                    AutomationProperties.SetName(button, {|CLC0006:"Guardar"|});
                    ToolTipService.SetToolTip(button, {|CLC0006:"Ayuda"|});
                    block.SetValue(TextBlock.TextProperty, {|CLC0006:"Hola"|});
                    button.SetCurrentValue(AutomationProperties.NameProperty, {|CLC0006:"Guardar"|});
                    MessageBox.Show({|CLC0006:"Hola"|}, {|CLC0006:"Clícalo"|});
                    var accent = {|CLC0006:"#FF0078D4"|};
                    var red = {|CLC0006:Colors.Red|};
                    var white = {|CLC0006:Brushes.White|};
                    var custom = {|CLC0006:Color.FromRgb(0x12, 0x34, 0x56)|};
                }
            }
            """,
            Stubs.Wpf
        );

    [Fact]
    public Task Keys_identifiers_glyphs_and_developer_text_are_allowed() =>
        Verify.VerifyAsync(
            """
            using System;
            using System.Collections.Generic;
            using System.Diagnostics;
            using System.Windows.Automation;
            using System.Windows.Controls;
            using System.Windows.Media;

            namespace Clicalo.UI.Wpf.Surfaces;

            public static class PanelBuilder
            {
                public static void Build(TextBlock block, Button button, IReadOnlyDictionary<string, string> strings, byte r)
                {
                    block.Text = strings["panel.title"];
                    block.Text = "";
                    block.Text = " · ";
                    block.Text = "";
                    block.Text = nameof(PanelBuilder);
                    button.Name = "SaveButton";
                    AutomationProperties.SetAutomationId(button, "save-button");
                    ToolTipService.SetPlacement(button, "Bottom");
                    block.SetValue(TextBlock.TagProperty, "tag");
                    Debug.Assert(r > 0, "Channel must be positive");
                    var clear = Colors.Transparent;
                    var none = Brushes.Transparent;
                    var mixed = Color.FromRgb(r, r, r);
                    if (block.Text.Length == 0)
                    {
                        throw new InvalidOperationException("The panel has no title");
                    }
                }
            }
            """,
            Stubs.Wpf
        );

    [Theory]
    [InlineData("Clicalo.Application.Notices")]
    [InlineData("Clicalo.Domain.Library")]
    [InlineData("Clicalo.Infrastructure.Localization")]
    [InlineData("Clicalo.PresentationTools")]
    public Task Other_layers_are_out_of_scope(string ns) =>
        Verify.VerifyAsync(
            "namespace "
                + ns
                + "; public sealed class Model { public string Title { get; set; } = \"Hola\"; public string Color = \"#FFFFFF\"; }"
        );

    [Fact]
    public Task A_justified_suppression_silences_the_rule() =>
        Verify.VerifyAsync(
            """
            using System.Diagnostics.CodeAnalysis;

            namespace Clicalo.Presentation.Welcome;

            public sealed class BrandViewModel
            {
                [SuppressMessage("Clicalo.Presentation", "CLC0006", Justification = "Brand name, never translated (TEM-008).")]
                public string Title => "Clícalo";
            }
            """
        );

    [Fact]
    public async Task Reports_the_text_and_its_target_in_the_message()
    {
        var test = Verify.Create(
            """
            namespace Clicalo.Presentation.Panel;

            public sealed class PanelViewModel
            {
                public string Title { get; set; } = {|#0:"Clícalo"|};
            }
            """
        );
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(Clicalo.Analyzers.Descriptors.LiteralText)
                .WithLocation(0)
                .WithArguments("Clícalo", "Title")
        );

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
