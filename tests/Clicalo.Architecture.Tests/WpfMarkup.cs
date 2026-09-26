namespace Clicalo.Architecture.Tests;

/// <summary>
/// Sources of the <c>wpf-markup</c> scenario of <see cref="EnforcementBuild"/>: a view whose XAML uses a type of its
/// own project, which makes WPF markup compilation build a temporary <c>Clicalo.UI.Wpf_&lt;random&gt;_wpftmp</c>
/// project next to the real one.
/// </summary>
internal static class WpfMarkup
{
    public const string View = """
        <UserControl x:Class="Clicalo.UI.Wpf.Markup.MarkupView"
                     xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                     xmlns:local="clr-namespace:Clicalo.UI.Wpf.Markup">
          <local:MarkupBadge />
        </UserControl>
        """;

    public const string CodeBehind = """
        namespace Clicalo.UI.Wpf.Markup;

        /// <summary>A view whose XAML uses a type of this project.</summary>
        public sealed partial class MarkupView
        {
            public MarkupView() => InitializeComponent();
        }
        """;

    public const string LocalType = """
        namespace Clicalo.UI.Wpf.Markup;

        /// <summary>A control of this project, used from XAML.</summary>
        public sealed class MarkupBadge : System.Windows.Controls.Border;
        """;
}
