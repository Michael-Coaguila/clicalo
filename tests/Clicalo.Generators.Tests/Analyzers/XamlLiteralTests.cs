using Microsoft.CodeAnalysis.Testing;
using Verify = Clicalo.Generators.Tests.Analyzers.AnalyzerVerifier<Clicalo.Analyzers.Presentation.PresentationLiteralAnalyzer>;

namespace Clicalo.Generators.Tests.Analyzers;

[Trait("Req", "IDI-002")]
[Trait("Req", "TEM-002")]
public sealed class XamlLiteralTests
{
    private const string Namespaces = """
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
            xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
            mc:Ignorable="d"
        """;

    [Fact]
    public Task Literal_text_in_text_attributes_is_an_error() =>
        VerifyXamlAsync(
            $$$"""
            <UserControl x:Class="Clicalo.UI.Wpf.Surfaces.PanelView" {{{Namespaces}}}
                AutomationProperties.Name="{|CLC0006:Panel|}">
              <StackPanel>
                <TextBlock Text="{|CLC0006:Hola|}" />
                <Button Content="{|CLC0006:_Guardar|}" ToolTip="{|CLC0006:Guarda el perfil|}" AutomationProperties.AutomationId="save" />
                <GroupBox Header="{|CLC0006:Teclas|}" Tag="Hola" />
                <TextBox AutomationProperties.HelpText="{|CLC0006:Escribe para buscar|}" x:Name="Search" />
                <TextBlock Text="{}{|CLC0006:Hola {0}|}" />
              </StackPanel>
            </UserControl>
            """
        );

    [Fact]
    public Task Literal_text_content_of_text_elements_is_an_error() =>
        VerifyXamlAsync(
            $$$"""
            <UserControl x:Class="Clicalo.UI.Wpf.Surfaces.PanelView" {{{Namespaces}}}>
              <StackPanel>
                <TextBlock>
                  {|CLC0006:Texto directo|}
                </TextBlock>
                <TextBlock><Run>{|CLC0006:Hola|}</Run> <Run Text="{Binding Name}" /></TextBlock>
                <Button><Button.Content>{|CLC0006:Guardar|}</Button.Content></Button>
                <Button><Button.ToolTip><![CDATA[{|CLC0006:Ayuda & más|}]]></Button.ToolTip></Button>
                <Path><Path.Data>M0,0 L10,10</Path.Data></Path>
              </StackPanel>
            </UserControl>
            """
        );

    [Fact]
    public Task Text_hidden_in_bindings_and_setters_is_an_error() =>
        VerifyXamlAsync(
            $$$"""
            <UserControl x:Class="Clicalo.UI.Wpf.Surfaces.PanelView" {{{Namespaces}}}>
              <UserControl.Resources>
                <Style TargetType="TextBlock">
                  <Setter Property="Text" Value="{|CLC0006:Hola|}" />
                  <Setter Property="Foreground" Value="{|CLC0006:#123456|}" />
                  <Setter Property="Margin" Value="4" />
                </Style>
              </UserControl.Resources>
              <StackPanel>
                <TextBlock Text="{Binding Count, StringFormat='{|CLC0006:{}{0} atajos|}'}" />
                <TextBlock Text="{Binding Title, FallbackValue={|CLC0006:Sin título|}}" />
                <TextBlock Text="{Binding Ratio, StringFormat={}{0:P0}}" />
                <TextBlock Text="{Binding Title, TargetNullValue={x:Static x:String.Empty}}" />
                <ContentControl ContentStringFormat="{|CLC0006:Página {0}|}" />
              </StackPanel>
            </UserControl>
            """
        );

    [Fact]
    public Task Literal_colors_are_errors() =>
        VerifyXamlAsync(
            $$$"""
            <UserControl x:Class="Clicalo.UI.Wpf.Surfaces.PanelView" {{{Namespaces}}}
                Background="{|CLC0006:#FF202020|}">
              <StackPanel>
                <TextBlock Foreground="{|CLC0006:White|}" Text="{Binding Name}" />
                <Border BorderBrush="{|CLC0006:red|}" Background="Transparent" Width="44" />
                <Rectangle Fill="{|CLC0006:#0F0|}" Stroke="{|CLC0006:sc#1,0.5,0.5,0.5|}" />
                <TextBlock Foreground="{Binding Accent, FallbackValue={|CLC0006:Red|}}" />
                <TextBlock Foreground="{DynamicResource Clicalo.Text}" Background="{StaticResource Clicalo.Panel}" />
              </StackPanel>
            </UserControl>
            """
        );

    [Fact]
    public Task Bindings_resources_glyphs_and_design_time_values_are_allowed() =>
        VerifyXamlAsync(
            $$$"""
            <UserControl x:Class="Clicalo.UI.Wpf.Surfaces.PanelView" {{{Namespaces}}}
                d:DesignWidth="300" d:Title="Diseño">
              <StackPanel>
                <TextBlock Text="{Binding Title}" d:Text="Texto de diseño" />
                <TextBlock Text="{x:Static local:Strings.Title}" />
                <TextBlock Text="{DynamicResource Clicalo.Greeting}" ToolTip="{StaticResource Hint}" />
                <TextBlock Text="&#xE8BB;" />
                <TextBlock Text="·" />
                <TextBlock Text="{}{0}" />
                <TextBlock Text="" />
                <Grid x:Name="Root" x:Uid="Root" Tag="Hola" />
                <d:DesignProperties Text="Hola" />
              </StackPanel>
            </UserControl>
            """
        );

    [Fact]
    public Task A_justified_comment_suppresses_an_element_and_its_content() =>
        VerifyXamlAsync(
            $$$"""
            <UserControl x:Class="Clicalo.UI.Wpf.Surfaces.PanelView" {{{Namespaces}}}>
              <StackPanel>
                <!-- CLC0006: Brand name, never translated (TEM-008). -->
                <TextBlock Text="Clícalo"><Run>Clícalo</Run></TextBlock>

                <!-- CLC0006: -->
                <TextBlock Text="{|CLC0006:Sin motivo|}" />
                <!-- A regular comment does not suppress anything. -->
                <TextBlock Text="{|CLC0006:Hola|}" />
              </StackPanel>
            </UserControl>
            """
        );

    [Fact]
    public async Task Resource_dictionaries_are_judged_by_the_assembly()
    {
        const string dictionary = $$$"""
            <ResourceDictionary {{{Namespaces}}}>
              <Color x:Key="Accent">{|CLC0006:#FF112233|}</Color>
              <SolidColorBrush x:Key="Danger" Color="{|CLC0006:Red|}" />
              <x:String x:Key="Greeting">{|CLC0006:Hola|}</x:String>
              <x:Double x:Key="Gap">12</x:Double>
            </ResourceDictionary>
            """;

        var inScope = CreateXamlTest(dictionary);
        inScope.SolutionTransforms.Add(
            (solution, projectId) => solution.WithProjectAssemblyName(projectId, "Clicalo.UI.Wpf")
        );
        await inScope.RunAsync(TestContext.Current.CancellationToken);

        var outOfScope = CreateXamlTest(
            dictionary
                .Replace("{|CLC0006:", "", StringComparison.Ordinal)
                .Replace("|}", "", StringComparison.Ordinal)
        );
        outOfScope.SolutionTransforms.Add(
            (solution, projectId) => solution.WithProjectAssemblyName(projectId, "Clicalo.App")
        );
        await outOfScope.RunAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("Clicalo.App.MainWindow")]
    [InlineData("Clicalo.Application.Shell")]
    public Task Views_outside_presentation_and_ui_are_out_of_scope(string xamlClass) =>
        VerifyXamlAsync(
            $$$"""
            <Window x:Class="{{{xamlClass}}}" {{{Namespaces}}} Title="Clícalo" Background="#FFFFFF">
              <TextBlock>Hola</TextBlock>
            </Window>
            """
        );

    [Fact]
    public Task Windows_line_endings_keep_exact_locations() =>
        VerifyXamlAsync(
            $$$"""
            <UserControl x:Class="Clicalo.UI.Wpf.Surfaces.PanelView" {{{Namespaces}}}>
              <StackPanel>
                <TextBlock
                    Margin="4"
                    Text="{|CLC0006:Hola|}" />
                <TextBlock>
                  {|CLC0006:Adiós|}
                </TextBlock>
              </StackPanel>
            </UserControl>
            """.ReplaceLineEndings("\r\n")
        );

    [Fact]
    public Task Malformed_or_foreign_files_are_ignored() =>
        VerifyXamlAsync(
            "<UserControl x:Class=\"Clicalo.UI.Wpf.Surfaces.PanelView\"><TextBlock Text=\"Hola\"></UserControl>"
        );

    [Fact]
    public async Task Other_additional_files_are_not_read_as_xaml()
    {
        var test = Verify.Create(
            "namespace Clicalo.UI.Wpf.Surfaces; public sealed class PanelView { }"
        );
        test.TestState.AdditionalFiles.Add(("/0/strings.es.json", "{ \"panel.title\": \"Hola\" }"));
        test.TestState.AdditionalFiles.Add(("/0/Theme.xml", "<Color>#FF0000</Color>"));
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Reports_the_text_and_the_attribute_in_the_message()
    {
        var test = CreateXamlTest(
            $$$"""
            <UserControl x:Class="Clicalo.UI.Wpf.Surfaces.PanelView" {{{Namespaces}}}>
              <TextBlock Text="{|#0:Hola|}" />
            </UserControl>
            """
        );
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(Clicalo.Analyzers.Descriptors.LiteralText)
                .WithLocation(0)
                .WithArguments("Hola", "TextBlock.Text")
        );

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    private static Task VerifyXamlAsync(string xaml) =>
        CreateXamlTest(xaml).RunAsync(TestContext.Current.CancellationToken);

    private static Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<
        Clicalo.Analyzers.Presentation.PresentationLiteralAnalyzer,
        DefaultVerifier
    > CreateXamlTest(string xaml)
    {
        var test = Verify.Create(
            "namespace Clicalo.UI.Wpf.Surfaces; public sealed partial class PanelView { }"
        );
        test.TestState.AdditionalFiles.Add(("/0/Surfaces/PanelView.xaml", xaml));
        return test;
    }
}
