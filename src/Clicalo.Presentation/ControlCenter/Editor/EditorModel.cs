namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>State B of the editor column, projected at once (docs/05 §1).</summary>
/// <param name="Duplicate">1. The repeated card.</param>
/// <param name="Identity">2. The identity.</param>
/// <param name="Picker">3. The icon picker, when open.</param>
/// <param name="Kinds">4. «Qué hace».</param>
/// <param name="Combo">5. The combination.</param>
/// <param name="Text">6. The text of a Text shortcut.</param>
/// <param name="Mouse">6. The mouse action.</param>
/// <param name="Steps">6. The steps of a macro.</param>
/// <param name="Target">6. The address or the target.</param>
/// <param name="Pin">7. Pin in Always visible.</param>
/// <param name="More">8. More options.</param>
/// <param name="Test">9. The «Probar» card, when open.</param>
/// <param name="Footer">10. The footer.</param>
public sealed record EditorModel(
    DuplicateCardModel? Duplicate,
    IdentityModel Identity,
    IconPickerModel? Picker,
    KindsModel Kinds,
    ComboModel? Combo,
    TextFieldModel? Text,
    MouseModel? Mouse,
    StepsModel? Steps,
    TargetModel? Target,
    PinModel Pin,
    MoreModel More,
    TestModel? Test,
    FooterModel Footer
);
