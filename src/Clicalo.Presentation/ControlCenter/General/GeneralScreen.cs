namespace Clicalo.Presentation.ControlCenter.General;

/// <summary>
/// What «General y panel» shows (docs/05 §3, GEN-001): the title, the 2 × 2 grid and, below, Disposición and
/// Transparencia on the left and Modo pestaña, Confirmación al tocar, Seguridad de teclas, Accesibilidad y datos and
/// Primeros pasos on the right.
/// </summary>
/// <param name="Title">[panelTitle].</param>
/// <param name="Subtitle">[panelSub].</param>
/// <param name="Look">Idioma, Tema, Tamaño and Vista.</param>
/// <param name="Layout">Disposición.</param>
/// <param name="Transparency">Transparencia.</param>
/// <param name="Dock">Modo pestaña.</param>
/// <param name="Feedback">Confirmación al tocar.</param>
/// <param name="Safety">Seguridad de teclas.</param>
/// <param name="Access">Accesibilidad y datos.</param>
/// <param name="Start">Primeros pasos.</param>
public sealed record GeneralScreen(
    string Title,
    string Subtitle,
    LookModel Look,
    LayoutModel Layout,
    TransparencyModel Transparency,
    DockModel Dock,
    FeedbackModel Feedback,
    SafetyModel Safety,
    AccessModel Access,
    StartModel Start
);
