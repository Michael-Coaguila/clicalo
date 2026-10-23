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
        new FrameworkPropertyMetadata(IconBox.Medium)
    );

    /// <summary>Identifies <see cref="OriginSymbol"/>.</summary>
    public static readonly DependencyProperty OriginSymbolProperty = DependencyProperty.Register(
        nameof(OriginSymbol),
        typeof(string),
        typeof(ShortcutTile),
        new FrameworkPropertyMetadata(null)
    );

    /// <summary>Identifies <see cref="TypeSymbol"/>.</summary>
    public static readonly DependencyProperty TypeSymbolProperty = DependencyProperty.Register(
        nameof(TypeSymbol),
        typeof(string),
        typeof(ShortcutTile),
        new FrameworkPropertyMetadata(null, OnBadgeChanged)
    );

    private static readonly DependencyPropertyKey IsTypeShownPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsTypeShown),
            typeof(bool),
            typeof(ShortcutTile),
            new FrameworkPropertyMetadata(false)
        );

    /// <summary>Identifies <see cref="IsTypeShown"/>.</summary>
    public static readonly DependencyProperty IsTypeShownProperty =
        IsTypeShownPropertyKey.DependencyProperty;

    /// <summary>Identifies <see cref="MaxNameLines"/>.</summary>
    public static readonly DependencyProperty MaxNameLinesProperty = DependencyProperty.Register(
        nameof(MaxNameLines),
        typeof(int),
        typeof(ShortcutTile),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsMeasure),
        static value => value is 1 or 2
    );

    /// <summary>Identifies <see cref="ReservesNameLines"/>.</summary>
    public static readonly DependencyProperty ReservesNameLinesProperty =
        DependencyProperty.Register(
            nameof(ReservesNameLines),
            typeof(bool),
            typeof(ShortcutTile),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure)
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
        new FrameworkPropertyMetadata(string.Empty, OnBadgeChanged)
    );

    private static readonly DependencyPropertyKey FootTextPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(FootText),
            typeof(string),
            typeof(ShortcutTile),
            new FrameworkPropertyMetadata(string.Empty)
        );

    /// <summary>Identifies <see cref="FootText"/>.</summary>
    public static readonly DependencyProperty FootTextProperty =
        FootTextPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey IsStateShownPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsStateShown),
            typeof(bool),
            typeof(ShortcutTile),
            new FrameworkPropertyMetadata(false)
        );

    /// <summary>Identifies <see cref="IsStateShown"/>.</summary>
    public static readonly DependencyProperty IsStateShownProperty =
        IsStateShownPropertyKey.DependencyProperty;

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

    /// <summary>Identifies <see cref="ShowsCard"/>.</summary>
    public static readonly DependencyProperty ShowsCardProperty = DependencyProperty.Register(
        nameof(ShowsCard),
        typeof(bool),
        typeof(ShortcutTile),
        new FrameworkPropertyMetadata(true)
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
    /// is nothing to say. Shown on the tile in the place of its key line (<see cref="FootText"/>), never over its icon
    /// or its voice number, and exposed as the UI Automation item status, so the state is never color alone and
    /// Narrator reads it with the name.
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

    /// <summary>Side of the icon box (<see cref="IconBox.Medium"/>, 24, by default; the glyph is centered in it).</summary>
    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    /// <summary>
    /// Material Symbols name of the type of the shortcut (Mantener, Alternar, Macro, Web, App, Texto) shown in the slot
    /// at the top right, in a box of <see cref="IconBox.Badge"/> in <c>muted</c> (filled in <c>accentInk</c> while the
    /// tile is chosen or held, in <c>warnText</c> while it is armed); null leaves the slot to <see cref="BadgeText"/>.
    /// </summary>
    public string? TypeSymbol
    {
        get => (string?)GetValue(TypeSymbolProperty);
        set => SetValue(TypeSymbolProperty, value);
    }

    /// <summary>
    /// Material Symbols name of the profile of origin of a Frequents tile or of a search result, drawn beside the icon in
    /// a box of <see cref="IconBox.Badge"/> in <c>muted</c> (FRE-003, D21); null draws nothing.
    /// </summary>
    public string? OriginSymbol
    {
        get => (string?)GetValue(OriginSymbolProperty);
        set => SetValue(OriginSymbolProperty, value);
    }

    /// <summary>Lines the name may take before «…»: 1 (direction C) or 2 (S at the largest text, CUA-011).</summary>
    public int MaxNameLines
    {
        get => (int)GetValue(MaxNameLinesProperty);
        set => SetValue(MaxNameLinesProperty, value);
    }

    /// <summary>
    /// Whether the name keeps the room of the <see cref="MaxNameLines"/> that fit even when it is shorter: tiles side
    /// by side, some with a name of one line and some of two, keep their icons on one line (D12).
    /// </summary>
    public bool ReservesNameLines
    {
        get => (bool)GetValue(ReservesNameLinesProperty);
        set => SetValue(ReservesNameLinesProperty, value);
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
    /// empty for none. It shows only on a tile without <see cref="TypeSymbol"/>.
    /// </summary>
    public string Badge
    {
        get => (string)GetValue(BadgeProperty);
        set => SetValue(BadgeProperty, value);
    }

    /// <summary>
    /// What the top right slot writes: <see cref="Badge"/> when the tile has no <see cref="TypeSymbol"/>; empty when
    /// the slot shows the type icon. The state never goes in this slot: it is too narrow beside the voice number and
    /// the icon (D12).
    /// </summary>
    public string BadgeText => (string)GetValue(BadgeTextProperty);

    /// <summary>Whether the top right slot shows the type icon (a <see cref="TypeSymbol"/>).</summary>
    public bool IsTypeShown => (bool)GetValue(IsTypeShownProperty);

    /// <summary>
    /// What the line under the name writes: <see cref="AccessibleState"/> while there is one («Manteniendo», «ACTIVO»,
    /// «Paso 2/5», ACC-003, EJE-010), in the interface font and <c>accentInk</c> (<c>warnText</c> while armed),
    /// otherwise <see cref="Keys"/>. It shows only while it fits under the name.
    /// </summary>
    public string FootText => (string)GetValue(FootTextProperty);

    /// <summary>Whether the line under the name shows the state instead of the keys.</summary>
    public bool IsStateShown => (bool)GetValue(IsStateShownProperty);

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
    /// Whether the tile is drawn on its card with its line (the default). Without it, a tile that is a tool among icon
    /// buttons (Scroll up and down of the bar, PES-008) looks like them at rest, and still shows held or chosen (D20).
    /// </summary>
    public bool ShowsCard
    {
        get => (bool)GetValue(ShowsCardProperty);
        set => SetValue(ShowsCardProperty, value);
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
    /// The secondary action of the tile (CUA-014, CUA-015): a right click, the Menu key, Shift+F10 or «clic derecho
    /// {nombre}» of Voice access, which WPF all deliver as <c>ContextMenuOpening</c>. The panel opens the tile menu; no
    /// WPF <c>ContextMenu</c> is ever shown on the panel's surfaces.
    /// </summary>
    public event EventHandler? SecondaryRequested;

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

    /// <inheritdoc />
    protected override void OnContextMenuOpening(ContextMenuEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        e.Handled = true;
        if (IsEnabled)
        {
            SecondaryRequested?.Invoke(this, EventArgs.Empty);
        }
    }

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

    private void UpdateBadgeText()
    {
        var hasState = !string.IsNullOrEmpty(AccessibleState);
        SetValue(BadgeTextPropertyKey, TypeSymbol is null ? Badge ?? string.Empty : string.Empty);
        SetValue(IsTypeShownPropertyKey, TypeSymbol is not null);
        SetValue(IsStateShownPropertyKey, hasState);
        SetValue(FootTextPropertyKey, hasState ? AccessibleState : Keys ?? string.Empty);
    }

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
