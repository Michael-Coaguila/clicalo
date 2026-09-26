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

    /// <summary>The part of <c>Microsoft.Extensions.Logging</c> the product uses (CLC0003).</summary>
    public const string Logging = """
        namespace Microsoft.Extensions.Logging
        {
            public enum LogLevel { Trace, Debug, Information, Warning, Error, Critical, None }

            public readonly struct EventId
            {
                public EventId(int id) { Id = id; }
                public int Id { get; }
            }

            public interface ILogger
            {
                void Log<TState>(LogLevel logLevel, EventId eventId, TState state, System.Exception exception, System.Func<TState, System.Exception, string> formatter);
                bool IsEnabled(LogLevel logLevel);
                System.IDisposable BeginScope<TState>(TState state);
            }

            public interface ILogger<out TCategoryName> : ILogger { }

            public static class LoggerExtensions
            {
                public static void LogInformation(this ILogger logger, string message, params object[] args) { }
                public static void LogError(this ILogger logger, System.Exception exception, string message, params object[] args) { }
                public static System.IDisposable BeginScope(this ILogger logger, string messageFormat, params object[] args) => null;
            }

            public static class LoggerMessage
            {
                public static System.Action<ILogger, T1, System.Exception> Define<T1>(LogLevel logLevel, EventId eventId, string formatString) => null;
            }

            [System.AttributeUsage(System.AttributeTargets.Method)]
            public sealed class LoggerMessageAttribute : System.Attribute
            {
                public LoggerMessageAttribute(int eventId, LogLevel level, string message) { }
            }
        }
        """;

    /// <summary><c>Clicalo.Domain.Privacy</c>: the sensitive wrapper types and the marker attribute (CLC0003).</summary>
    public const string Privacy = """
        namespace Clicalo.Domain.Privacy
        {
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct | System.AttributeTargets.Interface)]
            public sealed class SensitiveAttribute : System.Attribute { }

            public enum RedactionKind { FreeText, WindowTitle, SearchQuery, Secret }

            public sealed class Sensitive<T>
            {
                public Sensitive(T value, RedactionKind kind) { Value = value; Kind = kind; }
                public T Value { get; }
                public RedactionKind Kind { get; }
                public T Reveal() => Value;
                public override string ToString() => nameof(Sensitive<T>);
            }

            public sealed class SecretText
            {
                public int Length => 0;
                public override string ToString() => nameof(SecretText);
            }

            [Sensitive]
            public readonly record struct WindowTitle(string Value);

            [Sensitive]
            public abstract class CapturedInput { }

            public sealed class CapturedKey : CapturedInput { }
        }
        """;
}
