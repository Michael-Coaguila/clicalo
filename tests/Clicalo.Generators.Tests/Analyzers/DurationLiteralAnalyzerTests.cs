using Microsoft.CodeAnalysis.Testing;
using Verify = Clicalo.Generators.Tests.Analyzers.AnalyzerVerifier<Clicalo.Analyzers.Timing.DurationLiteralAnalyzer>;

namespace Clicalo.Generators.Tests.Analyzers;

[Trait("Req", "NFR-020")]
public sealed class DurationLiteralAnalyzerTests
{
    [Fact]
    public Task Literal_delays_and_timeouts_in_application_are_errors() =>
        Verify.VerifyAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            namespace Clicalo.Application.Engine;

            public static class Pacing
            {
                public static async Task Run(CancellationTokenSource cts, CancellationToken token)
                {
                    await Task.Delay({|CLC0004:500|}, token);
                    Thread.Sleep({|CLC0004:100|});
                    using var timeout = new CancellationTokenSource({|CLC0004:3_000|});
                    cts.CancelAfter({|CLC0004:250|});
                    await Task.Delay({|CLC0004:2 * 1000|});
                    await Task.Delay({|CLC0004:-1|});
                }
            }
            """,
            Stubs.Timings
        );

    [Fact]
    public Task Literal_time_spans_and_date_arithmetic_are_errors() =>
        Verify.VerifyAsync(
            """
            using System;
            using System.Threading;

            namespace Clicalo.Presentation.Panel;

            public static class Clock
            {
                public static void Run(TimeProvider time, TimerCallback callback, DateTimeOffset now)
                {
                    _ = TimeSpan.FromMilliseconds({|CLC0004:600|});
                    _ = TimeSpan.FromSeconds({|CLC0004:2.5|});
                    _ = {|CLC0004:new TimeSpan(0, 0, 5)|};
                    _ = now.AddSeconds({|CLC0004:30|});
                    _ = time.CreateTimer(callback, null, TimeSpan.FromSeconds({|CLC0004:1|}), Timeout.InfiniteTimeSpan);
                    _ = {|CLC0004:new Timer(callback, null, 100, 1000)|};
                }
            }
            """
        );

    [Fact]
    public Task Durations_declared_outside_timings_are_errors_where_declared_and_where_used() =>
        Verify.VerifyAsync(
            """
            using System.Threading.Tasks;

            namespace Clicalo.Application.Notices;

            public sealed class NoticeQueue
            {
                private const int DebounceMs = {|CLC0004:300|};

                public int RepeatInterval { get; set; } = {|CLC0004:40|};

                public async Task Show()
                {
                    var holdMs = {|CLC0004:600|};
                    RepeatInterval = {|CLC0004:25|};
                    await Task.Delay({|CLC0004:DebounceMs|});
                    await Task.Delay(holdMs);
                }
            }

            public sealed class NoticeOptions
            {
                public double TimeoutSeconds { get; set; }

                public static NoticeOptions Default => new() { TimeoutSeconds = {|CLC0004:15|} };
            }
            """
        );

    [Fact]
    public Task Literal_thresholds_compared_with_durations_are_errors() =>
        Verify.VerifyAsync(
            """
            using System;
            using System.Diagnostics;
            using Clicalo.Domain;

            namespace Clicalo.Application.Engine;

            public static class Press
            {
                public static bool IsLong(TimeSpan held, Stopwatch watch, long heldMs, double delaySeconds) =>
                    held.TotalMilliseconds >= {|CLC0004:600|}
                    || {|CLC0004:1_000|} < watch.ElapsedMilliseconds
                    || heldMs == {|CLC0004:250|}
                    || delaySeconds > {|CLC0004:2.5|};

                public static bool IsValid(TimeSpan held, long heldMs, int count) =>
                    heldMs > 0
                    && held.TotalMilliseconds >= Timings.LongPressMs
                    && heldMs < Timings.FlashMs * 2
                    && count > 3;
            }
            """,
            Stubs.Timings
        );

    [Fact]
    public Task Timings_constants_and_runtime_values_are_allowed() =>
        Verify.VerifyAsync(
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading;
            using System.Threading.Tasks;
            using Clicalo.Domain;

            namespace Clicalo.Application.Engine;

            public static class Pacing
            {
                public static async Task Run(int delay, TimeSpan span, double value)
                {
                    await Task.Delay(Timings.LongPressMs);
                    await Task.Delay(Timings.FlashMs * 2);
                    _ = TimeSpan.FromSeconds(Timings.DimDelaySeconds);
                    await Task.Delay(Timeout.Infinite);
                    await Task.Delay(TimeSpan.Zero);
                    await Task.Delay(delay);
                    await Task.Delay(span);
                    _ = new TimeSpan();
                    _ = Enumerable.Repeat(0, 3);
                    _ = new List<int>(16);
                    _ = Math.Round(value, 2);
                }
            }
            """,
            Stubs.Timings
        );

    [Theory]
    [InlineData("Clicalo.Domain.Engine")]
    [InlineData("Clicalo.Infrastructure.Persistence")]
    [InlineData("Clicalo.UI.Wpf.Surfaces")]
    [InlineData("Clicalo.ApplicationTools")]
    public Task Other_layers_are_out_of_scope(string ns) =>
        Verify.VerifyAsync(
            "using System.Threading.Tasks; namespace "
                + ns
                + "; public static class Pacing { public static Task Run() => Task.Delay(500); }"
        );

    [Fact]
    public Task A_justified_suppression_silences_the_rule() =>
        Verify.VerifyAsync(
            """
            using System.Diagnostics.CodeAnalysis;
            using System.Threading.Tasks;

            namespace Clicalo.Application.Engine;

            public static class Pacing
            {
                [SuppressMessage("Clicalo.Timing", "CLC0004", Justification = "Yield to the dispatcher; not a threshold.")]
                public static Task Yield() => Task.Delay(1);
            }
            """
        );

    [Fact]
    public async Task Reports_the_literal_and_the_target_in_the_message()
    {
        var test = Verify.Create(
            """
            using System.Threading.Tasks;

            namespace Clicalo.Application.Engine;

            public static class Pacing
            {
                public static Task Run() => Task.Delay({|#0:500|});
            }
            """
        );
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(Clicalo.Analyzers.Descriptors.DurationLiteral)
                .WithLocation(0)
                .WithArguments("500", "Task.Delay")
        );

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
