using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Controls;

namespace Clicalo.UI.Wpf.Automation;

/// <summary>
/// The accessible control behind every tile of the panel, the bar, the fixed row and the Pinned window
/// (blueprint §8.6, ACC-001, ACC-009, REG-06). It exposes the localized name with the voice number, the pattern of
/// its action, the key combination as help text and the state as text to UI Automation through
/// <see cref="ShortcutTileAutomationPeer"/>, so «mostrar números», «clic 4» and «clic Negrita» work with Voice access
/// without activating the window (S3).
/// </summary>
/// <remarks>
/// <para>
/// Touch never goes through this control: it goes through the surface's gesture recognizer (ADR-0006). A UI
/// Automation call raises <see cref="Invoked"/>, <see cref="Toggled"/>, <see cref="ExpandRequested"/> or
/// <see cref="CollapseRequested"/> and the surface forwards it with origin <c>UiaInvoke</c>; the control never takes
/// the focus or the foreground (UIA009). All visible and accessible text comes from the view model, already
/// localized (CLC0006).
/// </para>
/// <para>
/// REG-02: the box of the tile (layout, hit area and UI Automation bounds) is never smaller than
/// <see cref="TouchTarget.MinimumSize"/>. A <c>Width</c> or <c>Height</c> below it only shrinks the drawing, which
/// stays centered in the 44 × 44 target (<see cref="TouchTargetBox"/>).
/// </para>
/// <para>
/// The keyboard focus, shown by the 3 px ring of <see cref="FocusRingStyle"/> (TEM-009), only reaches a tile while
/// its window is active under a <c>TextInput</c> or <c>KeyboardNavigation</c> lease (keyboard and voice mode).
/// </para>
/// </remarks>
public sealed class ShortcutTile : Control
{
    /// <summary>Identifies <see cref="AccessibleName"/>.</summary>
    public static readonly DependencyProperty AccessibleNameProperty = DependencyProperty.Register(
        nameof(AccessibleName),
        typeof(string),
        typeof(ShortcutTile),
        new PropertyMetadata(string.Empty, OnAccessibleNameChanged)
    );

    /// <summary>Identifies <see cref="VoiceNumber"/>.</summary>
    public static readonly DependencyProperty VoiceNumberProperty = DependencyProperty.Register(
        nameof(VoiceNumber),
        typeof(int?),
        typeof(ShortcutTile),
        new PropertyMetadata(null, OnVoiceNumberChanged)
    );

    /// <summary>Identifies <see cref="Pattern"/>.</summary>
    public static readonly DependencyProperty PatternProperty = DependencyProperty.Register(
        nameof(Pattern),
        typeof(ShortcutTilePattern),
        typeof(ShortcutTile),
        new PropertyMetadata(ShortcutTilePattern.Invoke, OnPatternChanged)
    );

    /// <summary>Identifies <see cref="ToggleState"/>.</summary>
    public static readonly DependencyProperty ToggleStateProperty = DependencyProperty.Register(
        nameof(ToggleState),
        typeof(ToggleState),
        typeof(ShortcutTile),
        new PropertyMetadata(ToggleState.Off, OnToggleStateChanged)
    );

    /// <summary>Identifies <see cref="IsExpanded"/>.</summary>
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded),
        typeof(bool),
        typeof(ShortcutTile),
        new PropertyMetadata(false, OnIsExpandedChanged)
    );

    /// <summary>Identifies <see cref="AccessibleHelpText"/>.</summary>
    public static readonly DependencyProperty AccessibleHelpTextProperty =
        DependencyProperty.Register(
            nameof(AccessibleHelpText),
            typeof(string),
            typeof(ShortcutTile),
            new PropertyMetadata(string.Empty, OnAccessibleHelpTextChanged)
        );

    /// <summary>Identifies <see cref="AccessibleState"/>.</summary>
    public static readonly DependencyProperty AccessibleStateProperty = DependencyProperty.Register(
        nameof(AccessibleState),
        typeof(string),
        typeof(ShortcutTile),
        new PropertyMetadata(string.Empty, OnAccessibleStateChanged)
    );

    static ShortcutTile()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ShortcutTile),
            new FrameworkPropertyMetadata(typeof(ShortcutTile))
        );
        TemplateProperty.OverrideMetadata(
            typeof(ShortcutTile),
            new FrameworkPropertyMetadata(ShortcutTileTemplate.Default)
        );
        FocusVisualStyleProperty.OverrideMetadata(
            typeof(ShortcutTile),
            new FrameworkPropertyMetadata(FocusRingStyle.Tile)
        );
        TouchTarget.Enforce(typeof(ShortcutTile));
    }

    /// <summary>The localized name of the shortcut, without the voice number (from the view model).</summary>
    public string AccessibleName
    {
        get => (string)GetValue(AccessibleNameProperty);
        set => SetValue(AccessibleNameProperty, value);
    }

    /// <summary>
    /// The voice number shown on the tile when «Numbers for voice» is on (ACC-009, ACC-010); null when off. The UI
    /// Automation name then starts with «{n} ».
    /// </summary>
    public int? VoiceNumber
    {
        get => (int?)GetValue(VoiceNumberProperty);
        set => SetValue(VoiceNumberProperty, value);
    }

    /// <summary>The pattern exposed to UI Automation.</summary>
    public ShortcutTilePattern Pattern
    {
        get => (ShortcutTilePattern)GetValue(PatternProperty);
        set => SetValue(PatternProperty, value);
    }

    /// <summary>State of a <see cref="ShortcutTilePattern.Toggle"/> tile; <see cref="ToggleState.Indeterminate"/> is the third state of sticky keys.</summary>
    public ToggleState ToggleState
    {
        get => (ToggleState)GetValue(ToggleStateProperty);
        set => SetValue(ToggleStateProperty, value);
    }

    /// <summary>State of an <see cref="ShortcutTilePattern.ExpandCollapse"/> tile.</summary>
    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>
    /// The key combination with localized key names (IDI-003), such as «Ctrl + B», exposed as the UI Automation help
    /// text; empty for actions without keys.
    /// </summary>
    public string AccessibleHelpText
    {
        get => (string)GetValue(AccessibleHelpTextProperty);
        set => SetValue(AccessibleHelpTextProperty, value);
    }

    /// <summary>
    /// The localized state of the tile (ACC-003), such as «ACTIVO» for a Toggle shortcut that is on; empty when there
    /// is nothing to say. Shown on the tile and exposed as the UI Automation item status, so the state is never
    /// color alone and Narrator reads it with the name.
    /// </summary>
    public string AccessibleState
    {
        get => (string)GetValue(AccessibleStateProperty);
        set => SetValue(AccessibleStateProperty, value);
    }

    /// <summary>
    /// UI Automation invoked the tile (<c>IInvokeProvider.Invoke</c>). Raised on the UI thread right after the UI
    /// Automation call has returned, as UIA requires of Invoke.
    /// </summary>
    public event EventHandler? Invoked;

    /// <summary>UI Automation toggled the tile (<c>IToggleProvider.Toggle</c>); the view model decides the new state.</summary>
    public event EventHandler? Toggled;

    /// <summary>UI Automation asked to expand the tile; the view model opens the corresponding surface.</summary>
    public event EventHandler? ExpandRequested;

    /// <summary>UI Automation asked to collapse the tile; the view model closes the corresponding surface.</summary>
    public event EventHandler? CollapseRequested;

    /// <summary>
    /// The name UI Automation reads: <see cref="AccessibleName"/>, preceded by «{n} » when
    /// <see cref="VoiceNumber"/> is set (UIA001).
    /// </summary>
    public string AutomationName => ComposeName(VoiceNumber, AccessibleName);

    /// <summary>Raises <see cref="Invoked"/>; called by the peer.</summary>
    internal void RaiseInvoked() => Invoked?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises <see cref="Toggled"/>; called by the peer.</summary>
    internal void RaiseToggled() => Toggled?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises <see cref="ExpandRequested"/> or <see cref="CollapseRequested"/>; called by the peer.</summary>
    internal void RaiseExpandCollapseRequested(bool expand) =>
        (expand ? ExpandRequested : CollapseRequested)?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new ShortcutTileAutomationPeer(this);

    private static string ComposeName(int? voiceNumber, string? accessibleName) =>
        voiceNumber is { } number
            ? string.Create(CultureInfo.InvariantCulture, $"{number} {accessibleName}")
            : accessibleName ?? string.Empty;

    private static ShortcutTileAutomationPeer? ExistingPeer(DependencyObject tile) =>
        UIElementAutomationPeer.FromElement((ShortcutTile)tile) as ShortcutTileAutomationPeer;

    private static void OnAccessibleNameChanged(
        DependencyObject tile,
        DependencyPropertyChangedEventArgs e
    )
    {
        var number = ((ShortcutTile)tile).VoiceNumber;
        ExistingPeer(tile)
            ?.RaiseNameChanged(
                ComposeName(number, (string?)e.OldValue),
                ComposeName(number, (string?)e.NewValue)
            );
    }

    private static void OnVoiceNumberChanged(
        DependencyObject tile,
        DependencyPropertyChangedEventArgs e
    )
    {
        var name = ((ShortcutTile)tile).AccessibleName;
        ExistingPeer(tile)
            ?.RaiseNameChanged(
                ComposeName((int?)e.OldValue, name),
                ComposeName((int?)e.NewValue, name)
            );
    }

    private static void OnPatternChanged(
        DependencyObject tile,
        DependencyPropertyChangedEventArgs e
    ) =>
        ExistingPeer(tile)
            ?.RaisePatternChanged((ShortcutTilePattern)e.OldValue, (ShortcutTilePattern)e.NewValue);

    private static void OnToggleStateChanged(
        DependencyObject tile,
        DependencyPropertyChangedEventArgs e
    ) =>
        ExistingPeer(tile)
            ?.RaiseToggleStateChanged((ToggleState)e.OldValue, (ToggleState)e.NewValue);

    private static void OnIsExpandedChanged(
        DependencyObject tile,
        DependencyPropertyChangedEventArgs e
    ) => ExistingPeer(tile)?.RaiseExpandCollapseStateChanged((bool)e.OldValue, (bool)e.NewValue);

    private static void OnAccessibleHelpTextChanged(
        DependencyObject tile,
        DependencyPropertyChangedEventArgs e
    ) =>
        ExistingPeer(tile)
            ?.RaiseTextPropertyChanged(
                AutomationElementIdentifiers.HelpTextProperty,
                AutomationProperties.HelpTextProperty,
                (string?)e.OldValue,
                (string?)e.NewValue
            );

    private static void OnAccessibleStateChanged(
        DependencyObject tile,
        DependencyPropertyChangedEventArgs e
    ) =>
        ExistingPeer(tile)
            ?.RaiseTextPropertyChanged(
                AutomationElementIdentifiers.ItemStatusProperty,
                AutomationProperties.ItemStatusProperty,
                (string?)e.OldValue,
                (string?)e.NewValue
            );
}
