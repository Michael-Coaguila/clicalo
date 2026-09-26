using Microsoft.CodeAnalysis.Testing;
using Verify = Clicalo.Generators.Tests.Analyzers.AnalyzerVerifier<Clicalo.Analyzers.Privacy.SensitiveDataAnalyzer>;

namespace Clicalo.Generators.Tests.Analyzers;

[Trait("Req", "LOG-001")]
public sealed class SensitiveDataAnalyzerTests
{
    [Fact]
    public Task Sensitive_arguments_to_logger_extensions_are_errors() =>
        Verify.VerifyAsync(
            """
            using Clicalo.Domain.Privacy;
            using Microsoft.Extensions.Logging;

            public sealed class Engine
            {
                public static void Run(ILogger<Engine> logger, SecretText text, Sensitive<string> title, WindowTitle window, CapturedKey key)
                {
                    logger.LogInformation("Typing {Text}", {|CLC0003:text|});
                    logger.LogInformation("Title {Title} {Window}", {|CLC0003:title|}, {|CLC0003:window|});
                    logger.LogInformation("Key {Key}", (object){|CLC0003:key|});
                    using var scope = logger.BeginScope("Scope {Title}", {|CLC0003:title|});
                }
            }
            """,
            Stubs.Logging,
            Stubs.Privacy
        );

    [Fact]
    public Task Interpolating_or_concatenating_a_sensitive_value_into_a_log_is_an_error() =>
        Verify.VerifyAsync(
            """
            using Clicalo.Domain.Privacy;
            using Microsoft.Extensions.Logging;

            public static class Engine
            {
                public static void Run(ILogger logger, SecretText text, WindowTitle title, bool verbose)
                {
                    logger.LogInformation($"Typing {{|CLC0003:text|}} now");
                    logger.LogInformation("Title: " + {|CLC0003:title|});
                    logger.LogInformation(string.Format("{0}", {|CLC0003:text|}));
                    logger.LogInformation(verbose ? $"{{|CLC0003:title|}}" : "hidden");
                }
            }
            """,
            Stubs.Logging,
            Stubs.Privacy
        );

    [Fact]
    public Task Sensitive_values_nested_in_arrays_tuples_and_anonymous_objects_are_errors() =>
        Verify.VerifyAsync(
            """
            using Clicalo.Domain.Privacy;
            using Microsoft.Extensions.Logging;

            public static class Engine
            {
                public static void Run(ILogger logger, SecretText text, WindowTitle title, SecretText[] all)
                {
                    logger.LogInformation("{A}", new object[] { {|CLC0003:text|} });
                    logger.LogInformation("{A}", {|CLC0003:(1, title)|});
                    using var scope = logger.BeginScope(new { Title = {|CLC0003:title|} });
                    logger.LogInformation("{All}", {|CLC0003:all|});
                }
            }
            """,
            Stubs.Logging,
            Stubs.Privacy
        );

    [Fact]
    public Task Unwrapping_a_sensitive_value_into_a_log_is_an_error() =>
        Verify.VerifyAsync(
            """
            using Clicalo.Domain.Privacy;
            using Microsoft.Extensions.Logging;

            public static class Engine
            {
                public static void Run(ILogger logger, Sensitive<string> query)
                {
                    logger.LogInformation("Search {Query}", {|CLC0003:query.Value|});
                    logger.LogInformation($"Search {{|CLC0003:query.Reveal()|}}");
                }
            }
            """,
            Stubs.Logging,
            Stubs.Privacy
        );

    [Fact]
    public Task Logger_message_methods_log_delegates_and_direct_log_calls_are_sinks() =>
        Verify.VerifyAsync(
            """
            using System;
            using Clicalo.Domain.Privacy;
            using Microsoft.Extensions.Logging;

            public static partial class Log
            {
                [LoggerMessage(1, LogLevel.Information, "Typed {Text}")]
                public static partial void Typed(ILogger logger, SecretText text);

                public static partial void Typed(ILogger logger, SecretText text) { }

                private static readonly Action<ILogger, WindowTitle, Exception> Focused =
                    LoggerMessage.Define<WindowTitle>(LogLevel.Information, new EventId(2), "Focus {Title}");

                public static void Run(ILogger logger, SecretText text, WindowTitle title)
                {
                    Typed(logger, {|CLC0003:text|});
                    Focused(logger, {|CLC0003:title|}, null);
                    logger.Log(LogLevel.Information, new EventId(3), {|CLC0003:title|}, null, (s, e) => "");
                }
            }
            """,
            Stubs.Logging,
            Stubs.Privacy
        );

    [Fact]
    public Task Sensitive_values_in_exceptions_are_errors() =>
        Verify.VerifyAsync(
            """
            using System;
            using Clicalo.Domain.Privacy;

            public sealed class TextException : Exception
            {
                public TextException(SecretText text) : base($"Cannot type {{|CLC0003:text|}}") { }
                public TextException(string message) : base(message) { }
                public object Detail { get; set; }
            }

            public static class Engine
            {
                public static void Run(SecretText text, WindowTitle title)
                {
                    _ = new InvalidOperationException($"Window {{|CLC0003:title|}} refused focus");
                    _ = new TextException({|CLC0003:text|});
                    _ = new TextException("safe") { Detail = {|CLC0003:title|} };
                }
            }
            """,
            Stubs.Privacy
        );

    [Fact]
    public Task Debug_trace_and_event_sources_are_sinks() =>
        Verify.VerifyAsync(
            """
            using System.Diagnostics;
            using System.Diagnostics.Tracing;
            using Clicalo.Domain.Privacy;

            public sealed class PerfEvents : EventSource
            {
                public void Focus(string title) => WriteEvent(1, title);
            }

            public static class Engine
            {
                public static void Run(PerfEvents events, WindowTitle title)
                {
                    Debug.WriteLine({|CLC0003:title|});
                    Trace.TraceInformation("{0}", {|CLC0003:title|});
                    events.Focus($"{{|CLC0003:title|}}");
                }
            }
            """,
            Stubs.Privacy
        );

    [Fact]
    public Task Identifiers_lengths_and_redacted_text_are_allowed() =>
        Verify.VerifyAsync(
            """
            using Clicalo.Domain.Privacy;
            using Microsoft.Extensions.Logging;

            public sealed record Shortcut(int Id, SecretText Text);

            public static class Engine
            {
                public static void Run(ILogger logger, Shortcut shortcut, Sensitive<string> query, WindowTitle title)
                {
                    logger.LogInformation("Shortcut {Id} typed {Length} characters", shortcut.Id, shortcut.Text.Length);
                    logger.LogInformation("Search of kind {Kind}", query.Kind);
                    logger.LogInformation("Redacted {Title}", title.ToString());
                    Remember(title);
                }

                private static void Remember(WindowTitle title) { }
            }
            """,
            Stubs.Logging,
            Stubs.Privacy
        );

    [Fact]
    public Task A_sensitive_member_of_an_ordinary_object_is_still_reported() =>
        Verify.VerifyAsync(
            """
            using Clicalo.Domain.Privacy;
            using Microsoft.Extensions.Logging;

            public sealed record Shortcut(int Id, SecretText Text);

            public static class Engine
            {
                public static void Run(ILogger logger, Shortcut shortcut) =>
                    logger.LogInformation("Typing {Text}", {|CLC0003:shortcut.Text|});
            }
            """,
            Stubs.Logging,
            Stubs.Privacy
        );

    [Fact]
    public Task Without_the_privacy_contracts_the_rule_stays_silent() =>
        Verify.VerifyAsync(
            """
            using Microsoft.Extensions.Logging;

            public sealed class SecretText { }

            public static class Engine
            {
                public static void Run(ILogger logger, SecretText text) => logger.LogInformation("{T}", text);
            }
            """,
            Stubs.Logging
        );

    [Fact]
    public async Task Reports_value_type_and_sink_in_the_message()
    {
        var test = Verify.Create(
            """
            using Clicalo.Domain.Privacy;
            using Microsoft.Extensions.Logging;

            public static class Engine
            {
                public static void Run(ILogger logger, SecretText text) =>
                    logger.LogInformation("Typing {Text}", {|#0:text|});
            }
            """,
            Stubs.Logging,
            Stubs.Privacy
        );
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(Clicalo.Analyzers.Descriptors.SensitiveData)
                .WithLocation(0)
                .WithArguments("text", "SecretText", "LoggerExtensions.LogInformation")
        );

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
