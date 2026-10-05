using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

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

    /// <summary>Identifies <see cref="Symbol"/>.</summary>
    public static readonly DependencyProperty SymbolProperty = TouchButton.SymbolProperty.AddOwner(
        typeof(ShortcutTile)
    );

    /// <summary>Identifies <see cref="IconSize"/>.</summary>
    public static readonly DependencyProperty IconSizeProperty = DependencyProperty.Register(
        nameof(IconSize),
        typeof(double),
        typeof(ShortcutTile),
        new FrameworkPropertyMetadata(28d)
    );

    /// <summary>Identifies <see cref="Category"/>.</summary>
    public static readonly DependencyProperty CategoryProperty = DependencyProperty.Register(
        nameof(Category),
        typeof(CategoryToken),
        typeof(ShortcutTile),
        new FrameworkPropertyMetadata(CategoryToken.Edit, OnCategoryChanged)
    );

    /// <summary>Identifies <see cref="CategoryTint"/>.</summary>
    public static readonly DependencyProperty CategoryTintProperty = DependencyProperty.Register(
        nameof(CategoryTint),
        typeof(Brush),
        typeof(ShortcutTile),
        new FrameworkPropertyMetadata(null)
    );

    /// <summary>Identifies <see cref="CategoryWash"/>.</summary>
    public static readonly DependencyProperty CategoryWashProperty = DependencyProperty.Register(
        nameof(CategoryWash),
        typeof(Brush),
        typeof(ShortcutTile),
        new FrameworkPropertyMetadata(null)
    );

    /// <summary>Identifies <see cref="Keys"/>.</summary>
    public static readonly DependencyProperty KeysProperty = DependencyProperty.Register(
        nameof(Keys),
        typeof(string),
        typeof(ShortcutTile),
        new FrameworkPropertyMetadata(string.Empty)
    );

    /// <summary>Identifies <see cref="KeysFontSize"/>.</summary>
    public static readonly DependencyProperty KeysFontSizeProperty = DependencyProperty.Register(
        nameof(KeysFontSize),
        typeof(double),
        typeof(ShortcutTile),
        new FrameworkPropertyMetadata(11d)
    );

    /// <summary>Identifies <see cref="Badge"/>.</summary>
    public static readonly DependencyProperty BadgeProperty = DependencyProperty.Register(
        nameof(Badge),
        typeof(string),
        typeof(ShortcutTile),
        new FrameworkPropertyMetadata(string.Empty, OnBadgeChanged)
    );

    private static readonly DependencyPropertyKey BadgeTextPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(BadgeText),
            typeof(string),
            typeof(ShortcutTile),
            new FrameworkPropertyMetadata(string.Empty)
        );

    /// <summary>Identifies <see cref="BadgeText"/>.</summary>
    public static readonly DependencyProperty BadgeTextProperty =
        BadgeTextPropertyKey.DependencyProperty;

    /// <summary>Identifies <see cref="IsHeld"/>.</summary>
    public static readonly DependencyProperty IsHeldProperty = DependencyProperty.Register(
        nameof(IsHeld),
        typeof(bool),
        typeof(ShortcutTile),
        new FrameworkPropertyMetadata(false)
    );

    /// <summary>Identifies <see cref="IsArmed"/>.</summary>
    public static readonly DependencyProperty IsArmedProperty = DependencyProperty.Register(
        nameof(IsArmed),
        typeof(bool),
        typeof(ShortcutTile),
        new FrameworkPropertyMetadata(false)
    );

    /// <summary>Identifies <see cref="IsFlashing"/>.</summary>
    public static readonly DependencyProperty IsFlashingProperty = DependencyProperty.Register(
        nameof(IsFlashing),
        typeof(bool),
        typeof(ShortcutTile),
        new FrameworkPropertyMetadata(false)
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

    /// <summary>Creates a tile in the theme's colors and fonts, in the Edit category.</summary>
    public ShortcutTile()
    {
        SetResourceReference(FontFamilyProperty, ThemeKeys.UiFont);
        UseCategory(this, Category);
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

    /// <summary>Material Symbols name of the shortcut's icon (CUA-007); null for none.</summary>
    public string? Symbol
    {
        get => (string?)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    /// <summary>Side of the icon: 20, 28 or 34 in S, M or L (CUA-007).</summary>
    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    /// <summary>
    /// The color category of the shortcut (TEM-003): the icon, the badge text and the outline of an active tile take
    /// its tint, and an active, held, armed or flashing tile its wash.
    /// </summary>
    public CategoryToken Category
    {
        get => (CategoryToken)GetValue(CategoryProperty);
        set => SetValue(CategoryProperty, value);
    }

    /// <summary>The theme brush of the tint of <see cref="Category"/> (a resource reference; repaints with the theme).</summary>
    public Brush? CategoryTint
    {
        get => (Brush?)GetValue(CategoryTintProperty);
        set => SetValue(CategoryTintProperty, value);
    }

    /// <summary>The theme brush of the wash of <see cref="Category"/> (a resource reference; repaints with the theme).</summary>
    public Brush? CategoryWash
    {
        get => (Brush?)GetValue(CategoryWashProperty);
        set => SetValue(CategoryWashProperty, value);
    }

    /// <summary>
    /// The line under the name (CUA-007), in JetBrains Mono and <c>muted</c>: the combination, or the destination,
    /// action or profile; empty hides it (Compacta, «Mostrar teclas» off).
    /// </summary>
    public string Keys
    {
        get => (string)GetValue(KeysProperty);
        set => SetValue(KeysProperty, value);
    }

    /// <summary>Size of <see cref="Keys"/>: the size's keys text by the text scale, at least 11 (CUA-011, TEM-007).</summary>
    public double KeysFontSize
    {
        get => (double)GetValue(KeysFontSizeProperty);
        set => SetValue(KeysFontSizeProperty, value);
    }

    /// <summary>
    /// The type badge at the top right (CUA-007): MANTENER, ALTERNAR, the number of steps, WEB, APP, TXT or the pin;
    /// empty for none. While <see cref="AccessibleState"/> is set (ACTIVO), the badge shows the state instead.
    /// </summary>
    public string Badge
    {
        get => (string)GetValue(BadgeProperty);
        set => SetValue(BadgeProperty, value);
    }

    /// <summary>What the badge shows: <see cref="AccessibleState"/> when set, otherwise <see cref="Badge"/> (ACC-003).</summary>
    public string BadgeText => (string)GetValue(BadgeTextProperty);

    /// <summary>A Mantener tile held down: scale 0.95, category wash and a 2 px tint outline (CUA-009).</summary>
    public bool IsHeld
    {
        get => (bool)GetValue(IsHeldProperty);
        set => SetValue(IsHeldProperty, value);
    }

    /// <summary>A tile armed for confirmation: category wash and a 2 px <c>warn</c> outline (CUA-009).</summary>
    public bool IsArmed
    {
        get => (bool)GetValue(IsArmedProperty);
        set => SetValue(IsArmedProperty, value);
    }

    /// <summary>The 240 ms flash after running: category wash (CUA-009); the view model times it.</summary>
    public bool IsFlashing
    {
        get => (bool)GetValue(IsFlashingProperty);
        set => SetValue(IsFlashingProperty, value);
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

    private static void UseCategory(ShortcutTile tile, CategoryToken category)
    {
        tile.SetResourceReference(CategoryTintProperty, CategoryBrushKey.Tint(category));
        tile.SetResourceReference(CategoryWashProperty, CategoryBrushKey.Wash(category));
    }

    private static void OnCategoryChanged(
        DependencyObject tile,
        DependencyPropertyChangedEventArgs e
    ) => UseCategory((ShortcutTile)tile, (CategoryToken)e.NewValue);

    private static void OnBadgeChanged(
        DependencyObject tile,
        DependencyPropertyChangedEventArgs e
    ) => ((ShortcutTile)tile).UpdateBadgeText();

    private void UpdateBadgeText() =>
        SetValue(
            BadgeTextPropertyKey,
            string.IsNullOrEmpty(AccessibleState) ? Badge ?? string.Empty : AccessibleState
        );

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
    )
    {
        ((ShortcutTile)tile).UpdateBadgeText();
        ExistingPeer(tile)
            ?.RaiseTextPropertyChanged(
                AutomationElementIdentifiers.ItemStatusProperty,
                AutomationProperties.ItemStatusProperty,
                (string?)e.OldValue,
                (string?)e.NewValue
            );
    }
}
