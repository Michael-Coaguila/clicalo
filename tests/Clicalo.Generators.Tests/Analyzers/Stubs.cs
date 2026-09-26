namespace Clicalo.Generators.Tests.Analyzers;

/// <summary>
/// Minimal stand-ins for the framework and product types the rules are bound to. They reproduce only the shape the
/// rules look at: metadata names, inheritance, member names and parameter names.
/// </summary>
internal static class Stubs
{
    /// <summary>WPF windows and <c>Clicalo.UI.Wpf.Windowing.NonActivatingWindow</c> (CLC0001).</summary>
    public const string Windowing = """
        namespace System.Windows
        {
            public enum Visibility : byte { Visible = 0, Hidden = 1, Collapsed = 2 }

            public class UIElement
            {
                public Visibility Visibility { get; set; }
                public bool Focus() => true;
            }

            public class Window : UIElement
            {
                public bool ShowActivated { get; set; }
                public void Show() { }
                public bool? ShowDialog() => null;
                public bool Activate() => true;
                public void Hide() { }
            }
        }

        namespace Clicalo.UI.Wpf.Windowing
        {
            public abstract class NonActivatingWindow : System.Windows.Window
            {
                public void ShowPassive() { }
                public void HidePassive() { }
            }
        }
        """;
}
