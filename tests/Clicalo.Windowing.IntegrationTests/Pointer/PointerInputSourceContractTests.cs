using System.Windows;
using System.Windows.Interop;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Pointer;

namespace Clicalo.Windowing.IntegrationTests.Pointer;

/// <summary>
/// How a <see cref="PointerInputSource"/> attaches to and detaches from its window, in process and without input:
/// hidden windows only, so nothing is shown, activated or injected.
/// </summary>
[Trait("Req", "ACC-007")]
public sealed class PointerInputSourceContractTests
{
    [Fact]
    public void A_source_needs_a_window_a_sink_and_a_clock() =>
        WpfThread.Invoke(() =>
        {
            var window = new Window();
            var sink = new PointerRecorder(TimeProvider.System);
            Should.Throw<ArgumentNullException>(() =>
                new PointerInputSource(null!, sink, TimeProvider.System)
            );
            Should.Throw<ArgumentNullException>(() =>
                new PointerInputSource(window, null!, TimeProvider.System)
            );
            Should.Throw<ArgumentNullException>(() => new PointerInputSource(window, sink, null!));
        });

    [Fact]
    public void Attaching_before_the_handle_exists_fails() =>
        WpfThread.Invoke(() =>
        {
            var source = new PointerInputSource(
                new Window(),
                new PointerRecorder(TimeProvider.System),
                TimeProvider.System
            );

            Should.Throw<InvalidOperationException>(source.Attach);
            source.IsAttached.ShouldBeFalse();
        });

    [Fact]
    public void Attach_and_detach_are_idempotent_and_report_nothing_without_contacts() =>
        WithHiddenWindow(
            (window, recorder) =>
            {
                var source = new PointerInputSource(window, recorder, TimeProvider.System);

                source.Attach();
                source.Attach();
                source.IsAttached.ShouldBeTrue();
                source.ActiveContacts.ShouldBe(0);

                source.Detach();
                source.Detach();
                source.IsAttached.ShouldBeFalse();
                recorder.Frames.ShouldBeEmpty();
                recorder.Hovers.ShouldBeEmpty();

                source.Attach();
                source.Dispose();
                source.IsAttached.ShouldBeFalse();
            }
        );

    [Fact]
    public void A_source_belongs_to_the_thread_of_its_window()
    {
        var source = WithHiddenWindow(
            (window, recorder) => new PointerInputSource(window, recorder, TimeProvider.System)
        );

        Should.Throw<InvalidOperationException>(source.Attach);
    }

    [Fact]
    public void Closing_the_window_detaches_the_source() =>
        WpfThread.Invoke(() =>
        {
            var window = new Window { ShowActivated = false };
            _ = new WindowInteropHelper(window).EnsureHandle();
            var source = new PointerInputSource(
                window,
                new PointerRecorder(TimeProvider.System),
                TimeProvider.System
            );
            source.Attach();

            window.Close();

            source.IsAttached.ShouldBeFalse();
        });

    /// <summary>Runs <paramref name="test"/> on the WPF thread with a hidden window whose handle exists.</summary>
    private static T WithHiddenWindow<T>(Func<Window, PointerRecorder, T> test) =>
        WpfThread.Invoke(() =>
        {
            var window = new Window { ShowActivated = false };
            try
            {
                _ = new WindowInteropHelper(window).EnsureHandle();
                return test(window, new PointerRecorder(TimeProvider.System));
            }
            finally
            {
                window.Close();
            }
        });

    private static void WithHiddenWindow(Action<Window, PointerRecorder> test) =>
        WithHiddenWindow<bool>(
            (window, recorder) =>
            {
                test(window, recorder);
                return true;
            }
        );
}
