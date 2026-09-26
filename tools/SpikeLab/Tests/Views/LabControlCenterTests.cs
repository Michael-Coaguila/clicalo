using System.Windows.Controls;
using System.Windows.Interop;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Tools.SpikeLab.Views;

namespace Clicalo.Tools.SpikeLab.Tests.Views;

/// <summary>
/// The lab Control Center in process (its handle is created, but it is never shown): closing it asks the session to
/// give the foreground back first, and the session can tell when it closed while its lease was still being requested.
/// </summary>
public sealed class LabControlCenterTests
{
    [Fact]
    public void Closing_it_asks_the_session_first_and_keeps_it_open_until_the_session_closes_it() =>
        WpfThread.Invoke(() =>
        {
            var asked = new List<LabControlCenter>();
            var window = Create(asked);

            window.Close();

            asked.ShouldHaveSingleItem().ShouldBeSameAs(window);
            window.IsClosed.ShouldBeFalse();

            window.CloseNow();

            window.IsClosed.ShouldBeTrue();
            asked.Count.ShouldBe(1);
        });

    [Fact]
    public void It_has_three_named_fields() =>
        WpfThread.Invoke(() =>
        {
            var window = Create([]);
            try
            {
                window.FieldCount.ShouldBe(3);
                window.FieldsWithText.ShouldBe(0);
            }
            finally
            {
                window.CloseNow();
            }
        });

    private static LabControlCenter Create(List<LabControlCenter> asked)
    {
        var window = new LabControlCenter(
            closing =>
            {
                asked.Add(closing);
                return Task.CompletedTask;
            },
            (TextBox _) => { },
            (TextBox _) => { }
        );
        _ = new WindowInteropHelper(window).EnsureHandle();
        return window;
    }
}
