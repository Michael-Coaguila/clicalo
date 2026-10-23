using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace.Welcome;

/// <summary>
/// The progress bars of the welcome (BIE-001): one bar per step, the steps done and the one in view in the accent, the
/// rest in the line. UI Automation sees one progress bar named [obStep] whose read-only value is the step in view, from
/// 1 to the number of steps, and none of its bars (ACC-001, REG-06): a name on a plain Grid never reached it.
/// </summary>
public sealed class StepProgress : Grid
{
    /// <summary>The step in view, from 1; 0 before the first <see cref="Show"/>.</summary>
    public int Step { get; private set; }

    /// <summary>The number of steps.</summary>
    public int StepCount { get; private set; }

    /// <summary>Draws step <paramref name="step"/> (from 0) of <paramref name="count"/>, named <paramref name="name"/>.</summary>
    /// <param name="step">The step in view, from 0.</param>
    /// <param name="count">The number of steps.</param>
    /// <param name="name">[obStep], «Paso n de 5».</param>
    public void Show(int step, int count, string name)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(step);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, step + 1);
        ArgumentNullException.ThrowIfNull(name);
        var before = Step;
        Step = step + 1;
        StepCount = count;
        AutomationProperties.SetName(this, name);
        Children.Clear();
        ColumnDefinitions.Clear();
        for (var i = 0; i < count; i++)
        {
            // The bars share the width, Space.S1 apart: a column of the gap between two of them.
            if (i > 0)
            {
                ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Space.S1) });
            }

            ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            );
            // A radius of half its height: a larger one makes WPF draw a lens instead of a pill (D21 review).
            var bar = new Border
            {
                Height = Space.S1,
                CornerRadius = new CornerRadius(Space.S1 / 2),
            };
            Ui.Ink(bar, Border.BackgroundProperty, i <= step ? ColorToken.Accent : ColorToken.Line);
            SetColumn(bar, ColumnDefinitions.Count - 1);
            Children.Add(bar);
        }

        if (before != Step && UIElementAutomationPeer.FromElement(this) is { } peer)
        {
            peer.RaisePropertyChangedEvent(
                RangeValuePatternIdentifiers.ValueProperty,
                (double)before,
                (double)Step
            );
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    /// <summary>A read-only progress bar without children: the bars are its drawing.</summary>
    private sealed class Peer(StepProgress owner)
        : FrameworkElementAutomationPeer(owner),
            IRangeValueProvider
    {
        public double Value => owner.Step;

        public bool IsReadOnly => true;

        public double Maximum => owner.StepCount;

        public double Minimum => 1;

        public double LargeChange => 1;

        public double SmallChange => 1;

        public void SetValue(double value) =>
            throw new InvalidOperationException(
                "The step of the welcome only changes with its buttons."
            );

        public override object? GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.RangeValue
                ? this
                : base.GetPattern(patternInterface);

        protected override AutomationControlType GetAutomationControlTypeCore() =>
            AutomationControlType.ProgressBar;

        protected override string GetClassNameCore() => nameof(StepProgress);

        protected override bool IsKeyboardFocusableCore() => false;

        protected override List<AutomationPeer>? GetChildrenCore() => null;
    }
}
